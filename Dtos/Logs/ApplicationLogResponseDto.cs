namespace JobSeekerNet.Dtos.Logs;

public record ApplicationLogResponseDto
(
    int Id,
    string Event,
    string NewValue,
    DateTime CreatedAt
        
);
