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
        var group = app.MapGroup("/inteviews").RequireAuthorization();

       group.MapPost("/", async(InterviewRequestDto dto, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
            var UserIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(UserIdString, out int userId)) return Results.Unauthorized();

            var application = await dbContext.Applications.FirstOrDefaultAsync(a=>a.Id == dto.ApplicationId && a.UserId == userId);
            if (application == null) return Results.NotFound();

            var interview = new Interview
            {
                ApplicationId = dto.ApplicationId,
                Round = dto.Round,
                
            }
        });

    
    }
}
