using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace JobSeekerNet.Dtos.Applications;

public record ApplicationRequestDto(
    [Required]string Company,
    [Required]string Title,
    [Required]string Status,
    [MaxLength(2024)] string? JobPostingLink,
    [MaxLength(255)]string? Contact,
    DateTime ApplicationDate,
    [MaxLength(255)] string? SalaryRange,
    string? Notes,
    DateTime? NextFollowUp,
    string? FollowUpNotes,
    string? Tags

);

