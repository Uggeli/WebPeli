using System.Numerics;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.World;

/// <summary>
/// The WorldApi class is used to interact with the world.
/// </summary>
public static class WorldApi
{
    # region Entity Management
    public static bool AddEntity(int id, Position[] positions, byte volume = 200)
    {
        return World.EntityManager.AddEntity(id, positions, volume);
    }
    /// <summary>
    /// Add an entity to the world at the specified position. Returns true if successful.
    /// </summary>
    public static bool AddEntity(int id, byte volume = 200)
    {
        return World.EntityManager.AddEntity(id, volume);
    }

    public static void RemoveEntity(int id)
    {
        World.EntityManager.RemoveEntity(id);
    }
    public static Position[] GetEntityPositions(int id) 
    {
        return World.EntityManager.GetEntityPositions(id);
    }

    public static void SetEntityAction(int id, EntityAction action)
    {
        World.EntityManager.SetEntityAction(id, action);
    }

    public static void SetEntityType(int id, EntityType type)
    {
        World.EntityManager.SetEntityType(id, type);
    }

    public static void SetEntityFacing(int id, Direction facing)
    {
        World.EntityManager.SetEntityFacing(id, facing);
    }

    public static EntityAction GetEntityAction(int id)
    {
        return World.EntityManager.GetEntityAction(id);
    }

    public static EntityType GetEntityType(int id)
    {
        return World.EntityManager.GetEntityType(id);
    }

    public static Direction GetEntityFacing(int id)
    {
        return World.EntityManager.GetEntityFacing(id);
    }
    # endregion

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

 
    # region World Queries
    public static (TileMaterial material, TileSurface surface, TileProperties props)[] GetTilesInArea(Position topLeft, int width, int height)
    {
        return World.GetTilesInArea(topLeft, width, height);
    }

    public static Dictionary<Position, (int entityId, EntityAction, EntityType, Direction)[]> GetEntitiesInArea(Position topLeft, int width, int height)
    {
        return World.EntityManager.GetEntitiesInArea(topLeft, width, height);
    }
    public static (TileMaterial material, TileSurface surface, TileProperties props) GetTileInfo(Position pos)
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
                    if (World.GetTileAt(checkPos).material == material)
                    {
                        found.Add(checkPos);
                    }
                }
            }
            radius++;
        }
        return found;
    }
    # endregion

    // Physical State Changes
    public static bool TryMoveEntity(int id, Position[] newPositions)
    {
        return World.EntityManager.MoveEntity(id, newPositions);
    }

    public static bool ModifyTile(Position pos, TileMaterial? material = null, TileSurface? surface = null)
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

    public static bool IsInWorldBounds(Position pos)
    {
        if (pos.X < 0 || pos.Y < 0) return false;
        if (pos.X >= Config.WORLD_TILES || pos.Y >= Config.WORLD_TILES) return false;
        return true;
    }
}