using System.Security.Claims;
using JobSeekerNet.Data;
using JobSeekerNet.Dtos.Profiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JobSeekerNet.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Azure;
using Microsoft.EntityFrameworkCore.Query.Internal;



namespace JobSeekerNet.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/profile").RequireAuthorization();

        group.MapGet("/", async(ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) 
                return ResponseHelper.Error("Unauthorized", 401);

           var user = await dbContext.Users.FindAsync(userId);
           if (user == null) 
                return ResponseHelper.Error("Not Found", 404);

            var profileData = new
            {
                id = userId,
                name = user.Name,
                email = user.Email,
                weeklyTarget = user.WeeklyTarget
            };
            return ResponseHelper.Success(profileData, "Profile berhasil dimuat");

        });

        group.MapPatch("/", async([FromBody] ProfileUpdateRequestDto dto, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) 
                return ResponseHelper.Error("Unauthorized", 401);

           var user = await dbContext.Users.FindAsync(userId);
           if(user == null) 
                return ResponseHelper.Error("User tidak ditemukan");

           var emailExist = await dbContext.Users.AnyAsync(u=>u.Email == dto.Email.ToLower() && u.Id != userId);


           if( emailExist)
           return ResponseHelper.Error("Email sudah terdaftar");

           user.Name = dto.Name;
           user.Email = dto.Email.ToLower();
           if (dto.WeeklyTarget.HasValue) user.WeeklyTarget = dto.WeeklyTarget.Value;

           user.UpdatedAt = DateTime.UtcNow;
           await dbContext.SaveChangesAsync();

           return ResponseHelper.SuccessWithoutData("Profil berhasil di perbaharui");
        });

        group.MapDelete("/", async([FromBody] ProfileDeleteRequestDto dto, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) 
                return ResponseHelper.Error("unauthorized", 401);

           var user = await dbContext.Users.FindAsync(userId);
           if(user == null) return ResponseHelper.Error("Not Found", 404);

           bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.Password);

           if(!isPasswordValid)
                return ResponseHelper.Error("Password yang dimasukkan salah");

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

            return ResponseHelper.SuccessWithoutData("Akun berhasil dihapus");
        });
    }
}
