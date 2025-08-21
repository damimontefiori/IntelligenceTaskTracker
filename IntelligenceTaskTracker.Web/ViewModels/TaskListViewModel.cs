using IntelligenceTaskTracker.Web.Models;

namespace IntelligenceTaskTracker.Web.ViewModels;

public class TaskListViewModel
{
    public IEnumerable<TaskItem> Items { get; set; } = Enumerable.Empty<TaskItem>();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public string? Q { get; set; }
    public IntelligenceTaskTracker.Web.Models.TaskStatus? Status { get; set; }
}
