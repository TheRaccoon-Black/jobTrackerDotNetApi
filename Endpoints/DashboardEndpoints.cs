using System.Security.Claims;
using JobSeekerNet.Data;
using JobSeekerNet.Dtos.Dashboards;
using Microsoft.EntityFrameworkCore;

namespace JobSeekerNet.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dashboard").RequireAuthorization();

        group.MapGet("/", async(ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdInt = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if (!int.TryParse(userIdInt, out int userId)) return Results.Unauthorized();

           var now = DateTime.UtcNow;
           var activeStatuses = new[]{"Wishlist","Applied","Assessment","HR Interview","User Interview"};
           var earlyStages = new[]{"Wishlist","Appied","Assessment"};
           var interviewStages = new[]{"HR Interview", "User Interview"};


           var applications = await dbContext.Applications
           .Where(a=>a.UserId == userId)
           .Select(a=> new{a.Id,a.Status,a.ApplicationDate,a.Company, a.Title, a.CreatedAt})
           .ToListAsync();

           int totalApplications = applications.Count();

           var byStatus = dbContext.Applications.GroupBy(a=>a.Status).ToDictionary(g=>g.Key, g=>g.Count());
           int activeCount = applications.Count(a=>activeStatuses.Contains(a.Status));

           int staleCount = applications.Count(c =>
           {
              var daysIdle = c.ApplicationDate.HasValue ?(now - c.ApplicationDate.Value).TotalDays : 0;
              if (earlyStages.Contains(c.Status) && daysIdle >=14) return true;
              if( interviewStages.Contains(c.Status) && daysIdle >=7) return true; 

              return false;
           });

           int upcomingInterviews = await dbContext.Interviews
           .Include(i=> i.Application)
           .CountAsync(i=>i.Application!.UserId == userId && i.ScheduleDate > now);

           var recentApplications = applications
           .OrderByDescending(a=>a.ApplicationDate ?? a.CreatedAt)
           .Take(5)
           .Select(a=> new RecentApplicationDto(a.Id, a.Company, a.Title,a.Status, a.ApplicationDate))
           .ToList();

           var response = new DashboardResponseDto(
            totalApplications,
            byStatus,
            activeCount,
            staleCount,
            upcomingInterviews,
            recentApplications
           );
           
           return Results.Ok(response);


        });
    }
}
