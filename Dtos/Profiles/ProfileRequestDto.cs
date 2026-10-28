using System.ComponentModel.DataAnnotations;
namespace JobSeekerNet.Dtos.Profiles;

public record ProfileUpdateRequestDto(
    [Required][MaxLength(255)] string Name,
    [Required][EmailAddress][MaxLength(255)] string Email,
    [Range(0,100)] int? WeeklyTarget
);

public record ProfileDeleteRequestDto(
    [Required] string Password
);

