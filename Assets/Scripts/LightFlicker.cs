using UnityEngine;

/// <summary>Warm flickering light for campfires and lanterns.</summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    public float baseIntensity = 2f;
    public float flicker = 0.4f;
    public float speed = 6f;

    Light lightSource;
    float seed;

    void Awake()
    {
        lightSource = GetComponent<Light>();
        seed = Random.value * 100f;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed);
        lightSource.intensity = baseIntensity + (n - 0.5f) * 2f * flicker;
    }
}
