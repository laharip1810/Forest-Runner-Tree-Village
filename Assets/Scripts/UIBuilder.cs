using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds the complete UI (menu, HUD, game over, level complete) in code with a
/// green / brown / golden / cream palette and returns a fully wired UIManager.
/// </summary>
public static class UIBuilder
{
    static readonly Color ForestDark = Hex(0x1C3621);
    static readonly Color Forest = Hex(0x2E5A33);
    static readonly Color Leaf = Hex(0x4E8A3A);
    static readonly Color Brown = Hex(0x5A381C);
    static readonly Color BrownDark = Hex(0x3E2511);
    static readonly Color GoldC = Hex(0xF2B632);
    static readonly Color GoldDeepC = Hex(0xC98516);
    static readonly Color CreamC = Hex(0xF6EBD0);

    static Font font;
    static Sprite rounded;
    static Sprite circle;

    static Color Hex(int rgb, float a = 1f)
    {
        return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
    }

    static Color A(Color c, float a) { c.a = a; return c; }

    static void EnsureResources(BuildContext ctx)
    {
        font = ctx != null && ctx.font != null ? ctx.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rounded = ctx != null && ctx.roundedSprite != null ? ctx.roundedSprite : UISprites.RoundedSprite();
        circle = ctx != null && ctx.circleSprite != null ? ctx.circleSprite : UISprites.CircleSprite();
    }

