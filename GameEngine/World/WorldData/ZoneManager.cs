using WebPeli.GameEngine.Util;
namespace WebPeli.GameEngine.World.WorldData;

public static class ZoneManager
{
    public static void CreateZones(Chunk chunk)
    {
        bool[,] visited = new bool[Config.CHUNK_SIZE, Config.CHUNK_SIZE];
        for (byte x = 0; x < Config.CHUNK_SIZE; x++)
        {
            for (byte y = 0; y < Config.CHUNK_SIZE; y++)
            {
                if (visited[x, y]) continue;
                Zone? newZone = DiscoverZone(chunk, (x, y), ref visited);
                if (newZone != null && newZone is Zone zone)
                {
                    chunk.AddZone(zone);
                }
            }
        }
        if (Config.DebugMode)
        {
            World.WorldGenerator.DrawChunk(chunk);
            DrawZones(chunk);
            Console.WriteLine($"Chunk {chunk.X}, {chunk.Y} has {chunk.GetZones().Count()} zones");
        }
    }

    public static void DrawZones(Chunk chunk)
    {
        for (byte x = 0; x < Config.CHUNK_SIZE; x++)
        {
            for (byte y = 0; y < Config.CHUNK_SIZE; y++)
            {
                var zone = chunk.GetZoneAt(x, y);
                if (zone == null)
                {
                    Console.Write(" "); 
                }
                else
                {
                    var zoneTile = zone.Value.TilePositions.Contains((x, y));
                    var zoneEdge = zone.Value.Edges.ContainsKey((x, y));
                    char Glyph = ' ';
                    if (zoneEdge)
                    {
                        var edge = zone.Value.Edges[(x, y)];

                        if (edge.HasFlag(ZoneEdge.North))
                        {
                            Glyph = 'n';
                        }
                        if (edge.HasFlag(ZoneEdge.South))
                        {
                            Glyph = 's';
                        }
                        if (edge.HasFlag(ZoneEdge.East))
                        {
                            Glyph = 'e';
                        }
                        if (edge.HasFlag(ZoneEdge.West))
                        {
                            Glyph = 'w';
                        }

                        if (edge.HasFlag(ZoneEdge.ChunkNorth))
                        {
                            Glyph = 'N';
                        }
                        if (edge.HasFlag(ZoneEdge.ChunkSouth))
                        {
                            Glyph = 'S';
                        }
                        if (edge.HasFlag(ZoneEdge.ChunkEast))
                        {
                            Glyph = 'E';
                        }
                        if (edge.HasFlag(ZoneEdge.ChunkWest))
                        {
                            Glyph = 'W';
                        }

                    }
                    else if (zoneTile)
                    {
                        Glyph = 'Z';
                    }
                    else
                    {
                        Glyph = ' ';
                    }
                    Console.Write(Glyph);
                }
            }
            Console.WriteLine();
        }
    }

    public static void DrawZone(Zone zone, Position? position = null)
    {
        for (byte x = 0; x < Config.CHUNK_SIZE; x++)
        {
            for (byte y = 0; y < Config.CHUNK_SIZE; y++)
            {
                var zoneTile = zone.TilePositions.Contains((x, y));
                var zoneEdge = zone.Edges.ContainsKey((x, y));
                char Glyph = ' ';
                if (zoneEdge)
                {
                    var edge = zone.Edges[(x, y)];
                    if (edge.HasFlag(ZoneEdge.ChunkNorth))
                    {
                        Glyph = 'N';
                    }
                    if (edge.HasFlag(ZoneEdge.ChunkSouth))
                    {
                        Glyph = 'S';
                    }
                    if (edge.HasFlag(ZoneEdge.ChunkEast))
                    {
                        Glyph = 'E';
                    }
                    if (edge.HasFlag(ZoneEdge.ChunkWest))
                    {
                        Glyph = 'W';
                    }

                    if (edge.HasFlag(ZoneEdge.North))
                    {
                        Glyph = 'n';
                    }
                    if (edge.HasFlag(ZoneEdge.South))
                    {
                        Glyph = 's';
                    }
                    if (edge.HasFlag(ZoneEdge.East))
                    {
                        Glyph = 'e';
                    }
                    if (edge.HasFlag(ZoneEdge.West))
                    {
                        Glyph = 'w';
                    }
                }
                else if (zoneTile)
                {
                    Glyph = 'Z';
                }
                else
                {
                    Glyph = ' ';
                }

                if (position != null && position.Value.TilePosition == (x, y))
                {
                    Glyph = 'X';
                }


                Console.Write(Glyph);
            }
            Console.WriteLine();
        }
    }




