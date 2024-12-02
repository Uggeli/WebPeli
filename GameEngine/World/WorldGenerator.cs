using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.World;

internal static partial class World
{
    public static class WorldGenerator
    {
        // Core elevation thresholds for basic terrain types
        private static readonly float WATER_THRESHOLD = -0.3f;
        private static readonly float BEACH_THRESHOLD = -0.2f;
        private static readonly float MOUNTAIN_THRESHOLD = 0.5f;
        private static readonly float PEAK_THRESHOLD = 0.7f;

        public static void GenerateWorld()
        {
            GenerateChunks();
            DumbMapdataToFile();
            // GenerateAndCompare();
            // TestChunkAccess();
        }
        // Test method to expose the issue
        # region Testing
        private static void TestChunkAccess()
        {
            DummyChunk dummyChunk = new DummyChunk(0, 0);
            Chunk realChunk = new Chunk(0, 0);

            // Set a test pattern using actual CHUNK_SIZE
            for (byte x = 0; x < Config.CHUNK_SIZE_BYTE; x++)
            {
                for (byte y = 0; y < Config.CHUNK_SIZE_BYTE; y++)
                {
                    // Let's use a simpler pattern that will make issues obvious
                    byte value = (byte)((x + y) % 4); // Or any other pattern that's easy to spot
                    dummyChunk.SetTile(x, y, value);
                    realChunk.SetTile(x, y, value, TileSurface.None, TileProperties.None);
                }
            }

            // Check a few key positions, including edges
            byte[][] positionsToCheck =
            [
                [0, 0],
                [(byte)(Config.CHUNK_SIZE_BYTE-1), 0],
                [0, (byte)(Config.CHUNK_SIZE_BYTE-1)],
                [(byte)(Config.CHUNK_SIZE_BYTE-1), (byte)(Config.CHUNK_SIZE_BYTE-1)],
                // [64, 64],  // Middle-ish
                // [32, 96],  // Random positions
                // [96, 32]
            ];

            Console.WriteLine("Checking key positions:");
            foreach (byte[] pos in positionsToCheck)
            {
                byte x = pos[0], y = pos[1];
                byte dummyValue = dummyChunk.GetTile(x, y);
                byte realValue = realChunk.GetTile(x, y).material;
                Console.WriteLine($"Position ({x,3},{y,3}): Dummy={dummyValue,3} Real={realValue,3}" +
                                (dummyValue == realValue ? "" : " MISMATCH!"));
            }

            // Let's also check the actual 1D index calculation
            Console.WriteLine("\nChecking 1D index calculations for these positions:");
            foreach (byte[] pos in positionsToCheck)
            {
                byte x = pos[0], y = pos[1];
                int index = x * Config.CHUNK_SIZE_BYTE + y;
                Console.WriteLine($"Position ({x,3},{y,3}) -> 1D index: {index,5}" +
                                (index < Config.CHUNK_SIZE_BYTE * Config.CHUNK_SIZE_BYTE ? "" : " OVERFLOW!"));
            }

            // Add this to the test
            Console.WriteLine("\nVisual 4x4 section of arrays:");
            Console.WriteLine("Dummy:");
            for (byte y = 0; y < 4; y++)
            {
                for (byte x = 0; x < 4; x++)
                {
                    Console.Write($"{dummyChunk.GetTile(x, y),3} ");
                }
                Console.WriteLine();
            }

            Console.WriteLine("\nReal:");
            for (byte y = 0; y < 4; y++)
            {
                for (byte x = 0; x < 4; x++)
                {
                    Console.Write($"{realChunk.GetTile(x, y).material,3} ");
                }
                Console.WriteLine();
            }
        }

        // Dummy chunk class just for testing
        private class DummyChunk
        {
            public byte[,] MapData { get; } = new byte[Config.CHUNK_SIZE_BYTE, Config.CHUNK_SIZE_BYTE];
            public byte X { get; }
            public byte Y { get; }

            public DummyChunk(byte x, byte y)
            {
                X = x;
                Y = y;
            }

            public void SetTile(byte x, byte y, byte material) => MapData[x, y] = material;
            public byte GetTile(byte x, byte y) => MapData[x, y];
        }

