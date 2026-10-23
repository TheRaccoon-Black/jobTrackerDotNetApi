namespace JobSeekerNet.Dtos.Interviews;

public record InterviewResponseDto
(
    int Id,
    int ApplicationId,
    string Company,
    string Round,
    DateTime ScheduleAt,
    string? Location,
    string? Notes,
    string? Result
);
