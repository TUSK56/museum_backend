param(
    [string]$ArtifactsJsonPath = "..\Data\artifacts.json",
    [string]$CategoriesJsonPath = "..\Data\categories.json",
    [string]$OutputSqlPath = "publish\MonsterASP.net\sql\002-seed-artifacts-from-json.sql"
)

$ErrorActionPreference = "Stop"

function Escape-SqlString {
    param([string]$Value)
    if ($null -eq $Value) { return "NULL" }
    return "N'" + $Value.Replace("'", "''") + "'"
}

function Parse-Decimal {
    param([object]$Raw)
    if ($null -eq $Raw) { return "NULL" }
    $text = [string]$Raw
    $match = [regex]::Match($text, "-?\d+(\.\d+)?")
    if (-not $match.Success) { return "NULL" }
    return $match.Value
}

$artifactsPath = Resolve-Path $ArtifactsJsonPath
$categoriesPath = Resolve-Path $CategoriesJsonPath

$artifacts = Get-Content -Raw -Path $artifactsPath | ConvertFrom-Json
$categories = Get-Content -Raw -Path $categoriesPath | ConvertFrom-Json

$categoryByLegacyId = @{}
foreach ($cat in $categories) {
    $categoryByLegacyId[$cat.id] = $cat.name
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("-- Auto-generated from Data/artifacts.json + Data/categories.json")
[void]$sb.AppendLine("-- Safe to run multiple times (upsert pattern).")
[void]$sb.AppendLine("SET XACT_ABORT ON;")
[void]$sb.AppendLine("BEGIN TRANSACTION;")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("DECLARE @AdminUserId UNIQUEIDENTIFIER = (")
[void]$sb.AppendLine("    SELECT TOP 1 u.Id")
[void]$sb.AppendLine("    FROM [Users] u")
[void]$sb.AppendLine("    INNER JOIN [Roles] r ON r.Id = u.RoleId")
[void]$sb.AppendLine("    WHERE r.[Name] = N'Admin'")
[void]$sb.AppendLine("    ORDER BY u.CreatedAt")
[void]$sb.AppendLine(");")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("DECLARE @ArtifactId UNIQUEIDENTIFIER;")
[void]$sb.AppendLine("DECLARE @CategoryId UNIQUEIDENTIFIER;")
[void]$sb.AppendLine("DECLARE @EraId UNIQUEIDENTIFIER;")
[void]$sb.AppendLine("DECLARE @MaterialId UNIQUEIDENTIFIER;")
[void]$sb.AppendLine("DECLARE @DiscoveryLocationId UNIQUEIDENTIFIER;")
[void]$sb.AppendLine("DECLARE @ModelFileId UNIQUEIDENTIFIER;")
[void]$sb.AppendLine("DECLARE @ThumbFileId UNIQUEIDENTIFIER;")
[void]$sb.AppendLine("")

foreach ($artifact in $artifacts) {
    $legacyCategoryId = [string]$artifact.categoryId
    $categoryName = $null
    if ($categoryByLegacyId.ContainsKey($legacyCategoryId)) {
        $categoryName = [string]$categoryByLegacyId[$legacyCategoryId]
    }

    $name = [string]$artifact.name
    $slug = $name.ToLowerInvariant()
    $slug = [regex]::Replace($slug, "[^a-z0-9\s-]", "")
    $slug = [regex]::Replace($slug, "\s+", "-")
    $slug = [regex]::Replace($slug, "-+", "-")
    $slug = $slug.Trim("-")

    $period = [string]$artifact.period
    $material = [string]$artifact.material
    $discoverySite = [string]$artifact.discoverySite
    $image = [string]$artifact.image
    $image3D = [string]$artifact.image3D
    $description = [string]$artifact.description
    $historicalStory = [string]$artifact.associatedKing

    $height = Parse-Decimal $artifact.dimensions.height
    $width = Parse-Decimal $artifact.dimensions.width
    $depth = Parse-Decimal $artifact.dimensions.depth
    $weight = Parse-Decimal $artifact.dimensions.weight

    [void]$sb.AppendLine("-- " + $name)
    [void]$sb.AppendLine("SET @ArtifactId = (SELECT TOP 1 [Id] FROM [Artifacts] WHERE [Slug] = " + (Escape-SqlString $slug) + ");")
    [void]$sb.AppendLine("SET @CategoryId = NULL;")
    [void]$sb.AppendLine("SET @EraId = NULL;")
    [void]$sb.AppendLine("SET @MaterialId = NULL;")
    [void]$sb.AppendLine("SET @DiscoveryLocationId = NULL;")
    [void]$sb.AppendLine("SET @ModelFileId = NULL;")
    [void]$sb.AppendLine("SET @ThumbFileId = NULL;")

    if (-not [string]::IsNullOrWhiteSpace($categoryName)) {
        [void]$sb.AppendLine("SET @CategoryId = (SELECT TOP 1 [Id] FROM [Categories] WHERE [Name] = " + (Escape-SqlString $categoryName) + ");")
        [void]$sb.AppendLine("IF @CategoryId IS NULL BEGIN SET @CategoryId = NEWID(); INSERT INTO [Categories] ([Id], [Name]) VALUES (@CategoryId, " + (Escape-SqlString $categoryName) + "); END;")
    }
    if (-not [string]::IsNullOrWhiteSpace($period)) {
        [void]$sb.AppendLine("SET @EraId = (SELECT TOP 1 [Id] FROM [Eras] WHERE [Name] = " + (Escape-SqlString $period) + ");")
        [void]$sb.AppendLine("IF @EraId IS NULL BEGIN SET @EraId = NEWID(); INSERT INTO [Eras] ([Id], [Name], [StartYear], [EndYear]) VALUES (@EraId, " + (Escape-SqlString $period) + ", NULL, NULL); END;")
    }
    if (-not [string]::IsNullOrWhiteSpace($material)) {
        [void]$sb.AppendLine("SET @MaterialId = (SELECT TOP 1 [Id] FROM [Materials] WHERE [Name] = " + (Escape-SqlString $material) + ");")
        [void]$sb.AppendLine("IF @MaterialId IS NULL BEGIN SET @MaterialId = NEWID(); INSERT INTO [Materials] ([Id], [Name]) VALUES (@MaterialId, " + (Escape-SqlString $material) + "); END;")
    }
    if (-not [string]::IsNullOrWhiteSpace($discoverySite)) {
        [void]$sb.AppendLine("SET @DiscoveryLocationId = (SELECT TOP 1 [Id] FROM [DiscoveryLocations] WHERE [Name] = " + (Escape-SqlString $discoverySite) + ");")
        [void]$sb.AppendLine("IF @DiscoveryLocationId IS NULL BEGIN SET @DiscoveryLocationId = NEWID(); INSERT INTO [DiscoveryLocations] ([Id], [Name], [Latitude], [Longitude]) VALUES (@DiscoveryLocationId, " + (Escape-SqlString $discoverySite) + ", NULL, NULL); END;")
    }
    if (-not [string]::IsNullOrWhiteSpace($image3D)) {
        [void]$sb.AppendLine("SET @ModelFileId = (SELECT TOP 1 [Id] FROM [Files] WHERE [Url] = " + (Escape-SqlString $image3D) + ");")
        [void]$sb.AppendLine("IF @ModelFileId IS NULL BEGIN SET @ModelFileId = NEWID(); INSERT INTO [Files] ([Id], [FileName], [FileType], [Url], [StorageProvider], [UploadedBy], [CreatedAt]) VALUES (@ModelFileId, " + (Escape-SqlString ($slug + "-3d-embed")) + ", N'text/html', " + (Escape-SqlString $image3D) + ", N'external', ISNULL(@AdminUserId, '00000000-0000-0000-0000-000000000000'), SYSUTCDATETIME()); END;")
    }
    if (-not [string]::IsNullOrWhiteSpace($image)) {
        [void]$sb.AppendLine("SET @ThumbFileId = (SELECT TOP 1 [Id] FROM [Files] WHERE [Url] = " + (Escape-SqlString $image) + ");")
        [void]$sb.AppendLine("IF @ThumbFileId IS NULL BEGIN SET @ThumbFileId = NEWID(); INSERT INTO [Files] ([Id], [FileName], [FileType], [Url], [StorageProvider], [UploadedBy], [CreatedAt]) VALUES (@ThumbFileId, " + (Escape-SqlString ($slug + "-thumb")) + ", N'image', " + (Escape-SqlString $image) + ", N'external', ISNULL(@AdminUserId, '00000000-0000-0000-0000-000000000000'), SYSUTCDATETIME()); END;")
    }

    [void]$sb.AppendLine("IF @ArtifactId IS NULL")
    [void]$sb.AppendLine("BEGIN")
    [void]$sb.AppendLine("    SET @ArtifactId = NEWID();")
    [void]$sb.AppendLine("    INSERT INTO [Artifacts] ([Id], [Slug], [EraId], [CategoryId], [MaterialId], [DiscoveryLocationId], [ModelFileId], [ThumbnailFileId], [Height], [Width], [Depth], [Weight], [CreatedBy], [CreatedAt])")
    [void]$sb.AppendLine("    VALUES (@ArtifactId, " + (Escape-SqlString $slug) + ", @EraId, @CategoryId, @MaterialId, @DiscoveryLocationId, @ModelFileId, @ThumbFileId, $height, $width, $depth, $weight, @AdminUserId, SYSUTCDATETIME());")
    [void]$sb.AppendLine("END")
    [void]$sb.AppendLine("ELSE")
    [void]$sb.AppendLine("BEGIN")
    [void]$sb.AppendLine("    UPDATE [Artifacts]")
    [void]$sb.AppendLine("    SET [EraId] = @EraId,")
    [void]$sb.AppendLine("        [CategoryId] = @CategoryId,")
    [void]$sb.AppendLine("        [MaterialId] = @MaterialId,")
    [void]$sb.AppendLine("        [DiscoveryLocationId] = @DiscoveryLocationId,")
    [void]$sb.AppendLine("        [ModelFileId] = @ModelFileId,")
    [void]$sb.AppendLine("        [ThumbnailFileId] = @ThumbFileId,")
    [void]$sb.AppendLine("        [Height] = $height,")
    [void]$sb.AppendLine("        [Width] = $width,")
    [void]$sb.AppendLine("        [Depth] = $depth,")
    [void]$sb.AppendLine("        [Weight] = $weight")
    [void]$sb.AppendLine("    WHERE [Id] = @ArtifactId;")
    [void]$sb.AppendLine("END;")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("IF EXISTS (SELECT 1 FROM [ArtifactTranslations] WHERE [ArtifactId] = @ArtifactId AND [LanguageCode] = N'en')")
    [void]$sb.AppendLine("BEGIN")
    [void]$sb.AppendLine("    UPDATE [ArtifactTranslations]")
    [void]$sb.AppendLine("    SET [Name] = " + (Escape-SqlString $name) + ",")
    [void]$sb.AppendLine("        [Description] = " + (Escape-SqlString $description) + ",")
    [void]$sb.AppendLine("        [HistoricalStory] = " + (Escape-SqlString $historicalStory))
    [void]$sb.AppendLine("    WHERE [ArtifactId] = @ArtifactId AND [LanguageCode] = N'en';")
    [void]$sb.AppendLine("END")
    [void]$sb.AppendLine("ELSE")
    [void]$sb.AppendLine("BEGIN")
    [void]$sb.AppendLine("    INSERT INTO [ArtifactTranslations] ([Id], [ArtifactId], [LanguageCode], [Name], [Description], [HistoricalStory])")
    [void]$sb.AppendLine("    VALUES (NEWID(), @ArtifactId, N'en', " + (Escape-SqlString $name) + ", " + (Escape-SqlString $description) + ", " + (Escape-SqlString $historicalStory) + ");")
    [void]$sb.AppendLine("END;")
    [void]$sb.AppendLine("")
}

[void]$sb.AppendLine("COMMIT TRANSACTION;")
[void]$sb.AppendLine("SELECT COUNT(*) AS TotalArtifacts FROM [Artifacts];")
[void]$sb.AppendLine("SELECT COUNT(*) AS EnglishTranslations FROM [ArtifactTranslations] WHERE [LanguageCode] = N'en';")

$outputDir = Split-Path -Parent $OutputSqlPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

[System.IO.File]::WriteAllText((Resolve-Path -LiteralPath $outputDir).Path + "\" + (Split-Path -Leaf $OutputSqlPath), $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Output "Generated SQL seed file: $OutputSqlPath"
