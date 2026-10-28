using System;
using System.Diagnostics;
using System.Security.Claims;
using System.Xml.Serialization;
using JobSeekerNet.Data;
using JobSeekerNet.Dtos.Profiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;

namespace JobSeekerNet.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/profile").RequireAuthorization();

        group.MapGet("/", async(ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

           var user = await dbContext.Users.FindAsync(userId);
           if (user == null) return Results.NotFound();


           return Results.Ok(new
           {
               id = user.Id,
               name = user.Name,
               email = user.Email,
               weeklyTarget = user.WeeklyTarget
           });

        });

        group.MapPatch("/", async([FromBody] ProfileUpdateRequestDto dto, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

           var user = await dbContext.Users.FindAsync(userId);
           if(user == null) return Results.NotFound();

           var emailExist = await dbContext.Users.AnyAsync(u=>u.Email == dto.Email.ToLower() && u.Id != userId);


           if( emailExist)
           return Results.BadRequest(new {errors = new {email = new[]{"Email is already taken"}}});

           user.Name = dto.Name;
           user.Email = dto.Email.ToLower();
           if (dto.WeeklyTarget.HasValue) user.WeeklyTarget = dto.WeeklyTarget.Value;

           user.UpdatedAt = DateTime.UtcNow;
           await dbContext.SaveChangesAsync();

           return Results.NoContent();
        });

        group.MapDelete("/", async([FromBody] ProfileDeleteRequestDto dto, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

           var user = await dbContext.Users.FindAsync(userId);
           if(user == null) return Results.NotFound();

           bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.Password);

           if(!isPasswordValid)
                return Results.BadRequest(new {errors = new {password = new[]{"Invalid password."}}});

            var documents = await dbContext.Documents.Where(d => d.UserId == userId).ToListAsync();


            foreach(var doc in documents)
            {
                if(!string.IsNullOrEmpty(doc.FilePath) && System.IO.File.Exists(doc.FilePath))
                {
                    System.IO.File.Delete(doc.FilePath);
                }
            }

            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
