using System.Buffers.Binary;
using WebPeli.GameEngine.Systems;

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
    Error = 0xFF,
    // Debug messages (0x40-0x4F)
    DebugRequest = 0x40,
    DebugResponse = 0x41,
    DebugData = 0x42,
}

public enum DebugRequestType : byte
{
    ToggleDebugMode = 0,
    TogglePathfinding = 1,
    RequestFullState = 2
}

public record DebugState
{
    public required Season Season { get; init; }
    public required TimeOfDay TimeOfDay { get; init; }
    public required int Day { get; init; }
    public required int Year { get; init; }

    public required int TotalEntities { get; init; }
    public required int ActiveEntities { get; init; }
    public required int MovingEntities { get; init; }

    public required bool DebugMode { get; init; }
    public required bool PathfindingDebug { get; init; }
    public required int ActiveViewports { get; init; }

    public required Dictionary<string, int> SystemUpdateTimes { get; init; }
}


/// <summary>
/// Helper class for message encoding/decoding
/// </summary>
public static class MessageProtocol
{
    private const int HEADER_SIZE = 3;  // 1 byte type + 2 bytes length
    public static bool TryDecodeDebugRequest(ReadOnlySpan<byte> payload, out DebugRequestType requestType)
    {
        requestType = DebugRequestType.ToggleDebugMode; // Default
        if (payload.Length < 1) return false;

        requestType = (DebugRequestType)payload[0];
        return true;
    }

    public static byte[] EncodeDebugResponse(string message)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes(message);
        return EncodeMessage(MessageType.DebugResponse, payload);
    }

    public static byte[] EncodeDebugData(DebugState state)
    {
        var debugData = new
        {
            season = state.Season.ToString(),
            timeOfDay = state.TimeOfDay.ToString(),
            day = state.Day,
            year = state.Year,

            totalEntities = state.TotalEntities,
            activeEntities = state.ActiveEntities,
            movingEntities = state.MovingEntities,

            debugMode = state.DebugMode,
            pathfindingDebug = state.PathfindingDebug,
            activeViewports = state.ActiveViewports,

            performanceData = state.SystemUpdateTimes
        };

        var json = System.Text.Json.JsonSerializer.Serialize(debugData);
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        return EncodeMessage(MessageType.DebugData, jsonBytes);
    }

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
        var length = BinaryPrimitives.ReadUInt16LittleEndian(data[1..]);

        // Validate full message available
        if (data.Length < HEADER_SIZE + length)
            return false;

        // Extract payload
        payload = data.Slice(HEADER_SIZE, length);
        return true;
    }

    public static bool TryDecodeViewportRequest(ReadOnlySpan<byte> payload, out int cameraX, out int cameraY, out byte width, out byte height)
    {
        // Defaults
        cameraX = cameraY = 0;
        width = height = 0;

        if (payload.Length < 10) // 4 + 4 + 1 + 1
            return false;

        cameraX = BinaryPrimitives.ReadInt32LittleEndian(payload);
        cameraY = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        width = payload[8];
        height = payload[9];

        return true;
    }

    // Helper for error messages
    public static byte[] EncodeError(string message)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes(message);
        return EncodeMessage(MessageType.Error, payload);
    }
}