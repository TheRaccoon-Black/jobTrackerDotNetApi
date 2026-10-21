
namespace JobSeekerNet.Dtos.User;

public record UserResponseDto
(
    int Id,
    string Name,
    string Email,
    int WeeklyTarget
);
