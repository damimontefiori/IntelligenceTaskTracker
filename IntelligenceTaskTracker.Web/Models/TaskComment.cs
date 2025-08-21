using System.ComponentModel.DataAnnotations;

namespace IntelligenceTaskTracker.Web.Models;

public class TaskComment
{
    public int Id { get; set; }

    public int TaskId { get; set; }

    public TaskItem? Task { get; set; }

    [Required]
    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(200)]
    public string CreatedBy { get; set; } = "Anon";
}
