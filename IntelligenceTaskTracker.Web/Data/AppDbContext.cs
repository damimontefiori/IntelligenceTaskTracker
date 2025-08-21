using IntelligenceTaskTracker.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelligenceTaskTracker.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(p => p.Name);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.Property(p => p.Title).IsRequired().HasMaxLength(200);
            entity.HasIndex(p => p.Status);
            entity.HasIndex(p => p.ResponsibleUserId);
            entity.HasIndex(p => p.DueDate);
            entity.HasOne(p => p.ResponsibleUser)
                  .WithMany(u => u.Tasks)
                  .HasForeignKey(p => p.ResponsibleUserId)
                  .OnDelete(DeleteBehavior.Restrict); // No borrar usuario con tareas
        });

        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.Property(p => p.Comment).IsRequired();
            entity.HasOne(p => p.Task)
                  .WithMany(t => t.Comments)
                  .HasForeignKey(p => p.TaskId)
                  .OnDelete(DeleteBehavior.Cascade); // Eliminar comentarios al borrar tarea
        });
    }
}
