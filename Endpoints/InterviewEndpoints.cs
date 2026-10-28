using System.Security.Claims;
using JobSeekerNet.Data;
using JobSeekerNet.Dtos.Interviews;
using JobSeekerNet.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobSeekerNet.Endpoints;

public static class InterviewEndpoints
{
    public static void MapInterviewPoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/interviews").RequireAuthorization();

       group.MapPost("/", async(InterviewRequestDto dto, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
            var UserIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(UserIdString, out int userId)) return Results.Unauthorized();

            var application = await dbContext.Applications.FirstOrDefaultAsync(a=>a.Id == dto.ApplicationId && a.UserId == userId);
            if (application == null) return Results.NotFound();

            var interview = new Interview
            {
                ApplicationId = dto.ApplicationId,
                InterviewStage = dto.InterviewStage,
                Interviewer = dto.Interviewer,
                ScheduleDate = dto.ScheduleDate,
                LinkMeeting = dto.LinkMeeting,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Interviews.Add(interview);
            if(!string.IsNullOrWhiteSpace(dto.InterviewStage) && application.Status != dto.InterviewStage)
            {
                application.Status = dto.InterviewStage;
                application.UpdatedAt = DateTime.UtcNow;

                dbContext.ApplicationLogs.Add(
                    new ApplicationLog
                    {
                        ApplicationId = dto.ApplicationId,
                        UserId = userId,
                        Event = "interview_scheduled",
                        NewValue = dto.InterviewStage,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                );

            }
            await dbContext.SaveChangesAsync();

            var response = new InterviewResponseDto(
                interview.Id,
                interview.ApplicationId,
                application.Company,
                application.Title,
                interview.InterviewStage,
                interview.Interviewer,
                interview.ScheduleDate,
                interview.LinkMeeting,
                interview.Notes
            );
            return Results.Created($"/api/interviews/{interview.Id}",response);
        });
        group.MapGet("/upcoming", async(ClaimsPrincipal userClaims, JobSeekerContext dbContext)=>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var now = DateTime.UtcNow;

            var upComingList = await dbContext.Interviews
            .Include(i=>i.Application)
            .Where(i=>i.Application != null
            && i.Application.UserId == userId
            && i.ScheduleDate >= now)
            .OrderBy(i => i.ScheduleDate)
            .Select(i => new InterviewResponseDto(
                i.Id,
                i.ApplicationId,
                i.Application != null ? i.Application.Company : string.Empty,
                i.Application != null ? i.Application.Title : string.Empty,
                i.InterviewStage,
                i.Interviewer,
                i.ScheduleDate,
                i.LinkMeeting,
                i.Notes
            )).ToListAsync();

            return Results.Ok(upComingList);
        });

        group.MapGet("/application/{applicationId:int}",async(int applicationId, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var application = await dbContext.Applications.FirstOrDefaultAsync(i=>i.Id == applicationId && i.UserId == userId);
            if (application == null) return Results.NotFound();

            var interviews = await dbContext.Interviews
            .Where(i=>i.ApplicationId == applicationId)
            .OrderBy(i=>i.ScheduleDate)
            .Select(i=>new InterviewResponseDto(
                i.Id,
                i.ApplicationId,
                application.Company,
                application.Title,
                i.InterviewStage,
                i.Interviewer,
                i.ScheduleDate,
                i.LinkMeeting,
                i.Notes
            )).ToListAsync();

            return Results.Ok(interviews);
        });


        group.MapPut("/{id:int}", async(int id, InterviewRequestDto dto, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var interview = await dbContext.Interviews
                            .Include(i=>i.Application)
                            .FirstOrDefaultAsync(i=>i.Id == id && i.Application != null && i.Application.UserId ==userId);

            if (interview == null) return Results.NotFound();

            interview.InterviewStage = dto.InterviewStage;
            interview.Interviewer = dto.Interviewer;
            interview.ScheduleDate = dto.ScheduleDate;
            interview.LinkMeeting = dto.LinkMeeting;
            interview.Notes = dto.Notes;
            interview.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            return Results.NoContent();


        }
        );

        group.MapDelete("/{id:int}",async(int id, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

           var interview = await dbContext.Interviews
                            .Include(i=>i.Application)
                            .FirstOrDefaultAsync(i=>i.Id == id && i.Application != null && i.Application.UserId == userId);
            if (interview == null) return Results.NotFound();

            dbContext.Interviews.Remove(interview);
                await dbContext.SaveChangesAsync();
                return Results.NoContent();

        });

        group.MapGet("/", async(ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdContext = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdContext, out int userId)) return Results.Unauthorized();

           var now = DateTime.UtcNow;

           var rawInterviews = await dbContext.Interviews.Include(i=>i.Application)
           .Where(i=>i.Application!.UserId == userId)
           .Select(i=> new InterviewResponseDto(
            i.Id, i.ApplicationId, i.Application!.Company,  i.Application.Title,
            i.InterviewStage,i.Interviewer,i.ScheduleDate,i.LinkMeeting, i.Notes
           )).ToListAsync();

           var response = new
           {
               upcoming = rawInterviews.Where(i=> i.ScheduleDate > now).OrderBy(i=>i.ScheduleDate).ToList(),
               past = rawInterviews.Where(i=> i.ScheduleDate <= now && i.ScheduleDate != null).OrderByDescending(i=>i.ScheduleDate).ToList(),
               unscheduled = rawInterviews.Where(i=> !i.ScheduleDate.HasValue).ToList()
           };

           return Results.Ok(response);

        });


        group.MapGet("/calendar", async(int? year, int? month, ClaimsPrincipal userClaims, JobSeekerContext dbContext)=>
        {
            var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var currentYear = year?? DateTime.UtcNow.Year;
            var currentMonth = month ?? DateTime.UtcNow.Month;

            var startDate = new DateTime(currentYear, currentMonth, 1,0,0,0,DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1).AddSeconds(-1);

            var interviews = await dbContext.Interviews.Include(i=>i.Application)
            .Where(i=>i.Application!.UserId == userId && i.ScheduleDate >= startDate && i.ScheduleDate <= endDate).ToListAsync();

            var days = interviews.GroupBy(i=>i.ScheduleDate!.Value.ToString("yyyy-MM-dd"))
            .ToDictionary(g=>g.Key,
            g=>g.Select(i=>new InterviewCalendarItemDto(
                i.Id, i.Application!.Company, i.InterviewStage?? "User Interview",i.ScheduleDate!.Value.ToString("HH:mm")
            )).ToList());

            return Results.Ok(new
            {
                year = currentYear,
                month = currentMonth,
                days = days
            });
        });



    
    }
}
