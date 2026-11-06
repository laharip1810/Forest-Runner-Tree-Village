using UnityEngine;

/// <summary>Rotating golden coin collected through trigger detection.</summary>
public class Coin : MonoBehaviour
{
    public float spinSpeed = 160f;
    public float bobHeight = 0.12f;
    public int value = 1;

    Vector3 startPos;
    Vector3 startScale;
    bool collected;
    float collectTimer;
    bool initialised;

    void Init()
    {
        if (initialised) return;
        initialised = true;
        startPos = transform.position;
        startScale = transform.localScale;
    }

    void Awake() { Init(); }

    public void ResetCoin()
    {
        Init();
        collected = false;
        collectTimer = 0f;
        transform.position = startPos;
        transform.localScale = startScale;
        gameObject.SetActive(true);
    }

    void Update()
    {
        if (!collected)
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            float y = Mathf.Sin(Time.time * 3f + startPos.z * 0.5f) * bobHeight;
            transform.position = startPos + new Vector3(0f, y, 0f);
            return;
        }

        // Pop-up animation after collection.
        collectTimer += Time.deltaTime;
        float t = collectTimer / 0.3f;
        transform.Rotate(0f, spinSpeed * 4f * Time.deltaTime, 0f, Space.World);
        transform.position += Vector3.up * 6f * Time.deltaTime;
        transform.localScale = startScale * Mathf.Lerp(1.3f, 0f, t);
        if (t >= 1f) gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (other.GetComponent<PlayerController>() == null) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing) return;
        collected = true;
        collectTimer = 0f;
        gm.AddCoin(value);
    }
}
