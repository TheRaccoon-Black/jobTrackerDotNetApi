using System.Diagnostics;
using System.Security.Claims;
using JobSeekerNet.Data;
using JobSeekerNet.Dtos;
using JobSeekerNet.Dtos.Applications;
using JobSeekerNet.Dtos.Logs;
using JobSeekerNet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client.Extensibility;
using Microsoft.IdentityModel.Tokens;

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

        group.MapGet("/{id:int}", async(int id, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var application = await dbContext.Applications.FirstOrDefaultAsync(a=>a.Id == id && a.UserId == userId);
            if (application == null) return Results.NotFound();

            bool isStale = false;

            if(application.ApplicationDate.HasValue)
            {
                var dayIdle = (DateTime.UtcNow - application.ApplicationDate.Value).TotalDays;
                var earlyStages = new[]{"Wishlist","Applied","Assessment"};
                var interviewStages = new[]{"HRD Interview", "User Interview"};

                if (earlyStages.Contains(application.Status) && dayIdle > 14) isStale = true;
                if (interviewStages.Contains(application.Status) && dayIdle > 7) isStale = true;
            }

            var tagArray = string.IsNullOrEmpty(application.Tags)? new List<string>(): application.Tags.Split(",").ToList();
            var response = new ApplicationResponseDto(
                application.Id, application.Company, application.Title,application.Status, application.JobPostingLink,
                application.Contact, application.ApplicationDate, application.SalaryRange, application.Notes, application.NextFollowUp,
                application.FollowUpNotes, tagArray, isStale
            );
            return Results.Ok(response);
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

        group.MapPost("/{id:int}", async(int id, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var application = await dbContext.Applications.FirstOrDefaultAsync(a=>a.Id == id && a.UserId == userId);
            if (application == null) return Results.NotFound();

            bool isStale = false;
            if (application.ApplicationDate.HasValue)
            {
                var dayIdle = (DateTime.UtcNow - application.ApplicationDate.Value).TotalDays;
                var earlyStages = new[]{"Wishlist","Applied", "Assesment"};
                var interviewStages = new[]{"HRD Interview", "User Interview"};

                if (earlyStages.Contains(application.Status) && dayIdle > 14) isStale = true;
                if (interviewStages.Contains(application.Status) && dayIdle > 7) isStale = true;

            }

            var tagArray = string.IsNullOrEmpty(application.Tags)? new List<string>(): application.Tags.Split(",").ToList();

            var response = new ApplicationResponseDto(
                application.Id, application.Company, application.Title, application.Status,
                application.JobPostingLink, application.Contact, application.ApplicationDate, 
                application.SalaryRange, application.Notes, application.NextFollowUp, application.FollowUpNotes,
                tagArray, isStale
            );

            return Results.Ok(response);

        });

        group.MapPut("/{id:int}", async (int id, ApplicationRequestDto dto, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(!int.TryParse(userIdString,out int userId)) return Results.Unauthorized();

            var application = await dbContext.Applications.FirstOrDefaultAsync(a=> a.UserId == userId && a.Id == id);

            if (application == null) return Results.NotFound();
            bool isStatusChange = application.Status != dto.Status;

            string? normalizedTags = null;
            if (!string.IsNullOrWhiteSpace(dto.Tags))
            {
                var tagList = dto.Tags.Split(',')
                .Select(t => t.Trim().ToLower())
                .Where(t=> !string.IsNullOrEmpty(t))
                .Distinct();
                normalizedTags = string.Join(",", tagList);
            }

            application.Company = dto.Company;
            application.Title = dto.Company;
            application.Status = dto.Status;
            application.JobPostingLink = dto.JobPostingLink;
            application.Contact = dto.Contact;
            application.ApplicationDate = dto.ApplicationDate;
            application.SalaryRange = dto.SalaryRange;
            application.Notes = dto.Notes;
            application.NextFollowUp = dto.NextFollowUp;
            application.FollowUpNotes = dto.FollowUpNotes;
            application.Tags = normalizedTags;
            application.UpdatedAt = DateTime.UtcNow;

            if (isStatusChange)
            {
                var log = new ApplicationLog
                {
                    ApplicationId = application.Id,
                    UserId = userId,
                    Event = "status_updated",
                    NewValue = dto.Status,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.ApplicationLogs.Add(log);
            }
            await dbContext.SaveChangesAsync();

            return Results.NoContent();

        });

        group.MapDelete("/{id:int}", async(int id, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var application = await dbContext.Applications.FirstOrDefaultAsync(a=> a.Id == id && a.UserId == userId);;
            if (application == null) return Results.NotFound();

            var logs = await dbContext.ApplicationLogs.Where(s=>s.ApplicationId == id).ToListAsync();

            dbContext.ApplicationLogs.RemoveRange(logs);

            dbContext.Applications.Remove(application);

            await dbContext.SaveChangesAsync();

            return Results.NoContent();
        });

        group.MapGet("/{id:int}/logs",async(int id, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

           var applicationExist = dbContext.Applications.AnyAsync(a=>a.Id == id && a.UserId == userId);
           if (applicationExist == null) return Results.NotFound();

           var logs = await dbContext.ApplicationLogs
           .Where(l => l.ApplicationId == id)
           .OrderByDescending(l=>l.CreatedAt)
           .Select(l=>new ApplicationLogResponseDto(
            l.Id, l.Event, l.NewValue, l.CreatedAt ?? DateTime.UtcNow
           )).ToListAsync();

           return Results.Ok(logs);
        });

        group.MapGet("/tags",async(ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var tagLists = await dbContext.Applications
            .Where(i=>i.UserId == userId && !string.IsNullOrEmpty(i.Tags))
            .Select(i=>i.Tags)
            .ToListAsync();

            var uniqueTags = tagLists
            .SelectMany(t=>t!.Split(","))
            .Select(t=>t.Trim().ToLower())
            .Where(t=>!string.IsNullOrEmpty(t))
            .Distinct()
            .OrderBy(t=>t)
            .ToList();

            return Results.Ok(new {tags = uniqueTags});

        });



        group.MapPost("/{id:int}/status", async(int id, ClaimsPrincipal userClaims, JobSeekerContext dbContext, StatusUpdateRequestDto dto) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

           var app = await dbContext.Applications.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);
           if(app == null) return Results.NotFound();

           if (app.Status != dto.Status)
            {
                var oldStatus = app.Status;
                app.Status = dto.Status;
                app.UpdatedAt = DateTime.UtcNow;

                dbContext.ApplicationLogs.Add(new ApplicationLog
                {
                    ApplicationId = app.Id,
                    UserId = userId,
                    Event = "status_changed",
                    NewValue = dto.Status,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                await dbContext.SaveChangesAsync();

            }
            return Results.NoContent();
        });

        group.MapPost("/{id:int}/follow-up", async(int id, FollowUpRequestDto dto, ClaimsPrincipal userClaims, JobSeekerContext dbContext)=>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var application = await dbContext.Applications.FirstOrDefaultAsync(i=>i.Id == id && i.UserId == userId);
            if (application == null) return Results.NotFound();

            bool isDateChanged = application.NextFollowUp != dto.NextFollowUp;

            application.NextFollowUp = dto.NextFollowUp;
            application.FollowUpNotes= dto.FollowUpNotes;
            application.UpdatedAt = DateTime.UtcNow;

            if (isDateChanged && dto.NextFollowUp.HasValue)
            {
                dbContext.ApplicationLogs.Add(new ApplicationLog
                {
                ApplicationId = application.Id,
                UserId = userId,
                Event = "follow_up_set",
                NewValue = dto.NextFollowUp.Value.ToString("yyyy-MM-dd"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
                });
            }
            await dbContext.SaveChangesAsync();
            return Results.NoContent();

        });

        group.MapPost("/bulk", async(BulkActionRequestDto dto, ClaimsPrincipal userClaims, JobSeekerContext dbContex) =>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            if (dto.Ids == null || !dto.Ids.Any())
                return Results.BadRequest(new {errors = new {ids = new[]{"Ids cannot be empty."}}});
            var appToUpdate = await dbContex.Applications
            .Where(i=>i.UserId == userId && dto.Ids.Contains(i.Id))
            .ToListAsync();

            if (!appToUpdate.Any()) 
                return Results.Ok(new {affected = 0, message = "No valid applications found.."});
            
            int affectedCount = appToUpdate.Count();

            if (dto.Action == "delete")
            {
                dbContex.Applications.RemoveRange(appToUpdate);
                await dbContex.SaveChangesAsync();
                return Results.Ok(new {affected = affectedCount, message = $"Deleted {affectedCount} application(s)."});
            }else if(dto.Action == "set_status")
            {
                if (string.IsNullOrWhiteSpace(dto.Status))
                    return Results.BadRequest(new { errors = new {status = new[]{"Status is requiered."}}});

                foreach (var app in appToUpdate)
                {
                    if (app.Status != dto.Status)
                    {
                        app.Status = dto.Status;
                        app.UpdatedAt = DateTime.UtcNow;

                        dbContex.ApplicationLogs.Add(new ApplicationLog
                        {
                            ApplicationId = app.Id,
                            UserId = userId,
                            Event = "status_changed",
                            NewValue = dto.Status + " (bulk)",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });

                    }
                }
                await dbContex.SaveChangesAsync();
                return Results.Ok(new{affected = affectedCount, message = $"Status of {affectedCount} application(s) updated to {dto.Status}."});

            }
            return Results.BadRequest(new{errors = new {action = new []{"Invalid action"}}});

        });

        group.MapGet("/export",async(ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();


            var application =  await dbContext.Applications
            .Where(a => a.UserId == userId)
            .OrderByDescending(a=>a.ApplicationDate)
            .Take(5000)
            .ToListAsync();

            var builder = new System.Text.StringBuilder();

            builder.AppendLine("Company, Role, Status, Salary Range, Application Date, Contact, Job Posting Link, Notes");
            string EscapeCsv(string? text)
            {
                if (string.IsNullOrEmpty(text)) return "";
                if (text.Contains(',') || text.Contains('"') || text.Contains("\n") || text.Contains('\r'))
                {
                    return $"\"{text.Replace("\"","\"\"")}\"";

                }
                return text;
            }

            foreach (var app in application)
            {
                var dateStr = app.ApplicationDate.HasValue ? app.ApplicationDate.Value.ToString("yyyy-MM-dd"): "";
                builder.AppendLine($"{EscapeCsv(app.Company)}, {EscapeCsv(app.Title)},{EscapeCsv(app.Status)},{EscapeCsv(app.SalaryRange)},{dateStr},{EscapeCsv(app.Contact)},{EscapeCsv(app.JobPostingLink)},{EscapeCsv(app.Notes)}");


                
            }
            var fileName = $"applications-{DateTime.UtcNow:yyyyMMddHHmm}.csv";
            var fileBytes = System.Text.Encoding.UTF8.GetBytes(builder.ToString());

            return Results.File(fileBytes, "text/csv",fileDownloadName: fileName);


        });

        group.MapGet("/{id:int}/draft-follow-up",async(int id, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

           var userName = userClaims.FindFirst(ClaimTypes.Name)?.Value ?? "applicant";

           var app = await dbContext.Applications.FirstOrDefaultAsync(i=>i.Id == id && i.UserId == userId);
           if (app == null) return Results.NotFound();

           var to = !string.IsNullOrWhiteSpace(app.Contact) ?app.Contact : "Hiring Team";
           var subject = $"Following up : {app.Title} application at {app.Company}";
           var body = "";


           var daysIdle = app.ApplicationDate.HasValue?(DateTime.UtcNow - app.ApplicationDate.Value).TotalDays: 0;

           if (daysIdle >= 14)
            {
                body = $"Dear {to},\n\n" +
                       $"I hope this message finds you well. I am writing to follow up on my application for the {app.Title} position submitted on {app.ApplicationDate?.ToString("MMMM dd, yyyy")}. " +
                       $"I am still very interested in the opportunity to join {app.Company} and would love to know if there are any updates regarding my application status.\n\n" +
                       $"Please let me know if you need any additional information or documents from my end.\n\n" +
                       $"Best regards,\n{userName}";
            }
            else
            {
                body = $"Dear {to},\n\n" +
                       $"I am following up on my application for the {app.Title} position at {app.Company}. " +
                       $"I remain very interested in the role and look forward to any updates you might have on the hiring process.\n\n" +
                       $"Thank you for your time.\n\n" +
                       $"Best regards,\n{userName}";
            }

            return Results.Ok(new
            {
                to = to,
                subject = subject,
                body = body
            });
        });
    }
}
