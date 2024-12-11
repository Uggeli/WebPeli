using System.Buffers.Binary;
using WebPeli.GameEngine.Systems;
using WebPeli.Logging;
using System.Text;
using System.Text.Json;

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
    LogMessages = 0x43, // New type for log messages
}

public enum DebugRequestType : byte
{
    ToggleDebugMode = 0,
    TogglePathfinding = 1,
    RequestFullState = 2,
    RequestFullLog = 3
}

public record DebugLogRequest
{
    public string? Category { get; init; }
    public DateTime? Since { get; init; }
    public LogLevel? MinLevel { get; init; }
    public int? Limit { get; init; }
}

public record DebugState
{
    // Time data
    public required string Season { get; init; }
    public required string TimeOfDay { get; init; }
    public required int Day { get; init; }
    public required int Year { get; init; }
    
    // Entity data
    public required int TotalEntities { get; init; }
    public required int ActiveEntities { get; init; }
    public required int MovingEntities { get; init; }
    
    // System status
    public required bool DebugMode { get; init; }
    public required bool PathfindingDebug { get; init; }
    public required int ActiveViewports { get; init; }
    
    // System-specific counters
    public required int MetabolismEntities { get; init; }
    public required int AiEntities { get; init; }
    public required int VegetationCount { get; init; }
    
    // Performance data
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

    public static bool TryDecodeDebugLogRequest(ReadOnlySpan<byte> payload, out DebugLogRequest request)
    {
        try 
        {
            var json = Encoding.UTF8.GetString(payload);
            request = JsonSerializer.Deserialize<DebugLogRequest>(json) ?? 
                     new DebugLogRequest();
            return true;
        }
        catch 
        {
            request = new DebugLogRequest();
            return false;
        }
    }

    public static byte[] EncodeLogMessages(IEnumerable<LogMessage> messages)
    {
        var logData = messages.Select(m => new
        {
            timestamp = m.Timestamp.ToString("O"),
            level = m.Level.ToString(),
            category = m.CategoryName,
            message = m.Message,
            exception = m.Exception?.Message
        });

        var json = JsonSerializer.Serialize(logData);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        return EncodeMessage(MessageType.LogMessages, jsonBytes);
    }

    public static byte[] EncodeDebugResponse(string message)
    {
        var payload = Encoding.UTF8.GetBytes(message);
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

        var json = JsonSerializer.Serialize(debugData);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
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
        var payload = Encoding.UTF8.GetBytes(message);
        return EncodeMessage(MessageType.Error, payload);
    }
}