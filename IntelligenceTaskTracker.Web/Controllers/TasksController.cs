using IntelligenceTaskTracker.Web.Data;
using IntelligenceTaskTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = IntelligenceTaskTracker.Web.Models.TaskStatus;

namespace IntelligenceTaskTracker.Web.Controllers;

public class TasksController(AppDbContext db) : Controller
{
    // GET: /Tasks
    public async Task<IActionResult> Index()
    {
        var tasks = await db.Tasks
            .Include(t => t.ResponsibleUser)
            .OrderBy(t => t.Status)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();
        return View(tasks);
    }

    // GET: /Tasks/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var task = await db.Tasks
            .Include(t => t.ResponsibleUser)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();
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
        return RedirectToAction(nameof(Index));
    }

    // GET: /Tasks/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var task = await db.Tasks.FindAsync(id);
        if (task == null) return NotFound();
        await PopulateDropdownsAsync();
        return View(task);
    }

    // POST: /Tasks/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,DueDate,ResponsibleUserId,Status")] TaskItem input)
    {
        if (id != input.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
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
                return View(input);
            }
        }

        task.Title = input.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        task.DueDate = input.DueDate;
        task.ResponsibleUserId = input.ResponsibleUserId;
        task.Status = input.Status;

        await db.SaveChangesAsync();
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
        return RedirectToAction(nameof(Index));
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
}
