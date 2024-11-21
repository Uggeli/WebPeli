namespace WebPeli.GameEngine.Util;

public static class PerlinNoise
{
    private struct Vector2
    {
        public float x, y;
    }

    public static float Interpolate(float a0, float a1, float w)
    {
        return (a1 - a0) * w + a0;
    }

    private static Vector2 RandomGradient(int ix, int iy)
    {
        const uint w = 32;  // Assuming 32-bit uint
        const uint s = w / 2;
        uint a = (uint)ix, b = (uint)iy;
        a *= 3284157443; b ^= a << (int)s | a >> (int)(w - s);
        b *= 1911520717; a ^= b << (int)s | b >> (int)(w - s);
        a *= 2048419325;
        float random = a * (3.14159265f / int.MinValue);
        return new Vector2 { x = (float)Math.Cos(random), y = (float)Math.Sin(random) };
    }

    private static float DotGridGradient(int ix, int iy, float x, float y)
    {
        Vector2 gradient = RandomGradient(ix, iy);
        float dx = x - ix;
        float dy = y - iy;
        return dx * gradient.x + dy * gradient.y;
    }

    public static float Generate(float x, float y)
    {
        int x0 = (int)Math.Floor(x);
        int x1 = x0 + 1;
        int y0 = (int)Math.Floor(y);
        int y1 = y0 + 1;

        float sx = x - x0;
        float sy = y - y0;

        float n0, n1, ix0, ix1;

        n0 = DotGridGradient(x0, y0, x, y);
        n1 = DotGridGradient(x1, y0, x, y);
        ix0 = Interpolate(n0, n1, sx);

        n0 = DotGridGradient(x0, y1, x, y);
        n1 = DotGridGradient(x1, y1, x, y);
        ix1 = Interpolate(n0, n1, sx);

        return Interpolate(ix0, ix1, sy);
    }
}

public static class EnhancedPerlinNoise
{
    private struct Vector2
    {
        public float x, y;
    }

    // Smoother interpolation function (smoothstep)
    public static float Interpolate(float a0, float a1, float w)
    {
        // Smoothstep interpolation: 3w² - 2w³
        w = w * w * (3 - 2 * w);
        return (a1 - a0) * w + a0;
    }

    private static Vector2 RandomGradient(int ix, int iy)
    {
        const uint w = 32;
        const uint s = w / 2;
        uint a = (uint)ix, b = (uint)iy;
        a *= 3284157443; b ^= a << (int)s | a >> (int)(w - s);
        b *= 1911520717; a ^= b << (int)s | b >> (int)(w - s);
        a *= 2048419325;
        float random = a * (3.14159265f / int.MinValue);
        return new Vector2 { x = (float)Math.Cos(random), y = (float)Math.Sin(random) };
    }

    private static float DotGridGradient(int ix, int iy, float x, float y)
    {
        Vector2 gradient = RandomGradient(ix, iy);
        float dx = x - ix;
        float dy = y - iy;
        return dx * gradient.x + dy * gradient.y;
    }

    // Basic Perlin noise generation
    private static float GenerateBase(float x, float y)
    {
        int x0 = (int)Math.Floor(x);
        int x1 = x0 + 1;
        int y0 = (int)Math.Floor(y);
        int y1 = y0 + 1;

        float sx = x - x0;
        float sy = y - y0;

        float n0, n1, ix0, ix1;

        n0 = DotGridGradient(x0, y0, x, y);
        n1 = DotGridGradient(x1, y0, x, y);
        ix0 = Interpolate(n0, n1, sx);

        n0 = DotGridGradient(x0, y1, x, y);
        n1 = DotGridGradient(x1, y1, x, y);
        ix1 = Interpolate(n0, n1, sx);

        return Interpolate(ix0, ix1, sy);
    }

    // Generate noise with multiple octaves (fBm)
    public static float Generate(float x, float y, int octaves = 6, float persistence = 0.5f, float lacunarity = 2.0f)
    {
        float total = 0.0f;
        float frequency = 1.0f;
        float amplitude = 1.0f;
        float maxValue = 0.0f;  // Used for normalizing result

        for (int i = 0; i < octaves; i++)
        {
            total += GenerateBase(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        // Normalize the result
        return total / maxValue;
    }

    // Generate noise with regions (using thresholds)
    public static float GenerateRegions(float x, float y, float[] thresholds, int octaves = 6)
    {
        float noiseValue = Generate(x, y, octaves);
        
        // Find the appropriate threshold region
        for (int i = 0; i < thresholds.Length; i++)
        {
            if (noiseValue < thresholds[i])
                return (float)i / (thresholds.Length - 1);
        }
        
        return 1.0f;
    }

    // Generate terrain-like noise
    public static float GenerateTerrain(float x, float y)
    {
        // Layer 1: Base terrain shape
        float baseNoise = Generate(x, y, 6, 0.5f, 2.0f);
        
        // Layer 2: Add some medium details
        float detailNoise = Generate(x * 2.0f, y * 2.0f, 4, 0.4f, 2.5f) * 0.5f;
        
        // Layer 3: Fine details
        float fineNoise = Generate(x * 4.0f, y * 4.0f, 3, 0.3f, 3.0f) * 0.25f;

        // Combine layers
        float combined = (baseNoise + detailNoise + fineNoise) / 1.75f;
        
        // Add some non-linearity to create more distinct regions
        return (float)Math.Pow(combined, 1.5);
    }
}