using IntelligenceTaskTracker.Web.Data;
using IntelligenceTaskTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelligenceTaskTracker.Web.Controllers;

public class UsersController(AppDbContext db) : Controller
{
    // GET: /Users
    public async Task<IActionResult> Index()
    {
        var users = await db.Users
            .OrderBy(u => u.Name)
            .ToListAsync();
        ViewBag.CannotDeleteMessage = TempData["CannotDeleteMessage"] as string;
        return View(users);
    }

    // GET: /Users/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: /Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name")] User input)
    {
        if (!ModelState.IsValid)
            return View(input);

        var user = new User
        {
            Name = input.Name.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET: /Users/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();
        return View(user);
    }

    // POST: /Users/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] User input)
    {
        if (id != input.Id) return BadRequest();
        if (!ModelState.IsValid) return View(input);

        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.Name = input.Name.Trim();
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // POST: /Users/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();

        var hasTasks = await db.Tasks.AnyAsync(t => t.ResponsibleUserId == id);
        if (hasTasks)
        {
            TempData["CannotDeleteMessage"] = "No se puede eliminar el usuario porque tiene tareas asignadas.";
            return RedirectToAction(nameof(Index));
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
