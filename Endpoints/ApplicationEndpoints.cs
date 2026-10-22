using System.Security.Claims;
using JobSeekerNet.Data;
using JobSeekerNet.Dtos;
using JobSeekerNet.Dtos.Applications;
using JobSeekerNet.Models;
using Microsoft.EntityFrameworkCore;

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

        group.MapGet("/", async (ClaimsPrincipal userClaims, JobSeekerContext dbContext, int page = 1, int pageSize= 10
        ,string? status=null, string? search = null, string? sort = "newest") =>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var query = dbContext.Applications.Where(a=>a.UserId == userId).AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(stat => stat.Status.ToLower() == status.ToLower());
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(q => q.Company.Contains(search) || q.Title.Contains(search));

            query = sort?.ToLower() switch
            {
                "oldest" => query.OrderBy(a => a.ApplicationDate ),
                "company" =>query.OrderBy(a => a.Company),
                _ => query.OrderByDescending(a=> a.ApplicationDate)
            };

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems/(double)pageSize);

            var applications = await query
            .Skip((page-1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

            var items = applications.Select(a=>
            {
               bool isStale = false;

               if(a.ApplicationDate.HasValue)
                {
                    var dayIdle = (DateTime.UtcNow - a.ApplicationDate.Value).TotalDays;
                    var earlyStages = new[]{"Wishlist", "Applied", "Assesment"};
                    var interviewStages = new[]{"HR Interview","User Interview"};


                    if (earlyStages.Contains(a.Status) && dayIdle > 14) isStale = true;
                    if (interviewStages.Contains(a.Status) && dayIdle > 7) isStale = true;
                } 

                var tagArray = string.IsNullOrEmpty(a.Tags) ? new List<string>() : a.Tags.Split(",").ToList();
                return new ApplicationResponseDto(
                    a.Id, a.Company, a.Title, a.Status, a.JobPostingLink,
                    a.Contact, a.ApplicationDate, a.SalaryRange, a.Notes, a.NextFollowUp,
                    a.FollowUpNotes, tagArray, isStale
                );
            });

            var response = new PagedResponseDto<ApplicationResponseDto>(page,pageSize,totalPages, totalItems,items);
            return Results.Ok(response);

        });
    }
}
