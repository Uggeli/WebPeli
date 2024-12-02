namespace WebPeli.GameEngine.Util;

public readonly struct LocalTilePos : IEquatable<LocalTilePos>
{
    public byte ChunkX { get; init; }
    public byte ChunkY { get; init; }
    public byte X { get; init; }
    public byte Y { get; init; }

    public bool Equals(LocalTilePos other) =>
        ChunkX == other.ChunkX &&
        ChunkY == other.ChunkY &&
        X == other.X &&
        Y == other.Y;

    public override bool Equals(object? obj) =>
        obj is LocalTilePos pos && Equals(pos);

    public override int GetHashCode() =>
        HashCode.Combine(ChunkX, ChunkY, X, Y);

    public static bool operator ==(LocalTilePos left, LocalTilePos right) =>
        left.Equals(right);

    public static bool operator !=(LocalTilePos left, LocalTilePos right) =>
        !left.Equals(right);

    public override string ToString() => $"Chunk ({ChunkX}, {ChunkY}), Local ({X}, {Y})";
}
