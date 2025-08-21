using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IntelligenceTaskTracker.Web.Models;

public class TaskItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.NotAssigned;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DueDate { get; set; }

    public int? ResponsibleUserId { get; set; }

    [ForeignKey(nameof(ResponsibleUserId))]
    public User? ResponsibleUser { get; set; }

    // Navigation
    public List<TaskComment> Comments { get; set; } = new();
}