        private static void GenerateAndCompare()
        {
            Console.WriteLine("Generating comparison data...");
            float baseScale = 0.05f; // Adjust scale for terrain size

            Dictionary<(byte X, byte Y), DummyChunk> dummyChunks = new Dictionary<(byte X, byte Y), DummyChunk>();

            // Generate dummy chunks alongside real ones
            for (byte x = 0; x < Config.WORLD_SIZE; x++)
            {
                for (byte y = 0; y < Config.WORLD_SIZE; y++)
                {
                    DummyChunk dummyChunk = new DummyChunk(x, y);
                    Chunk realChunk = new Chunk(x, y);

                    // Generate terrain data for both
                    for (byte localX = 0; localX < Config.CHUNK_SIZE_BYTE; localX++)
                    {
                        for (byte localY = 0; localY < Config.CHUNK_SIZE_BYTE; localY++)
                        {
                            float worldX = x * Config.CHUNK_SIZE_BYTE + localX;
                            float worldY = y * Config.CHUNK_SIZE_BYTE + localY;

                            // Generate terrain value
                            float elevation = EnhancedPerlinNoise.GenerateTerrain(worldX * baseScale, worldY * baseScale);
                            (byte material, TileProperties _) = DetermineTileType(elevation);

                            // Set in both chunks
                            dummyChunk.SetTile(localX, localY, material);
                            realChunk.SetTile(localX, localY, material, TileSurface.None, TileProperties.None);
                        }
                    }

                    dummyChunks[(x, y)] = dummyChunk;
                    _chunks[(x, y)] = realChunk;
                }
            }

            // Write both to files for comparison
            using StreamWriter dummyWriter = new("mapdata_dummy.txt", false);
            using StreamWriter realWriter = new("mapdata_real.txt", false);
            for (byte worldY = 0; worldY < Config.WORLD_SIZE; worldY++)
            {
                for (byte chunkTileY = 0; chunkTileY < Config.CHUNK_SIZE_BYTE; chunkTileY++)
                {
                    for (byte worldX = 0; worldX < Config.WORLD_SIZE; worldX++)
                    {
                        DummyChunk dummyChunk = dummyChunks[(worldX, worldY)];
                        Chunk realChunk = _chunks[(worldX, worldY)];

                        // Write dummy chunk data
                        for (byte chunkTileX = 0; chunkTileX < Config.CHUNK_SIZE_BYTE; chunkTileX++)
                        {
                            dummyWriter.Write(dummyChunk.GetTile(chunkTileX, chunkTileY));
                        }
                        dummyWriter.Write(" ");

                        // Write real chunk data
                        for (byte chunkTileX = 0; chunkTileX < Config.CHUNK_SIZE_BYTE; chunkTileX++)
                        {
                            realWriter.Write(realChunk.GetTile(chunkTileX, chunkTileY).material);
                        }
                        realWriter.Write(" ");
                    }
                    dummyWriter.WriteLine();
                    realWriter.WriteLine();
                }
                dummyWriter.WriteLine();
                realWriter.WriteLine();
            }
        }

        private static void DumbMapdataToFile()
        {
            System.Console.WriteLine("Writing map data to file...");

            if (File.Exists("mapdata.txt"))
            {
                File.Delete("mapdata.txt");
            }

            using StreamWriter writer = new StreamWriter("mapdata.txt", false);

            // Write world row by row
            for (byte worldY = 0; worldY < Config.WORLD_SIZE; worldY++)
            {
                // For each row of chunks, we need to write CHUNK_SIZE lines
                for (byte chunkTileY = 0; chunkTileY < Config.CHUNK_SIZE_BYTE; chunkTileY++)
                {
                    // Write all chunks in this world row
                    for (byte worldX = 0; worldX < Config.WORLD_SIZE; worldX++)
                    {
                        Chunk chunk = _chunks[(worldX, worldY)];

                        // Write one row of this chunk
                        for (byte chunkTileX = 0; chunkTileX < Config.CHUNK_SIZE_BYTE; chunkTileX++)
                        {
                            writer.Write(chunk.GetTile(chunkTileX, chunkTileY).material);
                        }
                        writer.Write(" "); // Separate chunks horizontally
                    }
                    writer.WriteLine(); // End of row
                }
                writer.WriteLine(); // Separate chunks vertically
            }
        }
        # endregion
        private static void GenerateChunks()
        {
            for (byte worldX = 0; worldX < Config.WORLD_SIZE; worldX++)
            {
                for (byte worldY = 0; worldY < Config.WORLD_SIZE; worldY++)
                {
                    Chunk newChunk = new(worldX, worldY);
                    GenerateChunkTerrain(newChunk);
                    ZoneManager.CreateZones(newChunk);
                    _chunks[(worldX, worldY)] = newChunk;
                }
            }

            BuildChunkGraph();
        }

