using System.ComponentModel.DataAnnotations;

namespace JobSeekerNet.Dtos.User;

public record LoginRequestDto
(
    [Required] string Email,
    [Required] string Password
);
