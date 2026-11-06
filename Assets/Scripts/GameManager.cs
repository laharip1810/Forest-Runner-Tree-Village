using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState { Menu, Playing, GameOver, LevelComplete }

/// <summary>
/// Central game flow: Main Menu -> Playing -> Game Over / Level Complete -> Restart.
/// Tracks coins and score. Restarting resets the level in place (no scene reload needed).
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public PlayerController player;
    public CameraFollow cameraFollow;
    public UIManager ui;
    public AudioManager audioManager;

    [Header("Level")]
    public float startZ = 2f;
    public float finishZ = 388f;
    public int coinScore = 50;
    public int finishBonus = 1000;

    public GameState State { get; private set; }
    public int Coins { get; private set; }
    public int TotalCoins { get; private set; }
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public int StartFrame { get; private set; }
    public bool PlayerFell { get; private set; }

    const string BestKey = "ForestRunner_BestScore";
    readonly List<Coin> coins = new List<Coin>();
    float maxDistance;
    float runTime;
    Coroutine pendingPanel;

    void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 120;
        BestScore = PlayerPrefs.GetInt(BestKey, 0);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        coins.Clear();
        coins.AddRange(FindObjectsByType<Coin>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        TotalCoins = coins.Count;

        if (ui != null) ui.Bind(this);
        GoToMenu();
    }

    void Update()
    {
        switch (State)
        {
            case GameState.Menu:
                if (InputHelper.ConfirmPressed()) StartGame();
                break;

            case GameState.Playing:
                if (player != null)
                {
                    maxDistance = Mathf.Max(maxDistance, player.transform.position.z - startZ);
                    runTime += Time.deltaTime;
                    // Backup finish check in case the trigger is missed.
                    if (player.transform.position.z >= finishZ && !player.IsDead) LevelComplete();
                }
                RecalculateScore(false);
                if (ui != null) ui.UpdateHud(Coins, TotalCoins, Score, Progress());
                if (InputHelper.BackPressed()) GoToMenu();
                break;

            case GameState.GameOver:
            case GameState.LevelComplete:
                if (pendingPanel != null) break; // wait until the result screen is visible
                if (InputHelper.RestartPressed() || InputHelper.ConfirmPressed()) Restart();
                else if (InputHelper.BackPressed()) GoToMenu();
                break;
        }
    }

    public float Progress()
    {
        return Mathf.Clamp01(maxDistance / Mathf.Max(1f, finishZ - startZ));
    }

    void RecalculateScore(bool finished)
    {
        Score = Coins * coinScore + Mathf.FloorToInt(maxDistance) + (finished ? finishBonus : 0);
    }

    void ResetLevel()
    {
        if (pendingPanel != null) { StopCoroutine(pendingPanel); pendingPanel = null; }
        foreach (var c in coins) if (c != null) c.ResetCoin();
        Coins = 0;
        Score = 0;
        maxDistance = 0f;
        runTime = 0f;
        PlayerFell = false;
        if (player != null) player.ResetPlayer();
        if (cameraFollow != null) cameraFollow.Snap();
    }

    // ---------------- Public flow API (also used by UI buttons) ----------------

    public void GoToMenu()
    {
        ResetLevel();
        State = GameState.Menu;
        if (ui != null) ui.ShowMenu(BestScore);
        if (audioManager != null) audioManager.SetMusicVolume(audioManager.musicVolume);
    }

    public void StartGame()
    {
        if (State == GameState.Playing) return;
        if (State != GameState.Menu) ResetLevel();
        State = GameState.Playing;
        StartFrame = Time.frameCount;
        if (cameraFollow != null) cameraFollow.Snap();
        if (ui != null) ui.ShowHud();
        if (ui != null) ui.UpdateHud(Coins, TotalCoins, Score, 0f);
        if (audioManager != null) audioManager.PlayClick();
    }

    public void Restart()
    {
        ResetLevel();
        State = GameState.Menu; // allow StartGame to proceed
        StartGame();
    }

    public void AddCoin(int amount)
    {
        if (State != GameState.Playing) return;
        Coins += amount;
        RecalculateScore(false);
        if (audioManager != null) audioManager.PlayCoin();
        if (ui != null) ui.PulseCoins();
    }

    public void GameOver(string reason, bool fell)
    {
        if (State != GameState.Playing) return;
        State = GameState.GameOver;
        PlayerFell = fell;
        RecalculateScore(false);
        bool newBest = SaveBest();
        if (audioManager != null) audioManager.PlayHit();
        if (cameraFollow != null && !fell) cameraFollow.Shake(0.25f, 0.35f);
        pendingPanel = StartCoroutine(ShowAfter(0.9f, () =>
        {
            if (ui != null) ui.ShowGameOver(reason, Coins, TotalCoins, Score, BestScore, newBest);
        }));
    }

    public void LevelComplete()
    {
        if (State != GameState.Playing) return;
        State = GameState.LevelComplete;
        maxDistance = finishZ - startZ;
        RecalculateScore(true);
        bool newBest = SaveBest();
        if (player != null) player.Finish();
        if (audioManager != null) audioManager.PlayWin();
        if (ui != null) ui.UpdateHud(Coins, TotalCoins, Score, 1f);
        pendingPanel = StartCoroutine(ShowAfter(1.4f, () =>
        {
            if (ui != null) ui.ShowLevelComplete(Coins, TotalCoins, Score, BestScore, newBest, runTime);
        }));
    }

    bool SaveBest()
    {
        if (Score <= BestScore) return false;
        BestScore = Score;
        PlayerPrefs.SetInt(BestKey, BestScore);
        PlayerPrefs.Save();
        return true;
    }

    IEnumerator ShowAfter(float seconds, System.Action action)
    {
        yield return new WaitForSeconds(seconds);
        pendingPanel = null;
        action();
    }
}
