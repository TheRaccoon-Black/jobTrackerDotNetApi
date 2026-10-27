using System;
using System.ComponentModel.DataAnnotations;

namespace JobSeekerNet.Dtos.Applications;

public record StatusUpdateRequestDto(
    [Required] string Status
);

public record FollowUpRequestDto(
    DateTime? NextFollowUp,
    string? FollowUpNotes
);

public record BulkActionRequestDto(
    [Required] string Action,
    string? Status,
    [Required] List<int> Ids
);
