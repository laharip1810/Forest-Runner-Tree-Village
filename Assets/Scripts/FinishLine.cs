using UnityEngine;

/// <summary>Trigger at the village gate. Reaching it completes the level.</summary>
public class FinishLine : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        if (GameManager.Instance != null) GameManager.Instance.LevelComplete();
    }
}
