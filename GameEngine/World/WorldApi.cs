using System.Numerics;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.World;

/// <summary>
/// The WorldApi class is used to interact with the world.
/// </summary>
public static class WorldApi
{
    // Basic Entity Physical Access

    public static void AddEntity(int id, Position[] positions, byte volume = 200)
    {
        World.EntityManager.AddEntity(id, positions, volume);
    }

    public static Position[] GetEntityPositions(int id) 
    {
        return World.EntityManager.GetEntityPositions(id);
    }

    public static bool CanEntityFit(Position[] positions, byte volume)
    {
        if (positions == null || positions.Length == 0) return false;
        
        foreach (var pos in positions)
        {
            var chunk = World.GetChunk(pos);
            if (chunk == null || !chunk.CanAddEntity(pos, volume)) return false;
        }
        return true;
    }

    public static bool IsPositionWalkable(Position pos)
    {
        var (_, _, properties) = World.GetTileAt(pos);
        return properties.HasFlag(TileProperties.Walkable);
    }

    // Spatial Queries for AI/Pathing 
    public static Position[] GetPath(Position start, Position end)
    {
        return World.PathManager.GetPath(start, end);
    }

 
    // Viewport/Rendering
    public static byte[,] GetTilesInArea(Position center, int width, int height)
    {
        return World.GetTilesInArea(center, width, height);
    }


    // Resource/Interaction Queries
    public static (byte material, TileSurface surface, TileProperties props) GetTileInfo(Position pos)
    {
        return World.GetTileAt(pos);
    }

    public static IEnumerable<Position> FindNearestResource(Position pos, TileMaterial material)
    {
        // Simple spiral search starting from pos
        var found = new List<Position>();
        int radius = 1;
        while (radius < Config.CHUNK_SIZE * Config.WORLD_SIZE && !found.Any())
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Math.Abs(x) != radius && Math.Abs(y) != radius) continue;
                    
                    var checkPos = new Position { X = pos.X + x, Y = pos.Y + y };
                    if (World.GetTileAt(checkPos).material == (byte)material)
                    {
                        found.Add(checkPos);
                    }
                }
            }
            radius++;
        }
        return found;
    }

    // Physical State Changes
    public static bool TryMoveEntity(int id, Position[] newPositions)
    {
        return World.EntityManager.MoveEntity(id, newPositions);
    }

    public static bool ModifyTile(Position pos, byte? material = null, TileSurface? surface = null)
    {
        var current = World.GetTileAt(pos);
        World.SetTileAt(pos, 
            material ?? current.material,
            surface ?? current.surface,
            current.properties);
        return true;
    }
    
    public static void GenerateWorld()
    {
        World.GenerateWorld();
    }


}