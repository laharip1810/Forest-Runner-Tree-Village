using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click scene generator for Forest Runner - Tree Village.
///
/// Runs AUTOMATICALLY the first time the project is opened (when the game scene does not exist yet),
/// and can be re-run at any time from the menu:  Forest Runner > Build Game Scene (One Click).
///
/// It creates the forest environment, tree houses, platforms, bridges, player, camera, obstacles,
/// coins, UI and finish point, connects every script, saves materials/meshes/prefabs as assets,
/// saves Assets/Scenes/ForestRunner.unity, adds it to Build Settings and opens it.
/// </summary>
[InitializeOnLoad]
public static class GameSetup
{
    public const string ScenePath = "Assets/Scenes/ForestRunner.unity";
    const string AutoKey = "ForestRunner_AutoSetupAttempted";
    static double waitStart;

    static GameSetup()
    {
        if (Application.isBatchMode) return;
        if (File.Exists(ScenePath)) return;
        if (SessionState.GetBool(AutoKey, false)) return;
        SessionState.SetBool(AutoKey, true);
        waitStart = 0;
        EditorApplication.update += WaitThenAutoBuild;
    }

    static void WaitThenAutoBuild()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (waitStart <= 0) waitStart = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - waitStart < 2.0) return;
        EditorApplication.update -= WaitThenAutoBuild;
        if (File.Exists(ScenePath)) return;
        try
        {
            Debug.Log("[Forest Runner] First launch: generating the game scene automatically...");
            BuildScene(false);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Forest Runner] Automatic setup failed. Use the menu 'Forest Runner > Build Game Scene (One Click)'.\n" + e);
        }
    }

    // ------------------------------------------------------------------
    [MenuItem("Forest Runner/Build Game Scene (One Click)", false, 1)]
    public static void BuildFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Forest Runner", "Stop Play mode first, then run the setup again.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene(true);
    }

    [MenuItem("Forest Runner/Open Game Scene", false, 2)]
    public static void OpenScene()
    {
        if (!File.Exists(ScenePath))
        {
            BuildFromMenu();
            return;
        }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Forest Runner/Build Windows Game (.exe)", false, 20)]
    public static void BuildWindows()
    {
        if (!File.Exists(ScenePath)) BuildScene(false);
        string path = EditorUtility.SaveFilePanel("Save Windows build", "Builds/Windows", "ForestRunner", "exe");
        if (string.IsNullOrEmpty(path)) return;
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = path,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("[Forest Runner] Windows build result: " + report.summary.result + " -> " + path);
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorUtility.RevealInFinder(path);
    }

    // ------------------------------------------------------------------
    public static void BuildScene(bool interactive)
    {
        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets", "UI");
        EnsureFolder("Assets", "Audio");

        ApplyProjectSettings();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Clear previously generated assets (the old scene is no longer loaded).
        DeleteFolderContents("Assets/Materials", "t:Material");
        if (AssetDatabase.IsValidFolder("Assets/Prefabs/Meshes")) AssetDatabase.DeleteAsset("Assets/Prefabs/Meshes");
        EnsureFolder("Assets/Prefabs", "Meshes");

        var ctx = new BuildContext();
        ctx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ctx.roundedSprite = EnsureSprite("Assets/UI/RoundedPanel.png", UISprites.RoundedTexture(), UISprites.RoundedRadius);
        ctx.circleSprite = EnsureSprite("Assets/UI/Circle.png", UISprites.CircleTexture(), 0);
        ctx.music = LoadClip("ForestMusic");
        ctx.coin = LoadClip("Coin");
        ctx.jump = LoadClip("Jump");
        ctx.hit = LoadClip("Hit");
        ctx.win = LoadClip("Win");
        ctx.click = LoadClip("Click");

        EditorUtility.DisplayProgressBar("Forest Runner", "Building the forest village...", 0.3f);
        try
        {
            LevelBuilder.Build(ctx);

            EditorUtility.DisplayProgressBar("Forest Runner", "Saving materials and meshes...", 0.6f);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var m in ctx.materials)
                    AssetDatabase.CreateAsset(m, "Assets/Materials/" + Safe(m.name) + ".mat");
                int i = 0;
                foreach (var mesh in MeshFactory.Created)
                    AssetDatabase.CreateAsset(mesh, "Assets/Prefabs/Meshes/Mesh_" + (i++).ToString("000") + "_" + Safe(mesh.name) + ".asset");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();

            MarkEnvironmentStatic();

            EditorUtility.DisplayProgressBar("Forest Runner", "Saving prefabs and scene...", 0.85f);
            if (ctx.player != null) PrefabUtility.SaveAsPrefabAsset(ctx.player, "Assets/Prefabs/Player.prefab");
            if (ctx.coinTemplate != null) PrefabUtility.SaveAsPrefabAsset(ctx.coinTemplate, "Assets/Prefabs/Coin.prefab");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        var cam = GameObject.Find("Main Camera");
        if (cam != null) Selection.activeGameObject = cam;
        Debug.Log("[Forest Runner] Scene ready: " + ScenePath + "  -  press Play to start the game!");
        if (interactive)
            EditorUtility.DisplayDialog("Forest Runner - Tree Village",
                "The game scene has been generated and saved:\n\n" + ScenePath + "\n\nPress Play to start!", "Great!");
    }

    // ------------------------------------------------------------------
    static void ApplyProjectSettings()
    {
        PlayerSettings.companyName = "Forest Runner Studio";
        PlayerSettings.productName = "Forest Runner - Tree Village";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
    }

    static void MarkEnvironmentStatic()
    {
        var env = GameObject.Find("Environment");
        if (env == null) return;
        foreach (var t in env.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<Bobber>() != null || t.GetComponent<LightFlicker>() != null) continue;
            if (t.GetComponent<Canvas>() != null || t.GetComponentInParent<Canvas>() != null) continue;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }
    }

    static string Safe(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_').Replace('.', '_');
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    static void DeleteFolderContents(string folder, string filter)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return;
        foreach (var guid in AssetDatabase.FindAssets(filter, new[] { folder }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
    }

    static AudioClip LoadClip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + name + ".wav");
        if (clip == null) Debug.LogWarning("[Forest Runner] Audio clip not found: Assets/Audio/" + name + ".wav (a synthesised fallback will be used).");
        return clip;
    }

    static Sprite EnsureSprite(string path, Texture2D tex, int border)
    {
        if (!File.Exists(path)) File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogWarning("[Forest Runner] Could not import " + path + " as a sprite; using a generated sprite instead.");
        }
        return sprite;
    }
}
