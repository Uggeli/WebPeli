using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World.WorldData;
namespace WebPeli.GameEngine.World;
/// <summary>
/// The EntityManager class is used to manage entities in the world.
/// </summary>
internal static partial class World
{
    public static class EntityManager
    {
        private const int MAX_ENTITIES = 1_000_000;
        private const int WORLD_TILES = Config.WORLD_SIZE * Config.CHUNK_SIZE_BYTE;

        // Core entity data arrays - direct indexed access
        private static readonly byte[] _volumes = new byte[MAX_ENTITIES];
        private static readonly EntityAction[] _actions = new EntityAction[MAX_ENTITIES];
        private static readonly EntityType[] _types = new EntityType[MAX_ENTITIES];
        private static readonly Direction[] _facing = new Direction[MAX_ENTITIES];

        // Position tracking with fixed sizes
        private static readonly int[][] _positionGrid; // [x][y][entity_index]
        private static readonly byte[] _tileCount; // How many entities in each tile
        private static readonly Position[,] _positions; // [entity_id, position_index] 
        private static readonly byte[] _positionCount; // How many positions each entity uses

        // Track active entities
        private static readonly HashSet<int> _activeEntities;

        static EntityManager()
        {
            // Initialize position grid
            _positionGrid = new int[WORLD_TILES][];
            for (int x = 0; x < WORLD_TILES; x++)
            {
                _positionGrid[x] = new int[WORLD_TILES * Config.MAX_ENTITIES_PER_TILE];
            }

            _tileCount = new byte[WORLD_TILES * WORLD_TILES];
            _positions = new Position[MAX_ENTITIES, Config.MAX_ENTITY_SIZE * Config.MAX_ENTITY_SIZE];
            _positionCount = new byte[MAX_ENTITIES];
            _activeEntities = new HashSet<int>(MAX_ENTITIES);
        }

        public static bool AddEntity(int id, Position[] positions, byte volume = 200)
        {
            if (_activeEntities.Contains(id)) return false;
            if (positions.Length > Config.MAX_ENTITY_SIZE * Config.MAX_ENTITY_SIZE) return false;

            // Validate all positions have space
            foreach (var pos in positions)
            {
                int idx = pos.Y * WORLD_TILES + pos.X;
                if (_tileCount[idx] >= Config.MAX_ENTITIES_PER_TILE) return false;
            }

            // Store core data 
            _volumes[id] = volume;
            _actions[id] = EntityAction.None;
            _types[id] = EntityType.None;
            _facing[id] = Direction.Down;

            // Store positions
            _positionCount[id] = (byte)positions.Length;
            for (int i = 0; i < positions.Length; i++)
            {
                _positions[id, i] = positions[i];

                int idx = positions[i].Y * WORLD_TILES + positions[i].X;
                _positionGrid[positions[i].X][_tileCount[idx]] = id;
                _tileCount[idx]++;
            }

            _activeEntities.Add(id);
            return true;
        }

        public static bool AddEntity(int id, byte volume = 200)
        {
            if (_activeEntities.Contains(id)) return false;

            // Find random spawn point
            List<Position> positions = FindRandomSpawnPoint(volume);
            if (positions.Count == 0) return false;

            // Store core data 
            _volumes[id] = volume;
            _actions[id] = EntityAction.None;
            _types[id] = EntityType.None;
            _facing[id] = Direction.Down;

            // Store positions
            _positionCount[id] = (byte)positions.Count;
            for (int i = 0; i < positions.Count; i++)
            {
                _positions[id, i] = positions[i];

                int idx = positions[i].Y * WORLD_TILES + positions[i].X;
                _positionGrid[positions[i].X][_tileCount[idx]] = id;
                _tileCount[idx]++;
            }

            _activeEntities.Add(id);
            return true;
        }

        public static bool RemoveEntity(int id)
        {
            if (!_activeEntities.Contains(id)) return false;

            _volumes[id] = 0;
            _actions[id] = EntityAction.None;
            _types[id] = EntityType.None;
            _facing[id] = Direction.Down;

            // Remove from all positions
            for (int i = 0; i < _positionCount[id]; i++)
            {
                var pos = _positions[id, i];
                int idx = pos.Y * WORLD_TILES + pos.X;

                // Find and remove from position grid
                int gridIdx = FindEntityInTile(pos.X, pos.Y, id);
                if (gridIdx >= 0)
                {
                    // Shift remaining entities down
                    for (int j = gridIdx; j < _tileCount[idx] - 1; j++)
                    {
                        _positionGrid[pos.X][j] = _positionGrid[pos.X][j + 1];
                    }
                    _tileCount[idx]--;
                }
            }
            _activeEntities.Remove(id);
            return true;
        }

