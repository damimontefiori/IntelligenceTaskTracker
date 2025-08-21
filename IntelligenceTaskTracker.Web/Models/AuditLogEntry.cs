namespace IntelligenceTaskTracker.Web.Models;

public class AuditLogEntry
{
    public int Id { get; set; }
    public string Entity { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty; // Created/Updated/Deleted/Commented
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
