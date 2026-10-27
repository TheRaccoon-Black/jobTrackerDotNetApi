using System.Security.Claims;
using JobSeekerNet.Data;
using JobSeekerNet.Dtos.Documents;
using JobSeekerNet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobSeekerNet.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/documents").RequireAuthorization();

        var rootDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),"storage", "documents"));

        if (!Directory.Exists(rootDirectory)) Directory.CreateDirectory(rootDirectory);

        group.MapGet("/", async(int? page, int? pageSize, string? type, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

           int currentPage = page ?? 1;
           int limit = pageSize??10;
           int skip = (currentPage -1) * limit;

           var query = dbContext.Documents.Include(d=>d.Application)
           .Where(d=>d.UserId == userId);

           if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(d=>d.Type ==type);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems/(double)limit);

            var documents = await query
            .OrderByDescending(d=>d.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .Select(d=>new DocumentResponseDto(
                d.Id,
                d.FileName,
                d.Type,
                d.ApplicationId,
                d.Application != null ? d.Application.Company : null,
                d.Application != null? d.Application.Title : null,
                d.CreatedAt ?? DateTime.UtcNow
            )).ToListAsync();

            return Results.Ok(new {items = documents, page = currentPage, pageSize = limit, totalPages, totalItems});
        });

        group.MapPost("/", async(IFormFile file, [FromForm] string type, [FromForm] int? applicatinId, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

           var validtypes = new[]{"CV","Portfolio","Cover Letter","Other"};
           if(string.IsNullOrWhiteSpace(type)|| !validtypes.Contains(type))
                return Results.BadRequest(new {errors = new{type = new[]{"Type must be CV, Portfolio, Cover Letter,or Other"}}});

            if (applicatinId.HasValue)
            {
                var appExist = await dbContext.Documents.AnyAsync(a=>a.Id == applicatinId.Value && a.UserId == userId);
                if(!appExist) return Results.NotFound(new {message = "Application not found or does not belong to user"});

            }
            string targetPath = string.Empty;
            string originalFileName = string.Empty;

            if(file != null)
            {
                var allowedExtensions = new[]{".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".txt", ".png", ".jpg", ".jpeg", ".zip"};
                var extension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                    return Results.BadRequest(new {errors = new{file = new[]{"File not allowed."}}});
                if (file.Length > 5 * 1024 * 1024) 
                    return Results.BadRequest(new {errors = new{file = new[]{"File exceeds 5MB limit."}}});

                originalFileName = file.FileName;

                var safeFileName = $"{Guid.NewGuid()}{extension}";
                targetPath = Path.GetFullPath(Path.Combine(rootDirectory, safeFileName));

                if(!targetPath.StartsWith(rootDirectory))
                    return Results.BadRequest(new {errors = new {message = "Invalid file path detected"}});

                using var stream = new FileStream(targetPath, FileMode.Create);
                await file.CopyToAsync(stream);

            }
            else
            {
                return Results.BadRequest(new {errors = new {file = new[]{"File is Requiered."}}});

            }

            var document = new Document
            {
                UserId = userId,
                ApplicationId = applicatinId,
                FileName = originalFileName,
                FilePath = targetPath,
                Type = type,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync();

            return Results.Created($"/api/documents/{document.Id}", new {message = "Document uploaded success."});
        });

        group.MapGet("/{id:int}/download", async(int id, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

           var document = await dbContext.Documents.FirstOrDefaultAsync(i=>i.Id == id && i.UserId == userId);
           if (document == null) return Results.NotFound();

           if (!System.IO.File.Exists(document.FilePath))
                return Results.NotFound(new {message = "Phisycal file is missing from server."});
            var physicalPath = Path.GetFullPath(document.FilePath);

            if(!physicalPath.StartsWith(rootDirectory))
                return Results.BadRequest(new {message = "bad request"});

            return Results.File(physicalPath,"application/octet-stream", fileDownloadName: document.FileName);
        });

        group.MapDelete("/{id:int}", async(int id, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var document = await dbContext.Documents.FirstOrDefaultAsync(i=>i.Id == id && i.UserId == userId);
            if (document == null) return Results.NotFound();

            if (!string.IsNullOrEmpty(document.FilePath) && System.IO.File.Exists(document.FilePath))
            {
                System.IO.File.Delete(document.FilePath);
            }
            dbContext.Documents.Remove(document);
            await dbContext.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
