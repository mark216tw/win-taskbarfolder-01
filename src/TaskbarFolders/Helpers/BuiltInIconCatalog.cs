using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mark.TaskbarFolders.Helpers;

public static class BuiltInIconCatalog
{
    public const string Prefix = "builtin:";

    public const string DefaultIconId = "folder";

    public static string DefaultIconReference => CreateReference(DefaultIconId);

    private const int BitmapSize = 256;
    private const double GlyphExtent = 150;
    private const double StrokeWidth = 14;

    private static readonly ConcurrentDictionary<string, Geometry> GeometryCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, BitmapSource> ImageCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<BuiltInIconDefinition> All { get; } =
    [
        new("folder", "IconFolder", Color.FromRgb(0x25, 0x63, 0xEB),
            "M3,7 L9,7 L11,9 L21,9 L21,19 L3,19 Z"),
        new("mail", "IconMail", Color.FromRgb(0x08, 0x91, 0xB2),
            "M3,6 L21,6 L21,18 L3,18 Z M3,7 L12,13 L21,7"),
        new("calendar", "IconCalendar", Color.FromRgb(0xD9, 0x77, 0x06),
            "M4,6 L20,6 L20,20 L4,20 Z M4,10 L20,10 M8,4 L8,8 M16,4 L16,8"),
        new("notes", "IconNotes", Color.FromRgb(0x7C, 0x3A, 0xED),
            "M6,3 L15,3 L19,7 L19,21 L6,21 Z M15,3 L15,7 L19,7 M9,12 L16,12 M9,16 L16,16"),
        new("files", "IconFiles", Color.FromRgb(0x4B, 0x55, 0x63),
            "M4,7 L13,7 L17,11 L17,21 L4,21 Z M13,7 L13,11 L17,11 M7,3 L16,3 L20,7 L20,17"),
        new("browser", "IconBrowser", Color.FromRgb(0x0E, 0x9F, 0x6E),
            "M4,12 A8,8 0 1 1 20,12 A8,8 0 1 1 4,12 M4,12 L20,12 M12,4 A5,8 0 1 1 12,20 A5,8 0 1 1 12,4"),
        new("terminal", "IconTerminal", Color.FromRgb(0x1F, 0x29, 0x37),
            "M3,5 L21,5 L21,19 L3,19 Z M6,9 L10,12 L6,15 M11,15 L15,15"),
        new("settings", "IconSettings", Color.FromRgb(0x64, 0x74, 0x8B),
            "M12,8 A4,4 0 1 1 12,16 A4,4 0 1 1 12,8 M12,2 L12,5 M12,19 L12,22 M2,12 L5,12 M19,12 L22,12 M5,5 L7,7 M17,17 L19,19 M19,5 L17,7 M7,17 L5,19"),
        new("music", "IconMusic", Color.FromRgb(0xBE, 0x18, 0x5D),
            "M9,17 L9,6 L19,4 L19,15 M9,17 A2,2 0 1 1 5,17 A2,2 0 1 1 9,17 M19,15 A2,2 0 1 1 15,15 A2,2 0 1 1 19,15"),
        new("games", "IconGames", Color.FromRgb(0xE1, 0x1D, 0x48),
            "M5,8 L19,8 A4,4 0 0 1 23,12 L23,13 A3,3 0 0 1 17,13 L16,12 L8,12 L7,13 A3,3 0 0 1 1,13 L1,12 A4,4 0 0 1 5,8 Z M6,9 L6,12 M4.5,10.5 L7.5,10.5 M17,10 A1,1 0 1 1 17,12 A1,1 0 1 1 17,10 M19,11.5 A1,1 0 1 1 19,13.5 A1,1 0 1 1 19,11.5"),
        new("star", "IconStar", Color.FromRgb(0xF5, 0x9E, 0x0B),
            "M12,3 L14.8,9.1 L21.4,9.9 L16.5,14.4 L17.8,21 L12,17.7 L6.2,21 L7.5,14.4 L2.6,9.9 L9.2,9.1 Z"),
        new("home", "IconHome", Color.FromRgb(0x0E, 0xA5, 0xE9),
            "M3,11 L12,3 L21,11 M5,10 L5,21 L19,21 L19,10 M9,21 L9,15 L15,15 L15,21"),
        new("work", "IconWork", Color.FromRgb(0x7C, 0x3A, 0xED),
            "M3,8 L21,8 L21,20 L3,20 Z M9,8 L9,5 L15,5 L15,8 M3,13 L21,13"),
        new("code", "IconCode", Color.FromRgb(0x10, 0xB9, 0x81),
            "M8,8 L3,12 L8,16 M16,8 L21,12 L16,16 M14,5 L10,19"),
        new("chat", "IconChat", Color.FromRgb(0x06, 0xB6, 0xD4),
            "M4,5 L20,5 L20,16 L10,16 L6,20 L6,16 L4,16 Z M8,9 L16,9 M8,12 L14,12"),
        new("photo", "IconPhoto", Color.FromRgb(0xF9, 0x73, 0x16),
            "M3,5 L21,5 L21,19 L3,19 Z M3,16 L8,11 L12,15 L15,12 L21,18 M14.4,9.6 A1.6,1.6 0 1 1 17.6,9.6 A1.6,1.6 0 1 1 14.4,9.6"),
        new("video", "IconVideo", Color.FromRgb(0xEF, 0x44, 0x44),
            "M3,6 L15,6 L15,18 L3,18 Z M15,10 L21,7 L21,17 L15,14"),
        new("book", "IconBook", Color.FromRgb(0x8B, 0x5C, 0xF6),
            "M4,5 L4,19 L11,19 L12,20 L13,19 L20,19 L20,5 L13,5 L12,6 L11,5 Z M12,6 L12,20"),
        new("search", "IconSearch", Color.FromRgb(0x08, 0x91, 0xB2),
            "M10.5,4 A6.5,6.5 0 1 1 10.5,17 A6.5,6.5 0 1 1 10.5,4 M15.2,15.2 L21,21"),
        new("download", "IconDownload", Color.FromRgb(0x25, 0x63, 0xEB),
            "M12,3 L12,15 M7,10 L12,15 L17,10 M4,19 L20,19")
    ];