        private static void BuildChunkGraph()
        {

            for (byte worldX = 0; worldX < Config.WORLD_SIZE; worldX++)
            {
                for (byte worldY = 0; worldY < Config.WORLD_SIZE; worldY++)
                {
                    Chunk? chunk = GetChunk((worldX, worldY));
                    if (chunk == null) continue;

                    List<Zone> chunkZones = ZoneManager.GetZones(chunk);
                    ChunkConnection connections = ChunkConnection.None;

                    // First collect all edge tiles per direction
                    List<(byte x, byte y)> northEdgeTiles = [];
                    List<(byte x, byte y)> eastEdgeTiles = [];
                    List<(byte x, byte y)> southEdgeTiles = [];
                    List<(byte x, byte y)> westEdgeTiles = [];

                    foreach (Zone zone in chunkZones)
                    {
                        foreach (((byte X, byte Y) pos, ZoneEdge edge) in zone.Edges)
                        {
                            if (edge.HasFlag(ZoneEdge.ChunkNorth)) northEdgeTiles.Add(pos);
                            if (edge.HasFlag(ZoneEdge.ChunkEast)) eastEdgeTiles.Add(pos);
                            if (edge.HasFlag(ZoneEdge.ChunkSouth)) southEdgeTiles.Add(pos);
                            if (edge.HasFlag(ZoneEdge.ChunkWest)) westEdgeTiles.Add(pos);
                        }
                    }

                    // Now check for valid connections with neighbor chunks
                    // North neighbor
                    if (worldY > 0 && northEdgeTiles.Count > 0)
                    {
                        Chunk? northChunk = GetChunk((worldX, (byte)(worldY - 1)));
                        if (northChunk != null && HasMatchingEdges(chunk, northChunk, northEdgeTiles, Direction.North))
                        {
                            // Check what other directions we can reach from these northern tiles
                            if (CanReachEdge(chunk, northEdgeTiles, ZoneEdge.ChunkEast))
                                connections |= ChunkConnection.NorthEast;
                            if (CanReachEdge(chunk, northEdgeTiles, ZoneEdge.ChunkWest))
                                connections |= ChunkConnection.NorthWest;
                            if (CanReachEdge(chunk, northEdgeTiles, ZoneEdge.ChunkSouth))
                                connections |= ChunkConnection.NorthSouth;
                        }
                    }

                    // South neighbor
                    if (worldY < Config.WORLD_SIZE - 1 && southEdgeTiles.Count > 0)
                    {
                        Chunk? southChunk = GetChunk((worldX, (byte)(worldY + 1)));
                        if (southChunk != null && HasMatchingEdges(chunk, southChunk, southEdgeTiles, Direction.South))
                        {
                            // Check what other directions we can reach from these southern tiles
                            if (CanReachEdge(chunk, southEdgeTiles, ZoneEdge.ChunkEast))
                                connections |= ChunkConnection.SouthEast;
                            if (CanReachEdge(chunk, southEdgeTiles, ZoneEdge.ChunkWest))
                                connections |= ChunkConnection.SouthWest;
                        }
                    }

                    // East neighbor
                    if (worldX < Config.WORLD_SIZE - 1 && eastEdgeTiles.Count > 0)
                    {
                        Chunk? eastChunk = GetChunk(((byte)(worldX + 1), worldY));
                        if (eastChunk != null && HasMatchingEdges(chunk, eastChunk, eastEdgeTiles, Direction.East))
                        {
                            // Check what other directions we can reach from these eastern tiles
                            if (CanReachEdge(chunk, eastEdgeTiles, ZoneEdge.ChunkWest))
                                connections |= ChunkConnection.EastWest;
                        }
                    }

                    _chunkGraph[(worldX, worldY)] = connections;
                    chunk.Connections = connections;
                }
            }
        }

        // Helper to check if tiles on one edge can reach another edge through zones
        private static bool CanReachEdge(Chunk chunk, List<(byte x, byte y)> startTiles, ZoneEdge targetEdge)
        {
            foreach (Zone zone in chunk.GetZones())
            {
                // If this zone contains any of our start tiles and has the target edge type
                if (zone.TilePositions.Intersect(startTiles).Any() &&
                    zone.Edges.Any(e => e.Value.HasFlag(targetEdge)))
                {
                    return true;
                }
            }
            return false;
        }

