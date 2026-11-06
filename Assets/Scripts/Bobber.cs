using UnityEngine;

/// <summary>Gentle floating motion for fireflies, lanterns and other decorations.</summary>
public class Bobber : MonoBehaviour
{
    public float amplitude = 0.25f;
    public float speed = 1.5f;
    public Vector3 drift = Vector3.zero;
    public float phase;

    Vector3 basePos;

    void Start()
    {
        basePos = transform.localPosition;
    }

    void Update()
    {
        float t = Time.time * speed + phase;
        transform.localPosition = basePos
            + new Vector3(Mathf.Sin(t * 0.7f) * drift.x, Mathf.Sin(t) * amplitude, Mathf.Cos(t * 0.6f) * drift.z);
    }
}
