namespace GbaEmulator.Core;

public sealed class BmpFrameBuffer
{
    private readonly uint[] _pixels;

    public int Width { get; }
    public int Height { get; }

    public BmpFrameBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new uint[width * height];
    }

    public void Clear(byte red, byte green, byte blue)
    {
        uint color = PackColor(red, green, blue);
        Array.Fill(_pixels, color);
    }

    public void FillRect(int x, int y, int width, int height, byte red, byte green, byte blue)
    {
        uint color = PackColor(red, green, blue);
        int xEnd = Math.Min(x + width, Width);
        int yEnd = Math.Min(y + height, Height);

        for (int py = Math.Max(y, 0); py < yEnd; py++)
        {
            for (int px = Math.Max(x, 0); px < xEnd; px++)
            {
                _pixels[(py * Width) + px] = color;
            }
        }
    }

    public void SaveBmp(string path)
    {
        int rowSize = ((Width * 3) + 3) & ~3;
        int pixelDataSize = rowSize * Height;
        int fileSize = 54 + pixelDataSize;

        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(Width);
        writer.Write(Height);
        writer.Write((ushort)1);
        writer.Write((ushort)24);
        writer.Write(0);
        writer.Write(pixelDataSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        byte[] padding = new byte[rowSize - (Width * 3)];

        for (int y = Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < Width; x++)
            {
                uint color = _pixels[(y * Width) + x];
                writer.Write((byte)(color & 0xFF));
                writer.Write((byte)((color >> 8) & 0xFF));
                writer.Write((byte)((color >> 16) & 0xFF));
            }

            writer.Write(padding);
        }
    }

    private static uint PackColor(byte red, byte green, byte blue)
    {
        return (uint)(blue | (green << 8) | (red << 16));
    }
}
