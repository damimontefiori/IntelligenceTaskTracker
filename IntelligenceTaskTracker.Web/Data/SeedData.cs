using IntelligenceTaskTracker.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelligenceTaskTracker.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync())
        {
            var alice = new User { Name = "Alice" };
            var bob = new User { Name = "Bob" };
            db.Users.AddRange(alice, bob);
            await db.SaveChangesAsync();

            db.Tasks.AddRange(
                new TaskItem
                {
                    Title = "Definir backlog MVP",
                    Description = "Crear lista inicial de tareas",
                    Status = Models.TaskStatus.New,
                    ResponsibleUserId = alice.Id,
                },
                new TaskItem
                {
                    Title = "Configurar CI/CD",
                    Description = "Pipeline básico",
                    Status = Models.TaskStatus.InProgress,
                    ResponsibleUserId = bob.Id,
                },
                new TaskItem
                {
                    Title = "Crear estructura MVC",
                    Description = "Proyecto base",
                    Status = Models.TaskStatus.NotAssigned
                }
            );
            await db.SaveChangesAsync();
        }
    }
}
