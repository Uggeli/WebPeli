using System.Numerics;
using WebPeli.GameEngine.World;

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
        X = chunkPosition.X * Config.CHUNK_SIZE + tilePosition.X;
        Y = chunkPosition.Y * Config.CHUNK_SIZE + tilePosition.Y;
    }

    public int X { get; init; }  // World coordinates
    public int Y { get; init; }  // World coordinates
    public readonly (byte X, byte Y) ChunkPosition => (X: (byte)(X / Config.CHUNK_SIZE), Y: (byte)(Y / Config.CHUNK_SIZE));
    public readonly (byte X, byte Y) TilePosition => (X: (byte)(X % Config.CHUNK_SIZE), Y: (byte)(Y % Config.CHUNK_SIZE));
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

    public static Direction LookAt(Position from, Position to) => from.LookAt(to);
    public static Direction LookAt(int fromX, int fromY, int toX, int toY) => new Position(fromX, fromY).LookAt(new Position(toX, toY));



    public override string ToString() => $"({X}, {Y}) in chunk ({ChunkPosition.X}, {ChunkPosition.Y})";
}

public static class PositionExtensions
{
    public static Vector2 ToVector2(this Position pos) => new(pos.X, pos.Y);

    public static Position[] GetNeighbours(this Position pos)
    {
        var neighbours = new List<Position>();
        foreach ((int dx, int dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
        {
            var newPos = new Position(pos.X + dx, pos.Y + dy);
            if (!WorldApi.IsInWorldBounds(newPos))
            {
                continue;
            }
            neighbours.Add(newPos);
        }
        return [.. neighbours];
    }
}
