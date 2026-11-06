using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls all UI panels: Main Menu, HUD (coins, score, progress), Game Over and Level Complete.
/// Button listeners are attached in code at runtime, so no Inspector wiring of OnClick is required.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject menuPanel;
    public GameObject hudPanel;
    public GameObject gameOverPanel;
    public GameObject levelCompletePanel;

    [Header("HUD")]
    public Text coinText;
    public Text scoreText;
    public RectTransform progressFill;
    public RectTransform coinIcon;
    public Text hintText;

    [Header("Menu")]
    public Text menuBestText;
    public Button startButton;

    [Header("Game Over")]
    public Text gameOverReasonText;
    public Text gameOverStatsText;
    public Button gameOverRestartButton;
    public Button gameOverMenuButton;

    [Header("Level Complete")]
    public Text completeRatingText;
    public Text completeStatsText;
    public Button completeRestartButton;
    public Button completeMenuButton;

    GameManager gm;
    float coinPulse;
    float hintTimer;
    RectTransform animatingPanel;
    float panelAnim;

    public void Bind(GameManager manager)
    {
        gm = manager;
        Hook(startButton, () => gm.StartGame());
        Hook(gameOverRestartButton, () => gm.Restart());
        Hook(gameOverMenuButton, () => gm.GoToMenu());
        Hook(completeRestartButton, () => gm.Restart());
        Hook(completeMenuButton, () => gm.GoToMenu());
    }

    void Hook(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() =>
        {
            if (gm != null && gm.audioManager != null) gm.audioManager.PlayClick();
            action();
        });
    }

    void ShowOnly(GameObject panel)
    {
        if (menuPanel != null) menuPanel.SetActive(panel == menuPanel);
        if (gameOverPanel != null) gameOverPanel.SetActive(panel == gameOverPanel);
        if (levelCompletePanel != null) levelCompletePanel.SetActive(panel == levelCompletePanel);
        if (hudPanel != null) hudPanel.SetActive(panel == hudPanel || panel == gameOverPanel || panel == levelCompletePanel);

        if (panel != null && panel != hudPanel)
        {
            // Pop-in animation on the panel's card (first child).
            animatingPanel = panel.transform.childCount > 0 ? panel.transform.GetChild(0) as RectTransform : null;
            panelAnim = 0f;
            if (animatingPanel != null) animatingPanel.localScale = Vector3.one * 0.85f;
        }
    }

    public void ShowMenu(int best)
    {
        ShowOnly(menuPanel);
        if (menuBestText != null) menuBestText.text = best > 0 ? "BEST SCORE  " + best : "";
    }

    public void ShowHud()
    {
        ShowOnly(hudPanel);
        hintTimer = 6f;
        if (hintText != null) hintText.gameObject.SetActive(true);
    }

    public void UpdateHud(int coins, int totalCoins, int score, float progress)
    {
        if (coinText != null) coinText.text = coins + " / " + totalCoins;
        if (scoreText != null) scoreText.text = "SCORE  " + score;
        if (progressFill != null)
        {
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        }
    }

    public void PulseCoins() { coinPulse = 1f; }

    public void ShowGameOver(string reason, int coins, int totalCoins, int score, int best, bool newBest)
    {
        ShowOnly(gameOverPanel);
        if (hintText != null) hintText.gameObject.SetActive(false);
        if (gameOverReasonText != null) gameOverReasonText.text = reason;
        if (gameOverStatsText != null)
        {
            gameOverStatsText.text =
                "Coins   " + coins + " / " + totalCoins + "\n" +
                "Score   " + score + "\n" +
                (newBest ? "NEW BEST SCORE!" : "Best   " + best);
        }
    }

    public void ShowLevelComplete(int coins, int totalCoins, int score, int best, bool newBest, float time)
    {
        ShowOnly(levelCompletePanel);
        if (hintText != null) hintText.gameObject.SetActive(false);
        float ratio = totalCoins > 0 ? coins / (float)totalCoins : 1f;
        string rating = ratio >= 0.9f ? "PERFECT RUN!" : ratio >= 0.6f ? "GREAT RUN!" : "WELL DONE!";
        if (completeRatingText != null) completeRatingText.text = "You reached the Tree Village  -  " + rating;
        if (completeStatsText != null)
        {
            completeStatsText.text =
                "Coins   " + coins + " / " + totalCoins + "\n" +
                "Time   " + time.ToString("0.0") + " s\n" +
                "Score   " + score + "\n" +
                (newBest ? "NEW BEST SCORE!" : "Best   " + best);
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (coinIcon != null)
        {
            coinPulse = Mathf.MoveTowards(coinPulse, 0f, dt * 4f);
            coinIcon.localScale = Vector3.one * (1f + coinPulse * 0.35f);
        }

        if (hintText != null && hintText.gameObject.activeSelf)
        {
            hintTimer -= dt;
            var c = hintText.color;
            c.a = Mathf.Clamp01(hintTimer);
            hintText.color = c;
            if (hintTimer <= 0f) hintText.gameObject.SetActive(false);
        }

        if (animatingPanel != null && panelAnim < 1f)
        {
            panelAnim = Mathf.Min(1f, panelAnim + dt * 4f);
            // Ease-out-back pop.
            float t = panelAnim - 1f;
            float s = 1f + 2.2f * t * t * t + 1.2f * t * t;
            animatingPanel.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, s);
        }
    }
}
