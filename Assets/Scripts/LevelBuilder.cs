using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Optional assets handed to the builder (the editor setup passes saved assets; runtime fallback passes none).</summary>
public class BuildContext
{
    public Font font;
    public Sprite roundedSprite;
    public Sprite circleSprite;
    public AudioClip music, coin, jump, hit, win, click;
    public readonly List<Material> materials = new List<Material>();
    public GameObject player;
    public GameObject coinTemplate;
}

/// <summary>
/// Procedurally builds the complete Forest Runner level from Unity primitives and low-poly meshes:
/// forest, ravine, tree houses, platforms, bridges, obstacles, coins, player, camera, lighting,
/// audio, UI and game manager - all wired together.
/// Used by the editor GameSetup (to save a real scene) and as a runtime fallback.
/// </summary>
public static class LevelBuilder
{
    // ---------------- Layout constants ----------------
    public const float StartZ = 2f;
    public const float FinishZ = 388f;
    const float Elev = 3f;            // height of the elevated walkway
    const float RampUpStart = 80f;
    const float RavineStart = 95f;
    const float RavineEnd = 305f;
    const float RampDownEnd = 320f;
    const float RavineFloorY = -8f;
    const float Lane = 2f;

    static BuildContext ctx;
    static System.Random rng;
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Transform envRoot, decorRoot, structRoot, fxRoot, obstacleRoot, coinRoot;
    static readonly List<Vector3> keepClear = new List<Vector3>(); // (x, z, radius) areas where no trees are scattered

    // =====================================================================
    public static GameManager Build(BuildContext context)
    {
        ctx = context ?? new BuildContext();
        rng = new System.Random(20261106);
        mats.Clear();
        keepClear.Clear();
        MeshFactory.Reset();

        SetupLighting();

        envRoot = new GameObject("Environment").transform;
        decorRoot = Group("Forest Decor", envRoot);
        structRoot = Group("Wooden Structures", envRoot);
        fxRoot = new GameObject("Ambient Effects").transform;
        var gameplay = new GameObject("Gameplay").transform;
        obstacleRoot = Group("Obstacles", gameplay);
        coinRoot = Group("Coins", gameplay);

        BuildGround();
        BuildEntranceGate();
        BuildRamp("Ramp Up", RampUpStart, 0f, RavineStart, Elev);
        BuildElevatedWalkway();
        BuildRamp("Ramp Down", RavineEnd, Elev, RampDownEnd, 0f);
        BuildVillage();
        BuildFinish(gameplay);
        BuildForest();
        BuildFireflies();
        BuildObstaclesAndCoins();

        var audio = BuildAudio();
        var player = BuildPlayer(audio);
        var cam = BuildCamera(player.transform);
        var ui = UIBuilder.Build(ctx);

        var gmGO = new GameObject("GameManager");
        var gm = gmGO.AddComponent<GameManager>();
        gm.player = player;
        gm.cameraFollow = cam;
        gm.ui = ui;
        gm.audioManager = audio;
        gm.startZ = StartZ;
        gm.finishZ = FinishZ;

        ctx.player = player.gameObject;
        foreach (var m in mats.Values) ctx.materials.Add(m);
        return gm;
    }

    // =====================================================================
    // Helpers
    // =====================================================================
    static Color Hex(int rgb, float a = 1f)
    {
        return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
    }

    static float R(float min, float max) { return min + (float)rng.NextDouble() * (max - min); }
    static int RI(int min, int maxExclusive) { return rng.Next(min, maxExclusive); }
    static T Pick<T>(T[] arr) { return arr[rng.Next(arr.Length)]; }

