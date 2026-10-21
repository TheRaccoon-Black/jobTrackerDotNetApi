using System.Security.Claims;
using JobSeekerNet.Data;
using JobSeekerNet.Dtos.Applications;
using JobSeekerNet.Models;

namespace JobSeekerNet.Endpoints;

public static class ApplicationEndpoins
{
    public static void MapApplicationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/applications").RequireAuthorization();
        
        group.MapPost("/", async(ApplicationRequestDto dto, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            string? normalizedtags = null;
            if (!string.IsNullOrWhiteSpace(dto.Tags))
            {
                var taglist = dto.Tags.Split(',')
                .Select(t =>t.Trim().ToLower())
                .Where(t=>!string.IsNullOrEmpty(t))
                .Distinct();
                normalizedtags = string.Join(",", taglist);
            }

            var application = new Application
            {
                UserId = userId,
                Company = dto.Company,
                Title = dto.Title,
                Status = dto.Status,
                JobPostingLink = dto.JobPostingLink,
                Contact = dto.Contact,
                ApplicationDate = dto.ApplicationDate,
                SalaryRange = dto.SalaryRange,
                Notes = dto.Notes,
                NextFollowUp = dto.NextFollowUp,
                FollowUpNotes = dto.FollowUpNotes,
                Tags = normalizedtags,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Applications.Add(application);
            await dbContext.SaveChangesAsync();


            var log = new ApplicationLog
            {
                ApplicationId = application.Id,
                UserId = userId,
                Event = "created",
                NewValue = dto.Status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.ApplicationLogs.Add(log);
            await dbContext.SaveChangesAsync();


            var tagArray = string.IsNullOrEmpty(application.Tags) ? new List<string>():application.Tags.Split(',').ToList();

            var response = new ApplicationResponseDto(
                application.Id, application.Company, application.Title, application.Status, application.JobPostingLink, application.Contact,
                application.ApplicationDate, application.SalaryRange, application.Notes, application.NextFollowUp, application.FollowUpNotes,
                tagArray, false
            );
            return Results.Created($"/api/applications/{application.Id}", response);

        });
    }
}
