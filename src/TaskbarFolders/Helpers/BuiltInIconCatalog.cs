using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mark.TaskbarFolders.Helpers;

public static class BuiltInIconCatalog
{
    public const string Prefix = "builtin:";

    public const string DefaultIconId = "work-folder";

    public static string DefaultIconReference => CreateReference(DefaultIconId);

    public static Color DefaultBackground { get; } = Color.FromRgb(0x4B, 0x55, 0x63);

    private const int BitmapSize = 256;
    public const int PreviewBitmapSize = 64;
    private const double GlyphExtent = 150;
    private const double StrokeWidth = 14;

    private static readonly ConcurrentDictionary<string, Geometry> GeometryCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, BitmapSource> ImageCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, BitmapSource> PreviewImageCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<BuiltInIconTheme> Themes { get; } =
    [
        new("work", "IconThemeWork"),
        new("development", "IconThemeDevelopment"),
        new("media", "IconThemeMedia"),
        new("life", "IconThemeLife"),
        new("games", "IconThemeGames"),
        new("software", "IconThemeSoftware")
    ];

    public static IReadOnlyList<BuiltInIconDefinition> All { get; } = CreateIcons();

    private static readonly Dictionary<string, BuiltInIconDefinition> Index =
        All.ToDictionary(definition => definition.Id, StringComparer.OrdinalIgnoreCase);

    public static bool IsBuiltIn(string? iconReference) =>
        !string.IsNullOrWhiteSpace(iconReference) &&
        iconReference.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsKnown(string? iconReference) => Find(iconReference) is not null;

    public static string CreateReference(string iconId) => Prefix + iconId;

    public static string NormalizeReference(string? iconReference)
    {
        var definition = Find(iconReference);
        return definition is null ? DefaultIconReference : CreateReference(definition.Id);
    }

    public static BuiltInIconDefinition? Find(string? iconReference)
    {
        if (!IsBuiltIn(iconReference))
        {
            return null;
        }

        var id = iconReference![Prefix.Length..];
        return Index.TryGetValue(id, out var definition) ? definition : null;
    }

    public static BitmapSource? CreateImage(string? iconId, Color? background = null)
    {
        if (string.IsNullOrWhiteSpace(iconId) || !Index.TryGetValue(iconId, out var definition))
        {
            return null;
        }

        var color = background ?? DefaultBackground;
        var key = $"{definition.Id}|{color.R:X2}{color.G:X2}{color.B:X2}";
        return ImageCache.GetOrAdd(key, _ => Render(definition, color, BitmapSize));
    }

    public static BitmapSource? CreatePreviewImage(
        string? iconId,
        Color? background = null,
        int pixelSize = PreviewBitmapSize)
    {
        if (string.IsNullOrWhiteSpace(iconId) || !Index.TryGetValue(iconId, out var definition))
        {
            return null;
        }

        var clampedSize = Math.Clamp(pixelSize, 16, BitmapSize);
        var color = background ?? DefaultBackground;
        var key = $"{definition.Id}|{color.R:X2}{color.G:X2}{color.B:X2}|{clampedSize}";
        return PreviewImageCache.GetOrAdd(key, _ => Render(definition, color, clampedSize));
    }

    public static void ClearCaches()
    {
        ImageCache.Clear();
        PreviewImageCache.Clear();
    }

    public static void SaveIco(string iconReference, string destinationPath, Color? background = null)
    {
        var definition = Find(iconReference)
            ?? throw new ArgumentException("Unknown built-in icon.", nameof(iconReference));
        var image = Render(definition, background ?? DefaultBackground, BitmapSize);

        IcoWriter.Write(image, destinationPath);
    }

