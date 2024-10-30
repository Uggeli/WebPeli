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

