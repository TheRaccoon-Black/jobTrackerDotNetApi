using JobSeekerNet.Data;
using JobSeekerNet.Dtos.User;
using JobSeekerNet.Models;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;


namespace JobSeekerNet.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async(RegisterRequestDto dto, JobSeekerContext dbContext) =>
        {
           if ( await dbContext.Users.AnyAsync(a => a.Email == dto.Email)){
                return Results.BadRequest(new {errors = new {email = new[]{"Email sudah digunakan"}}});
            }

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email.ToLower(),
                Password = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                WeeklyTarget = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var response = new UserResponseDto(user.Id, user.Name, user.Email, user.WeeklyTarget);
            return Results.Created($"/api/auth/me", response);


        });


        group.MapPost("/login",async(LoginRequestDto dto, JobSeekerContext dbContext, IConfiguration config) =>
        {
            var user = await dbContext.Users.SingleOrDefaultAsync(a => a.Email == dto.Email.ToLower());

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
            {
                return Results.Unauthorized();
            }
            var token = GenerateJwtToken(user, config);


            var response = new UserResponseDto(user.Id, user.Name, user.Email, user.WeeklyTarget);

            return Results.Ok(new { token, user = response});
        });

        group.MapPost("/logout", () =>
        {
            return Results.NoContent();
        });

        group.MapGet("/me", async(ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
            var UserIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(UserIdString, out int userId)) return Results.Unauthorized();

            var user= await dbContext.Users.FindAsync(userId);
            if (user == null) return Results.NotFound();

            return Results.Ok(new UserResponseDto(user.Id, user.Name, user.Email, user.WeeklyTarget));

        }).RequireAuthorization();

    }


    private static string GenerateJwtToken(User user, IConfiguration config)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name)
        };

        var key= new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JwtSettings:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: config["JwtSettings:Issuer"],
            audience: config["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(Convert.ToDouble(config["JwtSettings:ExpireDays"])),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
