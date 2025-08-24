using System.Text;
using System.Text.Json;
using IntelligenceTaskTracker.Web.Data;
using IntelligenceTaskTracker.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TaskStatusEnum = IntelligenceTaskTracker.Web.Models.TaskStatus;

namespace IntelligenceTaskTracker.Web.Services.AI;

public class AiInsightsService : IInsightsService
{
    private readonly AppDbContext _db;
    private readonly IAiProvider _provider;
    private readonly IMemoryCache _cache;
    private readonly AiOptions _opts;

    public AiInsightsService(AppDbContext db, IAiProvider provider, IMemoryCache cache, IOptions<AiOptions> options)
    {
        _db = db;
        _provider = provider;
        _cache = cache;
        _opts = options.Value;
    }

    public void InvalidateForUser(int userId) => _cache.Remove(UserKey(userId));
    public void InvalidateForTask(int taskId) => _cache.Remove(TaskKey(taskId));

    public async Task<TaskInsight?> GetTaskInsightAsync(int taskId, bool forceRefresh = false, CancellationToken ct = default)
    {
        var cacheKey = TaskKey(taskId);
        if (!forceRefresh && _cache.TryGetValue<TaskInsight>(cacheKey, out var cached))
            return cached;

        var task = await _db.Tasks
            .Include(t => t.ResponsibleUser)
            .Include(t => t.Comments.OrderByDescending(c => c.CreatedAt))
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task == null) return null;

        var localAlerts = BuildLocalTaskAlerts(task);
        TaskInsight? result = null;
        bool aiWorked = false;

        try
        {
            var systemPrompt = BuildTaskSystemPrompt();
            var userPrompt = BuildTaskUserPrompt(task);
            var timeout = TimeSpan.FromSeconds(Math.Max(3, _opts.Limits.TimeoutSeconds));
            var json = await _provider.GenerateJsonAsync(systemPrompt, userPrompt, timeout, ct);
            if (!string.IsNullOrWhiteSpace(json))
            {
                result = ParseTaskInsight(json!, task);
                aiWorked = true;
            }
        }
        catch
        {
            // Ignorar errores del proveedor; haremos fallback local
        }

        result ??= new TaskInsight(
            task.Id,
            task.Title,
            BuildFallbackTaskSummary(task),
            task.Status.ToString(),
            ComputeRiskFromAlerts(localAlerts),
            localAlerts,
            [] // Sin próximos pasos en el fallback - que la IA los genere
        );

        // Agregar indicador de estado de la IA
        var statusIndicator = aiWorked ? "🤖 IA Activa" : "Servicio de IA no activo";
        result = result with { 
            Summary = $"{result.Summary} | {statusIndicator}",
            Alerts = MergeAlerts(result.Alerts, localAlerts) 
        };

