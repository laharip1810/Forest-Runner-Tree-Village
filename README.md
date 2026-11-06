# Forest Runner – Tree Village

A 3D low-poly adventure runner for **Unity 6** (Built-In Render Pipeline, C#, Windows PC).
Run through a forest, cross broken tree-top bridges over a ravine, collect golden coins and reach the Tree Village.

---

## How to open and play

1. Extract `ForestRunnerTreeVillage.zip`.
2. **Unity Hub → Add → Add project from disk →** select the `ForestRunnerTreeVillage` folder.
3. Open it with any **Unity 6** version (6000.0.x or newer). If Hub says the editor version differs, just pick your installed Unity 6 version.
4. Wait for the import to finish.
   - If Unity asks **"Enable the new Input System backends? (restart)"**, click **Yes**. The game works either way.
5. On first open the game scene is **generated automatically** and opened:
   `Assets/Scenes/ForestRunner.unity`
   (The Console shows: *"Scene ready … press Play to start the game!"*)
6. Press **Play**.

**If the scene was not generated** (or you want to rebuild it), use the top menu:
**Forest Runner → Build Game Scene (One Click)**.

Safety net: even if you press Play in an empty scene, the level is built at runtime automatically.

---

## Controls

| Action | Keys |
|---|---|
| Switch lane left / right | **A / D** or **← / →** |
| Jump | **Space**, **W** or **↑** |
| Start game | **Start Game** button, **Enter** or **Space** |
| Restart (after Game Over / Level Complete) | **Restart / Play Again** button, **R** or **Enter** |
| Back to Main Menu | **Main Menu** button or **Esc** |

---

## Gameplay

- The runner moves forward automatically and gradually speeds up.
- **Jump** over fallen logs, tree roots and broken bridge gaps.
- **Dodge** rocks and log piles by switching lanes.
- Hitting an obstacle or falling through a broken bridge = **Game Over**.
- Collect rotating golden coins (50 points each) plus 1 point per metre and a 1000-point bonus for finishing.
- Reach the golden **TREE VILLAGE** arch to complete the level. Best score is saved.

Level route: Forest path & village gate → ramp up → tree-top platforms and rope bridges over the river ravine (with tree houses) → ramp down → village with cottages, campfire and the Great Tree.

---

## Project structure

```
Assets/
  Scenes/      ForestRunner.unity (generated on first open)
  Scripts/     GameManager, PlayerController, PlayerAnimator, CameraFollow, Coin, Obstacle,
               FinishLine, UIManager, AudioManager, InputHelper, EventSystemSetup,
               LevelBuilder, UIBuilder, MeshFactory, UISprites, ProceduralAudio,
               RuntimeBootstrap, Bobber, LightFlicker
  Editor/      GameSetup.cs  (one-click scene generator + Windows build menu)
  Materials/   generated materials
  Prefabs/     Player.prefab, Coin.prefab, generated low-poly meshes
  Audio/       ForestMusic.wav, Coin.wav, Jump.wav, Hit.wav, Win.wav, Click.wav
  UI/          RoundedPanel.png, Circle.png (generated)
Packages/manifest.json
ProjectSettings/ProjectVersion.txt
```

No external or paid assets are used. All models are built from Unity primitives and procedural low-poly meshes; all audio was synthesised for this project.

---

## Windows build (.exe)

Menu **Forest Runner → Build Windows Game (.exe)** and choose a folder.
Requires the *Windows Build Support* module in Unity Hub (installed by default on Windows).
Alternatively: **File → Build Profiles → Windows → Build**.

---

## Troubleshooting

- **Pink/magenta objects:** make sure the project uses the Built-In Render Pipeline (no URP/HDRP asset under *Project Settings → Graphics*), then run **Forest Runner → Build Game Scene** again.
- **Buttons don't respond:** make sure the scene has the `EventSystem` object (it is created by the generator). Rebuilding the scene fixes it.
- **Console errors after changing Unity version:** run **Forest Runner → Build Game Scene (One Click)** once more.

## Testing note

The code was compile-checked against the Unity API in both input configurations (new Input System and legacy Input Manager). The project was generated outside the Unity Editor, so please do a full playthrough in Unity before submitting.
