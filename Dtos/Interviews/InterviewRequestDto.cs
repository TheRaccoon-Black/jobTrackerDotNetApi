using System.ComponentModel.DataAnnotations;

namespace JobSeekerNet.Dtos.Interviews;

public record InterviewRequestDto
(
        [Required] int ApplicationId,
        [Required][MaxLength(50)]string Round,
        [Required]DateTime ScheduleAt,
        [MaxLength(255)] string? Location,
        string? Note,
        [MaxLength(50)] string? result
);