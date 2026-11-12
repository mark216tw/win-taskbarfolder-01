using System.IO;
using System.Windows.Media.Imaging;

namespace Mark.TaskbarFolders.Helpers;

public static class IcoWriter
{
    public static void Write(BitmapSource frame, string destinationPath)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));

        byte[] png;
        using (var memory = new MemoryStream())
        {
            encoder.Save(memory);
            png = memory.ToArray();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        using var file = File.Create(destinationPath);
        using var writer = new BinaryWriter(file);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);

        var width = frame.PixelWidth >= 256 ? (byte)0 : (byte)frame.PixelWidth;
        var height = frame.PixelHeight >= 256 ? (byte)0 : (byte)frame.PixelHeight;
        writer.Write(width);
        writer.Write(height);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)png.Length);
        writer.Write((uint)22);
        writer.Write(png);
    }
}
