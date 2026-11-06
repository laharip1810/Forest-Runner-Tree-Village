using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural flat-shaded low-poly meshes (tapered trunks, cones, foliage blobs, rocks, roofs).
/// Meshes are cached so many objects share the same mesh.
/// </summary>
public static class MeshFactory
{
    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
    public static readonly List<Mesh> Created = new List<Mesh>();

    public static void Reset()
    {
        cache.Clear();
        Created.Clear();
    }

    static bool TryGet(string key, out Mesh mesh)
    {
        if (cache.TryGetValue(key, out mesh) && mesh != null) return true;
        mesh = null;
        return false;
    }

    static Mesh Finish(string key, List<Vector3> verts, List<int> tris)
    {
        var m = new Mesh();
        m.name = key;
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        cache[key] = m;
        Created.Add(m);
        return m;
    }

    /// <summary>Adds a triangle with its own vertices (flat shading), oriented away from 'inside'.</summary>
    static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        Vector3 centroid = (a + b + c) / 3f;
        if (Vector3.Dot(n, centroid - inside) < 0f)
        {
            Vector3 tmp = b; b = c; c = tmp;
        }
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    /// <summary>Tapered prism along +Y from y=0 to y=height. rTop = 0 gives a cone.</summary>
    public static Mesh Frustum(int sides, float rBottom, float rTop, float height)
    {
        string key = "Frustum_" + sides + "_" + rBottom + "_" + rTop + "_" + height;
        Mesh cached;
        if (TryGet(key, out cached)) return cached;

        var v = new List<Vector3>();
        var t = new List<int>();
        var bottom = new Vector3[sides];
        var top = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            bottom[i] = new Vector3(Mathf.Cos(a) * rBottom, 0f, Mathf.Sin(a) * rBottom);
            top[i] = new Vector3(Mathf.Cos(a) * rTop, height, Mathf.Sin(a) * rTop);
        }
        Vector3 apex = new Vector3(0f, height, 0f);
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            Vector3 mid = new Vector3(0f, height * 0.5f, 0f);
            if (rTop > 0.0001f)
            {
                Tri(v, t, bottom[i], bottom[j], top[j], mid);
                Tri(v, t, bottom[i], top[j], top[i], mid);
                Tri(v, t, top[i], top[j], apex, new Vector3(0f, height - 1f, 0f));
            }
            else
            {
                Tri(v, t, bottom[i], bottom[j], apex, mid);
            }
            Tri(v, t, bottom[i], bottom[j], Vector3.zero, new Vector3(0f, 1f, 0f));
        }
        return Finish(key, v, t);
    }

    /// <summary>Low-poly jittered sphere of diameter ~1, centred on the origin.</summary>
    public static Mesh Blob(int variant, int rings, int segments, float jitter)
    {
        string key = "Blob_" + variant + "_" + rings + "_" + segments + "_" + jitter;
        Mesh cached;
        if (TryGet(key, out cached)) return cached;

        var rng = new System.Random(1000 + variant * 37 + rings * 7 + segments);
        var grid = new Vector3[rings - 1, segments];
        for (int r = 1; r < rings; r++)
        {
            float phi = Mathf.PI * r / rings;
            for (int s = 0; s < segments; s++)
            {
                float theta = Mathf.PI * 2f * s / segments + (r % 2) * Mathf.PI / segments;
                float rad = 0.5f * (1f + ((float)rng.NextDouble() * 2f - 1f) * jitter);
                grid[r - 1, s] = new Vector3(
                    Mathf.Sin(phi) * Mathf.Cos(theta) * rad,
                    Mathf.Cos(phi) * rad,
                    Mathf.Sin(phi) * Mathf.Sin(theta) * rad);
            }
        }
        Vector3 topPole = new Vector3(0f, 0.5f * (1f + (float)rng.NextDouble() * jitter * 0.5f), 0f);
        Vector3 bottomPole = new Vector3(0f, -0.5f, 0f);

        var v = new List<Vector3>();
        var t = new List<int>();
        Vector3 c = Vector3.zero;
        for (int s = 0; s < segments; s++)
        {
            int n = (s + 1) % segments;
            Tri(v, t, topPole, grid[0, s], grid[0, n], c);
            Tri(v, t, bottomPole, grid[rings - 2, s], grid[rings - 2, n], c);
            for (int r = 0; r < rings - 2; r++)
            {
                Tri(v, t, grid[r, s], grid[r + 1, s], grid[r + 1, n], c);
                Tri(v, t, grid[r, s], grid[r + 1, n], grid[r, n], c);
            }
        }
        return Finish(key, v, t);
    }

    public static Mesh Rock(int variant)
    {
        return Blob(50 + variant, 4, 6, 0.28f);
    }

    public static Mesh Foliage(int variant)
    {
        return Blob(variant, 4, 7, 0.18f);
    }

    /// <summary>Triangular prism roof: width along X, depth along Z, ridge along Z. Base at y=0.</summary>
    public static Mesh Roof()
    {
        const string key = "Roof";
        Mesh cached;
        if (TryGet(key, out cached)) return cached;

        var v = new List<Vector3>();
        var t = new List<int>();
        Vector3 c = new Vector3(0f, 0.33f, 0f);
        Vector3 bl = new Vector3(-0.5f, 0f, -0.5f), br = new Vector3(0.5f, 0f, -0.5f);
        Vector3 fl = new Vector3(-0.5f, 0f, 0.5f), fr = new Vector3(0.5f, 0f, 0.5f);
        Vector3 rb = new Vector3(0f, 1f, -0.5f), rf = new Vector3(0f, 1f, 0.5f);
        // Slopes
        Tri(v, t, bl, fl, rf, c); Tri(v, t, bl, rf, rb, c);
        Tri(v, t, br, fr, rf, c); Tri(v, t, br, rf, rb, c);
        // Gables
        Tri(v, t, bl, br, rb, c);
        Tri(v, t, fl, fr, rf, c);
        // Bottom
        Tri(v, t, bl, br, fr, c); Tri(v, t, bl, fr, fl, c);
        return Finish(key, v, t);
    }
}