        public static bool MoveEntity(int id, Position[] newPositions)
        {
            if (!_activeEntities.Contains(id)) return false;
            if (newPositions.Length > Config.MAX_ENTITY_SIZE * Config.MAX_ENTITY_SIZE) return false;

            // Check if new positions have space
            foreach (var pos in newPositions)
            {
                int idx = pos.Y * WORLD_TILES + pos.X;
                if (_tileCount[idx] >= Config.MAX_ENTITIES_PER_TILE) return false;
            }

            // Remove from old positions
            for (int i = 0; i < _positionCount[id]; i++)
            {
                var oldPos = _positions[id, i];
                int idx = oldPos.Y * WORLD_TILES + oldPos.X;

                // Find and remove from position grid
                int gridIdx = FindEntityInTile(oldPos.X, oldPos.Y, id);
                if (gridIdx >= 0)
                {
                    // Shift remaining entities down
                    for (int j = gridIdx; j < _tileCount[idx] - 1; j++)
                    {
                        _positionGrid[oldPos.X][j] = _positionGrid[oldPos.X][j + 1];
                    }
                    _tileCount[idx]--;
                }
            }

            // Add to new positions 
            _positionCount[id] = (byte)newPositions.Length;
            for (int i = 0; i < newPositions.Length; i++)
            {
                _positions[id, i] = newPositions[i];

                int idx = newPositions[i].Y * WORLD_TILES + newPositions[i].X;
                _positionGrid[newPositions[i].X][_tileCount[idx]] = id;
                _tileCount[idx]++;
            }

            return true;
        }

        private static int FindEntityInTile(int x, int y, int entityId)
        {
            int idx = y * WORLD_TILES + x;
            for (int i = 0; i < _tileCount[idx]; i++)
            {
                if (_positionGrid[x][i] == entityId) return i;
            }
            return -1;
        }

        // Fast accessors
        public static void SetEntityAction(int id, EntityAction action) => _actions[id] = action;
        public static void SetEntityType(int id, EntityType type) => _types[id] = type;
        public static void SetEntityFacing(int id, Direction facing) => _facing[id] = facing;
        public static EntityAction GetEntityAction(int id) => _actions[id];
        public static EntityType GetEntityType(int id) => _types[id];
        public static Direction GetEntityFacing(int id) => _facing[id];

        public static Position[] GetEntityPositions(int id)
        {
            var positions = new Position[_positionCount[id]];
            for (int i = 0; i < _positionCount[id]; i++)
            {
                positions[i] = _positions[id, i];
            }
            return positions;
        }

        public static Dictionary<Position, (int entityId, EntityAction, EntityType, Direction)[]> GetEntitiesInArea(Position topLeft, int width, int height)
        {
            var result = new Dictionary<Position, (int, EntityAction, EntityType, Direction)[]>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var pos = new Position { X = topLeft.X + x, Y = topLeft.Y + y };
                    if (!IsInWorldBounds(pos.X, pos.Y)) continue;
                    int idx = pos.Y * WORLD_TILES + pos.X;

                    if (_tileCount[idx] > 0)
                    {
                        var tileEntities = new (int, EntityAction, EntityType, Direction)[_tileCount[idx]];
                        for (int i = 0; i < _tileCount[idx]; i++)
                        {
                            int entityId = _positionGrid[pos.X][i];
                            tileEntities[i] = (entityId, _actions[entityId], _types[entityId], _facing[entityId]);
                        }
                        result[pos] = tileEntities;
                    }
                }
            }

            return result;
        }

        private static List<Position> FindRandomSpawnPoint(byte volume, byte entitySize = 1)
        {
            byte attempts = 0;

            while (attempts < 10)
            {
                // Get random position in world
                int x = Tools.Random.Next(WORLD_TILES);
                int y = Tools.Random.Next(WORLD_TILES);
                Position pos = new Position { X = x, Y = y };
                List<Position> positions = [];

                bool validSpot = true;
                // Check entity size area
                for (int dx = 0; dx < entitySize && validSpot; dx++)
                {
                    for (int dy = 0; dy < entitySize && validSpot; dy++)
                    {
                        Position checkPos = pos + (dx, dy);

                        // Validate position
                        if (!IsInWorldBounds(checkPos.X, checkPos.Y) ||
                            !GetTileAt(checkPos).properties.HasFlag(TileProperties.Walkable))
                        {
                            validSpot = false;
                            break;
                        }

                        // Check if there's room in tile
                        int idx = checkPos.Y * WORLD_TILES + checkPos.X;
                        if (_tileCount[idx] >= Config.MAX_ENTITIES_PER_TILE)
                        {
                            validSpot = false;
                            break;
                        }

                        positions.Add(checkPos);
                    }
                }

                if (validSpot && positions.Count == entitySize * entitySize)
                {
                    return positions;
                }

                attempts++;
            }
            return [];
        }


    }
}