    public static Zone? DiscoverZone(Chunk chunk, (byte x, byte y) startPos, ref bool[,] visited)
    {
        // Find tiles for zone
        List<(byte, byte)> zoneTiles = []; // All walkable tiles in the zone
        Queue<(byte, byte)> openTiles = new();
        openTiles.Enqueue(startPos);
        Dictionary<(byte, byte), ZoneEdge> edges = [];

        while (openTiles.Count > 0)
        {
            (byte x, byte y) = openTiles.Dequeue();
            if (visited[x, y]) continue;
            visited[x, y] = true;    

            if (TileManager.IsWalkable(chunk.GetTile(x, y).properties))
            {
                zoneTiles.Add((x, y));
            }

            var neighbors = new (int, int)[]
            {
                (x, y - 1),
                (x, y + 1),
                (x - 1, y),
                (x + 1, y)
            };



            foreach (var (nx, ny) in neighbors)
            {
                if (!World.IsInChunkBounds(nx, ny)) continue;
                if (visited[nx, ny]) continue;
                var (_, _, properties) = chunk.GetTile(nx, ny);
                if (!TileManager.IsWalkable(properties)) continue;
                openTiles.Enqueue(((byte, byte))(nx, ny));
            }
        }
        // Found nothing, eatshit
        if (zoneTiles.Count <= 1) return null;

        // Edgedetection
        foreach (var (x, y) in zoneTiles)
        {
            
            var neighbors = new (int, int)[]
            {
                (x - 1, y), // North
                (x + 1, y), // South
                (x, y - 1), // West
                (x, y + 1)  // East
            };
            ZoneEdge edge = ZoneEdge.None;
            foreach (var (nx, ny) in neighbors)
            {
                // if (!World.IsInWorldBounds(chunk.X * Config.CHUNK_SIZE + x, chunk.Y * Config.CHUNK_SIZE + y)) continue;
                
                // this neighbour is outside of chunk
                if (!World.IsInChunkBounds(nx, ny))
                {
                    if (nx <= 0)
                    {
                        edge |= ZoneEdge.ChunkNorth;
                    }
                    if (nx >= Config.CHUNK_SIZE)
                    {
                        edge |= ZoneEdge.ChunkSouth;
                    }
                    if (y == 0)
                    {
                        edge |= ZoneEdge.ChunkWest;
                    }
                    if (y == Config.CHUNK_SIZE - 1)
                    {
                        edge |= ZoneEdge.ChunkEast;
                    }
                }
                
                // this neighbour is not part of the zone
                if (!zoneTiles.Contains(((byte, byte))(nx, ny)))
                {
                    if (nx < x)
                    {
                        edge |= ZoneEdge.North;
                    }
                    if (nx > x)
                    {
                        edge |= ZoneEdge.South;
                    }
                    if (ny < y)
                    {
                        edge |= ZoneEdge.West;
                    }
                    if (ny > y)
                    {
                        edge |= ZoneEdge.East;
                    }
                }

                if (edge != ZoneEdge.None)
                {
                    edges[(x, y)] = edge;
                }
            }
        }
        var zone = new Zone(IDManager.GetZoneId(), chunk.X, chunk.Y, zoneTiles, edges);
        return zone;
    }


    public static void UpdateZone(Chunk chunk, Zone zone)
    {
        // Remove zone from chunk
        chunk.RemoveZone(zone.Id);
        // Re-create zone
        var visited = new bool[Config.CHUNK_SIZE_BYTE, Config.CHUNK_SIZE_BYTE];
        // populate visited with zone tiles
        for (byte y = 0; y < Config.CHUNK_SIZE_BYTE; y++)
        {
            for (byte x = 0; x < Config.CHUNK_SIZE_BYTE; x++)
            {
                if (zone.TilePositions.Contains((x, y))) continue;
                visited[x, y] = true;
            }
        }
        DiscoverZone(chunk, zone.TilePositions.First(), ref visited);
    }

    public static List<Zone> GetZones(Chunk chunk)
    {
        return chunk.GetZones().ToList();
    }
}




// long paths tend to be incorrect in long run and long paths take long to run 