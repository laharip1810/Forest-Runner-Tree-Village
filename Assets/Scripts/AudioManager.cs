using UnityEngine;

/// <summary>Background music and sound effects. Falls back to synthesised sounds if clips are missing.</summary>
public class AudioManager : MonoBehaviour
{
    public AudioClip music;
    public AudioClip coinClip;
    public AudioClip jumpClip;
    public AudioClip hitClip;
    public AudioClip winClip;
    public AudioClip clickClip;

    [Range(0f, 1f)] public float musicVolume = 0.35f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    AudioSource musicSource;
    AudioSource sfxSource;
    float coinPitch = 1f;
    float lastCoinTime;

    void Awake()
    {
        if (music == null) music = ProceduralAudio.Music();
        if (coinClip == null) coinClip = ProceduralAudio.Coin();
        if (jumpClip == null) jumpClip = ProceduralAudio.Jump();
        if (hitClip == null) hitClip = ProceduralAudio.Hit();
        if (winClip == null) winClip = ProceduralAudio.Win();
        if (clickClip == null) clickClip = ProceduralAudio.Click();

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = music;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;
        musicSource.spatialBlend = 0f;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
    }

    void Start()
    {
        if (music != null) musicSource.Play();
    }

    public void SetMusicVolume(float v)
    {
        if (musicSource != null) musicSource.volume = v;
    }

    void Play(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, volume * sfxVolume);
    }

    public void PlayCoin()
    {
        // Rising pitch for coin streaks.
        coinPitch = Time.time - lastCoinTime < 0.6f ? Mathf.Min(coinPitch + 0.05f, 1.5f) : 1f;
        lastCoinTime = Time.time;
        Play(coinClip, 0.7f, coinPitch);
    }

    public void PlayJump()  { Play(jumpClip, 0.6f, Random.Range(0.95f, 1.05f)); }
    public void PlayHit()   { Play(hitClip, 1f, 1f); }
    public void PlayWin()   { Play(winClip, 1f, 1f); }
    public void PlayClick() { Play(clickClip, 0.8f, 1f); }
}