    // ---------------------------------------------------------------
    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var g = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)g.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretch(string name, Transform parent, float inset = 0f)
    {
        var g = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)g.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    static Image Img(RectTransform rt, Color c, Sprite sprite, bool raycast = false)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = raycast;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = sprite == rounded ? Image.Type.Sliced : Image.Type.Simple;
        }
        return img;
    }

    static Text Txt(Transform parent, string name, string text, int size, Color color, Vector2 pos, Vector2 box,
        TextAnchor align = TextAnchor.MiddleCenter, bool bold = true, Vector2 anchor = default(Vector2), bool shadow = true)
    {
        var rt = Rect(name, parent, anchor == default(Vector2) ? new Vector2(0.5f, 0.5f) : anchor, pos, box);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.lineSpacing = 1.15f;
        t.raycastTarget = false;
        if (shadow)
        {
            var s = rt.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.45f);
            s.effectDistance = new Vector2(2f, -3f);
        }
        return t;
    }

    /// <summary>A card with a golden border: returns the inner content rect.</summary>
    static RectTransform Card(Transform parent, string name, Vector2 size, Color inner)
    {
        var border = Rect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        Img(border, GoldC, rounded, true);
        var sh = border.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.5f);
        sh.effectDistance = new Vector2(0f, -10f);
        var content = Stretch("Inner", border, 7f);
        Img(content, inner, rounded);
        var deco = Stretch("Inner Line", content, 12f);
        var di = Img(deco, A(GoldC, 0.18f), rounded);
        di.fillCenter = false;
        return content;
    }

    static Button Btn(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bg, Color fg)
    {
        var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), pos, size);
        var img = Img(rt, bg, rounded, true);
        var sh = rt.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0.1f, 0.05f, 0f, 0.6f);
        sh.effectDistance = new Vector2(0f, -6f);
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors;
        cb.normalColor = new Color(0.93f, 0.93f, 0.93f, 1f);
        cb.highlightedColor = Color.white;
        cb.selectedColor = new Color(0.93f, 0.93f, 0.93f, 1f);
        cb.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        Txt(rt, "Label", label, Mathf.RoundToInt(size.y * 0.4f), fg, Vector2.zero, size, TextAnchor.MiddleCenter, true, default(Vector2), false);
        return b;
    }

    // ---------------------------------------------------------------
    public static UIManager Build(BuildContext ctx)
    {
        EnsureResources(ctx);

        var canvasGO = new GameObject("UI Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        var root = canvasGO.transform;

        var ui = canvasGO.AddComponent<UIManager>();

        BuildHud(root, ui);
        BuildMenu(root, ui);
        BuildGameOver(root, ui);
        BuildComplete(root, ui);

        // Event system (input module is added at runtime to match the active input backend)
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<EventSystemSetup>();
        }
        return ui;
    }

    static void BuildHud(Transform root, UIManager ui)
    {
        var hud = Stretch("HUD", root);
        ui.hudPanel = hud.gameObject;

        // Coins (top-left)
        var coinPanel = Rect("Coin Panel", hud, new Vector2(0f, 1f), new Vector2(190f, -75f), new Vector2(320f, 96f));
        Img(coinPanel, A(Brown, 0.9f), rounded);
        var coinBorder = Stretch("Border", coinPanel, 4f);
        Img(coinBorder, A(GoldC, 0.35f), rounded).fillCenter = false;
        var icon = Rect("Coin Icon", coinPanel, new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(64f, 64f));
        Img(icon, GoldC, circle);
        var iconInner = Rect("Coin Icon Inner", icon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
        Img(iconInner, GoldDeepC, circle);
        var iconMark = Rect("Coin Icon Mark", icon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 26f));
        Img(iconMark, GoldC, null);
        ui.coinIcon = icon;
        ui.coinText = Txt(coinPanel, "Coin Text", "0 / 0", 44, CreamC, new Vector2(195f, 0f), new Vector2(210f, 70f), TextAnchor.MiddleLeft, true, new Vector2(0f, 0.5f));

        // Score (top-right)
        var scorePanel = Rect("Score Panel", hud, new Vector2(1f, 1f), new Vector2(-210f, -75f), new Vector2(360f, 96f));
        Img(scorePanel, A(Brown, 0.9f), rounded);
        Img(Stretch("Border", scorePanel, 4f), A(GoldC, 0.35f), rounded).fillCenter = false;
        ui.scoreText = Txt(scorePanel, "Score Text", "SCORE  0", 42, GoldC, Vector2.zero, new Vector2(340f, 70f));

        // Progress bar (top-centre)
        var bar = Rect("Progress Bar", hud, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(640f, 38f));
        Img(bar, A(Brown, 0.92f), rounded);
        var track = Stretch("Track", bar, 7f);
        Img(track, A(ForestDark, 0.95f), rounded);
        var fill = Stretch("Fill", track, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        Img(fill, GoldC, rounded);
        ui.progressFill = fill;
        Txt(hud, "Progress Start", "FOREST", 22, CreamC, new Vector2(-280f, -102f), new Vector2(200f, 30f), TextAnchor.MiddleCenter, true, new Vector2(0.5f, 1f));
        Txt(hud, "Progress End", "TREE VILLAGE", 22, GoldC, new Vector2(260f, -102f), new Vector2(240f, 30f), TextAnchor.MiddleCenter, true, new Vector2(0.5f, 1f));

        // Hint (bottom)
        var hint = Rect("Hint Box", hud, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(900f, 70f));
        ui.hintText = Txt(hint, "Hint", "A / D  or  ARROWS  to switch lanes    |    SPACE  to jump", 32, CreamC, Vector2.zero, new Vector2(900f, 70f));
    }

    static void BuildMenu(Transform root, UIManager ui)
    {
        var panel = Stretch("Main Menu", root);
        Img(panel, new Color(0.05f, 0.12f, 0.06f, 0.5f), null, true);
        ui.menuPanel = panel.gameObject;

        var card = Card(panel, "Menu Card", new Vector2(980f, 760f), A(ForestDark, 0.95f));
        Txt(card, "Title Shadow", "FOREST RUNNER", 108, BrownDark, new Vector2(4f, 236f), new Vector2(940f, 130f), TextAnchor.MiddleCenter, true, default(Vector2), false);
        Txt(card, "Title", "FOREST RUNNER", 108, GoldC, new Vector2(0f, 242f), new Vector2(940f, 130f));
        Txt(card, "Subtitle", "~  T R E E   V I L L A G E  ~", 46, CreamC, new Vector2(0f, 150f), new Vector2(900f, 60f));
        var divider = Rect("Divider", card, new Vector2(0.5f, 0.5f), new Vector2(0f, 105f), new Vector2(560f, 5f));
        Img(divider, GoldC, null);
        Txt(card, "Tagline", "Race through the forest, leap across the broken tree-top bridges,\ncollect golden coins and reach the village!",
            28, A(CreamC, 0.9f), new Vector2(0f, 45f), new Vector2(900f, 90f), TextAnchor.MiddleCenter, false);

        ui.startButton = Btn(card, "Start Button", "START GAME", new Vector2(0f, -75f), new Vector2(460f, 112f), GoldC, BrownDark);

        var controls = Rect("Controls Box", card, new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(800f, 120f));
        Img(controls, A(Brown, 0.85f), rounded);
        Txt(controls, "Controls", "A / D  or  LEFT / RIGHT  -  switch lanes\nSPACE / W / UP  -  jump        ENTER  -  start",
            28, CreamC, Vector2.zero, new Vector2(780f, 110f), TextAnchor.MiddleCenter, false);

        ui.menuBestText = Txt(card, "Best", "", 30, GoldC, new Vector2(0f, -320f), new Vector2(600f, 44f));
    }

    static void BuildGameOver(Transform root, UIManager ui)
    {
        var panel = Stretch("Game Over", root);
        Img(panel, new Color(0.15f, 0.06f, 0.02f, 0.55f), null, true);
        ui.gameOverPanel = panel.gameObject;

        var card = Card(panel, "Game Over Card", new Vector2(860f, 660f), A(Brown, 0.97f));
        Txt(card, "Title", "GAME OVER", 100, GoldC, new Vector2(0f, 228f), new Vector2(820f, 120f));
        ui.gameOverReasonText = Txt(card, "Reason", "", 36, CreamC, new Vector2(0f, 140f), new Vector2(820f, 50f), TextAnchor.MiddleCenter, false);
        var stats = Rect("Stats Box", card, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(560f, 170f));
        Img(stats, A(BrownDark, 0.75f), rounded);
        ui.gameOverStatsText = Txt(stats, "Stats", "", 36, CreamC, Vector2.zero, new Vector2(540f, 160f));
        ui.gameOverRestartButton = Btn(card, "Restart Button", "RESTART", new Vector2(-180f, -180f), new Vector2(320f, 100f), GoldC, BrownDark);
        ui.gameOverMenuButton = Btn(card, "Menu Button", "MAIN MENU", new Vector2(180f, -180f), new Vector2(320f, 100f), Leaf, CreamC);
        Txt(card, "Hint", "Press R or ENTER to restart  -  ESC for menu", 24, A(CreamC, 0.7f), new Vector2(0f, -275f), new Vector2(800f, 40f), TextAnchor.MiddleCenter, false);
    }

    static void BuildComplete(Transform root, UIManager ui)
    {
        var panel = Stretch("Level Complete", root);
        Img(panel, new Color(0.05f, 0.14f, 0.05f, 0.5f), null, true);
        ui.levelCompletePanel = panel.gameObject;

        var card = Card(panel, "Complete Card", new Vector2(900f, 720f), A(Forest, 0.97f));
        Txt(card, "Title Shadow", "LEVEL COMPLETE!", 92, BrownDark, new Vector2(4f, 254f), new Vector2(860f, 120f), TextAnchor.MiddleCenter, true, default(Vector2), false);
        Txt(card, "Title", "LEVEL COMPLETE!", 92, GoldC, new Vector2(0f, 260f), new Vector2(860f, 120f));
        ui.completeRatingText = Txt(card, "Rating", "", 34, CreamC, new Vector2(0f, 175f), new Vector2(860f, 50f), TextAnchor.MiddleCenter, false);
        var stats = Rect("Stats Box", card, new Vector2(0.5f, 0.5f), new Vector2(0f, 25f), new Vector2(580f, 220f));
        Img(stats, A(ForestDark, 0.8f), rounded);
        ui.completeStatsText = Txt(stats, "Stats", "", 36, CreamC, Vector2.zero, new Vector2(560f, 210f));
        ui.completeRestartButton = Btn(card, "Play Again Button", "PLAY AGAIN", new Vector2(-185f, -200f), new Vector2(330f, 100f), GoldC, BrownDark);
        ui.completeMenuButton = Btn(card, "Menu Button", "MAIN MENU", new Vector2(185f, -200f), new Vector2(330f, 100f), Leaf, CreamC);
        Txt(card, "Hint", "Press R or ENTER to play again  -  ESC for menu", 24, A(CreamC, 0.7f), new Vector2(0f, -300f), new Vector2(800f, 40f), TextAnchor.MiddleCenter, false);
    }

    /// <summary>World-space text (used for signs on the gate and finish arch). Respects depth.</summary>
    public static void WorldText(Transform parent, string text, Vector3 localPos, Vector2 size, int fontSize, Color color, float scale)
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var g = new GameObject("Sign Text", typeof(RectTransform));
        var rt = (RectTransform)g.transform;
        rt.SetParent(parent, false);
        var c = g.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        rt.localPosition = localPos;
        rt.localRotation = Quaternion.identity;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one * scale;
        var t = Txt(rt, "Text", text, fontSize, color, Vector2.zero, size, TextAnchor.MiddleCenter, true, default(Vector2), false);
        ((RectTransform)t.transform).anchorMin = new Vector2(0.5f, 0.5f);
    }
}
