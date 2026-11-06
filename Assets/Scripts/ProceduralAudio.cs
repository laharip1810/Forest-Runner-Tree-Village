using UnityEngine;

/// <summary>
/// Tiny synthesiser used only as a fallback when the WAV files in Assets/Audio are not assigned.
/// </summary>
public static class ProceduralAudio
{
    const int Rate = 44100;

    static float Note(int semitonesFromA4) { return 440f * Mathf.Pow(2f, semitonesFromA4 / 12f); }

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static void AddTone(float[] buf, float start, float dur, float freq, float vol, float decay)
    {
        int s0 = (int)(start * Rate);
        int n = (int)(dur * Rate);
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 200f);
            float w = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.8f + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.2f;
            buf[s0 + i] += w * env * vol;
        }
    }

    public static AudioClip Coin()
    {
        var b = new float[(int)(0.35f * Rate)];
        AddTone(b, 0f, 0.1f, Note(14), 0.5f, 20f);
        AddTone(b, 0.07f, 0.28f, Note(19), 0.5f, 12f);
        return Make("CoinFallback", b);
    }

    public static AudioClip Jump()
    {
        int n = (int)(0.22f * Rate);
        var b = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(260f, 620f, t / 0.22f);
            phase += 2f * Mathf.PI * f / Rate;
            b[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 9f) * 0.4f;
        }
        return Make("JumpFallback", b);
    }

    public static AudioClip Hit()
    {
        int n = (int)(0.5f * Rate);
        var b = new float[n];
        var rng = new System.Random(3);
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            phase += 2f * Mathf.PI * Mathf.Lerp(140f, 50f, t * 2f) / Rate;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            b[i] = (Mathf.Sin(phase) * 0.7f + noise * 0.3f) * Mathf.Exp(-t * 7f) * 0.7f;
        }
        return Make("HitFallback", b);
    }

    public static AudioClip Win()
    {
        var b = new float[(int)(1.6f * Rate)];
        int[] notes = { 3, 7, 10, 15 };
        for (int i = 0; i < notes.Length; i++) AddTone(b, i * 0.14f, 1.2f, Note(notes[i]), 0.3f, 3f);
        AddTone(b, 0.56f, 1.0f, Note(10), 0.2f, 2.5f);
        return Make("WinFallback", b);
    }

    public static AudioClip Click()
    {
        var b = new float[(int)(0.08f * Rate)];
        AddTone(b, 0f, 0.08f, Note(7), 0.4f, 40f);
        return Make("ClickFallback", b);
    }

    public static AudioClip Music()
    {
        float bpm = 100f;
        float beat = 60f / bpm;
        int bars = 8;
        var b = new float[(int)(bars * 4 * beat * Rate)];
        int[] chords = { -9, -5, -4, -7 }; // C, E-ish, F, D (relative to A4)
        int[] melody = { 3, 7, 10, 7, 12, 10, 7, 5, 3, 5, 7, 10, 7, 5, 3, 0 };
        for (int bar = 0; bar < bars; bar++)
        {
            int root = chords[bar % chords.Length];
            for (int k = 0; k < 4; k++)
            {
                float t = (bar * 4 + k) * beat;
                AddTone(b, t, beat * 1.2f, Note(root - 12), 0.12f, 2.5f);
                AddTone(b, t + beat * 0.5f, beat * 0.6f, Note(root + 7 - 12), 0.06f, 5f);
                int m = melody[(bar * 4 + k) % melody.Length];
                if ((bar + k) % 3 != 2) AddTone(b, t, beat * 0.9f, Note(m), 0.08f, 4f);
            }
        }
        return Make("MusicFallback", b);
    }
}
