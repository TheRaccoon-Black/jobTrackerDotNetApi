namespace JobSeekerNet.Dtos.Documents;

public record DocumentResponseDto
(
    int Id,
    string FileName,
    string Type,
    int? ApplicationId,
    string? ApplicationCompany,
    string? ApplicationTitle,
    DateTime UploadedAt
);
