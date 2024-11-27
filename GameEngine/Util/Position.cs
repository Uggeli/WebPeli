using System.Numerics;

namespace WebPeli.GameEngine.Util;

public readonly struct Position
{
    // Constructors
    public Position(int x, int y)
    {
        X = x;
        Y = y;
    }
    public Position((byte X, byte Y) chunkPosition, (byte X, byte Y) tilePosition)
    {
        X = chunkPosition.X * Config.CHUNK_SIZE_BYTE + tilePosition.X;
        Y = chunkPosition.Y * Config.CHUNK_SIZE_BYTE + tilePosition.Y;
    }

    public int X { get; init; }  // World coordinates
    public int Y { get; init; }  // World coordinates
    public readonly (byte X, byte Y) ChunkPosition => (X: (byte)(X / Config.CHUNK_SIZE_BYTE), Y: (byte)(Y / Config.CHUNK_SIZE_BYTE));
    public readonly (byte X, byte Y) TilePosition => (X: (byte)(X % Config.CHUNK_SIZE_BYTE), Y: (byte)(Y % Config.CHUNK_SIZE_BYTE));
    public static Position operator +(Position a, Position b)
    {
        return new Position { X = a.X + b.X, Y = a.Y + b.Y };
    }
    public static Position operator +(Position a, (int X, int Y) b)
    {
        return new Position { X = a.X + b.X, Y = a.Y + b.Y };
    }
    public static Position operator -(Position a, Position b)
    {
        return new Position { X = a.X - b.X, Y = a.Y - b.Y };
    }
    public static Position operator -(Position a, (int X, int Y) b)
    {
        return new Position { X = a.X - b.X, Y = a.Y - b.Y };
    }
    public static bool operator ==(Position a, Position b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Position a, Position b) => a.X != b.X || a.Y != b.Y;
    public override bool Equals(object? obj)
    {
        return obj != null && obj is Position pos && X == pos.X && Y == pos.Y;
    }
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public Direction LookAt(Position target)
    {
        int dx = target.X - X;
        int dy = target.Y - Y;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            return dx > 0 ? Direction.Right : Direction.Left;
        }
        return dy > 0 ? Direction.Down : Direction.Up;
    }
    public override string ToString() => $"({X}, {Y}) in chunk ({ChunkPosition.X}, {ChunkPosition.Y})";
}

public static class PositionExtensions
{
    public static Vector2 ToVector2(this Position pos) => new(pos.X, pos.Y);
}
