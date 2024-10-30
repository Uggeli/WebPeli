namespace WebPeli.GameEngine;

public interface IEvent { } // Marker interface

public record ViewportData
{
    required public byte[,] TileGrid { get; init; }  // Texture IDs
    required public Dictionary<(int x, int y), EntityRenderData> Entities { get; init; }
}
public record EntityRenderData
{
    required public byte TextureId { get; init; }
    required public byte Facing { get; init; }
    required public string CurrentAction { get; init; }
}
public record TextureRequest : IEvent
{
    required public byte TextureId { get; init; }
    required public Guid CallbackId { get; init; }
}
public record TextureData
{
    required public byte TextureId { get; init; }
    required public string TextureName { get; init; }
}

public record ViewportRequest : IEvent
{
    public float CameraX { get; init; }
    public float CameraY { get; init; }
    public float ViewportWidth { get; init; }
    public float ViewportHeight { get; init; }
    public float? WorldWidth { get; init; }
    public float? WorldHeight { get; init; }
    public Guid CallbackId { get; init; }
}