    private static IReadOnlyList<BuiltInIconDefinition> CreateIcons()
    {
        var icons = new List<BuiltInIconDefinition>(60);

        AddTheme(icons, "work",
        [
            ("work-folder", "IconWorkFolder", "M3,7 L9,7 L11,9 L21,9 L21,19 L3,19 Z"),
            ("work-mail", "IconWorkMail", "M3,6 L21,6 L21,18 L3,18 Z M3,7 L12,13 L21,7"),
            ("work-calendar", "IconWorkCalendar", "M4,6 L20,6 L20,20 L4,20 Z M4,10 L20,10 M8,4 L8,8 M16,4 L16,8"),
            ("work-notes", "IconWorkNotes", "M6,3 L15,3 L19,7 L19,21 L6,21 Z M15,3 L15,7 L19,7 M9,12 L16,12 M9,16 L16,16"),
            ("work-tasks", "IconWorkTasks", "M4,5 L20,5 L20,19 L4,19 Z M7,9 L9,11 L12,7 M14,9 L17,9 M7,15 L9,17 L12,13 M14,15 L17,15"),
            ("work-files", "IconWorkFiles", "M4,7 L13,7 L17,11 L17,21 L4,21 Z M13,7 L13,11 L17,11 M7,3 L16,3 L20,7 L20,17"),
            ("work-search", "IconWorkSearch", "M10.5,4 A6.5,6.5 0 1 1 10.5,17 A6.5,6.5 0 1 1 10.5,4 M15.2,15.2 L21,21"),
            ("work-download", "IconWorkDownload", "M12,3 L12,15 M7,10 L12,15 L17,10 M4,19 L20,19"),
            ("work-briefcase", "IconWorkBriefcase", "M3,8 L21,8 L21,20 L3,20 Z M9,8 L9,5 L15,5 L15,8 M3,13 L21,13 M10,13 L10,15 L14,15 L14,13"),
            ("work-chart", "IconWorkChart", "M4,4 L4,20 L20,20 M7,16 L11,11 L14,14 L20,7")
        ]);

        AddTheme(icons, "development",
        [
            ("development-terminal", "IconDevelopmentTerminal", "M3,5 L21,5 L21,19 L3,19 Z M6,9 L10,12 L6,15 M11,15 L15,15"),
            ("development-code", "IconDevelopmentCode", "M8,8 L3,12 L8,16 M16,8 L21,12 L16,16 M14,5 L10,19"),
            ("development-settings", "IconDevelopmentSettings", "M12,8 A4,4 0 1 1 12,16 A4,4 0 1 1 12,8 M12,2 L12,5 M12,19 L12,22 M2,12 L5,12 M19,12 L22,12 M5,5 L7,7 M17,17 L19,19 M19,5 L17,7 M7,17 L5,19"),
            ("development-database", "IconDevelopmentDatabase", "M5,6 A7,3 0 1 1 19,6 A7,3 0 1 1 5,6 M5,6 L5,18 A7,3 0 0 0 19,18 L19,6 M5,12 A7,3 0 0 0 19,12"),
            ("development-server", "IconDevelopmentServer", "M4,4 L20,4 L20,10 L4,10 Z M4,14 L20,14 L20,20 L4,20 Z M7,7 L8,7 M7,17 L8,17 M11,7 L17,7 M11,17 L17,17"),
            ("development-cloud", "IconDevelopmentCloud", "M7,18 L17,18 A4,4 0 0 0 17,10 A6,6 0 0 0 5.5,11.5 A3.5,3.5 0 0 0 7,18 Z"),
            ("development-branch", "IconDevelopmentBranch", "M6,7 L6,17 M6,5 A2,2 0 1 1 6,9 A2,2 0 1 1 6,5 M6,17 A2,2 0 1 1 6,21 A2,2 0 1 1 6,17 M18,5 A2,2 0 1 1 18,9 A2,2 0 1 1 18,5 M18,9 C18,13 12,13 6,13"),
            ("development-bug", "IconDevelopmentBug", "M8,8 L16,8 L16,16 L8,16 Z M9,5 L7,3 M15,5 L17,3 M8,10 L4,10 M8,14 L4,14 M16,10 L20,10 M16,14 L20,14 M9,16 L7,19 M15,16 L17,19 M10,8 L10,5 M14,8 L14,5"),
            ("development-package", "IconDevelopmentPackage", "M3,8 L12,4 L21,8 L12,12 Z M3,8 L3,16 L12,20 L21,16 L21,8 M12,12 L12,20 M7.5,6 L16.5,10"),
            ("development-api", "IconDevelopmentApi", "M8,4 L4,8 L4,16 L8,20 M16,4 L20,8 L20,16 L16,20 M9,12 L15,12")
        ]);

        AddTheme(icons, "media",
        [
            ("media-music", "IconMediaMusic", "M9,17 L9,6 L19,4 L19,15 M9,17 A2,2 0 1 1 5,17 A2,2 0 1 1 9,17 M19,15 A2,2 0 1 1 15,15 A2,2 0 1 1 19,15"),
            ("media-video", "IconMediaVideo", "M3,6 L15,6 L15,18 L3,18 Z M15,10 L21,7 L21,17 L15,14"),
            ("media-photo", "IconMediaPhoto", "M3,5 L21,5 L21,19 L3,19 Z M3,16 L8,11 L12,15 L15,12 L21,18 M14.4,9.6 A1.6,1.6 0 1 1 17.6,9.6 A1.6,1.6 0 1 1 14.4,9.6"),
            ("media-camera", "IconMediaCamera", "M4,8 L8,8 L10,5 L14,5 L16,8 L20,8 L20,19 L4,19 Z M12,10 A3.5,3.5 0 1 1 12,17 A3.5,3.5 0 1 1 12,10"),
            ("media-headphones", "IconMediaHeadphones", "M4,14 L4,12 A8,8 0 0 1 20,12 L20,14 M4,14 L7,14 L7,20 L4,20 Z M17,14 L20,14 L20,20 L17,20 Z"),
            ("media-microphone", "IconMediaMicrophone", "M12,3 A3,3 0 0 1 15,6 L15,12 A3,3 0 0 1 9,12 L9,6 A3,3 0 0 1 12,3 Z M6,11 L6,12 A6,6 0 0 0 18,12 L18,11 M12,18 L12,22 M8,22 L16,22"),
            ("media-podcast", "IconMediaPodcast", "M12,10 A2,2 0 1 1 12,14 A2,2 0 1 1 12,10 M7,7 A8,8 0 0 0 7,17 M17,7 A8,8 0 0 1 17,17 M4,4 A12,12 0 0 0 4,20 M20,4 A12,12 0 0 1 20,20"),
            ("media-film", "IconMediaFilm", "M3,5 L21,5 L21,19 L3,19 Z M7,5 L7,19 M17,5 L17,19 M3,9 L7,9 M3,15 L7,15 M17,9 L21,9 M17,15 L21,15"),
            ("media-gallery", "IconMediaGallery", "M3,5 L15,5 L15,15 L3,15 Z M7,19 L21,19 L21,9 L15,9 M6,13 L9,10 L12,13 M13,9 A1,1 0 1 1 13,11 A1,1 0 1 1 13,9"),
            ("media-play", "IconMediaPlay", "M7,5 L19,12 L7,19 Z M4,5 L4,19")
        ]);

        AddTheme(icons, "life",
        [
            ("life-home", "IconLifeHome", "M3,11 L12,3 L21,11 M5,10 L5,21 L19,21 L19,10 M9,21 L9,15 L15,15 L15,21"),
            ("life-star", "IconLifeStar", "M12,3 L14.8,9.1 L21.4,9.9 L16.5,14.4 L17.8,21 L12,17.7 L6.2,21 L7.5,14.4 L2.6,9.9 L9.2,9.1 Z"),
            ("life-heart", "IconLifeHeart", "M12,21 C6,17 3,13.5 3,9.5 A4.5,4.5 0 0 1 12,7 A4.5,4.5 0 0 1 21,9.5 C21,13.5 18,17 12,21 Z"),
            ("life-cart", "IconLifeCart", "M3,4 L6,4 L8,15 L19,15 L21,7 L7,7 M8,18 A1.5,1.5 0 1 1 8,21 A1.5,1.5 0 1 1 8,18 M18,18 A1.5,1.5 0 1 1 18,21 A1.5,1.5 0 1 1 18,18"),
            ("life-food", "IconLifeFood", "M7,3 L7,11 M4,3 L4,8 A3,3 0 0 0 10,8 L10,3 M7,11 L7,21 M17,3 L17,21 M14,3 C14,8 15,10 17,10"),
            ("life-coffee", "IconLifeCoffee", "M4,8 L17,8 L17,14 A5,5 0 0 1 12,19 L9,19 A5,5 0 0 1 4,14 Z M17,10 L19,10 A2,2 0 0 1 19,14 L17,14 M6,3 C6,5 8,5 8,7 M10,3 C10,5 12,5 12,7 M14,3 C14,5 16,5 16,7 M3,21 L18,21"),
            ("life-travel", "IconLifeTravel", "M3,13 L21,4 L15,20 L11,14 L3,13 Z M11,14 L21,4"),
            ("life-weather", "IconLifeWeather", "M12,3 L12,5 M12,15 L12,17 M3,10 L5,10 M19,10 L21,10 M5.5,4.5 L7,6 M17,6 L18.5,4.5 M5.5,15.5 L7,14 M17,14 L18.5,15.5 M12,7 A3,3 0 1 1 12,13 A3,3 0 1 1 12,7"),
            ("life-health", "IconLifeHealth", "M12,3 A9,9 0 1 1 12,21 A9,9 0 1 1 12,3 M6,12 L9,12 L11,8 L14,16 L16,12 L18,12"),
            ("life-book", "IconLifeBook", "M4,5 L4,19 L11,19 L12,20 L13,19 L20,19 L20,5 L13,5 L12,6 L11,5 Z M12,6 L12,20")
        ]);

        AddTheme(icons, "games",
        [
            ("games-gamepad", "IconGamesGamepad", "M5,8 L19,8 A4,4 0 0 1 23,12 L23,13 A3,3 0 0 1 17,13 L16,12 L8,12 L7,13 A3,3 0 0 1 1,13 L1,12 A4,4 0 0 1 5,8 Z M6,9 L6,12 M4.5,10.5 L7.5,10.5 M17,10 A1,1 0 1 1 17,12 A1,1 0 1 1 17,10 M19,11.5 A1,1 0 1 1 19,13.5 A1,1 0 1 1 19,11.5"),
            ("games-chat", "IconGamesChat", "M4,5 L20,5 L20,16 L10,16 L6,20 L6,16 L4,16 Z M8,9 L16,9 M8,12 L14,12"),
            ("games-users", "IconGamesUsers", "M8,11 A3,3 0 1 1 8,5 A3,3 0 1 1 8,11 M2,20 A6,6 0 0 1 14,20 M16,11 A3,3 0 1 1 16,5 A3,3 0 1 1 16,11 M16,14 A5,5 0 0 1 22,19"),
            ("games-globe", "IconGamesGlobe", "M12,3 A9,9 0 1 1 12,21 A9,9 0 1 1 12,3 M3,12 L21,12 M12,3 A6,9 0 1 1 12,21 A6,9 0 1 1 12,3"),
            ("games-rocket", "IconGamesRocket", "M12,3 C15,6 16,10 16,14 L12,18 L8,14 C8,10 9,6 12,3 Z M8,14 L5,17 L6,20 L9,17 M16,14 L19,17 L18,20 L15,17 M12,8 A2,2 0 1 1 12,12 A2,2 0 1 1 12,8 M10,18 L12,22 L14,18"),
            ("games-trophy", "IconGamesTrophy", "M8,4 L16,4 L16,9 A4,4 0 0 1 8,9 Z M8,5 L4,5 L4,8 A4,4 0 0 0 8,12 M16,5 L20,5 L20,8 A4,4 0 0 1 16,12 M12,13 L12,18 M7,21 L17,21 M9,18 L15,18"),
            ("games-dice", "IconGamesDice", "M5,5 L19,5 L19,19 L5,19 Z M9,9 A1,1 0 1 1 9,11 A1,1 0 1 1 9,9 M15,9 A1,1 0 1 1 15,11 A1,1 0 1 1 15,9 M9,15 A1,1 0 1 1 9,17 A1,1 0 1 1 9,15 M15,15 A1,1 0 1 1 15,17 A1,1 0 1 1 15,15 M12,12 A1,1 0 1 1 12,14 A1,1 0 1 1 12,12"),
            ("games-puzzle", "IconGamesPuzzle", "M4,9 L4,4 L9,4 C9,6 10,7 12,7 C14,7 15,6 15,4 L20,4 L20,9 C18,9 17,10 17,12 C17,14 18,15 20,15 L20,20 L15,20 C15,18 14,17 12,17 C10,17 9,18 9,20 L4,20 L4,15 C6,15 7,14 7,12 C7,10 6,9 4,9 Z"),
            ("games-gift", "IconGamesGift", "M3,9 L21,9 L21,13 L3,13 Z M5,13 L5,21 L19,21 L19,13 M12,9 L12,21 M12,9 C9,9 7,7 7,5 A2,2 0 0 1 12,6 A2,2 0 0 1 17,5 C17,7 15,9 12,9 Z"),
            ("games-share", "IconGamesShare", "M18,3 A2,2 0 1 1 18,7 A2,2 0 1 1 18,3 M6,10 A2,2 0 1 1 6,14 A2,2 0 1 1 6,10 M18,17 A2,2 0 1 1 18,21 A2,2 0 1 1 18,17 M8,12 L16,6 M8,12 L16,18")
        ]);

        AddTheme(icons, "software",
        [
            ("software-app", "IconSoftwareApp", "M4,5 L20,5 L20,19 L4,19 Z M4,9 L20,9 M8,7 L8.1,7 M11,7 L11.1,7 M14,7 L14.1,7"),
            ("software-install", "IconSoftwareInstall", "M12,3 L12,15 M7,10 L12,15 L17,10 M5,19 L19,19 M5,19 L5,21 L19,21 L19,19"),
            ("software-update", "IconSoftwareUpdate", "M20,12 A8,8 0 1 1 17,5.5 M17,3 L17,6 L20,6 M4,12 A8,8 0 0 0 7,18.5 M7,21 L7,18 L4,18"),
            ("software-store", "IconSoftwareStore", "M6,8 L18,8 L19,21 L5,21 Z M9,8 L9,6 A3,3 0 0 1 15,6 L15,8"),
            ("software-plugin", "IconSoftwarePlugin", "M9,3 L9,8 M15,3 L15,8 M6,8 L18,8 L18,12 A6,6 0 0 1 6,12 Z M12,18 L12,21 M9,21 L15,21"),
            ("software-extension", "IconSoftwareExtension", "M4,4 L10,4 L10,10 L4,10 Z M14,4 L20,4 L20,10 L14,10 Z M4,14 L10,14 L10,20 L4,20 Z M14,14 L20,14 L20,20 L14,20 Z M10,7 L14,7 M7,10 L7,14 M17,10 L17,14 M10,17 L14,17"),
            ("software-license", "IconSoftwareLicense", "M7,3 A4,4 0 1 1 7,11 A4,4 0 1 1 7,3 M11,7 L21,7 M18,7 L18,10 M15,7 L15,9"),
            ("software-backup", "IconSoftwareBackup", "M4,7 L10,7 L12,9 L20,9 L20,20 L4,20 Z M8,14 A4,4 0 1 1 16,14 A4,4 0 1 1 8,14 M12,12 L12,14 L14,14"),
            ("software-driver", "IconSoftwareDriver", "M9,9 L15,9 L15,15 L9,15 Z M7,7 L17,7 L17,17 L7,17 Z M12,3 L12,7 M8,3 L8,7 M16,3 L16,7 M12,17 L12,21 M8,17 L8,21 M16,17 L16,21 M3,8 L7,8 M3,12 L7,12 M3,16 L7,16 M17,8 L21,8 M17,12 L21,12 M17,16 L21,16"),
            ("software-utility", "IconSoftwareUtility", "M14,4 A4,4 0 0 1 20,10 L11,19 A3,3 0 0 1 7,15 L16,6 A4,4 0 0 1 14,4 Z M9,17 L7,15")
        ]);

        return icons;
    }

