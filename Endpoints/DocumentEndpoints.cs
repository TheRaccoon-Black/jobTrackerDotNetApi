using System.Security.Claims;
using Azure;
using JobSeekerNet.Data;
using JobSeekerNet.Dtos.Documents;
using JobSeekerNet.Helpers;
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
           if(!int.TryParse(userIdString, out int userId)) return ResponseHelper.Error("Unauthorized", 401);
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
            var response = new {items = documents, page = currentPage, pageSize = limit, totalPages = totalItems};

            return ResponseHelper.Success(response);
        });

        group.MapPost("/", async(IFormFile file, [FromForm] string type, [FromForm] int? applicatinId, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if (!int.TryParse(userIdString, out int userId)) return ResponseHelper.Error("Unauthorized", 401);

           var validtypes = new[]{"CV","Portfolio","Cover Letter","Other"};
           if(string.IsNullOrWhiteSpace(type)|| !validtypes.Contains(type))
                return Results.BadRequest(new {errors = new{type = new[]{"Type must be CV, Portfolio, Cover Letter,or Other"}}});

            if (applicatinId.HasValue)
            {
                var appExist = await dbContext.Documents.AnyAsync(a=>a.Id == applicatinId.Value && a.UserId == userId);
                if(!appExist) 
                    return ResponseHelper.Error("Lamaran tidak ditemukan");

            }
            string targetPath = string.Empty;
            string originalFileName = string.Empty;

            if(file != null)
            {
                var allowedExtensions = new[]{".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".txt", ".png", ".jpg", ".jpeg", ".zip"};
                var extension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                    return ResponseHelper.Error("Jenis file tidak di izinkan");
                if (file.Length > 5 * 1024 * 1024) 
                    return ResponseHelper.Error("File melebihi 5 MB batas");

                originalFileName = file.FileName;

                var safeFileName = $"{Guid.NewGuid()}{extension}";
                targetPath = Path.GetFullPath(Path.Combine(rootDirectory, safeFileName));

                if(!targetPath.StartsWith(rootDirectory))
                    return ResponseHelper.Error("Path file tidak valid");

                using var stream = new FileStream(targetPath, FileMode.Create);
                await file.CopyToAsync(stream);

            }
            else
            {
                return ResponseHelper.Error("File wajib diisi");
                // return Results.BadRequest(new {errors = new {file = new[]{"File is Requiered."}}});

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

            // return Results.Created($"/api/documents/{document.Id}", new {message = "Document uploaded success."});
            return ResponseHelper.SuccessWithoutData("Dokumen berhasil di upload");
        }).DisableAntiforgery();

        group.MapGet("/{id:int}/download", async(int id, ClaimsPrincipal userClaims, JobSeekerContext dbContext) =>
        {
           var userIdString = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if(!int.TryParse(userIdString, out int userId)) return ResponseHelper.Error("Unauthorized", 401);

           var document = await dbContext.Documents.FirstOrDefaultAsync(i=>i.Id == id && i.UserId == userId);
           if (document == null) return ResponseHelper.Error("Not found", 404);

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
           if(!int.TryParse(userIdString, out int userId)) return ResponseHelper.Error("Unauthorized", 401);

            var document = await dbContext.Documents.FirstOrDefaultAsync(i=>i.Id == id && i.UserId == userId);
            if (document == null) return ResponseHelper.Error("Not found", 404);

            if (!string.IsNullOrEmpty(document.FilePath) && System.IO.File.Exists(document.FilePath))
            {
                System.IO.File.Delete(document.FilePath);
            }
            dbContext.Documents.Remove(document);
            await dbContext.SaveChangesAsync();

            return ResponseHelper.SuccessWithoutData("Data berhasil dihapus");
        });
    }
}