        _cache.Set(cacheKey, result, TimeSpan.FromHours(Math.Max(1, _opts.Limits.CacheTtlHours)));
        return result;
    }

    public async Task<UserInsight?> GetUserInsightAsync(int userId, bool forceRefresh = false, CancellationToken ct = default)
    {
        var cacheKey = UserKey(userId);
        if (!forceRefresh && _cache.TryGetValue<UserInsight>(cacheKey, out var cached))
            return cached;

        var user = await _db.Users
            .Include(u => u.Tasks)
            .ThenInclude(t => t.Comments)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return null;

        var tasks = user.Tasks
            .OrderBy(t => t.Status)
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .Take(Math.Max(1, _opts.Limits.MaxTasksPerUser))
            .ToList();

        UserInsight? result = null;
        bool aiWorked = false;
        try
        {
            var systemPrompt = BuildUserSystemPrompt();
            var userPrompt = BuildUserUserPrompt(user, tasks);
            var timeout = TimeSpan.FromSeconds(Math.Max(3, _opts.Limits.TimeoutSeconds));
            var json = await _provider.GenerateJsonAsync(systemPrompt, userPrompt, timeout, ct);
            if (!string.IsNullOrWhiteSpace(json))
            {
                result = ParseUserInsight(json!, user, tasks);
                aiWorked = true;
            }
        }
        catch
        {
            // fallback abajo
        }

        result ??= BuildFallbackUserInsight(user, tasks);

        // Agregar indicador de estado de la IA al resumen de usuario
        var statusIndicator = aiWorked ? "🤖 IA Activa" : "Servicio de IA no activo";
        result = result with { Summary = $"{result.Summary} | {statusIndicator}" };

        _cache.Set(cacheKey, result, TimeSpan.FromHours(Math.Max(1, _opts.Limits.CacheTtlHours)));
        return result;
    }

    // Prompts
    private static string BuildTaskSystemPrompt() => """
You are an assistant that analyzes a single task in a task tracker and returns ONLY valid minified JSON that matches this C#-like schema:
{
  "summary": string,
  "riskLevel": "low"|"medium"|"high",
  "alerts": [ { "code": string, "severity": "low"|"medium"|"high", "message": string } ],
  "nextActions": [ string ]
}

REQUISITO CRÍTICO DE RESUMEN: El campo "summary" debe ser un resumen COMPRENSIVO y DETALLADO que incluya:
1. Estado actual de la tarea y progreso general
2. Resumen cronológico de actividades importantes desde los comentarios MÁS RECIENTES hacia atrás
3. Evolución temporal: qué problemas había, qué se hizo para resolverlos, cuál es la situación actual
4. Contexto sobre el responsable y fechas si son relevantes
5. El resumen debe ser informativo, no una frase corta

ANÁLISIS CRONOLÓGICO CRÍTICO: Los comentarios están ordenados del MÁS RECIENTE al más antiguo.
- Analiza la EVOLUCIÓN temporal: ¿Los problemas fueron resueltos en comentarios más recientes?
- Si un comentario reciente dice "funciona", "resuelto", "deployar a tiempo", "listo", entonces los problemas anteriores YA NO SON ACTUALES
- Las alertas y próximos pasos deben reflejar ÚNICAMENTE el estado ACTUAL basado en los últimos comentarios
- Si hay resolución reciente, NO sugieras acciones para problemas ya resueltos (como solicitar extensiones)
- Los "nextActions" deben ser coherentes con el último estado reportado, no con problemas antiguos

REGLAS ESTRICTAS:
1. Comentarios más recientes tienen PRIORIDAD ABSOLUTA sobre comentarios antiguos
2. Si el último comentario es positivo, el riesgo debe ser bajo/medio, no alto
3. NO sugieras solicitar extensión si comentarios recientes indican que se va a tiempo
4. Las alertas deben ser del PRESENTE, no del pasado ya resuelto
5. El RESUMEN debe ser de al menos 2-3 oraciones, explicando el contexto cronológico de los comentarios

Rules: Respond in Spanish. Output strictly JSON with no markdown, no code fences.
""";

    private string BuildTaskUserPrompt(TaskItem task)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tarea: #{task.Id} - {task.Title}");
        sb.AppendLine($"Estado: {task.Status}");
        if (task.DueDate.HasValue) sb.AppendLine($"Fecha compromiso (UTC): {task.DueDate.Value:o}");
        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            sb.AppendLine("Descripción:");
            sb.AppendLine(task.Description);
        }
        if (task.ResponsibleUser is not null)
            sb.AppendLine($"Responsable: {task.ResponsibleUser.Name} (Id {task.ResponsibleUser.Id})");

        var comments = task.Comments
            .OrderByDescending(c => c.CreatedAt)
            .Take(Math.Max(0, _opts.Limits.MaxCommentsPerTask))
            .Select(c => $"- [{c.CreatedAt:o}] {c.CreatedBy}: {c.Comment.Replace('\n', ' ')}");
        
        sb.AppendLine("Comentarios recientes (ordenados del más reciente al más antiguo):");
        foreach (var c in comments) sb.AppendLine(c);
        sb.AppendLine();
        sb.AppendLine("INSTRUCCIONES PARA EL RESUMEN:");
        sb.AppendLine("- Genera un resumen comprensivo de al menos 2-3 oraciones");
        sb.AppendLine("- Incluye contexto cronológico: qué pasó, qué problemas había, cómo se resolvieron");
        sb.AppendLine("- Enfócate en el estado ACTUAL basado en los comentarios más recientes");
        sb.AppendLine("- Si hubo problemas que luego se resolvieron, menciona esa evolución");
        sb.AppendLine("- Incluye información sobre el progreso y próximos pasos si están claros");
        sb.AppendLine();
        sb.AppendLine("Genera el JSON del análisis con un resumen detallado:");
        return sb.ToString();
    }

    private static string BuildUserSystemPrompt() => """
You are an assistant that analyzes a user's workload and returns ONLY valid minified JSON:
{
  "summary": string,
  "overallStatus": "on_track"|"at_risk"|"off_track",
  "riskLevel": "low"|"medium"|"high",
  "alerts": [ { "code": string, "severity": "low"|"medium"|"high", "message": string } ],
  "taskSummaries": [ { "taskId": number, "title": string, "summary": string, "status": string, "riskLevel": string, "alerts": [ { "code": string, "severity": string, "message": string } ], "nextActions": [ string ] } ]
}
Rules: Respond in Spanish, strict JSON only, no markdown.
""";

    private string BuildUserUserPrompt(User user, List<TaskItem> tasks)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Usuario: {user.Name} (Id {user.Id})");
        sb.AppendLine("Tareas consideradas:");
        foreach (var t in tasks)
        {
            sb.AppendLine($"- #{t.Id} | {t.Title} | Estado: {t.Status} | Due: {(t.DueDate?.ToString("o") ?? "null")}");
        }
        sb.AppendLine("Resume riesgos y próximos pasos clave.");
        return sb.ToString();
    }

    // Reglas locales
    private static List<InsightAlert> BuildLocalTaskAlerts(TaskItem task)
    {
        var alerts = new List<InsightAlert>();
        var now = DateTime.UtcNow;
        
        // Alertas de fechas
        if (task.DueDate is DateTime due)
        {
            if (due.Date < now.Date && task.Status != TaskStatusEnum.Completed)
                alerts.Add(new InsightAlert("OVERDUE", "high", "La tarea está vencida."));
            else if ((due - now).TotalDays <= 2 && task.Status != TaskStatusEnum.Completed)
                alerts.Add(new InsightAlert("DUE_SOON", "medium", "La fecha compromiso es próxima."));
        }
        
        // Alertas de actividad
        var lastComment = task.Comments.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
        if (lastComment != null && (now - lastComment.CreatedAt).TotalDays >= 7 && task.Status != TaskStatusEnum.Completed)
            alerts.Add(new InsightAlert("STALE", "medium", "No hay actualizaciones recientes (>= 7 días)."));
        
        // Análisis cronológico de comentarios para detectar estado actual
        var recentComments = task.Comments
            .OrderByDescending(c => c.CreatedAt)
            .Take(10) // Aumentamos para mejor análisis cronológico
            .ToList();
            
        if (recentComments.Any())
        {
            var problemKeywords = new[] { "problema", "error", "retraso", "retras", "dificult", "complic", "bloque", "issue", "falla", "fall", "inesperado" };
            var positiveKeywords = new[] { "lista", "complet", "terminad", "finaliz", "resuel", "solucion", "avanz", "progres", "funciona", "deploy", "tiempo", "listo" };
            var resolutionKeywords = new[] { "resuel", "solucion", "funciona", "arregl", "fix", "correg", "deploy", "listo", "terminad" };
            
            // Analizamos cronológicamente: primero los más recientes (índice 0 = más reciente)
            var mostRecentComments = recentComments.Take(3).ToList();
            var hasRecentPositive = mostRecentComments.Any(c => 
                positiveKeywords.Any(keyword => 
                    c.Comment.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            
            var hasRecentResolution = mostRecentComments.Any(c => 
                resolutionKeywords.Any(keyword => 
                    c.Comment.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            
            // Solo reportar problemas si NO hay resolución reciente
            if (!hasRecentPositive && !hasRecentResolution)
            {
                var problemComments = recentComments.Where(c => 
                    problemKeywords.Any(keyword => 
                        c.Comment.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToList();
                
                if (problemComments.Any())
                {
                    var latestProblem = problemComments.First();
                    var daysSince = (now - latestProblem.CreatedAt).TotalDays;
                    
                    // Solo alertar si el problema es reciente y no hay resolución posterior
                    var problemsAfterResolution = problemComments.Where(p => 
                        !mostRecentComments.Any(r => r.CreatedAt > p.CreatedAt && 
                            resolutionKeywords.Any(keyword => 
                                r.Comment.Contains(keyword, StringComparison.OrdinalIgnoreCase)))).ToList();
                    
                    if (problemsAfterResolution.Any() && daysSince <= 3)
                    {
                        if (daysSince <= 1)
                            alerts.Add(new InsightAlert("RECENT_ISSUES", "medium", "Se reportaron problemas recientemente, pero verificar estado actual."));
                        else if (daysSince <= 3)
                            alerts.Add(new InsightAlert("UNRESOLVED_ISSUES", "low", "Problemas reportados sin claridad sobre resolución."));
                    }
                }
            }
        }
        
        return alerts;
    }

    private static string ComputeRiskFromAlerts(List<InsightAlert> alerts)
    {
        if (alerts.Any(a => a.Severity.Equals("high", StringComparison.OrdinalIgnoreCase))) return "high";
        if (alerts.Any(a => a.Severity.Equals("medium", StringComparison.OrdinalIgnoreCase))) return "medium";
        return "low";
    }

    private static List<InsightAlert> MergeAlerts(List<InsightAlert> a, List<InsightAlert> b)
    {
        var dict = new Dictionary<string, InsightAlert>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in a) dict[x.Code] = x;
        foreach (var x in b) dict[x.Code] = x;
        return dict.Values.ToList();
    }

    private static string BuildFallbackTaskSummary(TaskItem task)
    {
        var parts = new List<string>();
        
        // Analizar el estado actual
        parts.Add(task.Status == TaskStatusEnum.Completed ? "Tarea completada." : $"Tarea en estado {task.Status}.");
        
        // Información de fechas
        if (task.DueDate is DateTime due) 
        {
            var now = DateTime.UtcNow;
            if (due.Date < now.Date && task.Status != TaskStatusEnum.Completed)
                parts.Add($"**VENCIDA** desde {due:yyyy-MM-dd}.");
            else if ((due - now).TotalDays <= 2 && task.Status != TaskStatusEnum.Completed)
                parts.Add($"Vence próximamente: {due:yyyy-MM-dd}.");
            else
                parts.Add($"Compromiso: {due:yyyy-MM-dd}.");
        }
        
        // Analizar comentarios con enfoque cronológico
        var recentComments = task.Comments
            .OrderByDescending(c => c.CreatedAt)
            .Take(10) // Más comentarios para mejor análisis cronológico
            .ToList();
            
        if (recentComments.Any())
        {
            var latestComment = recentComments.First();
            var daysSinceUpdate = (DateTime.UtcNow - latestComment.CreatedAt).TotalDays;
            
            // Detectar palabras clave que indican problemas y resoluciones
            var problemKeywords = new[] { "problema", "error", "retraso", "retras", "dificult", "complic", "bloque", "issue", "falla", "fall", "inesperado" };
            var positiveKeywords = new[] { "lista", "complet", "terminad", "finaliz", "resuel", "solucion", "avanz", "progres", "bien", "ok", "funciona", "deploy", "tiempo" };
            var resolutionKeywords = new[] { "resuel", "solucion", "funciona", "arregl", "fix", "correg", "deploy", "listo", "terminad", "todo funciona" };
            
            // Análisis cronológico: priorizar comentarios más recientes
            var mostRecentComments = recentComments.Take(3).ToList();
            
            var hasRecentResolution = mostRecentComments.Any(c => 
                resolutionKeywords.Any(keyword => 
                    c.Comment.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
                    
            var hasRecentPositive = mostRecentComments.Any(c => 
                positiveKeywords.Any(keyword => 
                    c.Comment.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            
            // Solo considerar problemas si NO hay resolución reciente
            if (hasRecentResolution)
            {
                parts.Add("✅ Última actualización indica resolución exitosa.");
            }
            else if (hasRecentPositive)
            {
                parts.Add("✅ Última actualización positiva.");
            }
            else
            {
                var hasUnresolvedProblems = recentComments.Any(c => 
                    problemKeywords.Any(keyword => 
                        c.Comment.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
                        
                if (hasUnresolvedProblems)
                {
                    parts.Add("⚠️ Los comentarios indican problemas sin resolución clara.");
                }
            }
            
            // Agregar información sobre tiempo sin actualizaciones
            if (daysSinceUpdate >= 7)
            {
                parts.Add($"Sin actualizaciones por {(int)daysSinceUpdate} días.");
            }
        }
        
        return string.Join(" ", parts);
    }

    // Parseo
    private static TaskInsight ParseTaskInsight(string json, TaskItem task)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var summary = root.GetPropertyOrDefault("summary")?.GetString() ?? string.Empty;
        var risk = root.GetPropertyOrDefault("riskLevel")?.GetString() ?? "low";
        var alerts = new List<InsightAlert>();
        var nextActions = new List<string>();
        if (root.TryGetProperty("alerts", out var alertsEl) && alertsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in alertsEl.EnumerateArray())
            {
                var code = a.GetPropertyOrDefault("code")?.GetString() ?? "GENERIC";
                var sev = a.GetPropertyOrDefault("severity")?.GetString() ?? "low";
                var msg = a.GetPropertyOrDefault("message")?.GetString() ?? string.Empty;
                alerts.Add(new InsightAlert(code, sev, msg));
            }
        }
        if (root.TryGetProperty("nextActions", out var naEl) && naEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var n in naEl.EnumerateArray()) if (n.ValueKind == JsonValueKind.String) nextActions.Add(n.GetString()!);
        }
        return new TaskInsight(task.Id, task.Title, summary, task.Status.ToString(), risk, alerts, nextActions);
    }

    private static UserInsight BuildFallbackUserInsight(User user, List<TaskItem> tasks)
    {
        var overdue = tasks.Count(t => t.DueDate is DateTime d && d.Date < DateTime.UtcNow.Date && t.Status != TaskStatusEnum.Completed);
        var alerts = new List<InsightAlert>();
        if (overdue > 0) alerts.Add(new InsightAlert("MANY_OVERDUE", "high", $"Hay {overdue} tareas vencidas."));
        var risk = overdue > 0 ? "high" : "low";
        var taskSummaries = tasks.Select(t => new TaskInsight(t.Id, t.Title, t.Status == TaskStatusEnum.Completed ? "Completada." : $"Estado {t.Status}.", t.Status.ToString(), risk, new List<InsightAlert>(), new List<string>())).ToList();
        return new UserInsight(user.Id, user.Name, $"{tasks.Count} tareas asignadas.", overdue > 0 ? "off_track" : "on_track", risk, alerts, taskSummaries);
    }

    private static UserInsight ParseUserInsight(string json, User user, List<TaskItem> tasks)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var summary = root.GetPropertyOrDefault("summary")?.GetString() ?? $"{tasks.Count} tareas asignadas.";
        var overall = root.GetPropertyOrDefault("overallStatus")?.GetString() ?? "on_track";
        var risk = root.GetPropertyOrDefault("riskLevel")?.GetString() ?? "low";
        var alerts = new List<InsightAlert>();
        if (root.TryGetProperty("alerts", out var alertsEl) && alertsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in alertsEl.EnumerateArray())
            {
                var code = a.GetPropertyOrDefault("code")?.GetString() ?? "GENERIC";
                var sev = a.GetPropertyOrDefault("severity")?.GetString() ?? "low";
                var msg = a.GetPropertyOrDefault("message")?.GetString() ?? string.Empty;
                alerts.Add(new InsightAlert(code, sev, msg));
            }
        }
        var taskSummaries = new List<TaskInsight>();
        if (root.TryGetProperty("taskSummaries", out var tsEl) && tsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tsEl.EnumerateArray())
            {
                var id = t.GetPropertyOrDefault("taskId")?.GetInt32() ?? 0;
                var title = t.GetPropertyOrDefault("title")?.GetString() ?? (tasks.FirstOrDefault(x => x.Id == id)?.Title ?? string.Empty);
                var sum = t.GetPropertyOrDefault("summary")?.GetString() ?? string.Empty;
                var status = t.GetPropertyOrDefault("status")?.GetString() ?? (tasks.FirstOrDefault(x => x.Id == id)?.Status.ToString() ?? string.Empty);
                var riskLevel = t.GetPropertyOrDefault("riskLevel")?.GetString() ?? "low";
                var tAlerts = new List<InsightAlert>();
                if (t.TryGetProperty("alerts", out var taEl) && taEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in taEl.EnumerateArray())
                    {
                        var code = a.GetPropertyOrDefault("code")?.GetString() ?? "GENERIC";
                        var sev = a.GetPropertyOrDefault("severity")?.GetString() ?? "low";
                        var msg = a.GetPropertyOrDefault("message")?.GetString() ?? string.Empty;
                        tAlerts.Add(new InsightAlert(code, sev, msg));
                    }
                }
                var na = new List<string>();
                if (t.TryGetProperty("nextActions", out var naEl) && naEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var n in naEl.EnumerateArray()) if (n.ValueKind == JsonValueKind.String) na.Add(n.GetString()!);
                }
                taskSummaries.Add(new TaskInsight(id, title, sum, status, riskLevel, tAlerts, na));
            }
        }
        return new UserInsight(user.Id, user.Name, summary, overall, risk, alerts, taskSummaries);
    }

    private static string TaskKey(int id) => $"insight:task:{id}";
    private static string UserKey(int id) => $"insight:user:{id}";
}

internal static class JsonExtensions
{
    public static JsonElement? GetPropertyOrDefault(this JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v)) return v;
        return null;
    }
}