    static Material Mat(string name, int rgb, float smooth = 0.08f, float metal = 0f, int emission = -1, float emissionStrength = 1f)
    {
        Material m;
        if (mats.TryGetValue(name, out m)) return m;
        var shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        m = new Material(shader);
        m.name = name;
        m.color = Hex(rgb);
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_Metallic", metal);
        if (emission >= 0)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Hex(emission) * emissionStrength);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        m.enableInstancing = true;
        mats[name] = m;
        return m;
    }

    // Palette ----------------------------------------------------------
    static Material Grass { get { return Mat("Grass", 0x72AE4C); } }
    static Material GrassDark { get { return Mat("GrassDark", 0x5B9A3F); } }
    static Material Earth { get { return Mat("Earth", 0x7B5E40); } }
    static Material Dirt { get { return Mat("DirtPath", 0xB08A58); } }
    static Material Stone { get { return Mat("VillageStone", 0xC4B08C); } }
    static Material Wood { get { return Mat("Wood", 0x9A6634); } }
    static Material WoodLight { get { return Mat("WoodLight", 0xC08A52); } }
    static Material WoodDark { get { return Mat("WoodDark", 0x5E3B1F); } }
    static Material Bark { get { return Mat("Bark", 0x6B4528); } }
    static Material Rope { get { return Mat("Rope", 0xD2B278); } }
    static Material RockMat { get { return Mat("Rock", 0x8E918A, 0.15f); } }
    static Material RockDark { get { return Mat("RockDark", 0x6E726B, 0.15f); } }
    static Material Moss { get { return Mat("Moss", 0x6E9F3A); } }
    static Material Water { get { return Mat("Water", 0x3E9AB0, 0.85f); } }
    static Material RavineFloor { get { return Mat("RavineFloor", 0x52733A); } }
    static Material Wall { get { return Mat("CottageWall", 0xEADBB8); } }
    static Material RoofRed { get { return Mat("RoofRed", 0xB2502F); } }
    static Material RoofGreen { get { return Mat("RoofGreen", 0x5F8A3C); } }
    static Material RoofBrown { get { return Mat("RoofBrown", 0x7E4E2A); } }
    static Material WindowGlow { get { return Mat("WindowGlow", 0xFFD98A, 0.3f, 0f, 0xFFB648, 1.2f); } }
    static Material LanternGlow { get { return Mat("LanternGlow", 0xFFE3A0, 0.3f, 0f, 0xFFC25A, 2f); } }
    static Material Gold { get { return Mat("CoinGold", 0xF5BE2E, 0.75f, 0.6f, 0x9A6A00, 0.6f); } }
    static Material GoldDeep { get { return Mat("CoinGoldDeep", 0xDE9A1C, 0.7f, 0.6f, 0x6A4200, 0.5f); } }
    static Material Cream { get { return Mat("Cream", 0xF4E8CD); } }
    static Material Skin { get { return Mat("Skin", 0xF0C08A, 0.2f); } }
    static Material Tunic { get { return Mat("Tunic", 0x3E8A3E, 0.1f); } }
    static Material Pants { get { return Mat("Pants", 0x6A4426, 0.1f); } }
    static Material Boots { get { return Mat("Boots", 0x3B2414, 0.2f); } }
    static Material Black { get { return Mat("Eyes", 0x1A1A1A, 0.6f); } }
    static Material Scarf { get { return Mat("Scarf", 0xE9A92A, 0.2f); } }
    static Material Flame { get { return Mat("Flame", 0xFF9A2E, 0.2f, 0f, 0xFF7A1A, 2.5f); } }
    static Material FlameCore { get { return Mat("FlameCore", 0xFFE07A, 0.2f, 0f, 0xFFD050, 3f); } }
    static Material Firefly { get { return Mat("Firefly", 0xFFF2A0, 0.2f, 0f, 0xFFE070, 3f); } }
    static Material MushroomRed { get { return Mat("MushroomRed", 0xD8443A, 0.3f); } }
    static Material Hill { get { return Mat("DistantHill", 0x4F7F48); } }
    static Material Reed { get { return Mat("Reed", 0x7FA94A); } }

    static Material[] LeafMats
    {
        get
        {
            return new[] { Mat("Leaves1", 0x4E9B3E), Mat("Leaves2", 0x67B347), Mat("Leaves3", 0x3D8A3C), Mat("Leaves4", 0x85C255), Mat("LeavesAutumn", 0xC9A23A) };
        }
    }
    static Material[] PineMats { get { return new[] { Mat("Pine1", 0x2F7343), Mat("Pine2", 0x3B8A4A), Mat("Pine3", 0x276A3E) }; } }
    static Material[] FlowerMats { get { return new[] { Mat("FlowerYellow", 0xFFD83D), Mat("FlowerPink", 0xF28BB0), Mat("FlowerWhite", 0xFAF6EC), Mat("FlowerBlue", 0x7FA8F0) }; } }
    static Material[] BunMats { get { return new[] { Mat("FlagGold", 0xF2B632), Mat("FlagRed", 0xC8503A), Mat("FlagGreen", 0x4E9B3E), Mat("FlagCream", 0xF4E8CD) }; } }

    static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        if (parent != null) t.SetParent(parent, false);
        return t;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m,
        Vector3 euler = default(Vector3), bool collider = false, bool shadows = true)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(euler);
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 scale, Material m,
        Vector3 euler = default(Vector3), bool collider = false, bool shadows = true)
    {
        return Prim(PrimitiveType.Cube, name, parent, pos, scale, m, euler, collider, shadows);
    }

    static GameObject MeshObj(string name, Mesh mesh, Transform parent, Vector3 pos, Vector3 scale, Material m,
        Quaternion rot, bool shadows = true)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }

    /// <summary>Low-poly log/pole between two points.</summary>
    static GameObject Log(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material m, int sides = 7, bool shadows = true)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.001f) return null;
        var mesh = MeshFactory.Frustum(sides, 1f, 0.92f, 1f);
        return MeshObj(name, mesh, parent, a, new Vector3(radius, len, radius), m,
            Quaternion.FromToRotation(Vector3.up, d / len), shadows);
    }

    /// <summary>A sagging rope made of short straight segments.</summary>
    static void RopeLine(Transform parent, Vector3 a, Vector3 b, float sag, int segments = 4, float radius = 0.045f)
    {
        Vector3 prev = a;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = Vector3.Lerp(a, b, t) - Vector3.up * sag * Mathf.Sin(Mathf.PI * t);
            Log("Rope", parent, prev, p, radius, Rope, 5, false);
            prev = p;
        }
    }

    static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = center;
        var bc = g.AddComponent<BoxCollider>();
        bc.size = size;
        return g;
    }

    static float SurfaceY(float z)
    {
        if (z < RampUpStart) return 0f;
        if (z < RavineStart) return (z - RampUpStart) / (RavineStart - RampUpStart) * Elev;
        if (z <= RavineEnd) return Elev;
        if (z < RampDownEnd) return Elev - (z - RavineEnd) / (RampDownEnd - RavineEnd) * Elev;
        return 0f;
    }

    static bool IsClear(float x, float z)
    {
        foreach (var k in keepClear)
        {
            float dx = x - k.x, dz = z - k.y;
            if (dx * dx + dz * dz < k.z * k.z) return false;
        }
        return true;
    }

    static void KeepClear(float x, float z, float radius) { keepClear.Add(new Vector3(x, z, radius)); }

    // =====================================================================
    // Lighting & atmosphere
    // =====================================================================
    static void SetupLighting()
    {
        var sun = new GameObject("Sun (Directional Light)");
        var l = sun.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = Hex(0xFFF0D2);
        l.intensity = 1.25f;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.65f;
        sun.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
        RenderSettings.sun = l;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Hex(0xA9CBE0);
        RenderSettings.ambientEquatorColor = Hex(0x93AE7E);
        RenderSettings.ambientGroundColor = Hex(0x4D5C38);
        RenderSettings.skybox = null;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Hex(0xC6DDCB);
        RenderSettings.fogStartDistance = 55f;
        RenderSettings.fogEndDistance = 200f;
    }

    // =====================================================================
    // Ground, ravine and river
    // =====================================================================
    static void GroundSlab(string name, float x0, float x1, float z0, float z1)
    {
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, w = x1 - x0, d = z1 - z0;
        // Earth body (cliff faces) + grass top with collider
        Cube(name + " Earth", envRoot, new Vector3(cx, -4.8f, cz), new Vector3(w, 9f, d), Earth);
        Cube(name + " Grass", envRoot, new Vector3(cx, -0.15f, cz), new Vector3(w + 0.02f, 0.3f, d + 0.02f), Grass, default(Vector3), true);
    }

    static void BuildGround()
    {
        GroundSlab("Ground Forest", -90f, 90f, -45f, RavineStart);
        GroundSlab("Ground Village", -90f, 90f, RavineEnd, 490f);
        GroundSlab("Ravine Bank L", -90f, -24f, RavineStart, RavineEnd);
        GroundSlab("Ravine Bank R", 24f, 90f, RavineStart, RavineEnd);

        float midZ = (RavineStart + RavineEnd) * 0.5f, len = RavineEnd - RavineStart;
        Cube("Ravine Floor", envRoot, new Vector3(0f, RavineFloorY - 0.5f, midZ), new Vector3(48f, 1f, len), RavineFloor, default(Vector3), true);
        Cube("River", envRoot, new Vector3(0f, RavineFloorY + 0.35f, midZ), new Vector3(14f, 0.1f, len), Water, default(Vector3), false, false);

        // Dirt forest path and stone village path
        Cube("Forest Path", envRoot, new Vector3(0f, 0.02f, (-45f + RampUpStart) * 0.5f), new Vector3(7.2f, 0.04f, RampUpStart + 45f), Dirt, default(Vector3), false, false);
        Cube("Village Path", envRoot, new Vector3(0f, 0.02f, (RampDownEnd + 440f) * 0.5f), new Vector3(7.2f, 0.04f, 440f - RampDownEnd), Stone, default(Vector3), false, false);

        // Pebbles along path edges
        var pebbles = Group("Path Pebbles", decorRoot);
        for (float z = -40f; z < 440f; z += R(1.8f, 3.0f))
        {
            if (z > RampUpStart - 1f && z < RampDownEnd + 1f) continue;
            for (int s = -1; s <= 1; s += 2)
            {
                float sc = R(0.25f, 0.55f);
                MeshObj("Pebble", MeshFactory.Rock(RI(0, 4)), pebbles, new Vector3(s * R(3.7f, 4.1f), 0.05f, z + R(-0.4f, 0.4f)),
                    new Vector3(sc * 1.3f, sc * 0.6f, sc), rng.NextDouble() < 0.5 ? RockMat : RockDark, Quaternion.Euler(0f, R(0f, 360f), 0f), false);
            }
        }

        // Ravine floor decoration: rocks, reeds, bushes
        var ravine = Group("Ravine Decor", decorRoot);
        for (int i = 0; i < 70; i++)
        {
            float z = R(RavineStart + 2f, RavineEnd - 2f);
            float x = (rng.NextDouble() < 0.5 ? -1 : 1) * R(7.5f, 23f);
            float sc = R(0.6f, 2.2f);
            MeshObj("Ravine Rock", MeshFactory.Rock(RI(0, 4)), ravine, new Vector3(x, RavineFloorY + sc * 0.25f, z),
                new Vector3(sc * 1.3f, sc * 0.8f, sc), RockMat, Quaternion.Euler(0f, R(0f, 360f), 0f));
        }
        for (int i = 0; i < 90; i++)
        {
            float z = R(RavineStart + 1f, RavineEnd - 1f);
            float x = (rng.NextDouble() < 0.5 ? -1 : 1) * R(6.6f, 8.5f);
            float h = R(0.8f, 1.6f);
            MeshObj("Reed", MeshFactory.Frustum(4, 0.5f, 0f, 1f), ravine, new Vector3(x, RavineFloorY, z),
                new Vector3(0.12f, h, 0.12f), Reed, Quaternion.Euler(R(-12f, 12f), 0f, R(-12f, 12f)), false);
        }
        for (int i = 0; i < 40; i++)
        {
            float z = R(RavineStart + 2f, RavineEnd - 2f);
            float x = (rng.NextDouble() < 0.5 ? -1 : 1) * R(9f, 23f);
            Bush(ravine, new Vector3(x, RavineFloorY, z), R(0.9f, 1.8f));
        }
    }

    // =====================================================================
    // Trees, bushes, rocks, flowers, mushrooms
    // =====================================================================
    static void RoundTree(Transform parent, Vector3 pos, float s)
    {
        var t = Group("Tree", parent);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
        float h = 3.2f * s;
        MeshObj("Trunk", MeshFactory.Frustum(6, 1f, 0.6f, 1f), t, Vector3.zero, new Vector3(0.42f * s, h, 0.42f * s), Bark, Quaternion.identity);
        var leaves = LeafMats;
        var main = rng.NextDouble() < 0.12 ? leaves[4] : leaves[RI(0, 4)];
        var second = leaves[RI(0, 4)];
        MeshObj("Crown", MeshFactory.Foliage(RI(0, 4)), t, new Vector3(0f, h + 0.9f * s, 0f), Vector3.one * 3.4f * s, main, Quaternion.Euler(0f, R(0f, 360f), 0f));
        int extra = RI(2, 4);
        for (int i = 0; i < extra; i++)
        {
            float a = i * Mathf.PI * 2f / extra + R(0f, 1f);
            MeshObj("Crown", MeshFactory.Foliage(RI(0, 4)), t,
                new Vector3(Mathf.Cos(a) * 1.25f * s, h + R(0f, 0.6f) * s, Mathf.Sin(a) * 1.25f * s),
                Vector3.one * R(2.0f, 2.5f) * s, i == 0 ? second : main, Quaternion.Euler(0f, R(0f, 360f), 0f));
        }
        MeshObj("Crown Top", MeshFactory.Foliage(RI(0, 4)), t, new Vector3(R(-0.3f, 0.3f) * s, h + 2.2f * s, 0f), Vector3.one * 2.1f * s, main, Quaternion.identity);
    }

    static void PineTree(Transform parent, Vector3 pos, float s)
    {
        var t = Group("Pine", parent);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
        var m = Pick(PineMats);
        MeshObj("Trunk", MeshFactory.Frustum(6, 1f, 0.6f, 1f), t, Vector3.zero, new Vector3(0.32f * s, 1.8f * s, 0.32f * s), Bark, Quaternion.identity);
        var cone = MeshFactory.Frustum(7, 0.5f, 0f, 1f);
        MeshObj("Layer1", cone, t, new Vector3(0f, 1.2f * s, 0f), new Vector3(3.0f * s, 2.6f * s, 3.0f * s), m, Quaternion.identity);
        MeshObj("Layer2", cone, t, new Vector3(0f, 2.5f * s, 0f), new Vector3(2.3f * s, 2.4f * s, 2.3f * s), m, Quaternion.Euler(0f, 25f, 0f));
        MeshObj("Layer3", cone, t, new Vector3(0f, 3.7f * s, 0f), new Vector3(1.5f * s, 2.1f * s, 1.5f * s), m, Quaternion.Euler(0f, 50f, 0f));
    }

    static void RandomTree(Transform parent, Vector3 pos, float s)
    {
        if (rng.NextDouble() < 0.4) PineTree(parent, pos, s * 1.1f);
        else RoundTree(parent, pos, s);
    }

    static void Bush(Transform parent, Vector3 pos, float s)
    {
        var b = Group("Bush", parent);
        b.localPosition = pos;
        var m = Pick(LeafMats);
        int n = RI(2, 4);
        for (int i = 0; i < n; i++)
        {
            float sc = s * R(0.7f, 1.1f);
            MeshObj("Leaves", MeshFactory.Foliage(RI(0, 4)), b, new Vector3(R(-0.5f, 0.5f) * s, sc * 0.35f, R(-0.5f, 0.5f) * s),
                new Vector3(sc * 1.2f, sc * 0.85f, sc * 1.2f), m, Quaternion.Euler(0f, R(0f, 360f), 0f));
        }
        if (rng.NextDouble() < 0.35)
        {
            var fm = Pick(FlowerMats);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Sphere, "Berry", b, new Vector3(R(-0.6f, 0.6f) * s, s * R(0.4f, 0.75f), R(-0.6f, 0.6f) * s), Vector3.one * 0.16f, fm, default(Vector3), false, false);
        }
    }

    static void FlowerPatch(Transform parent, Vector3 pos)
    {
        var f = Group("Flowers", parent);
        f.localPosition = pos;
        var m = Pick(FlowerMats);
        int n = RI(3, 7);
        for (int i = 0; i < n; i++)
        {
            Vector3 p = new Vector3(R(-0.7f, 0.7f), 0f, R(-0.7f, 0.7f));
            Cube("Stem", f, p + new Vector3(0f, 0.15f, 0f), new Vector3(0.03f, 0.3f, 0.03f), GrassDark, default(Vector3), false, false);
            Prim(PrimitiveType.Sphere, "Bloom", f, p + new Vector3(0f, 0.32f, 0f), Vector3.one * 0.16f, m, default(Vector3), false, false);
        }
    }

    static void GrassTuft(Transform parent, Vector3 pos)
    {
        var g = Group("Grass Tuft", parent);
        g.localPosition = pos;
        var cone = MeshFactory.Frustum(4, 0.5f, 0f, 1f);
        var m = rng.NextDouble() < 0.5 ? GrassDark : Moss;
        for (int i = 0; i < 3; i++)
            MeshObj("Blade", cone, g, new Vector3(R(-0.15f, 0.15f), 0f, R(-0.15f, 0.15f)), new Vector3(0.12f, R(0.35f, 0.6f), 0.12f), m,
                Quaternion.Euler(R(-20f, 20f), R(0f, 90f), R(-20f, 20f)), false);
    }

    static void Mushroom(Transform parent, Vector3 pos, float s)
    {
        var g = Group("Mushroom", parent);
        g.localPosition = pos;
        Prim(PrimitiveType.Cylinder, "Stem", g, new Vector3(0f, 0.18f * s, 0f), new Vector3(0.14f, 0.18f, 0.14f) * s, Cream, default(Vector3), false, false);
        MeshObj("Cap", MeshFactory.Blob(9, 3, 7, 0.05f), g, new Vector3(0f, 0.38f * s, 0f), new Vector3(0.5f, 0.3f, 0.5f) * s, MushroomRed, Quaternion.identity, false);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 2.1f;
            Prim(PrimitiveType.Sphere, "Spot", g, new Vector3(Mathf.Cos(a) * 0.14f * s, 0.47f * s, Mathf.Sin(a) * 0.14f * s), Vector3.one * 0.07f * s, Cream, default(Vector3), false, false);
        }
    }

    static void Scatter(Transform parent, float z0, float z1, float xMin, float xMax, int trees, int bushes, int rocks, int flowers, int tufts, int mushrooms, float minTreeScale, float maxTreeScale)
    {
        for (int i = 0; i < trees; i++)
        {
            float z = R(z0, z1);
            float side = rng.NextDouble() < 0.5 ? -1f : 1f;
            float x = side * (xMin + Mathf.Pow((float)rng.NextDouble(), 1.4f) * (xMax - xMin));
            if (!IsClear(x, z)) continue;
            RandomTree(parent, new Vector3(x, 0f, z), R(minTreeScale, maxTreeScale));
        }
        for (int i = 0; i < bushes; i++)
        {
            float z = R(z0, z1);
            float x = (rng.NextDouble() < 0.5 ? -1f : 1f) * R(4.4f, xMin + 6f);
            if (!IsClear(x, z)) continue;
            Bush(parent, new Vector3(x, 0f, z), R(0.7f, 1.4f));
        }
        for (int i = 0; i < rocks; i++)
        {
            float z = R(z0, z1);
            float x = (rng.NextDouble() < 0.5 ? -1f : 1f) * R(4.6f, xMax);
            if (!IsClear(x, z)) continue;
            float sc = R(0.5f, 2.2f);
            var r = MeshObj("Rock", MeshFactory.Rock(RI(0, 4)), parent, new Vector3(x, sc * 0.25f, z), new Vector3(sc * 1.3f, sc * 0.9f, sc), RockMat, Quaternion.Euler(0f, R(0f, 360f), 0f));
            if (sc > 1.2f && rng.NextDouble() < 0.6)
                MeshObj("Moss", MeshFactory.Blob(7, 3, 6, 0.1f), r.transform, new Vector3(0f, 0.38f, 0f), new Vector3(0.7f, 0.25f, 0.7f), Moss, Quaternion.identity, false);
        }
        for (int i = 0; i < flowers; i++)
        {
            float z = R(z0, z1);
            float x = (rng.NextDouble() < 0.5 ? -1f : 1f) * R(4.3f, 16f);
            FlowerPatch(parent, new Vector3(x, 0f, z));
        }
        for (int i = 0; i < tufts; i++)
        {
            float z = R(z0, z1);
            float x = (rng.NextDouble() < 0.5 ? -1f : 1f) * R(4.2f, 22f);
            GrassTuft(parent, new Vector3(x, 0f, z));
        }
        for (int i = 0; i < mushrooms; i++)
        {
            float z = R(z0, z1);
            float x = (rng.NextDouble() < 0.5 ? -1f : 1f) * R(4.5f, 14f);
            Mushroom(parent, new Vector3(x, 0f, z), R(0.7f, 1.4f));
        }
    }

    static void BuildForest()
    {
        var forest = Group("Forest Start", decorRoot);
        Scatter(forest, -45f, RavineStart - 2f, 7.5f, 75f, 120, 60, 35, 32, 90, 20, 1.0f, 2.2f);

        var banks = Group("Ravine Banks", decorRoot);
        for (int i = 0; i < 110; i++)
        {
            float z = R(RavineStart - 2f, RavineEnd + 2f);
            float x = (rng.NextDouble() < 0.5 ? -1f : 1f) * R(26.5f, 80f);
            RandomTree(banks, new Vector3(x, 0f, z), R(1.2f, 2.4f));
        }
        for (int i = 0; i < 45; i++)
        {
            float z = R(RavineStart, RavineEnd);
            float x = (rng.NextDouble() < 0.5 ? -1f : 1f) * R(25f, 32f);
            Bush(banks, new Vector3(x, 0f, z), R(1f, 1.8f));
        }

        // Tall pines and round trees growing out of the ravine, crowns near bridge height.
        var ravineTrees = Group("Ravine Trees", decorRoot);
        for (int i = 0; i < 46; i++)
        {
            float z = R(RavineStart + 3f, RavineEnd - 3f);
            float x = (rng.NextDouble() < 0.5 ? -1f : 1f) * R(10.5f, 22f);
            if (!IsClear(x, z)) continue;
            if (rng.NextDouble() < 0.6) PineTree(ravineTrees, new Vector3(x, RavineFloorY, z), R(1.7f, 2.4f));
            else RoundTree(ravineTrees, new Vector3(x, RavineFloorY, z), R(2.0f, 2.7f));
        }

        var village = Group("Forest Village", decorRoot);
        Scatter(village, RampDownEnd - 10f, 485f, 17f, 75f, 100, 36, 26, 36, 80, 22, 1.1f, 2.3f);
        // Dense backdrop behind the great tree
        for (int i = 0; i < 30; i++)
        {
            float x = R(-17f, 17f), z = R(445f, 485f);
            if (!IsClear(x, z)) continue;
            RandomTree(village, new Vector3(x, 0f, z), R(1.6f, 2.6f));
        }

        // Distant low-poly hills
        var hills = Group("Distant Hills", decorRoot);
        for (float z = -40f; z <= 520f; z += 55f)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float sc = R(40f, 60f);
                MeshObj("Hill", MeshFactory.Foliage(RI(0, 4)), hills, new Vector3(s * R(95f, 115f), -2f, z + R(-15f, 15f)),
                    new Vector3(sc, sc * 0.55f, sc), Hill, Quaternion.Euler(0f, R(0f, 360f), 0f), false);
            }
        }
        for (float x = -110f; x <= 110f; x += 45f)
        {
            float sc = R(50f, 70f);
            MeshObj("Hill", MeshFactory.Foliage(RI(0, 4)), hills, new Vector3(x, -2f, R(530f, 560f)),
                new Vector3(sc, sc * 0.6f, sc), Hill, Quaternion.identity, false);
        }
    }

    static void BuildFireflies()
    {
        for (int i = 0; i < 70; i++)
        {
            Vector3 p;
            if (i < 35) p = new Vector3(R(-20f, 20f), R(-6f, 4f), R(RavineStart, RavineEnd));
            else p = new Vector3(R(-18f, 18f), R(0.8f, 4f), R(330f, 440f));
            if (Mathf.Abs(p.x) < 4.5f && p.y > -1f) p.x += p.x < 0 ? -5f : 5f;
            var f = Prim(PrimitiveType.Sphere, "Firefly", fxRoot, p, Vector3.one * 0.12f, Firefly, default(Vector3), false, false);
            var b = f.AddComponent<Bobber>();
            b.amplitude = R(0.2f, 0.6f);
            b.speed = R(0.8f, 2f);
            b.phase = R(0f, 10f);
            b.drift = new Vector3(R(0.3f, 1f), 0f, R(0.3f, 1f));
        }
    }

    // =====================================================================
    // Wooden structures
    // =====================================================================
    static void Lantern(Transform parent, Vector3 pos, bool hanging)
    {
        var g = Group("Lantern", parent);
        g.localPosition = pos;
        if (hanging) Log("Lantern Rope", g, new Vector3(0f, 0.75f, 0f), new Vector3(0f, 0.3f, 0f), 0.025f, Rope, 4, false);
        Cube("Lantern Top", g, new Vector3(0f, 0.28f, 0f), new Vector3(0.36f, 0.08f, 0.36f), WoodDark, default(Vector3), false, false);
        Cube("Lantern Glow", g, Vector3.zero, new Vector3(0.26f, 0.42f, 0.26f), LanternGlow, default(Vector3), false, false);
        Cube("Lantern Base", g, new Vector3(0f, -0.24f, 0f), new Vector3(0.32f, 0.07f, 0.32f), WoodDark, default(Vector3), false, false);
    }

    static void PointLight(Transform parent, Vector3 pos, int rgb, float intensity, float range, bool flicker)
    {
        var g = new GameObject("Point Light");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = Hex(rgb);
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        if (flicker)
        {
            var f = g.AddComponent<LightFlicker>();
            f.baseIntensity = intensity;
            f.flicker = intensity * 0.25f;
        }
    }

    static void BuildEntranceGate()
    {
        var gate = Group("Village Entrance Gate", structRoot);
        float z = 12f;
        for (int s = -1; s <= 1; s += 2)
        {
            Log("Gate Post", gate, new Vector3(s * 4.9f, -0.2f, z), new Vector3(s * 4.9f, 6.8f, z), 0.48f, WoodDark, 8);
            Log("Gate Brace", gate, new Vector3(s * 6.6f, -0.1f, z), new Vector3(s * 5.1f, 2.6f, z), 0.2f, Wood);
            for (int i = 0; i < 4; i++)
                MeshObj("Vine", MeshFactory.Foliage(RI(0, 4)), gate, new Vector3(s * 4.9f + R(-0.3f, 0.3f), R(1.5f, 6f), z - 0.4f),
                    Vector3.one * R(0.6f, 1f), Pick(LeafMats), Quaternion.identity);
            MeshObj("Gate Leaves", MeshFactory.Foliage(RI(0, 4)), gate, new Vector3(s * 5.2f, 7.2f, z), Vector3.one * 2.3f, LeafMats[1], Quaternion.identity);
            Lantern(gate, new Vector3(s * 3.4f, 4.95f, z - 0.1f), true);
        }
        Log("Gate Beam", gate, new Vector3(-6.3f, 6.3f, z), new Vector3(6.3f, 6.3f, z), 0.42f, Wood, 8);
        Log("Gate Beam 2", gate, new Vector3(-5.2f, 5.75f, z), new Vector3(5.2f, 5.75f, z), 0.22f, WoodDark);
        Cube("Gate Sign Frame", gate, new Vector3(0f, 6.6f, z - 0.45f), new Vector3(6.6f, 1.45f, 0.2f), WoodDark);
        Cube("Gate Sign", gate, new Vector3(0f, 6.6f, z - 0.56f), new Vector3(6.2f, 1.15f, 0.06f), WoodLight);
        UIBuilder.WorldText(gate, "WELCOME TO TREE VILLAGE", new Vector3(0f, 6.6f, z - 0.6f), new Vector2(600f, 110f), 62, Hex(0x4A2C12), 0.01f);
        KeepClear(-5f, z, 4f);
        KeepClear(5f, z, 4f);
    }

    /// <summary>A tree with a wooden house on a round deck around its trunk.</summary>
    static void TreeHouse(Vector3 trunkBase, float deckY, float trunkRadius, Material roof, bool ladder)
    {
        var th = Group("Tree House", structRoot);
        th.localPosition = Vector3.zero;
        float x = trunkBase.x, z = trunkBase.z;
        float crownY = deckY + 9f;
        Vector3 facing = new Vector3(-Mathf.Sign(x == 0f ? 1f : x), 0f, 0f);
        if (Mathf.Abs(x) < 0.01f) facing = Vector3.back;

        // Trunk and roots
        MeshObj("Giant Trunk", MeshFactory.Frustum(8, 1f, 0.62f, 1f), th, trunkBase, new Vector3(trunkRadius, crownY - trunkBase.y, trunkRadius), Bark, Quaternion.identity);
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2f / 5f + 0.3f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Log("Root", th, trunkBase + dir * trunkRadius * 0.6f + Vector3.up * 1.4f, trunkBase + dir * trunkRadius * 2.1f - Vector3.up * 0.2f, trunkRadius * 0.28f, Bark, 6);
        }

        // Deck with railing
        float deckR = 4.5f;
        MeshObj("Deck", MeshFactory.Frustum(10, 1f, 1f, 1f), th, new Vector3(x, deckY - 0.35f, z), new Vector3(deckR, 0.35f, deckR), WoodLight, Quaternion.Euler(0f, 18f, 0f));
        MeshObj("Deck Rim", MeshFactory.Frustum(10, 1f, 1f, 1f), th, new Vector3(x, deckY - 0.55f, z), new Vector3(deckR + 0.15f, 0.25f, deckR + 0.15f), WoodDark, Quaternion.Euler(0f, 18f, 0f));
        Vector3 prevTop = Vector3.zero;
        for (int i = 0; i <= 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            Vector3 p = new Vector3(x + Mathf.Cos(a) * (deckR - 0.2f), deckY, z + Mathf.Sin(a) * (deckR - 0.2f));
            // leave an opening toward the path for the rope bridge
            bool opening = Vector3.Dot(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), facing) > 0.9f;
            if (!opening && i < 12) Cube("Rail Post", th, p + Vector3.up * 0.55f, new Vector3(0.16f, 1.1f, 0.16f), WoodDark);
            Vector3 top = p + Vector3.up * 1.05f;
            if (i > 0 && !opening)
            {
                float prevA = (i - 1) * Mathf.PI * 2f / 12f;
                bool prevOpening = Vector3.Dot(new Vector3(Mathf.Cos(prevA), 0f, Mathf.Sin(prevA)), facing) > 0.9f;
                if (!prevOpening) Log("Rail", th, prevTop, top, 0.07f, Wood, 5, false);
            }
            prevTop = top;
        }
        // Braces under deck
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f + 0.4f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Log("Brace", th, new Vector3(x, deckY - 3.2f, z) + dir * trunkRadius * 0.8f, new Vector3(x, deckY - 0.5f, z) + dir * (deckR - 0.8f), 0.18f, WoodDark);
        }

        // House
        var house = Group("House", th);
        house.localPosition = new Vector3(x, deckY, z) - facing * 0.4f;
        house.localRotation = Quaternion.LookRotation(facing);
        Cube("Walls", house, new Vector3(0f, 1.5f, 0f), new Vector3(4.2f, 3f, 3.6f), Wall);
        for (int cx = -1; cx <= 1; cx += 2)
            for (int cz = -1; cz <= 1; cz += 2)
                Cube("Corner Beam", house, new Vector3(cx * 2.08f, 1.5f, cz * 1.78f), new Vector3(0.22f, 3.05f, 0.22f), WoodDark);
        Cube("Wall Beam", house, new Vector3(0f, 3.0f, 0f), new Vector3(4.4f, 0.2f, 3.8f), WoodDark);
        MeshObj("Roof", MeshFactory.Roof(), house, new Vector3(0f, 3.0f, 0f), new Vector3(5.2f, 2.3f, 4.6f), roof, Quaternion.Euler(0f, 90f, 0f));
        Cube("Chimney", house, new Vector3(1.2f, 4.3f, -0.8f), new Vector3(0.5f, 1.4f, 0.5f), RockMat);
        Cube("Door", house, new Vector3(0f, 0.9f, 1.81f), new Vector3(1.0f, 1.8f, 0.08f), WoodDark);
        Cube("Door Knob", house, new Vector3(0.3f, 0.9f, 1.86f), new Vector3(0.1f, 0.1f, 0.06f), Gold, default(Vector3), false, false);
        for (int wx = -1; wx <= 1; wx += 2)
        {
            Cube("Window", house, new Vector3(wx * 1.35f, 1.75f, 1.81f), new Vector3(0.75f, 0.75f, 0.06f), WindowGlow);
            Cube("Window Frame H", house, new Vector3(wx * 1.35f, 1.75f, 1.84f), new Vector3(0.85f, 0.08f, 0.04f), WoodDark, default(Vector3), false, false);
            Cube("Window Frame V", house, new Vector3(wx * 1.35f, 1.75f, 1.84f), new Vector3(0.08f, 0.85f, 0.04f), WoodDark, default(Vector3), false, false);
            Cube("Side Window", house, new Vector3(wx * 2.11f, 1.75f, 0f), new Vector3(0.06f, 0.7f, 0.7f), WindowGlow);
        }
        Lantern(house, new Vector3(-1.0f, 2.4f, 2.2f), false);

        // Crown
        var leaves = LeafMats;
        var lm = leaves[RI(0, 4)];
        MeshObj("Crown", MeshFactory.Foliage(RI(0, 4)), th, new Vector3(x, crownY + 1.5f, z), Vector3.one * 9.5f, lm, Quaternion.Euler(0f, R(0f, 360f), 0f));
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2f / 5f;
            MeshObj("Crown", MeshFactory.Foliage(RI(0, 4)), th, new Vector3(x + Mathf.Cos(a) * 4f, crownY + R(-0.5f, 1.2f), z + Mathf.Sin(a) * 4f),
                Vector3.one * R(5.5f, 7f), i % 2 == 0 ? lm : leaves[RI(0, 4)], Quaternion.Euler(0f, R(0f, 360f), 0f));
        }

        if (ladder)
        {
            Vector3 lp = new Vector3(x, 0f, z) + facing * (deckR + 0.2f);
            Vector3 side = Vector3.Cross(facing, Vector3.up).normalized * 0.45f;
            Log("Ladder Rail", th, lp + side, lp + side + Vector3.up * deckY, 0.07f, Wood, 5);
            Log("Ladder Rail", th, lp - side, lp - side + Vector3.up * deckY, 0.07f, Wood, 5);
            for (float y = 0.4f; y < deckY; y += 0.5f)
                Log("Rung", th, lp + side + Vector3.up * y, lp - side + Vector3.up * y, 0.05f, WoodLight, 5, false);
        }

        KeepClear(x, z, 8f);
    }

    static void RopeBridge(Transform parent, Vector3 a, Vector3 b, float width, float sag)
    {
        var g = Group("Rope Bridge", parent);
        Vector3 d = b - a;
        Vector3 flat = new Vector3(d.x, 0f, d.z);
        float len = flat.magnitude;
        Quaternion rot = Quaternion.LookRotation(flat.normalized);
        Vector3 side = rot * Vector3.right * (width * 0.5f + 0.05f);
        int n = Mathf.Max(3, Mathf.CeilToInt(len / 0.55f));
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            Vector3 p = Vector3.Lerp(a, b, t) - Vector3.up * sag * Mathf.Sin(Mathf.PI * t);
            var plank = Cube("Plank", g, p, new Vector3(width * R(0.92f, 1.04f), 0.1f, 0.42f), i % 3 == 0 ? Wood : WoodLight);
            plank.transform.localRotation = rot * Quaternion.Euler(0f, R(-4f, 4f), 0f);
        }
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 off = side * s;
            Log("Bridge Post", g, a + off - Vector3.up * 0.3f, a + off + Vector3.up * 1.3f, 0.1f, WoodDark, 6);
            Log("Bridge Post", g, b + off - Vector3.up * 0.3f, b + off + Vector3.up * 1.3f, 0.1f, WoodDark, 6);
            RopeLine(g, a + off + Vector3.up * 1.15f, b + off + Vector3.up * 1.15f, sag * 0.7f, 6);
            RopeLine(g, a + off, b + off, sag, 6, 0.035f);
            for (int i = 1; i < 6; i++)
            {
                float t = i / 6f;
                Vector3 low = Vector3.Lerp(a, b, t) + off - Vector3.up * sag * Mathf.Sin(Mathf.PI * t);
                Vector3 high = Vector3.Lerp(a, b, t) + off + Vector3.up * 1.15f - Vector3.up * sag * 0.7f * Mathf.Sin(Mathf.PI * t);
                Log("Rope", g, low, high, 0.03f, Rope, 4, false);
            }
        }
    }

    static void BuildRamp(string name, float zA, float yA, float zB, float yB)
    {
        var g = Group(name, structRoot);
        Vector3 a = new Vector3(0f, yA, zA), b = new Vector3(0f, yB, zB);
        Vector3 d = b - a;
        float len = d.magnitude;
        Quaternion rot = Quaternion.LookRotation(d / len, Vector3.up);
        Vector3 normal = rot * Vector3.up;
        Vector3 center = (a + b) * 0.5f - normal * 0.2f;
        var ramp = Cube("Ramp Deck", g, center, new Vector3(7.2f, 0.4f, len + 0.1f), Wood, default(Vector3), true);
        ramp.transform.localRotation = rot;
        // plank lines
        for (float t = 0.5f; t < len; t += 0.8f)
        {
            Vector3 p = a + d / len * t + normal * 0.01f;
            var line = Cube("Plank Line", g, p, new Vector3(7.0f, 0.02f, 0.06f), WoodDark, default(Vector3), false, false);
            line.transform.localRotation = rot;
        }
        // rails and supports
        for (float t = 0f; t <= len + 0.01f; t += len / 5f)
        {
            Vector3 p = a + d / len * t;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 post = p + new Vector3(s * 3.65f, 0f, 0f);
                Log("Rail Post", g, post - Vector3.up * 0.3f, post + Vector3.up * 1.15f, 0.1f, WoodDark, 6);
                if (p.y > 0.5f) Log("Support", g, new Vector3(post.x, -0.1f, post.z), post - Vector3.up * 0.3f, 0.2f, WoodDark, 6);
            }
        }
        for (int s = -1; s <= 1; s += 2)
            Log("Hand Rail", g, a + new Vector3(s * 3.65f, 1.05f, 0f), b + new Vector3(s * 3.65f, 1.05f, 0f), 0.08f, Wood, 6);
    }

    static void Platform(float z0, float z1, float width, int bridgeSide)
    {
        var g = Group("Platform " + z0, structRoot);
        float len = z1 - z0, mid = (z0 + z1) * 0.5f, hw = width * 0.5f;
        Box("Platform Collider", g, new Vector3(0f, Elev - 0.3f, mid), new Vector3(width, 0.6f, len));
        Cube("Deck", g, new Vector3(0f, Elev - 0.2f, mid), new Vector3(width, 0.4f, len), WoodLight);
        Cube("Edge Beam", g, new Vector3(-hw + 0.15f, Elev - 0.25f, mid), new Vector3(0.35f, 0.5f, len + 0.1f), WoodDark);
        Cube("Edge Beam", g, new Vector3(hw - 0.15f, Elev - 0.25f, mid), new Vector3(0.35f, 0.5f, len + 0.1f), WoodDark);
        for (float z = z0 + 0.5f; z < z1; z += 0.9f)
            Cube("Plank Line", g, new Vector3(0f, Elev + 0.005f, z), new Vector3(width - 0.8f, 0.02f, 0.05f), Wood, default(Vector3), false, false);

        // Pillars down to the ravine floor + cross braces
        for (int sx = -1; sx <= 1; sx += 2)
        {
            Vector3 pa = new Vector3(sx * (hw - 0.6f), RavineFloorY, z0 + 0.7f);
            Vector3 pb = new Vector3(sx * (hw - 0.6f), RavineFloorY, z1 - 0.7f);
            Log("Pillar", g, pa, pa + Vector3.up * (Elev - 0.4f - RavineFloorY), 0.45f, WoodDark, 8);
            Log("Pillar", g, pb, pb + Vector3.up * (Elev - 0.4f - RavineFloorY), 0.45f, WoodDark, 8);
            Log("Cross Brace", g, pa + Vector3.up * 1.5f, pb + Vector3.up * 9f, 0.18f, Wood, 6);
            Log("Cross Brace", g, pb + Vector3.up * 1.5f, pa + Vector3.up * 9f, 0.18f, Wood, 6);
        }

        // Railings (with an opening where a side rope bridge attaches)
        for (int sx = -1; sx <= 1; sx += 2)
        {
            float x = sx * (hw - 0.25f);
            bool hasBridge = bridgeSide == sx || bridgeSide == 2;
            var posts = new List<float>();
            for (float z = z0 + 0.4f; z <= z1 - 0.4f + 0.01f; z += (len - 0.8f) / Mathf.Round((len - 0.8f) / 2.4f))
            {
                if (hasBridge && Mathf.Abs(z - mid) < 1.4f) { posts.Add(float.NaN); continue; }
                posts.Add(z);
                Cube("Rail Post", g, new Vector3(x, Elev + 0.55f, z), new Vector3(0.2f, 1.1f, 0.2f), WoodDark);
            }
            for (int i = 1; i < posts.Count; i++)
            {
                if (float.IsNaN(posts[i]) || float.IsNaN(posts[i - 1])) continue;
                float zm = (posts[i] + posts[i - 1]) * 0.5f, zl = posts[i] - posts[i - 1];
                Cube("Rail", g, new Vector3(x, Elev + 1.05f, zm), new Vector3(0.14f, 0.14f, zl), Wood);
                Cube("Rail Low", g, new Vector3(x, Elev + 0.55f, zm), new Vector3(0.1f, 0.1f, zl), Wood, default(Vector3), false, false);
            }
            if (hasBridge)
            {
                Vector3 start = new Vector3(sx * hw, Elev - 0.05f, mid);
                Vector3 end = new Vector3(sx * 14.6f, Elev + 0.45f, mid);
                RopeBridge(g, start, end, 2f, 0.7f);
            }
            // Corner lantern posts
            for (int sz = 0; sz <= 1; sz++)
            {
                float z = sz == 0 ? z0 + 0.4f : z1 - 0.4f;
                Log("Lantern Post", g, new Vector3(x, Elev, z), new Vector3(x, Elev + 2.2f, z), 0.12f, WoodDark, 6);
                Lantern(g, new Vector3(x, Elev + 2.45f, z), false);
            }
        }
    }

    static void BridgeSegment(Transform parent, float z0, float z1, bool brokenStart, bool brokenEnd)
    {
        var g = Group("Bridge " + z0 + "-" + z1, parent);
        float len = z1 - z0, mid = (z0 + z1) * 0.5f;
        Box("Bridge Collider", g, new Vector3(0f, Elev - 0.3f, mid), new Vector3(7.2f, 0.6f, len));
        for (float z = z0 + 0.3f; z < z1 - 0.05f; z += 0.62f)
        {
            var p = Cube("Plank", g, new Vector3(R(-0.1f, 0.1f), Elev - 0.09f, z), new Vector3(7.0f + R(-0.2f, 0.3f), 0.18f, 0.54f),
                rng.NextDouble() < 0.5 ? Wood : WoodLight);
            p.transform.localRotation = Quaternion.Euler(0f, R(-2.5f, 2.5f), 0f);
        }
        Cube("Stringer", g, new Vector3(-3.0f, Elev - 0.3f, mid), new Vector3(0.3f, 0.3f, len), WoodDark);
        Cube("Stringer", g, new Vector3(3.0f, Elev - 0.3f, mid), new Vector3(0.3f, 0.3f, len), WoodDark);

        // Rope railings
        int posts = Mathf.Max(1, Mathf.RoundToInt(len / 4f));
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * 3.65f;
            for (int i = 0; i <= posts; i++)
            {
                float z = Mathf.Lerp(z0 + 0.3f, z1 - 0.3f, i / (float)posts);
                Log("Rope Post", g, new Vector3(x, Elev - 0.5f, z), new Vector3(x, Elev + 1.25f, z), 0.11f, WoodDark, 6);
                if (i > 0)
                {
                    float zp = Mathf.Lerp(z0 + 0.3f, z1 - 0.3f, (i - 1) / (float)posts);
                    RopeLine(g, new Vector3(x, Elev + 1.15f, zp), new Vector3(x, Elev + 1.15f, z), 0.18f, 4);
                    RopeLine(g, new Vector3(x, Elev + 0.6f, zp), new Vector3(x, Elev + 0.6f, z), 0.12f, 3, 0.035f);
                }
            }
        }

        // Broken, dangling planks at the edges of a gap
        if (brokenEnd) BrokenEdge(g, z1, 1f);
        if (brokenStart) BrokenEdge(g, z0, -1f);
    }

    static void BrokenEdge(Transform parent, float zEdge, float dir)
    {
        for (int i = 0; i < 3; i++)
        {
            float x = R(-2.8f, 2.8f);
            float ang = R(45f, 75f) * dir;
            var p = Cube("Broken Plank", parent, new Vector3(x, Elev - 0.7f, zEdge + dir * 0.15f), new Vector3(R(0.4f, 0.8f), 0.12f, 1.3f), WoodLight);
            p.transform.localRotation = Quaternion.Euler(ang, R(-20f, 20f), R(-10f, 10f));
        }
        // Snapped stringer ends
        Log("Snapped Rope", parent, new Vector3(-3.65f, Elev + 1.15f, zEdge), new Vector3(-3.4f, Elev - 0.8f, zEdge + dir * 0.5f), 0.04f, Rope, 4, false);
        Log("Snapped Rope", parent, new Vector3(3.65f, Elev + 1.15f, zEdge), new Vector3(3.5f, Elev - 0.9f, zEdge + dir * 0.4f), 0.04f, Rope, 4, false);
    }

    static void Bridge(float z0, float z1, float[] gaps)
    {
        var g = Group("Bridge " + z0, structRoot);
        float cur = z0;
        bool brokenStart = false;
        if (gaps != null)
        {
            for (int i = 0; i < gaps.Length; i += 2)
            {
                BridgeSegment(g, cur, gaps[i], brokenStart, true);
                cur = gaps[i + 1];
                brokenStart = true;
            }
        }
        BridgeSegment(g, cur, z1, brokenStart, false);
    }

    // Elevated route over the ravine
    static readonly float[][] platformsDef =
    {
        new float[] { 95f, 110f, 0f },
        new float[] { 135f, 148f, -1f },
        new float[] { 180f, 195f, 1f },
        new float[] { 235f, 250f, 2f },
        new float[] { 290f, 305f, 0f },
    };

    static void BuildElevatedWalkway()
    {
        foreach (var p in platformsDef)
        {
            int side = (int)p[2];
            Platform(p[0], p[1], 10f, side);
            float mid = (p[0] + p[1]) * 0.5f;
            if (side == -1 || side == 2) TreeHouse(new Vector3(-19f, RavineFloorY, mid), Elev + 0.5f, 1.8f, RoofRed, false);
            if (side == 1 || side == 2) TreeHouse(new Vector3(19f, RavineFloorY, mid), Elev + 0.5f, 1.8f, side == 2 ? RoofGreen : RoofBrown, false);
        }
        Bridge(110f, 135f, new[] { 121f, 124f });
        Bridge(148f, 180f, new[] { 168f, 171f });
        Bridge(195f, 235f, new[] { 205f, 208f, 222f, 225f });
        Bridge(250f, 290f, new[] { 272f, 275f });

    }

    // =====================================================================
    // Village (final destination)
    // =====================================================================
    static void Cottage(Vector3 pos, Material roof)
    {
        var c = Group("Cottage", structRoot);
        c.localPosition = pos;
        Vector3 facing = new Vector3(-Mathf.Sign(pos.x), 0f, 0f);
        c.localRotation = Quaternion.LookRotation(facing);
        Cube("Foundation", c, new Vector3(0f, 0.2f, 0f), new Vector3(5f, 0.4f, 4.4f), RockMat);
        Cube("Walls", c, new Vector3(0f, 1.9f, 0f), new Vector3(4.6f, 3f, 4f), Wall);
        for (int cx = -1; cx <= 1; cx += 2)
            for (int cz = -1; cz <= 1; cz += 2)
                Cube("Corner Beam", c, new Vector3(cx * 2.28f, 1.9f, cz * 1.98f), new Vector3(0.24f, 3.05f, 0.24f), WoodDark);
        Cube("Beam", c, new Vector3(0f, 3.4f, 0f), new Vector3(4.8f, 0.2f, 4.2f), WoodDark);
        MeshObj("Roof", MeshFactory.Roof(), c, new Vector3(0f, 3.4f, 0f), new Vector3(5.6f, 2.6f, 5.2f), roof, Quaternion.Euler(0f, 90f, 0f));
        Cube("Chimney", c, new Vector3(-1.4f, 4.6f, -0.6f), new Vector3(0.6f, 1.8f, 0.6f), RockDark);
        Cube("Door", c, new Vector3(0f, 1.3f, 2.01f), new Vector3(1.1f, 1.9f, 0.08f), WoodDark);
        Cube("Step", c, new Vector3(0f, 0.15f, 2.5f), new Vector3(1.6f, 0.3f, 0.8f), RockMat);
        for (int wx = -1; wx <= 1; wx += 2)
        {
            Cube("Window", c, new Vector3(wx * 1.45f, 2.1f, 2.01f), new Vector3(0.8f, 0.8f, 0.06f), WindowGlow);
            Cube("Shutter", c, new Vector3(wx * 1.45f - 0.55f, 2.1f, 2.03f), new Vector3(0.28f, 0.85f, 0.05f), RoofGreen);
            Cube("Shutter", c, new Vector3(wx * 1.45f + 0.55f, 2.1f, 2.03f), new Vector3(0.28f, 0.85f, 0.05f), RoofGreen);
            Cube("Flower Box", c, new Vector3(wx * 1.45f, 1.6f, 2.12f), new Vector3(0.9f, 0.2f, 0.25f), Wood);
            for (int f = 0; f < 3; f++)
                Prim(PrimitiveType.Sphere, "Flower", c, new Vector3(wx * 1.45f + (f - 1) * 0.28f, 1.78f, 2.12f), Vector3.one * 0.2f, FlowerMats[(f + wx + 2) % 4], default(Vector3), false, false);
        }
        // Barrel & crate
        Prim(PrimitiveType.Cylinder, "Barrel", c, new Vector3(2.9f, 0.55f, 1.3f), new Vector3(0.8f, 0.55f, 0.8f), Wood);
        Cube("Crate", c, new Vector3(2.9f, 0.4f, 0.2f), new Vector3(0.8f, 0.8f, 0.8f), WoodLight, new Vector3(0f, 20f, 0f));
        KeepClear(pos.x, pos.z, 6f);
    }

    static void Bunting(Vector3 a, Vector3 b)
    {
        var g = Group("Bunting", structRoot);
        RopeLine(g, a, b, 0.6f, 8, 0.03f);
        var flags = BunMats;
        int n = 14;
        for (int i = 1; i < n; i++)
        {
            float t = i / (float)n;
            Vector3 p = Vector3.Lerp(a, b, t) - Vector3.up * (0.6f * Mathf.Sin(Mathf.PI * t) + 0.25f);
            Cube("Flag", g, p, new Vector3(0.32f, 0.32f, 0.03f), flags[i % flags.Length], new Vector3(0f, 0f, 45f), false, false);
        }
    }

    static void BuildVillage()
    {
        Cottage(new Vector3(-11.5f, 0f, 342f), RoofRed);
        Cottage(new Vector3(11.5f, 0f, 354f), RoofGreen);
        Cottage(new Vector3(-11.5f, 0f, 368f), RoofBrown);
        Cottage(new Vector3(11.5f, 0f, 378f), RoofRed);
        Cottage(new Vector3(-12f, 0f, 400f), RoofGreen);
        Cottage(new Vector3(12f, 0f, 404f), RoofBrown);

        // Fences along the village path
        var fence = Group("Fences", structRoot);
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * 4.8f;
            float prev = float.NaN;
            for (float z = 324f; z <= 384f; z += 2.5f)
            {
                Cube("Fence Post", fence, new Vector3(x, 0.5f, z), new Vector3(0.18f, 1.0f, 0.18f), WoodDark);
                if (!float.IsNaN(prev))
                {
                    Cube("Fence Rail", fence, new Vector3(x, 0.75f, (z + prev) * 0.5f), new Vector3(0.09f, 0.12f, 2.5f), Wood);
                    Cube("Fence Rail", fence, new Vector3(x, 0.4f, (z + prev) * 0.5f), new Vector3(0.09f, 0.12f, 2.5f), Wood);
                }
                prev = z;
            }
        }

        // Lantern posts and bunting
        var lamps = Group("Lantern Posts", structRoot);
        for (float z = 330f; z <= 382f; z += 13f)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 5.6f;
                Log("Lamp Post", lamps, new Vector3(x, 0f, z), new Vector3(x, 4.4f, z), 0.13f, WoodDark, 6);
                Log("Lamp Arm", lamps, new Vector3(x, 4.2f, z), new Vector3(x - s * 0.9f, 4.2f, z), 0.06f, WoodDark, 5);
                Lantern(lamps, new Vector3(x - s * 0.9f, 3.45f, z), true);
            }
            Bunting(new Vector3(-5.6f, 4.3f, z), new Vector3(5.6f, 4.3f, z));
        }
        PointLight(lamps, new Vector3(0f, 3.3f, 369f), 0xFFC46A, 1.6f, 12f, false);

        // Campfire
        var fire = Group("Campfire", structRoot);
        fire.localPosition = new Vector3(-7.5f, 0f, 414f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            MeshObj("Fire Stone", MeshFactory.Rock(i % 4), fire, new Vector3(Mathf.Cos(a) * 1.1f, 0.15f, Mathf.Sin(a) * 1.1f), new Vector3(0.6f, 0.4f, 0.5f), RockDark, Quaternion.Euler(0f, a * 57f, 0f));
        }
        for (int i = 0; i < 3; i++)
        {
            float a = i * 2.1f;
            Log("Fire Log", fire, new Vector3(Mathf.Cos(a) * 0.7f, 0.1f, Mathf.Sin(a) * 0.7f), new Vector3(-Mathf.Cos(a) * 0.2f, 0.45f, -Mathf.Sin(a) * 0.2f), 0.13f, Bark, 6);
        }
        var cone = MeshFactory.Frustum(6, 0.5f, 0f, 1f);
        var f1 = MeshObj("Flame", cone, fire, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 1.4f, 0.9f), Flame, Quaternion.identity, false);
        f1.AddComponent<Bobber>().amplitude = 0.05f;
        MeshObj("Flame Core", cone, fire, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.9f, 0.5f), FlameCore, Quaternion.Euler(0f, 30f, 0f), false);
        PointLight(fire, new Vector3(0f, 1.2f, 0f), 0xFF9A40, 2.2f, 14f, true);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 2.1f + 0.5f;
            Log("Seat Log", fire, new Vector3(Mathf.Cos(a) * 2.6f - 0.9f * Mathf.Sin(a), 0.3f, Mathf.Sin(a) * 2.6f + 0.9f * Mathf.Cos(a)),
                new Vector3(Mathf.Cos(a) * 2.6f + 0.9f * Mathf.Sin(a), 0.3f, Mathf.Sin(a) * 2.6f - 0.9f * Mathf.Cos(a)), 0.3f, Wood, 7);
        }
        KeepClear(-7.5f, 414f, 4f);

        // The Great Tree - the village heart and final destination
        TreeHouse(new Vector3(0f, 0f, 428f), 6.5f, 3.2f, RoofRed, true);
        Box("Great Tree Blocker", structRoot, new Vector3(0f, 2f, 426f), new Vector3(10f, 4f, 2f));
        TreeHouse(new Vector3(-16f, 0f, 420f), 5.5f, 1.6f, RoofGreen, true);
        TreeHouse(new Vector3(16f, 0f, 420f), 5.5f, 1.6f, RoofBrown, true);
        RopeBridge(structRoot, new Vector3(-4.6f, 6.5f, 427f), new Vector3(-11.6f, 5.5f, 421f), 1.6f, 0.6f);
        RopeBridge(structRoot, new Vector3(4.6f, 6.5f, 427f), new Vector3(11.6f, 5.5f, 421f), 1.6f, 0.6f);

        // Ground-level tree houses along the forest path
        TreeHouse(new Vector3(-15f, 0f, 48f), 5f, 1.5f, RoofRed, true);
        TreeHouse(new Vector3(16f, 0f, 72f), 5.5f, 1.6f, RoofGreen, true);
        TreeHouse(new Vector3(15f, 0f, 26f), 4.5f, 1.4f, RoofBrown, true);
    }

    static void BuildFinish(Transform gameplay)
    {
        var arch = Group("Finish Arch", structRoot);
        float z = FinishZ;
        for (int s = -1; s <= 1; s += 2)
        {
            Log("Arch Post", arch, new Vector3(s * 5f, -0.2f, z), new Vector3(s * 5f, 7f, z), 0.5f, WoodDark, 8);
            MeshObj("Arch Leaves", MeshFactory.Foliage(RI(0, 4)), arch, new Vector3(s * 5.3f, 7.4f, z), Vector3.one * 2.6f, LeafMats[3], Quaternion.identity);
            Lantern(arch, new Vector3(s * 3.6f, 5.0f, z - 0.2f), true);
        }
        Log("Arch Beam", arch, new Vector3(-6.4f, 6.5f, z), new Vector3(6.4f, 6.5f, z), 0.45f, Wood, 8);
        Cube("Finish Sign Frame", arch, new Vector3(0f, 6.8f, z - 0.5f), new Vector3(6.4f, 1.6f, 0.22f), WoodDark);
        Cube("Finish Sign", arch, new Vector3(0f, 6.8f, z - 0.62f), new Vector3(6.0f, 1.3f, 0.06f), Gold);
        UIBuilder.WorldText(arch, "TREE VILLAGE", new Vector3(0f, 6.8f, z - 0.66f), new Vector2(560f, 120f), 84, Hex(0x4A2C12), 0.01f);
        Bunting(new Vector3(-5f, 5.6f, z - 0.2f), new Vector3(5f, 5.6f, z - 0.2f));
        // Finish banner on the ground
        for (int i = -3; i <= 3; i++)
            Cube("Finish Line", arch, new Vector3(i * 1f, 0.05f, z), new Vector3(1f, 0.04f, 0.6f), i % 2 == 0 ? Cream : WoodDark, default(Vector3), false, false);

        var trigger = new GameObject("Finish Trigger");
        trigger.transform.SetParent(gameplay, false);
        trigger.transform.localPosition = new Vector3(0f, 2f, z + 0.5f);
        var bc = trigger.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(10f, 5f, 1f);
        var rb = trigger.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        trigger.AddComponent<FinishLine>();
        KeepClear(0f, z, 7f);
    }

    // =====================================================================
    // Obstacles & coins
    // =====================================================================
    static Transform ObstacleRoot(string name, Vector3 pos, string message, Vector3 colliderSize, float colliderCenterY)
    {
        var g = new GameObject(name);
        g.transform.SetParent(obstacleRoot, false);
        g.transform.localPosition = pos;
        var bc = g.AddComponent<BoxCollider>();
        bc.size = colliderSize;
        bc.center = new Vector3(0f, colliderCenterY, 0f);
        g.AddComponent<Obstacle>().hitMessage = message;
        return g.transform;
    }

    static void FallenLog(float z)
    {
        float y = SurfaceY(z);
        var t = ObstacleRoot("Obstacle - Fallen Log", new Vector3(0f, y, z), "You tripped over a fallen log!", new Vector3(6.8f, 0.7f, 0.7f), 0.35f);
        Log("Log", t, new Vector3(-3.5f, 0.36f, 0f), new Vector3(3.5f, 0.36f, 0f), 0.36f, Bark, 8);
        MeshObj("Log End", MeshFactory.Frustum(8, 1f, 1f, 1f), t, new Vector3(3.48f, 0.36f, 0f), new Vector3(0.3f, 0.05f, 0.3f), WoodLight, Quaternion.Euler(0f, 0f, -90f), false);
        MeshObj("Log End", MeshFactory.Frustum(8, 1f, 1f, 1f), t, new Vector3(-3.48f, 0.36f, 0f), new Vector3(0.3f, 0.05f, 0.3f), WoodLight, Quaternion.Euler(0f, 0f, 90f), false);
        Log("Branch", t, new Vector3(1.2f, 0.5f, 0f), new Vector3(1.7f, 1.0f, 0.3f), 0.08f, Bark, 5, false);
        MeshObj("Moss", MeshFactory.Blob(7, 3, 6, 0.1f), t, new Vector3(-1.2f, 0.7f, 0f), new Vector3(1.4f, 0.25f, 0.6f), Moss, Quaternion.identity, false);
        MeshObj("Leaves", MeshFactory.Foliage(1), t, new Vector3(1.75f, 1.05f, 0.3f), Vector3.one * 0.5f, LeafMats[1], Quaternion.identity, false);
    }

    static void LogPile(float z, int lane)
    {
        float y = SurfaceY(z), x = lane * Lane;
        var t = ObstacleRoot("Obstacle - Log Pile", new Vector3(x, y, z), "You ran into a pile of logs!", new Vector3(1.6f, 1.25f, 0.95f), 0.62f);
        Log("Log", t, new Vector3(-0.8f, 0.3f, -0.22f), new Vector3(0.8f, 0.3f, -0.22f), 0.3f, Bark, 7);
        Log("Log", t, new Vector3(-0.8f, 0.3f, 0.22f), new Vector3(0.8f, 0.3f, 0.22f), 0.3f, Wood, 7);
        Log("Log", t, new Vector3(-0.75f, 0.82f, 0f), new Vector3(0.75f, 0.82f, 0f), 0.3f, Bark, 7);
    }

    static void RockObstacle(float z, int lane)
    {
        float y = SurfaceY(z), x = lane * Lane;
        var t = ObstacleRoot("Obstacle - Rock", new Vector3(x, y, z), "You smashed into a rock!", new Vector3(1.4f, 1.35f, 1.15f), 0.67f);
        MeshObj("Boulder", MeshFactory.Rock(RI(0, 4)), t, new Vector3(0f, 0.62f, 0f), new Vector3(1.55f, 1.4f, 1.3f), RockMat, Quaternion.Euler(0f, R(0f, 360f), 0f));
        MeshObj("Moss", MeshFactory.Blob(7, 3, 6, 0.1f), t, new Vector3(0.1f, 1.22f, 0f), new Vector3(0.8f, 0.25f, 0.7f), Moss, Quaternion.identity, false);
        MeshObj("Pebble", MeshFactory.Rock(1), t, new Vector3(0.75f, 0.12f, 0.4f), new Vector3(0.45f, 0.3f, 0.4f), RockDark, Quaternion.identity, false);
    }

    static void Roots(float z, int laneFrom, int laneTo)
    {
        float y = SurfaceY(z);
        float x0 = laneFrom * Lane - 1f, x1 = laneTo * Lane + 1f;
        float cx = (x0 + x1) * 0.5f, w = x1 - x0;
        var t = ObstacleRoot("Obstacle - Tree Roots", new Vector3(cx, y, z), "You tripped over the tree roots!", new Vector3(w, 0.45f, 0.8f), 0.22f);
        for (int r = 0; r < 2; r++)
        {
            float zo = r == 0 ? -0.2f : 0.2f;
            int segs = 8;
            Vector3 prev = new Vector3(-w * 0.5f - 0.4f, 0.02f, zo);
            for (int i = 1; i <= segs; i++)
            {
                float tt = i / (float)segs;
                float px = Mathf.Lerp(-w * 0.5f - 0.4f, w * 0.5f + 0.4f, tt);
                float py = 0.08f + Mathf.Abs(Mathf.Sin(tt * Mathf.PI * (2f + r))) * 0.3f;
                Vector3 p = new Vector3(px, py, zo + Mathf.Sin(tt * 7f + r) * 0.12f);
                Log("Root", t, prev, p, r == 0 ? 0.15f : 0.12f, Bark, 6);
                prev = p;
            }
        }
        MeshObj("Moss", MeshFactory.Blob(7, 3, 6, 0.1f), t, new Vector3(0f, 0.1f, 0f), new Vector3(0.8f, 0.2f, 0.6f), Moss, Quaternion.identity, false);
    }

    static void CoinAt(Vector3 p)
    {
        var g = new GameObject("Coin");
        g.transform.SetParent(coinRoot, false);
        g.transform.localPosition = p;
        var sc = g.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.65f;
        var rb = g.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        Prim(PrimitiveType.Cylinder, "Coin Body", g.transform, Vector3.zero, new Vector3(0.75f, 0.045f, 0.75f), Gold, new Vector3(90f, 0f, 0f), false, true);
        Prim(PrimitiveType.Cylinder, "Coin Face", g.transform, Vector3.zero, new Vector3(0.48f, 0.06f, 0.48f), GoldDeep, new Vector3(90f, 0f, 0f), false, false);
        Cube("Coin Mark", g.transform, Vector3.zero, new Vector3(0.12f, 0.3f, 0.13f), Gold, default(Vector3), false, false);
        g.AddComponent<Coin>();
        if (ctx.coinTemplate == null) ctx.coinTemplate = g;
    }

    static void CoinLine(int lane, float z0, float z1, float spacing = 2f)
    {
        for (float z = z0; z <= z1 + 0.01f; z += spacing)
            CoinAt(new Vector3(lane * Lane, SurfaceY(z) + 1f, z));
    }

    static void CoinArc(int lane, float zc, int count = 5, float half = 3f, float height = 1.5f)
    {
        float y0 = SurfaceY(zc);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float z = zc - half + t * half * 2f;
            float y = y0 + 1f + height * Mathf.Sin(Mathf.PI * t);
            CoinAt(new Vector3(lane * Lane, y, z));
        }
    }

    static void BuildObstaclesAndCoins()
    {
        // ---------- Forest path (ground level) ----------
        CoinLine(0, 16f, 24f);
        RockObstacle(26f, -1);
        FallenLog(34f); CoinArc(0, 34f);
        RockObstacle(44f, 0); RockObstacle(44f, 1); CoinLine(-1, 39f, 49f);
        Roots(53f, -1, 0); CoinLine(1, 50f, 56f);
        FallenLog(61f); CoinArc(1, 61f);
        RockObstacle(69f, -1); RockObstacle(69f, 1); CoinLine(0, 65f, 73f);
        LogPile(76f, 0); CoinLine(-1, 74f, 78f);
        CoinLine(0, 83f, 93f, 2.5f); // up the ramp

        // ---------- Tree-top walkway ----------
        CoinLine(0, 100f, 106f, 3f);
        CoinLine(0, 112f, 118f);
        CoinArc(0, 122.5f, 5, 3.2f, 1.4f);                     // gap 121-124
        RockObstacle(128f, 1);
        FallenLog(131.5f); CoinArc(-1, 131.5f);
        RockObstacle(141f, -1); RockObstacle(141f, 0); CoinLine(1, 137f, 146f);
        FallenLog(155f); CoinArc(1, 155f);
        Roots(161f, 0, 1); CoinLine(-1, 158f, 164f);
        CoinArc(0, 169.5f, 5, 3.2f, 1.4f);                     // gap 168-171
        LogPile(176f, -1); CoinLine(1, 174f, 178f);
        RockObstacle(185f, 1); RockObstacle(191f, -1); CoinLine(0, 183f, 193f);
        FallenLog(198f); CoinArc(0, 198f);
        CoinArc(1, 206.5f, 5, 3.2f, 1.4f);                     // gap 205-208
        Roots(215.5f, -1, 1); CoinArc(-1, 215.5f, 4, 2.5f, 1.2f);
        CoinArc(0, 223.5f, 5, 3.2f, 1.4f);                     // gap 222-225
        RockObstacle(230f, 0); RockObstacle(230f, -1); CoinLine(1, 227f, 233f);
        LogPile(243f, 0); CoinLine(1, 238f, 248f, 2.5f); CoinLine(-1, 238f, 248f, 2.5f);
        RockObstacle(256f, -1); RockObstacle(262f, 1); CoinLine(0, 253f, 263f);
        FallenLog(265f); CoinArc(0, 265f);
        CoinArc(0, 273.5f, 5, 3.2f, 1.4f);                     // gap 272-275
        Roots(282f, -1, 0); CoinLine(1, 279f, 286f);
        CoinLine(0, 292f, 302f, 2.5f);
        CoinLine(0, 308f, 318f, 2.5f); // down the ramp

        // ---------- Village approach ----------
        FallenLog(330f); CoinArc(0, 330f);
        Roots(340f, 0, 1); CoinLine(-1, 336f, 344f);
        RockObstacle(350f, -1); CoinLine(1, 348f, 352f);
        // Celebration zig-zag of coins to the finish
        for (int i = 0; i < 14; i++)
        {
            float z = 356f + i * 2.2f;
            int lane = Mathf.RoundToInt(Mathf.Sin(i * 0.55f) * 1.2f);
            CoinAt(new Vector3(Mathf.Clamp(lane, -1, 1) * Lane, 1f, z));
        }

    }

    // =====================================================================
    // Player, camera, audio
    // =====================================================================
    static AudioManager BuildAudio()
    {
        var g = new GameObject("AudioManager");
        var am = g.AddComponent<AudioManager>();
        am.music = ctx.music;
        am.coinClip = ctx.coin;
        am.jumpClip = ctx.jump;
        am.hitClip = ctx.hit;
        am.winClip = ctx.win;
        am.clickClip = ctx.click;
        return am;
    }

    static Transform Pivot(string name, Transform parent, Vector3 pos)
    {
        var t = Group(name, parent);
        t.localPosition = pos;
        return t;
    }

    static PlayerController BuildPlayer(AudioManager audio)
    {
        var p = new GameObject("Player");
        p.transform.position = new Vector3(0f, 0.1f, StartZ);

        var cc = p.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.92f, 0f);
        cc.stepOffset = 0.3f;
        cc.slopeLimit = 50f;
        cc.skinWidth = 0.05f;
        cc.minMoveDistance = 0f;

        var model = Group("Model", p.transform);
        // Torso
        Cube("Torso", model, new Vector3(0f, 1.08f, 0f), new Vector3(0.66f, 0.72f, 0.4f), Tunic);
        Cube("Belt", model, new Vector3(0f, 0.76f, 0f), new Vector3(0.7f, 0.12f, 0.43f), WoodDark);
        Cube("Buckle", model, new Vector3(0f, 0.76f, 0.22f), new Vector3(0.14f, 0.1f, 0.03f), Gold, default(Vector3), false, false);
        Cube("Scarf", model, new Vector3(0f, 1.44f, 0f), new Vector3(0.5f, 0.12f, 0.44f), Scarf);
        Cube("Scarf Tail", model, new Vector3(0.12f, 1.28f, -0.24f), new Vector3(0.14f, 0.3f, 0.05f), Scarf, new Vector3(-15f, 0f, 0f), false, false);
        Cube("Backpack", model, new Vector3(0f, 1.1f, -0.3f), new Vector3(0.5f, 0.55f, 0.22f), Wood);
        Cube("Backpack Flap", model, new Vector3(0f, 1.3f, -0.42f), new Vector3(0.52f, 0.18f, 0.04f), WoodDark, default(Vector3), false, false);
        // Head
        var head = Pivot("Head", model, new Vector3(0f, 1.55f, 0f));
        Prim(PrimitiveType.Sphere, "Face", head, new Vector3(0f, 0.2f, 0f), new Vector3(0.48f, 0.5f, 0.46f), Skin);
        Prim(PrimitiveType.Sphere, "Eye L", head, new Vector3(-0.1f, 0.24f, 0.21f), new Vector3(0.07f, 0.09f, 0.05f), Black, default(Vector3), false, false);
        Prim(PrimitiveType.Sphere, "Eye R", head, new Vector3(0.1f, 0.24f, 0.21f), new Vector3(0.07f, 0.09f, 0.05f), Black, default(Vector3), false, false);
        Prim(PrimitiveType.Sphere, "Nose", head, new Vector3(0f, 0.17f, 0.23f), new Vector3(0.07f, 0.07f, 0.06f), Skin, default(Vector3), false, false);
        Prim(PrimitiveType.Cylinder, "Hat Brim", head, new Vector3(0f, 0.42f, 0f), new Vector3(0.66f, 0.025f, 0.66f), RoofGreen);
        MeshObj("Hat", MeshFactory.Frustum(6, 0.5f, 0.3f, 1f), head, new Vector3(0f, 0.42f, 0f), new Vector3(0.75f, 0.3f, 0.75f), RoofGreen, Quaternion.identity);
        Cube("Feather", head, new Vector3(0.2f, 0.6f, -0.05f), new Vector3(0.05f, 0.3f, 0.08f), Scarf, new Vector3(0f, 0f, -25f), false, false);
        // Arms (pivots at shoulders)
        var armL = Pivot("Arm L", model, new Vector3(-0.44f, 1.38f, 0f));
        Cube("Sleeve", armL, new Vector3(0f, -0.15f, 0f), new Vector3(0.2f, 0.32f, 0.2f), Tunic);
        Cube("Hand", armL, new Vector3(0f, -0.43f, 0f), new Vector3(0.17f, 0.26f, 0.17f), Skin);
        var armR = Pivot("Arm R", model, new Vector3(0.44f, 1.38f, 0f));
        Cube("Sleeve", armR, new Vector3(0f, -0.15f, 0f), new Vector3(0.2f, 0.32f, 0.2f), Tunic);
        Cube("Hand", armR, new Vector3(0f, -0.43f, 0f), new Vector3(0.17f, 0.26f, 0.17f), Skin);
        // Legs (pivots at hips)
        var legL = Pivot("Leg L", model, new Vector3(-0.17f, 0.72f, 0f));
        Cube("Thigh", legL, new Vector3(0f, -0.25f, 0f), new Vector3(0.24f, 0.5f, 0.26f), Pants);
        Cube("Boot", legL, new Vector3(0f, -0.6f, 0.04f), new Vector3(0.26f, 0.24f, 0.34f), Boots);
        var legR = Pivot("Leg R", model, new Vector3(0.17f, 0.72f, 0f));
        Cube("Thigh", legR, new Vector3(0f, -0.25f, 0f), new Vector3(0.24f, 0.5f, 0.26f), Pants);
        Cube("Boot", legR, new Vector3(0f, -0.6f, 0.04f), new Vector3(0.26f, 0.24f, 0.34f), Boots);

        var anim = p.AddComponent<PlayerAnimator>();
        anim.model = model;
        anim.head = head;
        anim.leftArm = armL;
        anim.rightArm = armR;
        anim.leftLeg = legL;
        anim.rightLeg = legR;

        var pc = p.AddComponent<PlayerController>();
        pc.animator = anim;
        pc.audioManager = audio;
        return pc;
    }

    static CameraFollow BuildCamera(Transform player)
    {
        var g = new GameObject("Main Camera");
        g.tag = "MainCamera";
        var cam = g.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex(0xC6DDCB);
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.2f;
        cam.farClipPlane = 260f;
        g.AddComponent<AudioListener>();
        var follow = g.AddComponent<CameraFollow>();
        follow.target = player;
        g.transform.position = player.position + follow.offset;
        g.transform.LookAt(player.position + follow.lookOffset);
        return follow;
    }
}
