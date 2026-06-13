using GlucoTrack.Api.Data;
using GlucoTrack.Api.Services;
using GlucoTrack.Shared.DTOs.Sync;
using GlucoTrack.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlucoTrack.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/products").RequireAuthorization();

        g.MapGet("/search", SearchAsync);
        g.MapPost("/", CreateAsync);
        g.MapPut("/{id:guid}", UpdateAsync);
        g.MapDelete("/{id:guid}", DeleteAsync);
        g.MapPost("/{id:guid}/share", ShareAsync);
        g.MapPost("/{id:guid}/clone", CloneAsync);
        g.MapPost("/{id:guid}/react", ReactAsync);
        g.MapGet("/favorites", GetFavoritesAsync);
        g.MapPost("/{id:guid}/image", UploadImageAsync).DisableAntiforgery();
        g.MapGet("/{id:guid}/image", ServeImageAsync).AllowAnonymous();

        return app;
    }

    // GET /api/products/search?q=&category=&source=external&skip=&take=
    // source=external → skip own products (used to load base+shared separately)
    private static async Task<IResult> SearchAsync(
        string? q, int? category, string? source, int skip, int take,
        AppDbContext db, ICurrentUserService currentUser)
    {
        take = Math.Min(take <= 0 ? 30 : take, 100);
        var userId = currentUser.UserId;

        // Load disliked product IDs to exclude
        var disliked = await db.ProductReactions.IgnoreQueryFilters()
            .Where(r => r.UserId == userId && r.Reaction == -1)
            .Select(r => r.ProductId).ToListAsync();
        var dislikedSet = disliked.ToHashSet();

        // Load user reactions map
        var reactions = await db.ProductReactions.IgnoreQueryFilters()
            .Where(r => r.UserId == userId)
            .ToDictionaryAsync(r => r.ProductId, r => r.Reaction);

        // Load usage counts
        var usage = await db.ProductUsages.IgnoreQueryFilters()
            .Where(u => u.UserId == userId)
            .ToDictionaryAsync(u => u.ProductId, u => u.UseCount);

        var query = db.Products.IgnoreQueryFilters()
            .Include(p => p.Ingredients).ThenInclude(i => i.IngredientProduct)
            .Where(p => !p.IsDeleted && !dislikedSet.Contains(p.Id));

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.ToLower().Contains(q.ToLower()));

        if (category.HasValue)
            query = query.Where(p => (int)p.Category == category.Value);

        var all = await query.ToListAsync();

        // Bucket and order: mine → base → shared
        List<(Product p, string src)> buckets = [];

        if (source != "external")
        {
            buckets.AddRange(all
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => usage.GetValueOrDefault(p.Id))
                .ThenBy(p => p.Name)
                .Select(p => (p, "mine")));
        }

        buckets.AddRange(all
            .Where(p => p.UserId == null) // base
            .OrderBy(p => p.Name)
            .Select(p => (p, "base")));

        buckets.AddRange(all
            .Where(p => p.UserId != null && p.UserId != userId && p.OwnerType == ProductOwnerType.Shared)
            .OrderByDescending(p => p.LikesCount - p.DislikesCount)
            .ThenBy(p => p.Name)
            .Select(p => (p, "shared")));

        var results = buckets.Skip(skip).Take(take)
            .Select(b => new ProductSearchItemDto(
                SyncEndpoints.MapProductToDto(b.p),
                usage.GetValueOrDefault(b.p.Id),
                reactions.TryGetValue(b.p.Id, out var r) ? r : null,
                b.src))
            .ToList();

        return Results.Ok(results);
    }

    // POST /api/products
    private static async Task<IResult> CreateAsync(
        ProductCreateRequest req, AppDbContext db, ICurrentUserService currentUser)
    {
        var userId = currentUser.UserId;
        var product = new Product
        {
            Id = Guid.NewGuid(), UserId = userId,
            Name = req.Name, Category = (ProductCategory)req.Category,
            OwnerType = ProductOwnerType.Private,
            MeasureType = (ProductMeasureType)req.MeasureType,
            PieceWeightG = req.PieceWeightG,
            DefaultServingG = req.DefaultServingG > 0 ? req.DefaultServingG : 100,
            IsComposite = req.IsComposite, TotalYieldG = req.TotalYieldG,
            UpdatedAtUtc = DateTime.UtcNow
        };

        if (req.IsComposite && req.Ingredients?.Count > 0)
            SetCompositeNutrients(product, req.Ingredients, db);
        else
            SetManualNutrients(product, req);

        db.Products.Add(product);

        if (req.Ingredients?.Count > 0)
            foreach (var ing in req.Ingredients)
                db.ProductIngredients.Add(new ProductIngredient
                {
                    Id = Guid.NewGuid(), CompositeProductId = product.Id,
                    IngredientProductId = ing.ProductId, Grams = ing.Grams
                });

        await db.SaveChangesAsync();
        return Results.Ok(SyncEndpoints.MapProductToDto(
            await db.Products.Include(p => p.Ingredients).ThenInclude(i => i.IngredientProduct)
                .FirstAsync(p => p.Id == product.Id)));
    }

    // PUT /api/products/{id}
    private static async Task<IResult> UpdateAsync(
        Guid id, ProductCreateRequest req, AppDbContext db, ICurrentUserService currentUser)
    {
        var product = await db.Products.IgnoreQueryFilters()
            .Include(p => p.Ingredients)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return Results.NotFound();
        if (product.UserId != currentUser.UserId) return Results.Forbid();

        product.Name = req.Name;
        product.Category = (ProductCategory)req.Category;
        product.MeasureType = (ProductMeasureType)req.MeasureType;
        product.PieceWeightG = req.PieceWeightG;
        product.DefaultServingG = req.DefaultServingG > 0 ? req.DefaultServingG : 100;
        product.IsComposite = req.IsComposite;
        product.TotalYieldG = req.TotalYieldG;
        product.UpdatedAtUtc = DateTime.UtcNow;

        db.ProductIngredients.RemoveRange(product.Ingredients);

        if (req.IsComposite && req.Ingredients?.Count > 0)
        {
            SetCompositeNutrients(product, req.Ingredients, db);
            foreach (var ing in req.Ingredients)
                db.ProductIngredients.Add(new ProductIngredient
                {
                    Id = Guid.NewGuid(), CompositeProductId = product.Id,
                    IngredientProductId = ing.ProductId, Grams = ing.Grams
                });
        }
        else
            SetManualNutrients(product, req);

        await db.SaveChangesAsync();

        await db.Entry(product).Collection(p => p.Ingredients).Query()
            .Include(i => i.IngredientProduct).LoadAsync();
        return Results.Ok(SyncEndpoints.MapProductToDto(product));
    }

    // DELETE /api/products/{id}
    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db, ICurrentUserService currentUser)
    {
        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return Results.NotFound();
        if (product.UserId != currentUser.UserId) return Results.Forbid();
        product.IsDeleted = true; product.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // POST /api/products/{id}/share  — make private → shared
    private static async Task<IResult> ShareAsync(Guid id, AppDbContext db, ICurrentUserService currentUser)
    {
        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return Results.NotFound();
        if (product.UserId != currentUser.UserId) return Results.Forbid();
        product.OwnerType = ProductOwnerType.Shared;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok();
    }

    // POST /api/products/{id}/clone  — clone to current user's private
    private static async Task<IResult> CloneAsync(Guid id, AppDbContext db, ICurrentUserService currentUser)
    {
        var src = await db.Products.IgnoreQueryFilters()
            .Include(p => p.Ingredients)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (src is null) return Results.NotFound();

        var clone = new Product
        {
            Id = Guid.NewGuid(), UserId = currentUser.UserId,
            Name = src.Name + " (копия)",
            Category = src.Category, OwnerType = ProductOwnerType.Private,
            MeasureType = src.MeasureType, PieceWeightG = src.PieceWeightG,
            DefaultServingG = src.DefaultServingG, IsComposite = src.IsComposite,
            TotalYieldG = src.TotalYieldG,
            GlycemicIndex = src.GlycemicIndex,
            CaloriesPer100g = src.CaloriesPer100g, ProteinPer100g = src.ProteinPer100g,
            FatPer100g = src.FatPer100g, CarbsPer100g = src.CarbsPer100g, FiberPer100g = src.FiberPer100g,
            VitaminA = src.VitaminA, VitaminC = src.VitaminC, VitaminD = src.VitaminD,
            VitaminB1 = src.VitaminB1, VitaminB2 = src.VitaminB2, VitaminB3 = src.VitaminB3,
            VitaminB4 = src.VitaminB4, VitaminB5 = src.VitaminB5, VitaminB6 = src.VitaminB6,
            VitaminB9 = src.VitaminB9, VitaminB12 = src.VitaminB12,
            VitaminE = src.VitaminE, VitaminK = src.VitaminK, VitaminH = src.VitaminH,
            Calcium = src.Calcium, Phosphorus = src.Phosphorus, Potassium = src.Potassium,
            Sodium = src.Sodium, Chlorine = src.Chlorine, Magnesium = src.Magnesium,
            Sulfur = src.Sulfur, Iron = src.Iron, Zinc = src.Zinc, Selenium = src.Selenium, Iodine = src.Iodine,
            Manganese = src.Manganese, Copper = src.Copper, Fluorine = src.Fluorine, Chromium = src.Chromium,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Products.Add(clone);

        foreach (var ing in src.Ingredients)
            db.ProductIngredients.Add(new ProductIngredient
            {
                Id = Guid.NewGuid(), CompositeProductId = clone.Id,
                IngredientProductId = ing.IngredientProductId, Grams = ing.Grams
            });

        await db.SaveChangesAsync();
        return Results.Ok(new { id = clone.Id });
    }

    // POST /api/products/{id}/react  body: { reaction: 1|-1|0 }
    private static async Task<IResult> ReactAsync(
        Guid id, ReactRequest req, AppDbContext db, ICurrentUserService currentUser)
    {
        var userId = currentUser.UserId;

        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return Results.NotFound();

        var existing = await db.ProductReactions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == id);

        int oldReaction = existing?.Reaction ?? 0;
        int newReaction = req.Reaction;

        // Update reaction table
        if (newReaction == 0)
        {
            if (existing is not null) db.ProductReactions.Remove(existing);
        }
        else if (existing is null)
        {
            db.ProductReactions.Add(new ProductReaction
                { UserId = userId, ProductId = id, Reaction = newReaction, CreatedAtUtc = DateTime.UtcNow });
        }
        else
        {
            existing.Reaction = newReaction;
        }

        // Adjust denormalized counters
        if (oldReaction == 1) product.LikesCount = Math.Max(0, product.LikesCount - 1);
        if (oldReaction == -1) product.DislikesCount = Math.Max(0, product.DislikesCount - 1);
        if (newReaction == 1) product.LikesCount++;
        if (newReaction == -1) product.DislikesCount++;

        await db.SaveChangesAsync();
        return Results.Ok(new { product.LikesCount, product.DislikesCount });
    }

    // GET /api/products/favorites
    private static async Task<IResult> GetFavoritesAsync(AppDbContext db, ICurrentUserService currentUser)
    {
        var userId = currentUser.UserId;
        var likedIds = await db.ProductReactions.IgnoreQueryFilters()
            .Where(r => r.UserId == userId && r.Reaction == 1)
            .Select(r => r.ProductId).ToListAsync();

        var products = await db.Products.IgnoreQueryFilters()
            .Include(p => p.Ingredients).ThenInclude(i => i.IngredientProduct)
            .Where(p => likedIds.Contains(p.Id) && !p.IsDeleted)
            .ToListAsync();

        return Results.Ok(products.Select(SyncEndpoints.MapProductToDto).ToList());
    }

    // GET /api/products/{id}/image — serve image regardless of extension
    private static IResult ServeImageAsync(Guid id, IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.WebRootPath, "images", "products");
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".webp" })
        {
            var path = Path.Combine(dir, $"{id}{ext}");
            if (File.Exists(path))
            {
                var mime = ext == ".png" ? "image/png" : ext == ".webp" ? "image/webp" : "image/jpeg";
                return Results.File(path, mime, enableRangeProcessing: false);
            }
        }
        return Results.NotFound();
    }

    // POST /api/products/{id}/image  — multipart upload, max 2 MB
    private static async Task<IResult> UploadImageAsync(
        Guid id, IFormFile file, AppDbContext db,
        ICurrentUserService currentUser, IWebHostEnvironment env)
    {
        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return Results.NotFound();
        if (product.UserId != currentUser.UserId) return Results.Forbid();

        const long maxBytes = 2 * 1024 * 1024; // 2 MB
        if (file.Length > maxBytes)
            return Results.BadRequest("Файл слишком большой. Максимум 2 МБ.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return Results.BadRequest("Допустимые форматы: jpg, png, webp.");

        var imagesDir = Path.Combine(env.WebRootPath, "images", "products");
        Directory.CreateDirectory(imagesDir);

        // Remove any previous image for this product (different extension)
        foreach (var old in Directory.GetFiles(imagesDir, $"{id}.*"))
            File.Delete(old);

        var filePath = Path.Combine(imagesDir, $"{id}{ext}");
        await using var stream = File.Create(filePath);
        await file.CopyToAsync(stream);

        return Results.Ok(new { url = $"/images/products/{id}{ext}" });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetManualNutrients(Product p, ProductCreateRequest req)
    {
        p.GlycemicIndex = req.GlycemicIndex;
        p.CaloriesPer100g = req.CaloriesPer100g; p.ProteinPer100g = req.ProteinPer100g;
        p.FatPer100g = req.FatPer100g; p.CarbsPer100g = req.CarbsPer100g;
        p.FiberPer100g = req.FiberPer100g;
        p.VitaminA = req.VitaminA; p.VitaminC = req.VitaminC; p.VitaminD = req.VitaminD;
        p.VitaminB1 = req.VitaminB1; p.VitaminB2 = req.VitaminB2; p.VitaminB3 = req.VitaminB3;
        p.VitaminB4 = req.VitaminB4; p.VitaminB5 = req.VitaminB5; p.VitaminB6 = req.VitaminB6;
        p.VitaminB9 = req.VitaminB9; p.VitaminB12 = req.VitaminB12;
        p.VitaminE = req.VitaminE; p.VitaminK = req.VitaminK; p.VitaminH = req.VitaminH;
        p.Calcium = req.Calcium; p.Phosphorus = req.Phosphorus; p.Potassium = req.Potassium;
        p.Sodium = req.Sodium; p.Chlorine = req.Chlorine; p.Magnesium = req.Magnesium;
        p.Sulfur = req.Sulfur; p.Iron = req.Iron; p.Zinc = req.Zinc; p.Selenium = req.Selenium; p.Iodine = req.Iodine;
        p.Manganese = req.Manganese; p.Copper = req.Copper; p.Fluorine = req.Fluorine; p.Chromium = req.Chromium;
    }

    // Recalculate nutrients from ingredients (async-free since we query inline)
    private static void SetCompositeNutrients(
        Product product, List<IngredientRequest> ingredients, AppDbContext db)
    {
        // Load ingredient products synchronously via local lookup
        var ids = ingredients.Select(i => i.ProductId).ToList();
        var ingProducts = db.Products.IgnoreQueryFilters()
            .Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id);

        double totalInput = ingredients.Sum(i => i.Grams);
        double yieldG = product.TotalYieldG ?? totalInput;
        if (yieldG <= 0) yieldG = totalInput;

        double cal = 0, prot = 0, fat = 0, carbs = 0, fiber = 0;
        double giNumerator = 0, giDenominator = 0; // weighted GI by carbs
        double? vitA=null,vitC=null,vitD=null,vitB1=null,vitB2=null,vitB3=null,vitB4=null,vitB5=null,vitB6=null,vitB9=null,vitB12=null,vitE=null,vitK=null,vitH=null;
        double? calcium=null,phosphorus=null,potassium=null,sodium=null,chlorine=null,magnesium=null,sulfur=null,iron=null,zinc=null,selenium=null,iodine=null,manganese=null,copper=null,fluorine=null,chromium=null;

        foreach (var ing in ingredients)
        {
            if (!ingProducts.TryGetValue(ing.ProductId, out var ip)) continue;
            double k = ing.Grams / 100.0;
            cal += ip.CaloriesPer100g * k; prot += ip.ProteinPer100g * k;
            fat += ip.FatPer100g * k; carbs += ip.CarbsPer100g * k;
            fiber += ip.FiberPer100g * k;
            if (ip.GlycemicIndex.HasValue) { var carbsK = ip.CarbsPer100g * k; giNumerator += ip.GlycemicIndex.Value * carbsK; giDenominator += carbsK; }
            void Add(ref double? acc, double? v) { if (v.HasValue) acc = (acc ?? 0) + v.Value * k; }
            Add(ref vitA, ip.VitaminA); Add(ref vitC, ip.VitaminC); Add(ref vitD, ip.VitaminD);
            Add(ref vitB1, ip.VitaminB1); Add(ref vitB2, ip.VitaminB2); Add(ref vitB3, ip.VitaminB3);
            Add(ref vitB4, ip.VitaminB4); Add(ref vitB5, ip.VitaminB5); Add(ref vitB6, ip.VitaminB6);
            Add(ref vitB9, ip.VitaminB9); Add(ref vitB12, ip.VitaminB12);
            Add(ref vitE, ip.VitaminE); Add(ref vitK, ip.VitaminK); Add(ref vitH, ip.VitaminH);
            Add(ref calcium, ip.Calcium); Add(ref phosphorus, ip.Phosphorus); Add(ref potassium, ip.Potassium);
            Add(ref sodium, ip.Sodium); Add(ref chlorine, ip.Chlorine); Add(ref magnesium, ip.Magnesium);
            Add(ref sulfur, ip.Sulfur); Add(ref iron, ip.Iron); Add(ref zinc, ip.Zinc); Add(ref selenium, ip.Selenium); Add(ref iodine, ip.Iodine);
            Add(ref manganese, ip.Manganese); Add(ref copper, ip.Copper); Add(ref fluorine, ip.Fluorine); Add(ref chromium, ip.Chromium);
        }

        double factor = 100.0 / yieldG;
        double? F(double? v) => v is null ? null : v * factor;
        product.GlycemicIndex = giDenominator > 0 ? (int)Math.Round(giNumerator / giDenominator) : null;
        product.CaloriesPer100g = cal * factor; product.ProteinPer100g = prot * factor;
        product.FatPer100g = fat * factor; product.CarbsPer100g = carbs * factor;
        product.FiberPer100g = fiber * factor;
        product.VitaminA=F(vitA); product.VitaminC=F(vitC); product.VitaminD=F(vitD);
        product.VitaminB1=F(vitB1); product.VitaminB2=F(vitB2); product.VitaminB3=F(vitB3);
        product.VitaminB4=F(vitB4); product.VitaminB5=F(vitB5); product.VitaminB6=F(vitB6);
        product.VitaminB9=F(vitB9); product.VitaminB12=F(vitB12);
        product.VitaminE=F(vitE); product.VitaminK=F(vitK); product.VitaminH=F(vitH);
        product.Calcium=F(calcium); product.Phosphorus=F(phosphorus); product.Potassium=F(potassium);
        product.Sodium=F(sodium); product.Chlorine=F(chlorine); product.Magnesium=F(magnesium);
        product.Sulfur=F(sulfur); product.Iron=F(iron); product.Zinc=F(zinc); product.Selenium=F(selenium); product.Iodine=F(iodine);
        product.Manganese=F(manganese); product.Copper=F(copper); product.Fluorine=F(fluorine); product.Chromium=F(chromium);
    }

    // ── Request records ───────────────────────────────────────────────────────

    private record ProductCreateRequest(
        string Name, int Category, int MeasureType,
        double? PieceWeightG, double DefaultServingG,
        bool IsComposite, double? TotalYieldG,
        int? GlycemicIndex,
        double CaloriesPer100g, double ProteinPer100g, double FatPer100g,
        double CarbsPer100g, double FiberPer100g,
        double? VitaminA, double? VitaminC, double? VitaminD,
        double? VitaminB1, double? VitaminB2, double? VitaminB3,
        double? VitaminB4, double? VitaminB5, double? VitaminB6,
        double? VitaminB9, double? VitaminB12, double? VitaminE, double? VitaminK, double? VitaminH,
        double? Calcium, double? Phosphorus, double? Potassium,
        double? Sodium, double? Chlorine, double? Magnesium,
        double? Sulfur, double? Iron, double? Zinc, double? Selenium, double? Iodine,
        double? Manganese, double? Copper, double? Fluorine, double? Chromium,
        List<IngredientRequest>? Ingredients);

    private record IngredientRequest(Guid ProductId, double Grams);
    private record ReactRequest(int Reaction);
}
