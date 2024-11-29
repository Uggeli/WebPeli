using System.Collections.Concurrent;
using WebPeli.GameEngine.Managers;
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
        // Track which chunks each entity occupies
        private static ConcurrentDictionary<int, HashSet<(byte ChunkX, byte ChunkY)>> _entityChunks = [];
        // Just track volume per entity (we get positions from chunks)
        private static ConcurrentDictionary<int, byte> _entityVolumes = [];
        private static ConcurrentDictionary<int, EntityAction> _entityActions = [];
        private static ConcurrentDictionary<int, EntityType> _entityTypes = [];
        private static ConcurrentDictionary<int, Direction> _entityFacing = [];

        public static void AddEntity(int id, Position[] positions, byte volume = 200)
        {
            if (_entityChunks.ContainsKey(id)) return;

            // First validate all positions
            var chunks = new HashSet<(byte ChunkX, byte ChunkY)>();
            foreach (var pos in positions)
            {
                // Check if position is valid
                var chunk = GetChunk(pos.ChunkPosition);
                if (chunk == null || !chunk.CanAddEntity(pos, volume)) return;
                chunks.Add(pos.ChunkPosition);
            }

            // Then add to all chunks
            foreach (var pos in positions)
            {
                var chunk = GetChunk(pos.ChunkPosition);
                chunk?.AddEntity(id, pos, volume);
            }

            // Track which chunks this entity occupies
            _entityChunks[id] = chunks;
            _entityVolumes[id] = volume;
        }

        public static void SetEntityAction(int id, EntityAction action)
        {
            _entityActions[id] = action;
        }

        public static void SetEntityType(int id, EntityType type)
        {
            _entityTypes[id] = type;
        }

        public static void SetEntityFacing(int id, Direction facing)
        {
            _entityFacing[id] = facing;
        }

        public static EntityAction GetEntityAction(int id)
        {
            return _entityActions.TryGetValue(id, out EntityAction action) ? action : EntityAction.None;
        }

        public static EntityType GetEntityType(int id)
        {
            return _entityTypes.TryGetValue(id, out EntityType type) ? type : EntityType.None;
        }

        public static Direction GetEntityFacing(int id)
        {
            return _entityFacing.TryGetValue(id, out Direction facing) ? facing : Direction.Down;
        }

        public static bool MoveEntity(int id, Position[] newPositions)
        {
            if (!_entityVolumes.TryGetValue(id, out byte volume)) return false;

            // Get old positions from chunks
            var oldChunks = _entityChunks[id];

            // Validate new positions first
            foreach (var pos in newPositions)
            {
                if (!CanMoveTo(pos))
                {
                    EventManager.Emit(new EntityMovementFailed { EntityId = id });
                    return false;
                }
            }

            // Remove from old chunks
            foreach (var chunkPos in oldChunks)
            {
                var chunk = GetChunk(chunkPos);
                // Get positions in this chunk from chunk itself
                chunk?.RemoveEntity(id);
            }

            // Add to new chunks & update tracking
            var newChunks = new HashSet<(byte ChunkX, byte ChunkY)>();
            foreach (var pos in newPositions)
            {
                var chunk = GetChunk(pos.ChunkPosition);
                chunk?.AddEntity(id, pos, volume);
                newChunks.Add(pos.ChunkPosition);
            }
            _entityChunks[id] = newChunks;
            return true;
        }

        public static bool CanEntityFit(Position[] positions, byte volume)
        {
            if (positions == null || positions.Length == 0) return false;

            foreach (var pos in positions)
            {
                var chunk = GetChunk(pos);
                if (chunk == null || !chunk.CanAddEntity(pos, volume)) return false;
            }
            return true;
        }

        public static bool AddEntity(int id, byte volume = 200)
        {
            List<Position> positions = FindRandomSpawnPoint(volume);
            if (positions.Count == 0) return false;

            AddEntity(id, [..positions], volume);
            return true;
        }

        private static List<Position> FindRandomSpawnPoint(byte volume, byte entitySize = 1)
        {

            Chunk? chunk = GetChunk(((byte)Tools.Random.Next(Config.WORLD_SIZE), (byte)Tools.Random.Next(Config.WORLD_SIZE)));
            if (chunk == null) return [];

            byte attempts = 0;

            while (attempts < 10)
            {
                byte x = (byte)Tools.Random.Next(Config.CHUNK_SIZE_BYTE);
                byte y = (byte)Tools.Random.Next(Config.CHUNK_SIZE_BYTE);
                Position pos = new Position { X = chunk.X * Config.CHUNK_SIZE_BYTE + x, Y = chunk.Y * Config.CHUNK_SIZE_BYTE + y };
                List<Position> positions = [];
                for (int dx = 0; dx < entitySize; dx++)
                {
                    for (int dy = 0; dy < entitySize; dy++)
                    {
                        Position checkPos = pos + (dx, dy);
                        if (!chunk.CanAddEntity(checkPos, volume) || !IsInWorldBounds(checkPos) || !IsInChunkBounds(checkPos) || !GetTileAt(checkPos).properties.HasFlag(TileProperties.Walkable))
                        {
                            attempts++;
                            continue;
                        }
                        positions.Add(checkPos);
                    }
                }

                if (positions.Count == entitySize * entitySize)
                {
                    return positions;
                }
            }
            return [];
        }

        public static void RemoveEntity(int id)
        {
            if (!_entityChunks.TryGetValue(id, out HashSet<(byte ChunkX, byte ChunkY)>? chunks)) return;

            foreach (var chunkPos in chunks)
            {
                var chunk = GetChunk(chunkPos);
                chunk?.RemoveEntity(id);
            }

            _entityChunks.TryRemove(id, out _);
            _entityVolumes.TryRemove(id, out _);
        }

        private static bool CanMoveTo(Position pos)
        {
            (byte X, byte Y) = pos.TilePosition;
            return IsInChunkBounds(X, Y) && GetTileAt(pos).properties.HasFlag(TileProperties.Walkable);
        }

        public static Position[] GetEntityPositions(int id)
        {
            var positions = new List<Position>();
            var chunks = _entityChunks[id];

            foreach (var chunkPos in chunks)
            {
                var chunk = GetChunk(chunkPos);
                if (chunk != null)
                {
                    positions.AddRange(chunk.GetEntityPositions(id));
                }
            }
            return [.. positions];
        }

        public static Dictionary<Position, (int entityId, EntityAction, EntityType, Direction)[]> GetEntitiesInArea(Position topLeft, int width, int height)
        {
            var entities = new Dictionary<Position, (int entityId, EntityAction, EntityType, Direction)[]>();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Position pos = topLeft + (x, y);
                    var chunk = GetChunk(pos.ChunkPosition);
                    if (chunk == null) continue;
                    List<(int, EntityAction, EntityType, Direction)> entitiesInTile = [];
                    foreach (var entityId in chunk.GetEntitiesAt(pos.TilePosition.X, pos.TilePosition.Y))
                    {
                        entitiesInTile.Add((entityId, GetEntityAction(entityId), GetEntityType(entityId), GetEntityFacing(entityId)));
                    }
                    if (entitiesInTile.Count > 0)
                    {
                        entities[pos] = [.. entitiesInTile];
                    }
                }
            }
            return entities;
        }

    }
}