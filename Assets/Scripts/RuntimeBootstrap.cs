using UnityEngine;

/// <summary>
/// Safety net: if Play is pressed in a scene that does not contain the game
/// (for example an empty scene), the complete level is generated at runtime.
/// The normal path is the saved scene Assets/Scenes/ForestRunner.unity.
/// </summary>
public static class RuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureGameExists()
    {
        if (Object.FindAnyObjectByType<GameManager>() != null) return;
        Debug.Log("[Forest Runner] No game found in this scene - building the level at runtime.");
        LevelBuilder.Build(new BuildContext());
    }
}
