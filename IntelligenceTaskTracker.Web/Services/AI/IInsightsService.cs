using IntelligenceTaskTracker.Web.Models;

namespace IntelligenceTaskTracker.Web.Services.AI;

public record InsightAlert(string Code, string Severity, string Message);
public record TaskInsight(int TaskId, string Title, string Summary, string Status, string RiskLevel, List<InsightAlert> Alerts, List<string> NextActions);
public record UserInsight(int UserId, string UserName, string Summary, string OverallStatus, string RiskLevel, List<InsightAlert> Alerts, List<TaskInsight> TaskSummaries);

public interface IInsightsService
{
    Task<UserInsight?> GetUserInsightAsync(int userId, bool forceRefresh = false, CancellationToken ct = default);
    Task<TaskInsight?> GetTaskInsightAsync(int taskId, bool forceRefresh = false, CancellationToken ct = default);
    void InvalidateForUser(int userId);
    void InvalidateForTask(int taskId);
}
