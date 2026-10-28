using System.Data;

namespace JobSeekerNet.Dtos.Interviews;

public record InterviewResponseDto
(
    int Id,
    int ApplicationId,
    string Company,
    string Title,
    string? InterviewStage,
    string? Interviewer,
    DateTime? ScheduleDate,
    string? LinkMeeting,
    string? Notes
);
 public record InterviewCalendarItemDto(
    int Id,
    string Company,
    string InterviewStage,
    string Time
 );