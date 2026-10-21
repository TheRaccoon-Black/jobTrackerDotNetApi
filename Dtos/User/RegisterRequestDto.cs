using System.ComponentModel.DataAnnotations;

namespace JobSeekerNet.Dtos.User;

public record RegisterRequestDto (

    [Required][StringLength(50)] string Name,
    [Required][EmailAddress] string Email,
    [Required][MinLength(8)]string Password

);
