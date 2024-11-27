namespace WebPeli.GameEngine.World.WorldData;

// Zone is a collection of tiles that are connected to each other and have some kind of edge
// Zone can be used to determine if a tile is part of a room, a cave, a building, etc.
public struct Zone(int id, byte chunkX, byte chunkY, IEnumerable<(byte X, byte Y)> positions, Dictionary<(byte X, byte Y), ZoneEdge> edges)
{
    public readonly int Id { get; init; } = id;
    public readonly Dictionary<(byte X, byte Y), ZoneEdge> Edges { get; init; } = edges;
    public readonly (byte X, byte Y) ChunkPosition { get; init; } = (chunkX, chunkY);
    public HashSet<(byte X, byte Y)> TilePositions { get; set; } = positions.ToHashSet();
    public override string ToString() => $"Zone {Id} at {ChunkPosition}";
}




// long paths tend to be incorrect in long run and long paths take long to run 