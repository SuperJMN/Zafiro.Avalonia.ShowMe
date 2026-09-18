using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Zafiro.Avalonia.ShowMe.Protocol;

public enum ShowMePixelFormat : byte
{
    Rgba8888 = 0,
    Bgra8888 = 1,
}

public record ShowMeFramePacket(
    int Width,
    int Height,
    int Stride,
    byte[] PixelData,
    ShowMePixelFormat Format = ShowMePixelFormat.Rgba8888
);

public static class ShowMeFraming
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task WriteControlMessageAsync(Stream stream, ShowMeMessage message, CancellationToken ct = default)
    {
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var header = new byte[5];
        header[0] = (byte)ShowMePacketType.ControlMessage;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1, 4), jsonBytes.Length);

        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(jsonBytes, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task WriteFrameAsync(Stream stream, int width, int height, int stride, byte[] pixels, ShowMePixelFormat format = ShowMePixelFormat.Rgba8888, CancellationToken ct = default)
    {
        var header = new byte[18];
        header[0] = (byte)ShowMePacketType.Frame;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1, 4), width);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(5, 4), height);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(9, 4), stride);
        header[13] = (byte)format;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(14, 4), pixels.Length);

        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(pixels, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<object?> ReadPacketAsync(Stream stream, CancellationToken ct = default)
    {
        var typeBuffer = new byte[1];
        if (!await ReadExactAsync(stream, typeBuffer, ct).ConfigureAwait(false))
        {
            return null;
        }

        var packetType = (ShowMePacketType)typeBuffer[0];

        if (packetType == ShowMePacketType.ControlMessage)
        {
            var lenBuffer = new byte[4];
            if (!await ReadExactAsync(stream, lenBuffer, ct).ConfigureAwait(false))
            {
                return null;
            }

            var length = BinaryPrimitives.ReadInt32LittleEndian(lenBuffer);
            var payload = new byte[length];
            if (!await ReadExactAsync(stream, payload, ct).ConfigureAwait(false))
            {
                return null;
            }

            return JsonSerializer.Deserialize<ShowMeMessage>(payload, JsonOptions);
        }
        else if (packetType == ShowMePacketType.Frame)
        {
            var metaBuffer = new byte[17];
            if (!await ReadExactAsync(stream, metaBuffer, ct).ConfigureAwait(false))
            {
                return null;
            }

            var width = BinaryPrimitives.ReadInt32LittleEndian(metaBuffer.AsSpan(0, 4));
            var height = BinaryPrimitives.ReadInt32LittleEndian(metaBuffer.AsSpan(4, 4));
            var stride = BinaryPrimitives.ReadInt32LittleEndian(metaBuffer.AsSpan(8, 4));
            var format = (ShowMePixelFormat)metaBuffer[12];
            var length = BinaryPrimitives.ReadInt32LittleEndian(metaBuffer.AsSpan(13, 4));

            var pixels = new byte[length];
            if (!await ReadExactAsync(stream, pixels, ct).ConfigureAwait(false))
            {
                return null;
            }

            return new ShowMeFramePacket(width, height, stride, pixels, format);
        }

        throw new InvalidOperationException($"Tipo de paquete ShowMe desconocido: {packetType}");
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }
            totalRead += read;
        }
        return true;
    }
}
