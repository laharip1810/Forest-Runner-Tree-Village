using UnityEngine;

/// <summary>Generates the rounded panel and circle sprites used by the UI (no image files required).</summary>
public static class UISprites
{
    public const int RoundedSize = 64;
    public const int RoundedRadius = 22;
    public const int CircleSize = 128;

    public static Texture2D RoundedTexture()
    {
        return Make(RoundedSize, RoundedRadius);
    }

    public static Texture2D CircleTexture()
    {
        return Make(CircleSize, CircleSize / 2);
    }

    static Texture2D Make(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    public static Sprite RoundedSprite()
    {
        var tex = RoundedTexture();
        float b = RoundedRadius;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    public static Sprite CircleSprite()
    {
        var tex = CircleTexture();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
