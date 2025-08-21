using IntelligenceTaskTracker.Web.Models;

namespace IntelligenceTaskTracker.Web.ViewModels;

public class KanbanColumn
{
    public string Title { get; set; } = string.Empty;
    public List<TaskItem> Tasks { get; set; } = new();
}

public class KanbanBoardViewModel
{
    public List<KanbanColumn> Columns { get; set; } = new();
}

public class TasksByUserGroup
{
    public string GroupName { get; set; } = string.Empty; // Usuario o "Not Assigned"
    public int? UserId { get; set; }
    public List<TaskItem> Tasks { get; set; } = new();
}

public class ByResourceViewModel
{
    public List<TasksByUserGroup> Groups { get; set; } = new();
    public int? FilterUserId { get; set; }
}
