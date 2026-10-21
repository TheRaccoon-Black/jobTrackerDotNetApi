namespace JobSeekerNet.Dtos.Applications;

public record ApplicationResponseDto
(
    int Id,
    string Company,
    string Title,
    string Status,
    string? JobPostingLink,
    string? Contact,
    DateTime? ApplicationDate,
    string? SalaryRange,
    string? Notes,
    DateTime? NextFollowUp,
    string? FollowUpNotes,
    List<string> Tags,
    bool Stale
);
