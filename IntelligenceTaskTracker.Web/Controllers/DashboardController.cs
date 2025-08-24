using IntelligenceTaskTracker.Web.Data;
using IntelligenceTaskTracker.Web.Models;
using IntelligenceTaskTracker.Web.Services.AI;
using IntelligenceTaskTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = IntelligenceTaskTracker.Web.Models.TaskStatus;

namespace IntelligenceTaskTracker.Web.Controllers;

public class DashboardController(AppDbContext db, IInsightsService? insightsService = null) : Controller
{
    // GET: /Dashboard
    public async Task<IActionResult> Index()
    {
        var tasks = await db.Tasks.Include(t => t.ResponsibleUser).ToListAsync();

        var notAssigned = tasks.Where(t => t.ResponsibleUserId == null).ToList();
        var newTasks = tasks.Where(t => t.Status == TaskStatusEnum.New).ToList();
        var inProgress = tasks.Where(t => t.Status == TaskStatusEnum.InProgress).ToList();
        var completed = tasks.Where(t => t.Status == TaskStatusEnum.Completed).ToList();

        // Orden dentro de cada columna: New, InProgress, Completed (aplica donde tenga sentido)
        KanbanColumn OrderCol(string title, List<TaskItem> list)
        {
            var ordered = list
                .OrderBy(t => t.Status == TaskStatusEnum.New ? 0 : t.Status == TaskStatusEnum.InProgress ? 1 : 2)
                .ThenByDescending(t => t.CreatedAt)
                .ToList();
            return new KanbanColumn { Title = title, Tasks = ordered };
        }

        var vm = new KanbanBoardViewModel
        {
            Columns = new List<KanbanColumn>
            {
                OrderCol("Not Assigned", notAssigned),
                OrderCol("New", newTasks),
                OrderCol("In Progress", inProgress),
                OrderCol("Completed", completed)
            }
        };

        return View(vm);
    }

    // GET: /Dashboard/ByResource?userId=1
    public async Task<IActionResult> ByResource(int? userId)
    {
        var tasks = await db.Tasks.Include(t => t.ResponsibleUser).ToListAsync();
        if (userId.HasValue)
        {
            tasks = tasks.Where(t => t.ResponsibleUserId == userId.Value).ToList();
        }

        var users = await db.Users.OrderBy(u => u.Name).ToListAsync();

        // Agrupar por usuario y agregar grupo Not Assigned
        var groups = new List<TasksByUserGroup>();

        if (!userId.HasValue)
        {
            // Sin filtro: incluir columna Not Assigned
            groups.Add(new TasksByUserGroup
            {
                GroupName = "Not Assigned",
                UserId = null,
                Tasks = tasks.Where(t => t.ResponsibleUserId == null)
                              .OrderBy(t => t.Status == TaskStatusEnum.New ? 0 : t.Status == TaskStatusEnum.InProgress ? 1 : 2)
                              .ThenByDescending(t => t.CreatedAt)
                              .ToList()
            });

            foreach (var u in users)
            {
                var list = tasks.Where(t => t.ResponsibleUserId == u.Id)
                                 .OrderBy(t => t.Status == TaskStatusEnum.New ? 0 : t.Status == TaskStatusEnum.InProgress ? 1 : 2)
                                 .ThenByDescending(t => t.CreatedAt)
                                 .ToList();
                groups.Add(new TasksByUserGroup { GroupName = u.Name, UserId = u.Id, Tasks = list });
            }
        }
        else
        {
            // Con filtro: mostrar solo el recurso seleccionado
            var u = users.FirstOrDefault(x => x.Id == userId.Value);
            if (u != null)
            {
                var list = tasks.Where(t => t.ResponsibleUserId == u.Id)
                                 .OrderBy(t => t.Status == TaskStatusEnum.New ? 0 : t.Status == TaskStatusEnum.InProgress ? 1 : 2)
                                 .ThenByDescending(t => t.CreatedAt)
                                 .ToList();
                groups.Add(new TasksByUserGroup { GroupName = u.Name, UserId = u.Id, Tasks = list });
            }
        }

        var vm = new ByResourceViewModel { Groups = groups, FilterUserId = userId };
        ViewBag.Users = users;
        return View(vm);
    }

    // GET: /Dashboard/GetUserInsight/5?refresh=true
    public async Task<IActionResult> GetUserInsight(int id, bool refresh = false)
    {
        if (insightsService == null) return Json(new { error = "Servicio IA no disponible" });
        
        try
        {
            var insight = await insightsService.GetUserInsightAsync(id, refresh);
            if (insight == null) return Json(new { error = "Usuario no encontrado" });
            return Json(insight);
        }
        catch
        {
            return Json(new { error = "Error obteniendo resumen IA" });
        }
    }
}
