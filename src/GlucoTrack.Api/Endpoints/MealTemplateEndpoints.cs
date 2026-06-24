using GlucoTrack.Api.Data;
using GlucoTrack.Api.Services;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace GlucoTrack.Api.Endpoints;

public static class MealTemplateEndpoints
{
    public static IEndpointRouteBuilder MapMealTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/meal-templates").RequireAuthorization();
        g.MapPost("/{id:guid}/image", UploadImageAsync).DisableAntiforgery();
        g.MapGet("/{id:guid}/image", ServeImageAsync).AllowAnonymous();
        return app;
    }

    // GET /api/meal-templates/{id}/image
    private static IResult ServeImageAsync(Guid id, IWebHostEnvironment env)
    {
        var root = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var path = Path.Combine(root, "images", "meal_templates", $"{id}.jpg");
        if (File.Exists(path))
            return Results.File(path, "image/jpeg", enableRangeProcessing: false);
        return Results.NotFound();
    }

    // POST /api/meal-templates/{id}/image — multipart upload with SkiaSharp processing
    private static async Task<IResult> UploadImageAsync(
        Guid id, IFormFile file, AppDbContext db,
        ICurrentUserService currentUser, IWebHostEnvironment env)
    {
        var tpl = await db.MealTemplates.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tpl is null) return Results.NotFound();
        if (tpl.UserId != currentUser.UserId) return Results.Forbid();

        const long maxUploadBytes = 8 * 1024 * 1024; // 8 MB raw upload limit
        if (file.Length > maxUploadBytes)
            return Results.BadRequest("Файл слишком большой. Максимум 8 МБ.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return Results.BadRequest("Допустимые форматы: JPG, PNG, WebP.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var rawBytes = ms.ToArray();

        using var original = SKBitmap.Decode(rawBytes);
        if (original is null)
            return Results.BadRequest("Файл повреждён или не является изображением.");

        if (original.Width < 20 || original.Height < 20)
            return Results.BadRequest($"Слишком маленькое изображение ({original.Width}×{original.Height} px). Минимум 20×20.");

        const int maxDim = 1024;
        SKBitmap bitmap;
        if (original.Width > maxDim || original.Height > maxDim)
        {
            double scale = Math.Min((double)maxDim / original.Width, (double)maxDim / original.Height);
            var newW = Math.Max(1, (int)(original.Width * scale));
            var newH = Math.Max(1, (int)(original.Height * scale));
            bitmap = original.Resize(new SKImageInfo(newW, newH), SKSamplingOptions.Default);
        }
        else
        {
            bitmap = original;
        }

        using var skImage = SKImage.FromBitmap(bitmap);
        using var encoded = skImage.Encode(SKEncodedImageFormat.Jpeg, 85);
        if (bitmap != original) bitmap.Dispose();

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var imagesDir = Path.Combine(webRoot, "images", "meal_templates");
        Directory.CreateDirectory(imagesDir);

        var filePath = Path.Combine(imagesDir, $"{id}.jpg");
        await File.WriteAllBytesAsync(filePath, encoded.ToArray());

        tpl.HasImage = true;
        tpl.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { url = $"/api/meal-templates/{id}/image" });
    }
}
