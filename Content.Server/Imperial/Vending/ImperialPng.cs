using System.IO;
using System.IO.Compression;

namespace Content.Server.Imperial.Vending;

/// <summary>
/// Минимальный PNG-кодек для снимков камеры (8 бит, RGB/RGBA/серый/палитра на чтение, RGBA или серый на запись).
/// </summary>
public static class ImperialPng
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Пиксели RGBA построчно сверху вниз.</summary>
    public static bool TryDecode(byte[] png, out int width, out int height, out byte[] rgba)
    {
        width = height = 0;
        rgba = Array.Empty<byte>();
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            return false;

        int depth = 0, colorType = 0, interlace = 0;
        byte[]? palette = null;
        byte[]? alpha = null;
        using var idat = new MemoryStream();
        var pos = 8;
        while (pos + 8 <= png.Length)
        {
            var len = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
            var type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            var data = pos + 8;
            if (len < 0 || data + len > png.Length)
                return false;

            switch (type)
            {
                case "IHDR":
                    width = (png[data] << 24) | (png[data + 1] << 16) | (png[data + 2] << 8) | png[data + 3];
                    height = (png[data + 4] << 24) | (png[data + 5] << 16) | (png[data + 6] << 8) | png[data + 7];
                    depth = png[data + 8];
                    colorType = png[data + 9];
                    interlace = png[data + 12];
                    break;
                case "PLTE":
                    palette = png.AsSpan(data, len).ToArray();
                    break;
                case "tRNS":
                    alpha = png.AsSpan(data, len).ToArray();
                    break;
                case "IDAT":
                    idat.Write(png, data, len);
                    break;
            }

            pos = data + len + 4;
            if (type == "IEND")
                break;
        }

        if (width <= 0 || height <= 0 || width > 1024 || height > 1024 || depth != 8 || interlace != 0)
            return false;

        var channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
        if (channels == 0 || colorType == 3 && palette == null)
            return false;

        idat.Position = 0;
        byte[] raw;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
        using (var outStream = new MemoryStream())
        {
            z.CopyTo(outStream);
            raw = outStream.ToArray();
        }

        var stride = width * channels;
        if (raw.Length < (stride + 1) * height)
            return false;

        var prev = new byte[stride];
        var line = new byte[stride];
        rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            Array.Copy(raw, y * (stride + 1) + 1, line, 0, stride);
            for (var i = 0; i < stride; i++)
            {
                int a = i >= channels ? line[i - channels] : 0;
                int b = prev[i];
                int c = i >= channels ? prev[i - channels] : 0;
                line[i] = filter switch
                {
                    1 => (byte) (line[i] + a),
                    2 => (byte) (line[i] + b),
                    3 => (byte) (line[i] + (a + b) / 2),
                    4 => (byte) (line[i] + Paeth(a, b, c)),
                    _ => line[i],
                };
            }

            for (var x = 0; x < width; x++)
            {
                var o = (y * width + x) * 4;
                switch (colorType)
                {
                    case 0:
                        rgba[o] = rgba[o + 1] = rgba[o + 2] = line[x];
                        rgba[o + 3] = 255;
                        break;
                    case 2:
                        rgba[o] = line[x * 3];
                        rgba[o + 1] = line[x * 3 + 1];
                        rgba[o + 2] = line[x * 3 + 2];
                        rgba[o + 3] = 255;
                        break;
                    case 3:
                        var idx = line[x];
                        if (idx * 3 + 2 >= palette!.Length)
                            return false;
                        rgba[o] = palette[idx * 3];
                        rgba[o + 1] = palette[idx * 3 + 1];
                        rgba[o + 2] = palette[idx * 3 + 2];
                        rgba[o + 3] = alpha != null && idx < alpha.Length ? alpha[idx] : (byte) 255;
                        break;
                    case 4:
                        rgba[o] = rgba[o + 1] = rgba[o + 2] = line[x * 2];
                        rgba[o + 3] = line[x * 2 + 1];
                        break;
                    default:
                        Array.Copy(line, x * 4, rgba, o, 4);
                        break;
                }
            }

            (prev, line) = (line, prev);
        }

        return true;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    /// <summary>RGBA (grey = false) или 8-битный серый (grey = true), фильтр 0.</summary>
    public static byte[] Encode(int width, int height, byte[] rgba, bool grey)
    {
        var channels = grey ? 1 : 4;
        var raw = new byte[(width * channels + 1) * height];
        for (var y = 0; y < height; y++)
        {
            var row = y * (width * channels + 1);
            for (var x = 0; x < width; x++)
            {
                var o = (y * width + x) * 4;
                if (grey)
                    raw[row + 1 + x] = (byte) Math.Clamp(0.299f * rgba[o] + 0.587f * rgba[o + 1] + 0.114f * rgba[o + 2], 0, 255);
                else
                    Array.Copy(rgba, o, raw, row + 1 + x * 4, 4);
            }
        }

        using var ms = new MemoryStream();
        ms.Write(Signature);
        var ihdr = new byte[13];
        WriteInt(ihdr, 0, width);
        WriteInt(ihdr, 4, height);
        ihdr[8] = 8;
        ihdr[9] = (byte) (grey ? 0 : 6);
        Chunk(ms, "IHDR", ihdr);

        using (var data = new MemoryStream())
        {
            using (var z = new ZLibStream(data, CompressionLevel.Optimal, true))
                z.Write(raw);
            Chunk(ms, "IDAT", data.ToArray());
        }

        Chunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void WriteInt(byte[] buf, int at, int value)
    {
        buf[at] = (byte) (value >> 24);
        buf[at + 1] = (byte) (value >> 16);
        buf[at + 2] = (byte) (value >> 8);
        buf[at + 3] = (byte) value;
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var head = new byte[8];
        WriteInt(head, 0, data.Length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        Array.Copy(typeBytes, 0, head, 4, 4);
        s.Write(head);
        s.Write(data);

        var crc = 0xFFFFFFFFu;
        foreach (var b in typeBytes)
            crc = Crc(crc, b);
        foreach (var b in data)
            crc = Crc(crc, b);
        crc ^= 0xFFFFFFFFu;
        var tail = new byte[4];
        WriteInt(tail, 0, (int) crc);
        s.Write(tail);
    }

    private static readonly uint[] CrcTable = BuildCrc();

    private static uint[] BuildCrc()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }

        return table;
    }

    private static uint Crc(uint crc, byte b) => CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
}
