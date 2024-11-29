using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.World;

internal static partial class World
{
    public static class PathManager
    {
        // Pathfinding
        private static (int X, int Y)[] GetChunkNeighbours(int x, int y)
        {
            List<(int X, int Y)> neighbours = [];
            foreach ((int dx, int dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                int nx = x + dx;
                int ny = y + dy;
                if (IsInWorldBounds(nx, ny))
                {
                    neighbours.Add((nx, ny));
                }
            }
            return [.. neighbours];
        }

        public static (int x, int y) GetNeighborPosition((int x, int y) pos, Direction direction)
        {
            (int, int) value = direction switch
            {
                Direction.North => (pos.x, pos.y - 1),
                Direction.East => (pos.x + 1, pos.y),
                Direction.South => (pos.x, pos.y + 1),
                Direction.West => (pos.x - 1, pos.y),
                _ => pos
            };
            return value;
        }

        public static Position LocalToWorld(LocalTilePos local) => new()
        {
            X = (local.ChunkX * Config.CHUNK_SIZE_BYTE) + local.X,
            Y = (local.ChunkY * Config.CHUNK_SIZE_BYTE) + local.Y
        };

        public static LocalTilePos WorldToLocal(Position pos) => new()
        {
            ChunkX = (byte)(pos.X / Config.CHUNK_SIZE_BYTE),
            ChunkY = (byte)(pos.Y / Config.CHUNK_SIZE_BYTE),
            X = (byte)(pos.X % Config.CHUNK_SIZE_BYTE),
            Y = (byte)(pos.Y % Config.CHUNK_SIZE_BYTE)
        };

        public static Position[] GetPath(Position worldStart, Position worldEnd)
        {
            if (Config.DebugPathfinding)
            {
                Console.WriteLine("=== PATHFINDING START ===");
                Console.WriteLine($"From {worldStart} to {worldEnd}");
            }

            try
            {
                LocalTilePos localStart = WorldToLocal(worldStart);
                LocalTilePos localEnd = WorldToLocal(worldEnd);

                if (Config.DebugPathfinding)
                {
                    Console.WriteLine("\n=== CHUNK LEVEL PATHFINDING ===");
                    Console.WriteLine($"Local start: {localStart}, Local end: {localEnd}");
                }

                (LocalTilePos[] chunkPath, LocalTilePos chunkEnd) = FindPathChunkLevel(localStart, localEnd);
                if (chunkPath.Length == 0)
                {
                    if (Config.DebugPathfinding)
                    {
                        Console.WriteLine("ERROR: No path found at chunk level");
                        Console.WriteLine("=== PATHFINDING END ===\n");
                    }
                    return [];
                }

                if (Config.DebugPathfinding)
                {
                    Console.WriteLine($"Found chunk path: {string.Join(", ", chunkPath.Select(c => c.ToString()))}");
                    Console.WriteLine($"Chunk endpoint: {chunkEnd}");
                    Console.WriteLine("\n=== ZONE LEVEL PATHFINDING ===");
                }

                LocalTilePos[] chunks = chunkPath.Take(2).ToArray();
                (HashSet<Position> SearchSpace, Position zoneEnd) = FindZonePath(localStart, localEnd, chunkEnd, chunks);

                if (SearchSpace.Count == 0)
                {
                    if (Config.DebugPathfinding)
                    {
                        Console.WriteLine("ERROR: No valid zones found");
                        Console.WriteLine("=== PATHFINDING END ===\n");
                    }
                    return [];
                }

                if (Config.DebugPathfinding)
                {
                    Console.WriteLine($"Search space size: {SearchSpace.Count}");
                    Console.WriteLine($"Zone endpoint: {zoneEnd}");
                    Console.WriteLine("\n=== TILE LEVEL PATHFINDING ===");
                }

                Position[] tilePath = FindTilePath(worldStart, zoneEnd, SearchSpace);

                if (Config.DebugPathfinding)
                {
                    Console.WriteLine($"Final path length: {tilePath.Length}");
                    Console.WriteLine("=== PATHFINDING END ===\n");
                }

                return tilePath;
            }
            catch (Exception ex)
            {
                if (Config.DebugPathfinding)
                {
                    Console.WriteLine($"ERROR: Pathfinding failed with exception: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    Console.WriteLine("=== PATHFINDING END ===\n");
                }
                throw;
            }
        }

        private static (LocalTilePos[], LocalTilePos) FindPathChunkLevel(LocalTilePos start, LocalTilePos end)
        {
            List<(byte X, byte Y)> path = [];
            (byte ChunkX, byte ChunkY) current = (start.ChunkX, start.ChunkY);
            (byte ChunkX, byte ChunkY) target = (end.ChunkX, end.ChunkY);

            // If in same or adjacent chunks, that's our path
            if (Math.Abs(current.ChunkX - target.ChunkX) <= 1 &&
                Math.Abs(current.ChunkY - target.ChunkY) <= 1)
            {
                path.Add(current);
                if (current != target) path.Add(target);

                // End point is original if in same chunk, otherwise pick suitable point in second chunk
                LocalTilePos newEnd = current == target ? end : PickEndpointInChunk(target, end);
                return (path.Select(p => new LocalTilePos { ChunkX = p.X, ChunkY = p.Y }).ToArray(), newEnd);
            }

            // Otherwise do proper A* for chunks...
            // Returns first two chunks in path and endpoint in second chunk
            path = [.. ChunkLevelAStar(current, target)];
            if (path.Count == 0) return ([], default);

            (byte X, byte Y)[] firstTwoChunks = path.Take(2).ToArray();
            (byte X, byte Y) endChunk = firstTwoChunks[1];

            LocalTilePos newEndpoint = PickEndpointInChunk(endChunk, end);

            return (firstTwoChunks.Select(p => new LocalTilePos { ChunkX = p.X, ChunkY = p.Y }).ToArray(),
                    newEndpoint);
        }

        public static (HashSet<Position> searchSpace, Position newEndpos) FindZonePath(LocalTilePos start,
                                                                                       LocalTilePos end,
                                                                                       LocalTilePos chunkEnd,
                                                                                       LocalTilePos[] chunks)
        {
            Chunk? startChunk = GetChunk((chunks[0].ChunkX, chunks[0].ChunkY));
            Chunk? endChunk;
            if (chunks.Length == 1)
                endChunk = GetChunk((chunks[0].ChunkX, chunks[0].ChunkY));
            else
                endChunk = GetChunk((chunks[1].ChunkX, chunks[1].ChunkY));
            if (startChunk == null || endChunk == null)
                return ([], default);

            // Find start zone
            IEnumerable<Zone> zones = startChunk.GetZones();
            if (!zones.Any()) return ([], default);

            Zone startZone = zones.First(z => z.TilePositions.Contains((start.X, start.Y)));

            // Look for valid path through zones using edges
            ChunkConnection connection = _chunkGraph[(startChunk.X, startChunk.Y)];
            Direction direction = GetConnectionDirection(startChunk, endChunk);

            // Get zones that can reach the boundary in the right direction
            List<Zone> endZones = startZone.Edges.Values
                .Where(e => HasMatchingEdge(e, direction))
                .SelectMany(e => GetConnectedZones(endChunk, GetOppositeEdge(direction)))
                .Distinct()
                .ToList();

            if (endZones.Count == 0) return ([], default);

            // Pick closest end zone and suitable endpoint in it
            Zone endZone = PickBestEndZone(endZones, chunkEnd);
            Position newEnd;
            if (!endZone.TilePositions.Contains((end.X, end.Y)))
            {
                newEnd = PickEndpointInZone(endZone, chunkEnd);
            }
            else
            {
                newEnd = LocalToWorld(end);
            }

            // Create search space from both zones
            HashSet<Position> searchSpace = [];
            foreach (Zone zone in new[] { startZone, endZone })
            {
                foreach ((byte x, byte y) in zone.TilePositions)
                {
                    searchSpace.Add(new Position
                    {
                        X = (zone.ChunkPosition.X * Config.CHUNK_SIZE) + x,
                        Y = (zone.ChunkPosition.Y * Config.CHUNK_SIZE) + y
                    });
                }
            }
            return (searchSpace, newEnd);
        }

        private static Position[] FindTilePath(Position start, Position end, HashSet<Position> searchSpace)
        {
            // Simple A* through the combined space
            PriorityQueue<Position, float> openSet = new();
            HashSet<Position> closedSet = [];
            Dictionary<Position, Position> cameFrom = [];
            Dictionary<Position, float> gScore = [];

            if (!searchSpace.Contains(start))
            {
                if (Config.DebugPathfinding)
                {
                    Console.WriteLine("Start was not in search space");
                    Console.WriteLine($"Start: {start}");
                }
                return [];
            }

            if (!searchSpace.Contains(end))
            {
                if (Config.DebugPathfinding)
                {
                    Console.WriteLine("End was not in search space");
                    Console.WriteLine($"End: {end}");
                }
                return [];
            }

            openSet.Enqueue(start, 0);
            gScore[start] = 0;

            while (openSet.Count > 0)
            {
                Position current = openSet.Dequeue();
                if (current == end)
                {
                    return ReconstructPath(cameFrom, current);
                }

                closedSet.Add(current);

                // Check neighbors (just cardinal directions for now)
                foreach ((int, int) delta in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    Position next = new()
                    {
                        X = current.X + delta.Item1,
                        Y = current.Y + delta.Item2
                    };

                    if (!searchSpace.Contains(next) || closedSet.Contains(next))
                        continue;

                    float tentativeG = gScore[current] + 1;

                    if (!gScore.ContainsKey(next) || tentativeG < gScore[next])
                    {
                        cameFrom[next] = current;
                        gScore[next] = tentativeG;
                        int h = Math.Abs(end.X - next.X) + Math.Abs(end.Y - next.Y);
                        openSet.Enqueue(next, tentativeG + h);
                    }
                }
            }

            return [];
        }

        // Helper for finding best endpoint in a chunk based on direction we're heading
        private static LocalTilePos PickEndpointInChunk((byte X, byte Y) chunk, LocalTilePos target)
        {
            // For now just pick middle of chunk - could be smarter based on target direction
            return new LocalTilePos
            {
                ChunkX = chunk.X,
                ChunkY = chunk.Y,
                X = Config.CHUNK_SIZE_BYTE / 2,
                Y = Config.CHUNK_SIZE_BYTE / 2
            };
        }

        private static Position PickEndpointInZone(Zone zone, LocalTilePos target)
        {
            // Just grab any walkable position from the zone
            var (X, Y) = zone.TilePositions.First();
            return new Position
            {
                X = (zone.ChunkPosition.X * Config.CHUNK_SIZE_BYTE) + X,
                Y = (zone.ChunkPosition.Y * Config.CHUNK_SIZE_BYTE) + Y
            };
        }
        private static Zone PickBestEndZone(List<Zone> possibleZones, LocalTilePos target)
        {
            // For now just pick first one - could be smarter based on distance to target
            return possibleZones[0];
        }

        private static Direction GetConnectionDirection(LocalTilePos from, LocalTilePos to)
        {
            if (to.ChunkX > from.ChunkX) return Direction.Right;
            if (to.ChunkX < from.ChunkX) return Direction.Left;
            if (to.ChunkY > from.ChunkY) return Direction.Down;
            return Direction.Up;
        }

        private static Direction GetConnectionDirection(Chunk from, Chunk to)
        {
            if (to.X > from.X) return Direction.Right;
            if (to.X < from.X) return Direction.Left;
            if (to.Y > from.Y) return Direction.Down;
            return Direction.Up;
        }



        private static bool HasMatchingEdge(ZoneEdge edge, Direction direction) =>
            direction switch
            {
                Direction.Right => edge.HasFlag(ZoneEdge.ChunkEast),
                Direction.Left => edge.HasFlag(ZoneEdge.ChunkWest),
                Direction.Down => edge.HasFlag(ZoneEdge.ChunkSouth),
                Direction.Up => edge.HasFlag(ZoneEdge.ChunkNorth),
                _ => false
            };

        private static IEnumerable<Zone> GetConnectedZones(Chunk chunk, ZoneEdge edge)
        {
            return chunk.GetZones().Where(z =>
                z.Edges.Values.Any(e => e.HasFlag(edge)));
        }

        private static ZoneEdge GetOppositeEdge(Direction direction) =>
            direction switch
            {
                Direction.Right => ZoneEdge.ChunkWest,
                Direction.Left => ZoneEdge.ChunkEast,
                Direction.Down => ZoneEdge.ChunkNorth,
                Direction.Up => ZoneEdge.ChunkSouth,
                _ => ZoneEdge.None
            };

        private static (byte X, byte Y)[] ChunkLevelAStar((byte, byte) start, (byte, byte) end)
        {
            Chunk? startChunk = GetChunk(start);
            Chunk? endChunk = GetChunk(end);
            if (startChunk == null || endChunk == null)
            {
                if (Config.DebugPathfinding)
                {
                    Console.WriteLine("Invalid start or end chunk in A*");
                    Console.WriteLine($"Start: {start}");
                    Console.WriteLine($"End: {end}");
                }
                return [];
            }

            Queue<(int X, int Y)> openSet = new();
            HashSet<(int X, int Y)> closedSet = [];
            Dictionary<(int X, int Y), (int X, int Y)> cameFrom = [];

            openSet.Enqueue((startChunk.X, startChunk.Y));
            (byte X, byte Y) endPos = (endChunk.X, endChunk.Y);

            while (openSet.Count > 0)
            {
                (int X, int Y) current = openSet.Dequeue();
                if (current == endPos) return ReconstructChunkPath(cameFrom, current);

                foreach ((int X, int Y) neighbour in GetChunkNeighbours(current.X, current.Y))
                {
                    if (closedSet.Contains(neighbour)) continue;

                    (int dx, int dy) delta = (dx: current.X - neighbour.X, dy: current.Y - neighbour.Y);
                    ChunkConnection connection = _chunkGraph[neighbour];

                    bool canMove = (delta, connection) switch
                    {
                        ((1, 0) or (-1, 0), var c) when c.HasFlag(ChunkConnection.EastWest) => true,
                        ((0, 1) or (0, -1), var c) when c.HasFlag(ChunkConnection.NorthSouth) => true,
                        _ => false
                    };

                    if (!canMove) continue;

                    openSet.Enqueue(neighbour);
                    closedSet.Add(neighbour);
                    cameFrom[neighbour] = current;
                }
            }
            if (Config.DebugPathfinding)
            {
                Console.WriteLine("A* failed to find path between chunks");
                Console.WriteLine($"Start: {start}");
                Console.WriteLine($"End: {end}");
            }
            return [];
        }

        private static (byte X, byte Y)[] ReconstructChunkPath(Dictionary<(int X, int Y), (int X, int Y)> cameFrom, (int X, int Y) current)
        {
            List<(byte X, byte Y)> path = [];
            while (cameFrom.TryGetValue(current, out (int X, int Y) previous))
            {
                path.Add(((byte X, byte Y))(current.X, current.Y));
                current = previous;
            }
            path.Reverse();
            if (path.Count == 1) return [.. path];
            return path.Take(2).ToArray();
        }

        private static Position[] ReconstructPath(Dictionary<Position, Position> cameFrom, Position current)
        {
            List<Position> path = [current];
            while (cameFrom.TryGetValue(current, out Position previous))
            {
                path.Add(previous);
                current = previous;
            }
            path.Reverse();
            return [.. path];
        }
    }
}


