using IntelligenceTaskTracker.Web.Data;
using IntelligenceTaskTracker.Web.Models;
using IntelligenceTaskTracker.Web.Services.AI;
using IntelligenceTaskTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = IntelligenceTaskTracker.Web.Models.TaskStatus;

namespace IntelligenceTaskTracker.Web.Controllers;

public class TasksController(AppDbContext db, IInsightsService? insightsService = null) : Controller
{
    // GET: /Tasks
    public async Task<IActionResult> Index(string? q, TaskStatusEnum? status, int page = 1, int pageSize = 10)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 10;

        var query = db.Tasks
            .Include(t => t.ResponsibleUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(t =>
                t.Title.Contains(term) ||
                (t.Description != null && t.Description.Contains(term)) ||
                (t.ResponsibleUser != null && t.ResponsibleUser.Name.Contains(term))
            );
        }
        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(t => t.Status)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new TaskListViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Q = q,
            Status = status.HasValue ? (IntelligenceTaskTracker.Web.Models.TaskStatus?)status.Value : null
        };
        return View(vm);
    }

    // GET: /Tasks/Details/5
    public async Task<IActionResult> Details(int id, string? returnUrl = null)
    {
        var task = await db.Tasks
            .Include(t => t.ResponsibleUser)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();
        // Ordenar comentarios por fecha desc (recientes primero)
        task.Comments = task.Comments.OrderByDescending(c => c.CreatedAt).ToList();

        // Preservar URL de origen si es local (y evitar loop a Details)
        string? resolvedReturnUrl = returnUrl;
        if (string.IsNullOrWhiteSpace(resolvedReturnUrl))
        {
            var referer = Request.Headers.Referer.ToString();
            if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            {
                if (string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
                {
                    var local = uri.PathAndQuery;
                    if (Url.IsLocalUrl(local) && !local.StartsWith($"/Tasks/Details/{id}", StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedReturnUrl = local;
                    }
                }
            }
        }
        ViewBag.ReturnUrl = resolvedReturnUrl;

        return View(task);
    }

    // GET: /Tasks/Create
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return View(new TaskItem { Status = TaskStatusEnum.New });
    }

    // POST: /Tasks/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Title,Description,DueDate,ResponsibleUserId,Status")] TaskItem input)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(input);
        }

        // Validar usuario responsable si se envía
        if (input.ResponsibleUserId is int uid)
        {
            var exists = await db.Users.AnyAsync(u => u.Id == uid);
            if (!exists)
            {
                ModelState.AddModelError(nameof(TaskItem.ResponsibleUserId), "Usuario seleccionado no existe.");
                await PopulateDropdownsAsync();
                return View(input);
            }
        }

        var entity = new TaskItem
        {
            Title = input.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            DueDate = input.DueDate,
            ResponsibleUserId = input.ResponsibleUserId,
            Status = input.Status,
            CreatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(entity);
        await db.SaveChangesAsync();
        await LogAuditAsync("TaskItem", entity.Id, "Created", $"Title='{entity.Title}', Status={entity.Status}");
        return RedirectToAction(nameof(Index));
    }

    // GET: /Tasks/Edit/5
    public async Task<IActionResult> Edit(int id, string? returnUrl)
    {
        var task = await db.Tasks
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();
        await PopulateDropdownsAsync();
        task.Comments = task.Comments.OrderByDescending(c => c.CreatedAt).ToList();
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            ViewBag.ReturnUrl = returnUrl;
        }
        return View(task);
    }

    // POST: /Tasks/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,DueDate,ResponsibleUserId,Status")] TaskItem input, string? returnUrl)
    {
        if (id != input.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                ViewBag.ReturnUrl = returnUrl;
            return View(input);
        }

        var task = await db.Tasks.FindAsync(id);
        if (task == null) return NotFound();

        if (input.ResponsibleUserId is int uid)
        {
            var exists = await db.Users.AnyAsync(u => u.Id == uid);
            if (!exists)
            {
                ModelState.AddModelError(nameof(TaskItem.ResponsibleUserId), "Usuario seleccionado no existe.");
                await PopulateDropdownsAsync();
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    ViewBag.ReturnUrl = returnUrl;
                return View(input);
            }
        }

        var old = ($"Title='{task.Title}', Status={task.Status}, Responsible={task.ResponsibleUserId?.ToString() ?? "null"}, Due={task.DueDate?.ToString("o") ?? "null"}");
        var oldResponsibleUserId = task.ResponsibleUserId;
        task.Title = input.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        task.DueDate = input.DueDate;
        task.ResponsibleUserId = input.ResponsibleUserId;
        task.Status = input.Status;

        await db.SaveChangesAsync();
        var @new = ($"Title='{task.Title}', Status={task.Status}, Responsible={task.ResponsibleUserId?.ToString() ?? "null"}, Due={task.DueDate?.ToString("o") ?? "null"}");
        await LogAuditAsync("TaskItem", task.Id, "Updated", $"{old} -> {@new}");

        // Invalidar caché AI si hay servicio disponible
        if (insightsService != null)
        {
            insightsService.InvalidateForTask(task.Id);
            if (oldResponsibleUserId is int oldUserId)
            {
                insightsService.InvalidateForUser(oldUserId);
            }
            if (task.ResponsibleUserId is int newUserId)
            {
                insightsService.InvalidateForUser(newUserId);
            }
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    // POST: /Tasks/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var task = await db.Tasks.FindAsync(id);
        if (task == null) return NotFound();
        db.Tasks.Remove(task);
        await db.SaveChangesAsync();
        await LogAuditAsync("TaskItem", id, "Deleted", $"Title='{task.Title}'");
        return RedirectToAction(nameof(Index));
    }

    // POST: /Tasks/AddComment/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int id, string createdBy, string comment, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(createdBy))
            ModelState.AddModelError(nameof(createdBy), "Ingrese su nombre.");
        if (string.IsNullOrWhiteSpace(comment))
            ModelState.AddModelError(nameof(comment), "El comentario es obligatorio.");

        // Sanitizar returnUrl para que sea siempre local
        string? sanitizedReturnUrl = null;
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            sanitizedReturnUrl = returnUrl;
        }

        if (!ModelState.IsValid)
        {
            // Re-render Details with current task and ModelState errors, preserving returnUrl
            var taskVm = await db.Tasks
                .Include(t => t.ResponsibleUser)
                .Include(t => t.Comments)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (taskVm == null) return NotFound();
            taskVm.Comments = taskVm.Comments.OrderByDescending(c => c.CreatedAt).ToList();
            ViewBag.ReturnUrl = sanitizedReturnUrl;
            return View("Details", taskVm);
        }

        var entry = new TaskComment
        {
            TaskId = id,
            Comment = comment.Trim(),
            CreatedBy = createdBy.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        db.TaskComments.Add(entry);
        await db.SaveChangesAsync();
        await LogAuditAsync("TaskItem", id, "Commented", $"By='{entry.CreatedBy}', Len={entry.Comment.Length}");
        
        // Invalidar caché AI si hay servicio disponible
        if (insightsService != null)
        {
            insightsService.InvalidateForTask(id);
            var task = await db.Tasks.FindAsync(id);
            if (task?.ResponsibleUserId is int userId)
            {
                insightsService.InvalidateForUser(userId);
            }
        }
        
        return RedirectToAction(nameof(Details), new { id, returnUrl = sanitizedReturnUrl });
    }

    // POST: /Tasks/ChangeStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, TaskStatusEnum status)
    {
        var task = await db.Tasks.FindAsync(id);
        if (task == null) return NotFound();
        var old = task.Status;
        if (task.Status == status) return Ok(new { unchanged = true });
        task.Status = status;
        await db.SaveChangesAsync();
        await LogAuditAsync("TaskItem", task.Id, "Updated", $"Status: {old} -> {status}");
        return Ok(new { ok = true });
    }

    // GET: /Tasks/GetInsight/5?refresh=true
    public async Task<IActionResult> GetInsight(int id, bool refresh = false)
    {
        if (insightsService == null) return Json(new { error = "Servicio IA no disponible" });
        
        try
        {
            var insight = await insightsService.GetTaskInsightAsync(id, refresh);
            if (insight == null) return Json(new { error = "Tarea no encontrada" });
            return Json(insight);
        }
        catch
        {
            return Json(new { error = "Error obteniendo resumen IA" });
        }
    }

    private async Task PopulateDropdownsAsync()
    {
        var users = await db.Users.OrderBy(u => u.Name).ToListAsync();
        ViewBag.Users = new SelectList(users, nameof(Models.User.Id), nameof(Models.User.Name));
        ViewBag.Statuses = Enum.GetValues(typeof(TaskStatusEnum))
            .Cast<TaskStatusEnum>()
            .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = s.ToString() })
            .ToList();
    }

    // Método de debug para probar la IA
    [HttpGet]
    public async Task<IActionResult> DebugAI(int id)
    {
        if (insightsService == null) 
            return Json(new { error = "Servicio de IA no disponible" });

        try
        {
            // Forzar refrescado para probar la API real
            var insight = await insightsService.GetTaskInsightAsync(id, forceRefresh: true);
            if (insight == null) 
                return Json(new { error = "Tarea no encontrada" });
            
            return Json(new { 
                success = true, 
                taskId = id, 
                insight = insight,
                message = "Resumen generado usando IA de OpenAI GPT-4o"
            });
        }
        catch (Exception ex)
        {
            return Json(new { 
                error = "Error en servicio IA", 
                details = ex.Message,
                type = ex.GetType().Name
            });
        }
    }

    // Método temporal para verificar qué tareas existen
    [HttpGet]
    public async Task<IActionResult> DebugListTasks()
    {
        var tasks = await db.Tasks
            .Include(t => t.ResponsibleUser)
            .Select(t => new { 
                t.Id, 
                t.Title, 
                t.Status, 
                ResponsibleUser = t.ResponsibleUser != null ? t.ResponsibleUser.Name : null,
                CommentsCount = t.Comments.Count()
            })
            .ToListAsync();
        
        return Json(new { 
            totalTasks = tasks.Count,
            tasks = tasks
        });
    }

    private async Task LogAuditAsync(string entity, int entityId, string action, string? details)
    {
        db.AuditLogs.Add(new AuditLogEntry
        {
            Entity = entity,
            EntityId = entityId,
            Action = action,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
