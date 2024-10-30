namespace WebPeli.Transport;

public enum MessageType : byte
{
    ViewportRequest = 0x01,
    ViewportData = 0x02,
    Error = 0x03
}
