namespace JobSeekerNet.Dtos.Interviews;

public record InterviewResponseDto
(
    int Id,
    int ApplicationId,
    string Company,
    string? InterviewStage,
    string? Interrviewer,
    DateTime? ScheduleDate,
    string? LinkMeeting,
    string? Notes
);
