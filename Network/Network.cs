using System.Buffers.Binary;

namespace WebPeli.Network;

/// <summary>
/// Message types for client-server communication
/// </summary>
public enum MessageType : byte
{
    // Client -> Server messages (0x01-0x7F)
    ViewportRequest = 0x01,
    CellInfo = 0x02,        // Future use

    // Server -> Client messages (0x81-0xFE)
    ViewportData = 0x81,
    CellData = 0x82,        // Future use
    Error = 0xFF
}

/// <summary>
/// Helper class for message encoding/decoding
/// </summary>
public static class MessageProtocol
{
    private const int HEADER_SIZE = 3;  // 1 byte type + 2 bytes length

    public static byte[] EncodeMessage(MessageType type, ReadOnlySpan<byte> payload)
    {
        var message = new byte[HEADER_SIZE + payload.Length];

        // Write header
        message[0] = (byte)type;
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(1), (ushort)payload.Length);

        // Write payload
        payload.CopyTo(message.AsSpan(HEADER_SIZE));

        return message;
    }

    public static bool TryDecodeMessage(ReadOnlySpan<byte> data, out MessageType type, out ReadOnlySpan<byte> payload)
    {
        // Defaults for out params
        type = 0;
        payload = default;

        // Check minimum message size
        if (data.Length < HEADER_SIZE)
            return false;

        // Read header
        type = (MessageType)data[0];
        var length = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(1));

        // Validate full message available
        if (data.Length < HEADER_SIZE + length)
            return false;

        // Extract payload
        payload = data.Slice(HEADER_SIZE, length);
        return true;
    }

    // Helper for viewport requests
    public static byte[] EncodeViewportRequest(float cameraX, float cameraY, float width, float height)
    {
        var payload = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(0), cameraX);
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(4), cameraY);
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(8), width);
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(12), height);

        return EncodeMessage(MessageType.ViewportRequest, payload);
    }

    public static bool TryDecodeViewportRequest(ReadOnlySpan<byte> payload, out float cameraX, out float cameraY, out float width, out float height)
    {
        // Defaults
        cameraX = cameraY = width = height = 0;

        if (payload.Length < 16)
            return false;

        cameraX = BinaryPrimitives.ReadSingleLittleEndian(payload);
        cameraY = BinaryPrimitives.ReadSingleLittleEndian(payload.Slice(4));
        width = BinaryPrimitives.ReadSingleLittleEndian(payload.Slice(8));
        height = BinaryPrimitives.ReadSingleLittleEndian(payload.Slice(12));

        return true;
    }

    // Helper for viewport data responses
    public static byte[] EncodeViewportData(byte[,] tileGrid)
    {
        var width = (ushort)tileGrid.GetLength(0);
        var height = (ushort)tileGrid.GetLength(1);
        var payload = new byte[4 + (width * height)];

        // Write dimensions
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0), width);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), height);

        // Write tile data
        var i = 4;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                payload[i++] = tileGrid[x, y];

        return EncodeMessage(MessageType.ViewportData, payload);
    }

    public static bool TryDecodeViewportData(
        ReadOnlySpan<byte> payload,
        out ushort width,
        out ushort height,
        out byte[,]? tileGrid)
    {
        // Defaults
        width = height = 0;
        tileGrid = null;

        if (payload.Length < 4)
            return false;

        // Read dimensions
        width = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        height = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(2));

        // Validate data size
        if (payload.Length != 4 + (width * height))
            return false;

        // Read tile data
        tileGrid = new byte[width, height];
        var i = 4;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                tileGrid[x, y] = payload[i++];

        return true;
    }

    // Helper for error messages
    public static byte[] EncodeError(string message)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes(message);
        return EncodeMessage(MessageType.Error, payload);
    }

    public static bool TryDecodeError(ReadOnlySpan<byte> payload, out string? message)
    {
        message = null;
        try
        {
            message = System.Text.Encoding.UTF8.GetString(payload);
            return true;
        }
        catch
        {
            return false;
        }
    }
}