    private static void AddTheme(
        List<BuiltInIconDefinition> icons,
        string themeId,
        (string Id, string NameResourceKey, string PathData)[] entries)
    {
        var theme = Themes.Single(candidate => candidate.Id == themeId);
        foreach (var entry in entries)
        {
            icons.Add(new BuiltInIconDefinition(
                entry.Id,
                theme.Id,
                entry.NameResourceKey,
                entry.PathData));
        }
    }

    private static Geometry GetGeometry(BuiltInIconDefinition definition) =>
        GeometryCache.GetOrAdd(definition.Id, _ =>
        {
            var geometry = Geometry.Parse(definition.PathData);
            geometry.Freeze();
            return geometry;
        });

    private static BitmapSource Render(BuiltInIconDefinition definition, Color background, int bitmapSize)
    {
        var ratio = bitmapSize / (double)BitmapSize;
        var cornerRadius = 56 * ratio;
        var glyphExtent = GlyphExtent * ratio;
        var strokeWidth = StrokeWidth * ratio;

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var backgroundBrush = new SolidColorBrush(background);
            backgroundBrush.Freeze();
            context.DrawRoundedRectangle(
                backgroundBrush,
                null,
                new Rect(0, 0, bitmapSize, bitmapSize),
                cornerRadius,
                cornerRadius);

            var geometry = GetGeometry(definition);
            var bounds = geometry.Bounds;
            var scale = glyphExtent / Math.Max(bounds.Width, bounds.Height);

            var pen = new Pen(Brushes.White, strokeWidth / scale)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();

            context.PushTransform(new TransformGroup
            {
                Children =
                {
                    new ScaleTransform(scale, scale),
                    new TranslateTransform(
                        ((bitmapSize - (bounds.Width * scale)) / 2) - (bounds.X * scale),
                        ((bitmapSize - (bounds.Height * scale)) / 2) - (bounds.Y * scale))
                }
            });
            context.DrawGeometry(null, pen, geometry);
            context.Pop();
        }

        var bitmap = new RenderTargetBitmap(bitmapSize, bitmapSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
