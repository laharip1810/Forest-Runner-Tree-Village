using UnityEngine;

/// <summary>
/// Smooth third-person follow camera. Slowly orbits the player on the main menu
/// and holds position when the player falls, so the fall reads clearly.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 3.8f, -7.5f);
    public Vector3 lookOffset = new Vector3(0f, 1.3f, 5f);
    public float smoothTime = 0.12f;
    public float lateralFollow = 0.6f;

    [Header("Menu orbit")]
    public float orbitRadius = 8.5f;
    public float orbitHeight = 3.2f;
    public float orbitSpeed = 12f;

    Vector3 velocity;
    float orbitAngle = 200f;
    float shakeTime;
    float shakeStrength;

    public void Snap()
    {
        if (target == null) return;
        transform.position = DesiredFollowPosition();
        transform.LookAt(target.position + lookOffset);
        velocity = Vector3.zero;
    }

    public void Shake(float strength, float duration)
    {
        shakeStrength = strength;
        shakeTime = duration;
    }

    Vector3 DesiredFollowPosition()
    {
        Vector3 p = target.position;
        return new Vector3(p.x * lateralFollow, p.y, p.z) + offset;
    }

    void LateUpdate()
    {
        if (target == null) return;
        var gm = GameManager.Instance;
        GameState state = gm != null ? gm.State : GameState.Playing;

        Vector3 desired;
        Vector3 look;

        if (state == GameState.Menu)
        {
            orbitAngle += orbitSpeed * Time.deltaTime;
            float rad = orbitAngle * Mathf.Deg2Rad;
            desired = target.position + new Vector3(Mathf.Sin(rad) * orbitRadius, orbitHeight, Mathf.Cos(rad) * orbitRadius);
            look = target.position + Vector3.up * 1.2f;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.6f);
        }
        else
        {
            bool fell = gm != null && gm.PlayerFell;
            look = target.position + (fell ? Vector3.up : lookOffset);
            if (!fell)
            {
                desired = DesiredFollowPosition();
                if (state == GameState.LevelComplete)
                {
                    // Swing round to the front of the player to watch the celebration.
                    desired = target.position + new Vector3(3f, 2.6f, 7f);
                    look = target.position + Vector3.up * 1.2f;
                    transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.8f);
                }
                else
                {
                    transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
                }
            }
        }

        Quaternion targetRot = Quaternion.LookRotation(look - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-10f * Time.deltaTime));

        if (shakeTime > 0f)
        {
            shakeTime -= Time.deltaTime;
            transform.position += Random.insideUnitSphere * shakeStrength * Mathf.Clamp01(shakeTime * 4f);
        }
    }
}