    private static readonly Dictionary<string, BuiltInIconDefinition> Index =
        All.ToDictionary(definition => definition.Id, StringComparer.OrdinalIgnoreCase);

    public static bool IsBuiltIn(string? iconReference) =>
        !string.IsNullOrWhiteSpace(iconReference) &&
        iconReference.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static string CreateReference(string iconId) => Prefix + iconId;

    public static BuiltInIconDefinition? Find(string? iconReference)
    {
        if (!IsBuiltIn(iconReference))
        {
            return null;
        }

        var id = iconReference![Prefix.Length..];
        return Index.TryGetValue(id, out var definition) ? definition : null;
    }

    public static BitmapSource? CreateImage(string? iconId)
    {
        if (string.IsNullOrWhiteSpace(iconId) || !Index.TryGetValue(iconId, out var definition))
        {
            return null;
        }

        return ImageCache.GetOrAdd(definition.Id, _ => Render(definition));
    }

    public static void SaveIco(string iconReference, string destinationPath)
    {
        var definition = Find(iconReference)
            ?? throw new ArgumentException("Unknown built-in icon.", nameof(iconReference));
        var image = CreateImage(definition.Id)
            ?? throw new InvalidOperationException("Unable to render built-in icon.");

        IcoWriter.Write(image, destinationPath);
    }

    private static Geometry GetGeometry(BuiltInIconDefinition definition) =>
        GeometryCache.GetOrAdd(definition.Id, _ =>
        {
            var geometry = Geometry.Parse(definition.PathData);
            geometry.Freeze();
            return geometry;
        });

    private static BitmapSource Render(BuiltInIconDefinition definition)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var background = new SolidColorBrush(definition.Color);
            background.Freeze();
            context.DrawRoundedRectangle(
                background,
                null,
                new Rect(0, 0, BitmapSize, BitmapSize),
                56,
                56);

            var geometry = GetGeometry(definition);
            var bounds = geometry.Bounds;
            var scale = GlyphExtent / Math.Max(bounds.Width, bounds.Height);

            var pen = new Pen(Brushes.White, StrokeWidth / scale)
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
                        ((BitmapSize - (bounds.Width * scale)) / 2) - (bounds.X * scale),
                        ((BitmapSize - (bounds.Height * scale)) / 2) - (bounds.Y * scale))
                }
            });
            context.DrawGeometry(null, pen, geometry);
            context.Pop();
        }

        var bitmap = new RenderTargetBitmap(BitmapSize, BitmapSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
