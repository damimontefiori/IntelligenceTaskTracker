using System.ComponentModel.DataAnnotations;

namespace IntelligenceTaskTracker.Web.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<TaskItem> Tasks { get; set; } = new();
}