        // Helper to check if edge tiles match up between chunks
        private static bool HasMatchingEdges(Chunk chunk1, Chunk chunk2, List<(byte x, byte y)> edgeTiles, Direction direction)
        {
            return edgeTiles.Select(pos =>
            {
                var (x, y) = GetOppositeEdge(pos, direction);
                bool isMatch = TileManager.IsWalkable(chunk1.GetTile(pos.x, pos.y).properties) &&
                              TileManager.IsWalkable(chunk2.GetTile(x, y).properties);

                #if DEBUG
                Console.WriteLine($"Checking edge: Chunk1({pos.x},{pos.y}) -> Chunk2({x},{y}) = {isMatch}");
                #endif

                return isMatch;
            }).Any(isMatch => isMatch);
        }

        private static (byte x, byte y) GetOppositeEdge((byte x, byte y) pos, Direction direction)
        {
            // We assume the position is on the edge of the chunk
            return direction switch
            {
                Direction.North => ((byte)(pos.x + (Config.CHUNK_SIZE - 1)), pos.y),
                Direction.South => ((byte)(pos.x - (Config.CHUNK_SIZE - 1)), pos.y),
                Direction.East => (pos.x, (byte)(pos.y - (Config.CHUNK_SIZE - 1))),
                Direction.West => (pos.x, (byte)(pos.y + (Config.CHUNK_SIZE - 1))),
                _ => pos
            };




        }

        private static void GenerateChunkTerrain(Chunk chunk)
        {

            float baseScale = 0.05f; // Adjust scale for terrain size

            for (byte localX = 0; localX < Config.CHUNK_SIZE; localX++)
            {
                for (byte localY = 0; localY < Config.CHUNK_SIZE; localY++)
                {
                    float worldX = chunk.X * Config.CHUNK_SIZE + localX;
                    float worldY = chunk.Y * Config.CHUNK_SIZE + localY;
                    if (Config.GenerateFlatWorld)
                    {
                        chunk.SetTile(localX, localY, (byte)TileMaterial.Dirt, TileSurface.None, TileProperties.Walkable | TileProperties.Breakable);
                        continue;
                    }
                    // Generate base terrain elevation
                    float elevation = EnhancedPerlinNoise.GenerateTerrain(worldX * baseScale, worldY * baseScale);
                    (byte material, TileProperties properties) = DetermineTileType(elevation);
                    chunk.SetTile(localX, localY, material, TileSurface.None, properties);
                }
            }

        }

        private static (byte material, TileProperties properties) DetermineTileType(float elevation)
        {
            TileMaterial material;
            TileProperties properties;

            if (elevation < WATER_THRESHOLD)
            {
                // Deep water
                material = TileMaterial.Water;
                properties = TileProperties.Transparent | TileProperties.BlocksProjectiles;
            }
            else if (elevation < BEACH_THRESHOLD)
            {
                // Beach/Shore
                material = TileMaterial.Sand;
                properties = TileProperties.Walkable | TileProperties.Breakable;
            }
            else if (elevation < MOUNTAIN_THRESHOLD)
            {
                // Regular terrain
                material = TileMaterial.Dirt;
                properties = TileProperties.Walkable | TileProperties.Solid | TileProperties.Breakable;
            }
            else if (elevation < PEAK_THRESHOLD)
            {
                // Mountains
                material = TileMaterial.Stone;
                properties = TileProperties.Solid | TileProperties.BlocksLight | TileProperties.Breakable;
            }
            else
            {
                // High peaks
                material = TileMaterial.Stone;
                properties = TileProperties.Solid | TileProperties.BlocksLight | TileProperties.BlocksProjectiles;
            }

            return ((byte)material, properties);
        }

        public static void DrawChunk(Chunk chunk)
        {
            for (byte y = 0; y < Config.CHUNK_SIZE_BYTE; y++)
            {
                for (byte x = 0; x < Config.CHUNK_SIZE_BYTE; x++)
                {
                    (byte material, TileSurface _, TileProperties properties) = chunk.GetTile(x, y);
                    char glyph = material switch
                    {
                        (byte)TileMaterial.Water => '~',
                        (byte)TileMaterial.Sand => '.',
                        (byte)TileMaterial.Dirt => ',',
                        (byte)TileMaterial.Stone => '#',
                        _ => '?'
                    };
                    if (!properties.HasFlag(TileProperties.Walkable))
                    {
                        glyph = 'X';
                    }

                    Console.Write(glyph);

                }
                Console.WriteLine();
            }
        }
    }
}


