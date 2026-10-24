using System.ComponentModel.DataAnnotations;

namespace JobSeekerNet.Dtos.Interviews;

public record InterviewRequestDto
(
        [Required] int ApplicationId,
        [Required][MaxLength(50)] string InterviewStage,
        [MaxLength(255)] string? Interviewer,
        [Required] DateTime ScheduleDate,
        [MaxLength(2048)] string? LinkMeeting,
        string? Notes
);