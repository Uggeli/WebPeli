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
            var chunk = GetChunk((x, y));
            if (chunk == null)
            {
                return [];
            }

            var neighbours = new List<(int X, int Y)>();

            foreach ((int dx, int dy) in new[]{(-1, 0),(1, 0),(0, -1),(0, 1)})
            {
                int nx = x + dx;
                int ny = y + dy;

                if (!IsInWorldBounds(nx, ny))
                {
                    continue;
                }

                var nchunk = GetChunk((nx, ny));
                if (nchunk == null)
                {
                    continue;
                }
                neighbours.Add((nx, ny));
            }
            return [.. neighbours];
        }

        // private static Position[] GetNeighbours(Position pos)
        // {
        //     var neighbours = new List<Position>();
        //     foreach ((int dx, int dy) in new[]{(-1, 0),(1, 0),(0, -1),(0, 1)})
        //     {
        //         var nx = pos.X + dx;
        //         var ny = pos.Y + dy;
        //         if (!IsInWorldBounds(nx, ny))
        //         {
        //             continue;
        //         }
        //         neighbours.Add(new Position(nx, ny));
        //     }
        //     return [.. neighbours];
        // }

        public static Position LocalToWorld(LocalTilePos local) => new()
        {
            X = (local.ChunkX * Config.CHUNK_SIZE_BYTE) + local.X,
            Y = (local.ChunkY * Config.CHUNK_SIZE_BYTE) + local.Y
        };

        public static LocalTilePos WorldToLocal(Position pos)
        {
            return new()
            {
                ChunkX = (byte)(pos.X / Config.CHUNK_SIZE_BYTE),
                ChunkY = (byte)(pos.Y / Config.CHUNK_SIZE_BYTE),
                X = (byte)(pos.X % Config.CHUNK_SIZE_BYTE),
                Y = (byte)(pos.Y % Config.CHUNK_SIZE_BYTE)
            };
        } 

        public static Position[] GetPath(Position worldStart, Position worldEnd)
        {
            try
            {
                Chunk? StartChunk = GetChunk(worldStart);
                Chunk? EndChunk = GetChunk(worldEnd);

                if (StartChunk == null || EndChunk == null) return [];
                if (StartChunk == EndChunk) return BresenhamsLine((worldStart.X, worldStart.Y), (worldEnd.X, worldEnd.Y)).Select(p => new Position(p.X, p.Y)).ToArray();
                // if pathing is even possible
                if(!FindPathChunkLevel(StartChunk, EndChunk, worldStart, worldEnd)) return [];
                Position[] tilePath = FindTilePath(worldStart, worldEnd);
                if (tilePath.Length == 0) return [];
                else return tilePath;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return [];
            }
        }



        /// <summary>
        /// Find path between two points on chunk level., Returns first two chunks in path
        /// </summary>
        /// <param name="start">Starting chunk</param>
        /// <param name="end">End chunk</param>
        /// <param name="startPos">Start pos in world coordinates</param>
        /// <param name="endPos">End pos in world coordinates</param>
        /// <returns></returns>
        private static bool FindPathChunkLevel(Chunk start, Chunk end, Position startPos, Position endPos)
        {
            // If in same or adjacent chunks, that's our path
            if (Math.Abs(start.X - end.X) <= 1 && Math.Abs(start.Y - end.Y) <= 1)
            {
                return true;
            }

            var path = new List<(int x, int y)> { (startPos.ChunkPosition.X, startPos.ChunkPosition.Y) };
            var blacklist = new HashSet<(int x, int y)>();
            var visited = new HashSet<(int x, int y)> { (startPos.ChunkPosition.X, startPos.ChunkPosition.Y) };
            PriorityQueue<(int x, int y), float> positionsToTry = new();
            positionsToTry.Enqueue((startPos.ChunkPosition.X, startPos.ChunkPosition.Y), 0);

            while (positionsToTry.Count > 0)
            {
                var currentPos = positionsToTry.Dequeue();
                var linePath = BresenhamsLine(currentPos, (end.X, end.Y));

                for (int i = 0; i < linePath.Length; i++)
                {
                    currentPos = linePath[i];
                    visited.Add(currentPos);

                    // if path length is 1, we are there already
                    if (linePath.Length == 1)
                    {
                        return true;
                    }
                    // if path lenght is 2 check from connection between those two chunks
                    else if (linePath.Length == 2)
                    {
                        var requiredConnection = GetRequiredConnection(Position.LookAt(startPos.X, startPos.Y, endPos.X, endPos.Y), Position.LookAt(endPos.X, endPos.Y, startPos.X, startPos.Y));
                        if (start.Connections.HasFlag(requiredConnection))
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }

                    if (linePath.Length > 2 && i > 0 && i < linePath.Length - 1)
                    {
                        (int previousPosX, int previousPosY) = linePath[i - 1];
                        (int nextPosX, int nextPosY) = linePath[i + 1];


                        // Check if we can move from previous to current and current to next
                        var prevDirection = Position.LookAt(previousPosX, previousPosY, currentPos.x, currentPos.y);
                        var nextDirection = Position.LookAt(currentPos.x, currentPos.y, nextPosX, nextPosY);
                        var requiredConnection = GetRequiredConnection(prevDirection, nextDirection);
                        var currentChunk = GetChunk(currentPos);
                        if (currentChunk == null)
                        {
                            blacklist.Add(currentPos);
                            break;
                        }
                        if (currentChunk.Connections.HasFlag(requiredConnection))
                        {
                            path.Add(currentPos);
                        }

                        // we cant move from previous to current, so we need to find a new path
                        else
                        {
                            blacklist.Add(currentPos);
                            var neighbours = GetChunkNeighbours(previousPosX, previousPosY);
                            // Add neighbours to try
                            foreach (var neighbour in neighbours)
                            {
                                if (visited.Contains(neighbour) || blacklist.Contains(neighbour))
                                {
                                    continue;
                                }
                                positionsToTry.Enqueue(neighbour, ManhattanDistance(neighbour, (end.X, end.Y)));
                            }
                            break;
                        }
                    }

                    // If we are at the end, return path
                    if (currentPos == (end.X, end.Y))
                    {
                        return true;
                    }

                }
                
            }
            return false;
        }

        private static ChunkConnection GetRequiredConnection(Direction from, Direction to)
        {
            return (from, to) switch
            {
                // From north
                (Direction.North, Direction.South) => ChunkConnection.NorthSouth,
                (Direction.North, Direction.East) => ChunkConnection.NorthEast,
                (Direction.North, Direction.West) => ChunkConnection.NorthWest,
                // From south
                (Direction.South, Direction.North) => ChunkConnection.NorthSouth,
                (Direction.South, Direction.East) => ChunkConnection.SouthEast,
                (Direction.South, Direction.West) => ChunkConnection.SouthWest,
                // From east
                (Direction.East, Direction.North) => ChunkConnection.NorthEast,
                (Direction.East, Direction.South) => ChunkConnection.SouthEast,
                (Direction.East, Direction.West) => ChunkConnection.EastWest,
                // From west
                (Direction.West, Direction.North) => ChunkConnection.NorthWest,
                (Direction.West, Direction.South) => ChunkConnection.SouthWest,
                (Direction.West, Direction.East) => ChunkConnection.EastWest,
                _ => ChunkConnection.None
            };
            
        }

        private static (int X, int Y)[] BresenhamsLine((int x, int y)start, (int x, int y) end)
        {
            var points = new List<(int x, int y)>();
            int x0 = start.x;
            int y0 = start.y;
            int x1 = end.x;
            int y1 = end.y;

            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                points.Add((x0, y0));
                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
            return [.. points];            
        }



        private static Position[] FindTilePath(Position start, Position end)
        {
            var startChunk = GetChunk(start);
            if (startChunk == null)
            {
                return [];
            }
            Zone? startingZone = startChunk.GetZoneAt(start.TilePosition.X, start.TilePosition.Y);
            if (startingZone == null)
            {
                return [];
            }

            var openSet = new PriorityQueue<Position, float>();
            var closedSet = new HashSet<Position>();
            var cameFrom = new Dictionary<Position, Position>();
            var gScore = new Dictionary<Position, float>();
            var fScore = new Dictionary<Position, float>();

            openSet.Enqueue(start, 0);
            gScore[start] = 0;
            fScore[start] = ManhattanDistance(start, end);

            while(openSet.Count > 0)
            {
                // new position from queue
                var current = openSet.Dequeue();

                // are we there yet?
                if (current == end || !startingZone.Value.TilePositions.Contains(current.TilePosition))
                {
                    var path = new List<Position>();
                    while (cameFrom.TryGetValue(current, out var c))
                    {
                        path.Add(current);
                        current = c;
                    }
                    path.Add(start);
                    path.Reverse();
                    return [.. path];
                }

                closedSet.Add(current);
                foreach (var neighbour in current.GetNeighbours())
                {
                    if (!TileManager.IsWalkable(startChunk.GetTile(neighbour.TilePosition.X, neighbour.TilePosition.Y).properties))
                    {
                        continue;
                    }

                    if (closedSet.Contains(neighbour))
                        continue;

                    var tentativeGScore = gScore[current] + 1;
                    if (!gScore.TryGetValue(neighbour, out _) || tentativeGScore < gScore[neighbour])
                    {
                        cameFrom[neighbour] = current;
                        gScore[neighbour] = tentativeGScore;
                        var f = tentativeGScore + ManhattanDistance(neighbour, end);
                        fScore[neighbour] = f;
                        openSet.Enqueue(neighbour, f);
                    }
                }
            }
            return [];
        }


        private static Dictionary<(int X, int Y), (int X, int Y)> ChunkLevelAStar((byte, byte) start, (byte, byte) end)
        {
            
            var openSet = new PriorityQueue<(int X, int Y), float>();
            var closedSet = new HashSet<(int X, int Y)>();
            var cameFrom = new Dictionary<(int X, int Y), (int X, int Y)>();
            var gScore = new Dictionary<(int X, int Y), float>();
            var fScore = new Dictionary<(int X, int Y), float>();

            openSet.Enqueue(start, 0);
            gScore[start] = 0;
            fScore[start] = ManhattanDistance(start, end);

            while (openSet.Count > 0)
            {
                var current = openSet.Dequeue();
                if (current == end)
                    return cameFrom;

                closedSet.Add(current);
                foreach (var neighbor in GetChunkNeighbours(current.X, current.Y))
                {
                    // can we move to this neighbour?
                    var currentChunk = GetChunk(current);
                    var neighbourChunk = GetChunk(neighbor);
                    var cameFromChunk = GetChunk(cameFrom.TryGetValue(current, out var c) ? c : current);
                    if (currentChunk == null || neighbourChunk == null || cameFromChunk == null)
                    {
                        continue;
                    }

                    var requiredConnection = GetRequiredConnection(Position.LookAt(cameFromChunk.X, cameFromChunk.Y, currentChunk.X, currentChunk.Y), Position.LookAt(currentChunk.X, currentChunk.Y, neighbourChunk.X, neighbourChunk.Y));
                    if (!currentChunk.Connections.HasFlag(requiredConnection))
                    {
                        continue;
                    }

                    if (closedSet.Contains(neighbor))
                        continue;

                    var tentativeGScore = gScore[current] + 1;
                    if (!gScore.TryGetValue(neighbor, out _) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        var f = tentativeGScore + ManhattanDistance(neighbor, end);
                        fScore[neighbor] = f;
                        openSet.Enqueue(neighbor, f);
                    }
                }
            }
            return [];
        }

        private static float ManhattanDistance((int X, int Y) a, (int X, int Y) b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        private static float ManhattanDistance(Position a, Position b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        
    }
}


