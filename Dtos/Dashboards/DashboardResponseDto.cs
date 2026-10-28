namespace JobSeekerNet.Dtos.Dashboards;

public record DashboardResponseDto
(
    int TotalApplications,
    Dictionary<string, int> ByStatus,
    int ActiveCount,
    int StaleCount,
    int UpcomingInterviews,
    List<RecentApplicationDto> RecentApplications
);

public record RecentApplicationDto(
    int Id,
    string Company,
    string Title,
    string Status,
    DateTime? ApplicationDate
);
