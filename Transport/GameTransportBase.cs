using System.Buffers.Binary;
using System.Text;
using WebPeli.GameEngine;
using WebPeli.GameEngine.Managers;

namespace WebPeli.Transport;

// Base class for message handling
public abstract class GameTransportBase
{
    protected readonly ViewportManager _viewportManager;
    protected readonly ILogger _logger;
    protected const int MaxMessageSize = 1024 * 64; // 64KB max message size

    protected GameTransportBase(ViewportManager viewportManager, ILogger logger)
    {
        _viewportManager = viewportManager;
        _logger = logger;
    }

    protected Memory<byte> EncodeMessage(MessageType type, ReadOnlyMemory<byte> payload)
    {
        var message = new byte[3 + payload.Length];
        message[0] = (byte)type;
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(1), (ushort)payload.Length);
        payload.CopyTo(message.AsMemory(3));
        return message;
    }

    protected async Task HandleViewportRequestAsync(ReadOnlyMemory<byte> payload, Func<MessageType, ReadOnlyMemory<byte>, Task> sendMessage)
    {
        var span = payload.Span;
        
        if (payload.Length < 16) // Minimum size for viewport request
        {
            await SendErrorAsync(sendMessage, 0x01, "Invalid viewport request size");
            return;
        }

        try 
        {
            var screenX = BinaryPrimitives.ReadSingleLittleEndian(span);
            var screenY = BinaryPrimitives.ReadSingleLittleEndian(span[4..]);
            var width = BinaryPrimitives.ReadSingleLittleEndian(span[8..]);
            var height = BinaryPrimitives.ReadSingleLittleEndian(span[12..]);

            float? worldWidth = null;
            float? worldHeight = null;
            if (payload.Length >= 24)
            {
                worldWidth = BinaryPrimitives.ReadSingleLittleEndian(span[16..]);
                worldHeight = BinaryPrimitives.ReadSingleLittleEndian(span[20..]);
            }

            var tcs = new TaskCompletionSource<ViewportDataBinary>();
            var callbackId = EventManager.RegisterCallback((ViewportDataBinary data) => {
                tcs.SetResult(data);
            });

            EventManager.Emit(new ViewportRequest {
                CameraX = screenX,
                CameraY = screenY,
                ViewportWidth = width,
                ViewportHeight = height,
                WorldWidth = worldWidth,
                WorldHeight = worldHeight,
                CallbackId = callbackId
            });

            var result = await tcs.Task;
            await sendMessage(MessageType.ViewportData, result.EncodedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling viewport request");
            await SendErrorAsync(sendMessage, 0x02, "Server error processing viewport request");
        }
    }

    protected async Task SendErrorAsync(Func<MessageType, ReadOnlyMemory<byte>, Task> sendMessage, byte errorCode, string message)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var payload = new byte[3 + messageBytes.Length];
        payload[0] = errorCode;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), (ushort)messageBytes.Length);
        messageBytes.CopyTo(payload.AsSpan(3));
        
        await sendMessage(MessageType.Error, payload);
    }
}