using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VirtualMuseum.Domain.Entities;

namespace VirtualMuseum.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(MuseumDbContext context)
    {
        if (!await context.Roles.AnyAsync())
        {
            await SeedCoreAsync(context);
        }

        // Keep this outside of the "first run only" seed so Docker/dev environments
        // can sync artifact 3D URLs from JSON into database-backed artifacts.
        try
        {
            await SyncArtifactsFromJsonAsync(context);
        }
        catch
        {
            // Do not block API startup if optional JSON sync fails.
            // Core app data remains available and admin CRUD still works.
        }
    }

    private static async Task SeedCoreAsync(MuseumDbContext context)
    {
        var adminRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            CreatedAt = DateTime.UtcNow
        };
        var userRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = "User",
            CreatedAt = DateTime.UtcNow
        };
        context.Roles.AddRange(adminRole, userRole);

        // Admin User - Password: admin@123 (BCrypt hashed)
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Administrator",
            Email = "admin@museum.com",
            Region = "System",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin@123"),
            RoleId = adminRole.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // Eras
        var era1 = new Era { Id = Guid.NewGuid(), Name = "Ancient Egypt", StartYear = -3100, EndYear = -30 };
        var era2 = new Era { Id = Guid.NewGuid(), Name = "Roman Empire", StartYear = -27, EndYear = 476 };
        var era3 = new Era { Id = Guid.NewGuid(), Name = "Renaissance", StartYear = 1300, EndYear = 1600 };
        context.Eras.AddRange(era1, era2, era3);

        // Categories
        var cat1 = new Category { Id = Guid.NewGuid(), Name = "Sculpture" };
        var cat2 = new Category { Id = Guid.NewGuid(), Name = "Pottery" };
        var cat3 = new Category { Id = Guid.NewGuid(), Name = "Jewelry" };
        context.Categories.AddRange(cat1, cat2, cat3);

        // Materials
        var mat1 = new Material { Id = Guid.NewGuid(), Name = "Stone" };
        var mat2 = new Material { Id = Guid.NewGuid(), Name = "Bronze" };
        var mat3 = new Material { Id = Guid.NewGuid(), Name = "Clay" };
        context.Materials.AddRange(mat1, mat2, mat3);

        await context.SaveChangesAsync();

        // Artifacts
        var artifact1 = new Artifact
        {
            Id = Guid.NewGuid(),
            Slug = "ancient-egyptian-bust",
            EraId = era1.Id,
            CategoryId = cat1.Id,
            MaterialId = mat1.Id,
            Height = 45,
            Width = 30,
            Depth = 25,
            Weight = 15,
            CreatedBy = adminUser.Id,
            CreatedAt = DateTime.UtcNow
        };
        var artifact2 = new Artifact
        {
            Id = Guid.NewGuid(),
            Slug = "roman-amphora",
            EraId = era2.Id,
            CategoryId = cat2.Id,
            MaterialId = mat3.Id,
            Height = 60,
            Width = 35,
            Depth = 35,
            Weight = 8,
            CreatedBy = adminUser.Id,
            CreatedAt = DateTime.UtcNow
        };
        var artifact3 = new Artifact
        {
            Id = Guid.NewGuid(),
            Slug = "renaissance-medal",
            EraId = era3.Id,
            CategoryId = cat3.Id,
            MaterialId = mat2.Id,
            Height = 5,
            Width = 5,
            Depth = 0.5m,
            Weight = 0.1m,
            CreatedBy = adminUser.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Artifacts.AddRange(artifact1, artifact2, artifact3);
        await context.SaveChangesAsync();
    }

    private static async Task SyncArtifactsFromJsonAsync(MuseumDbContext context)
    {
        var seedDir = ResolveSeedDataDirectory();
        if (seedDir == null)
            return;

        var categoriesFile = Path.Combine(seedDir, "categories.json");
        var artifactsFile = Path.Combine(seedDir, "artifacts.json");
        if (!File.Exists(categoriesFile) || !File.Exists(artifactsFile))
            return;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var categorySeed = JsonSerializer.Deserialize<List<CategorySeed>>(await File.ReadAllTextAsync(categoriesFile), options)
                           ?? new List<CategorySeed>();
        var artifactSeed = JsonSerializer.Deserialize<List<ArtifactSeed>>(await File.ReadAllTextAsync(artifactsFile), options)
                           ?? new List<ArtifactSeed>();

        if (artifactSeed.Count == 0)
            return;

        var adminUserId = await context.Users
            .Where(u => u.Role != null && u.Role.Name == "Admin")
            .Select(u => u.Id)
            .FirstOrDefaultAsync();
        if (adminUserId == Guid.Empty)
        {
            adminUserId = await context.Users.Select(u => u.Id).FirstOrDefaultAsync();
        }

        var categoryIdByLegacyId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in categorySeed.Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Name)))
        {
            var existing = await context.Categories.FirstOrDefaultAsync(c => c.Name == seed.Name);
            if (existing == null)
            {
                existing = new Category
                {
                    Id = Guid.NewGuid(),
                    Name = seed.Name!.Trim()
                };
                context.Categories.Add(existing);
            }
            categoryIdByLegacyId[seed.Id!] = existing.Id;
        }

        await context.SaveChangesAsync();

        foreach (var seed in artifactSeed.Where(a => !string.IsNullOrWhiteSpace(a.Name)))
        {
            var slug = Slugify(seed.Name!);
            var artifact = await context.Artifacts
                .Include(a => a.Translations)
                .FirstOrDefaultAsync(a => a.Slug == slug);

            if (artifact == null)
            {
                artifact = new Artifact
                {
                    Id = Guid.NewGuid(),
                    Slug = slug,
                    CreatedAt = DateTime.UtcNow
                };
                context.Artifacts.Add(artifact);
            }

            artifact.CreatedBy ??= adminUserId == Guid.Empty ? null : adminUserId;

            if (!string.IsNullOrWhiteSpace(seed.CategoryId) &&
                categoryIdByLegacyId.TryGetValue(seed.CategoryId!, out var catId))
            {
                artifact.CategoryId = catId;
            }

            if (!string.IsNullOrWhiteSpace(seed.Period))
            {
                var era = await context.Eras.FirstOrDefaultAsync(e => e.Name == seed.Period);
                if (era == null)
                {
                    era = new Era
                    {
                        Id = Guid.NewGuid(),
                        Name = seed.Period!.Trim()
                    };
                    context.Eras.Add(era);
                    await context.SaveChangesAsync();
                }
                artifact.EraId = era.Id;
            }

            if (!string.IsNullOrWhiteSpace(seed.Material))
            {
                var material = await context.Materials.FirstOrDefaultAsync(m => m.Name == seed.Material);
                if (material == null)
                {
                    material = new Material
                    {
                        Id = Guid.NewGuid(),
                        Name = seed.Material!.Trim()
                    };
                    context.Materials.Add(material);
                    await context.SaveChangesAsync();
                }
                artifact.MaterialId = material.Id;
            }

            artifact.Height = ParseNumeric(seed.Dimensions?.Height);
            artifact.Width = ParseNumeric(seed.Dimensions?.Width);
            artifact.Depth = ParseNumeric(seed.Dimensions?.Depth);
            artifact.Weight = ParseNumeric(seed.Dimensions?.Weight);

            if (!string.IsNullOrWhiteSpace(seed.Image3D))
            {
                var modelFile = await context.Files.FirstOrDefaultAsync(f => f.Url == seed.Image3D);
                if (modelFile == null)
                {
                    modelFile = new MuseumFile
                    {
                        Id = Guid.NewGuid(),
                        FileName = $"{slug}-3d-embed",
                        FileType = "text/html",
                        Url = seed.Image3D!,
                        StorageProvider = "external",
                        UploadedBy = adminUserId == Guid.Empty ? Guid.Empty : adminUserId,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Files.Add(modelFile);
                    await context.SaveChangesAsync();
                }
                artifact.ModelFileId = modelFile.Id;
            }

            if (!string.IsNullOrWhiteSpace(seed.Image))
            {
                var thumbnailFile = await context.Files.FirstOrDefaultAsync(f => f.Url == seed.Image);
                if (thumbnailFile == null)
                {
                    thumbnailFile = new MuseumFile
                    {
                        Id = Guid.NewGuid(),
                        FileName = $"{slug}-thumb",
                        FileType = "image",
                        Url = seed.Image!,
                        StorageProvider = "external",
                        UploadedBy = adminUserId == Guid.Empty ? Guid.Empty : adminUserId,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Files.Add(thumbnailFile);
                    await context.SaveChangesAsync();
                }
                artifact.ThumbnailFileId = thumbnailFile.Id;
            }

            var translation = artifact.Translations.FirstOrDefault(t => t.LanguageCode == "en");
            if (translation == null)
            {
                translation = new ArtifactTranslation
                {
                    Id = Guid.NewGuid(),
                    ArtifactId = artifact.Id,
                    LanguageCode = "en",
                    Name = seed.Name!.Trim()
                };
                artifact.Translations.Add(translation);
            }

            translation.Name = seed.Name!.Trim();
            translation.Description = seed.Description?.Trim();
            translation.HistoricalStory = seed.AssociatedKing?.Trim();
        }

        await context.SaveChangesAsync();
    }

    private static string? ResolveSeedDataDirectory()
    {
        var candidates = new[]
        {
            "/seed-data",
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "VirtualMuseum Front-End", "src", "Data"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "VirtualMuseum Front-End", "src", "Data")
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static string Slugify(string value)
    {
        var slug = value.ToLowerInvariant().Trim();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        return slug;
    }

    private static decimal? ParseNumeric(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var match = Regex.Match(raw, @"-?\d+(\.\d+)?");
        if (!match.Success)
            return null;

        return decimal.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private sealed class CategorySeed
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class ArtifactSeed
    {
        public string? CategoryId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Image { get; set; }
        public string? Period { get; set; }
        public string? AssociatedKing { get; set; }
        public string? Material { get; set; }
        public string? Image3D { get; set; }
        public DimensionSeed? Dimensions { get; set; }
    }

    private sealed class DimensionSeed
    {
        public string? Height { get; set; }
        public string? Width { get; set; }
        public string? Depth { get; set; }
        public string? Weight { get; set; }
    }
}
