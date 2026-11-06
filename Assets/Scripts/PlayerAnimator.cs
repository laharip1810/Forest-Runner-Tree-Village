using UnityEngine;

/// <summary>
/// Procedural animation for the primitive-built runner character:
/// idle breathing, running limb swing, jump pose, crash and victory celebration.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    public Transform model;
    public Transform leftArm;
    public Transform rightArm;
    public Transform leftLeg;
    public Transform rightLeg;
    public Transform head;

    float speed;
    bool grounded = true;
    float lateral;
    float runCycle;
    bool dead;
    bool celebrating;
    float stateTimer;
    float jumpSquash;

    public void ResetPose()
    {
        dead = false;
        celebrating = false;
        stateTimer = 0f;
        runCycle = 0f;
        jumpSquash = 0f;
        if (model != null)
        {
            model.localRotation = Quaternion.identity;
            model.localPosition = Vector3.zero;
            model.localScale = Vector3.one;
        }
    }

    public void SetState(float forwardSpeed, bool isGrounded, float lateralOffset)
    {
        speed = forwardSpeed;
        grounded = isGrounded;
        lateral = lateralOffset;
    }

    public void OnJump() { jumpSquash = 1f; }
    public void Die() { dead = true; stateTimer = 0f; }
    public void Celebrate() { celebrating = true; stateTimer = 0f; }

    static void SetLimb(Transform t, float angle)
    {
        if (t != null) t.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }

    void Update()
    {
        if (model == null) return;
        float dt = Time.deltaTime;
        stateTimer += dt;
        jumpSquash = Mathf.MoveTowards(jumpSquash, 0f, dt * 5f);

        if (dead)
        {
            // Fall backwards after crashing.
            float k = Mathf.Clamp01(stateTimer / 0.35f);
            model.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -80f, k), 0f, 0f);
            model.localPosition = new Vector3(0f, Mathf.Lerp(0f, 0.3f, k), 0f);
            SetLimb(leftArm, -150f); SetLimb(rightArm, -150f);
            SetLimb(leftLeg, 20f); SetLimb(rightLeg, -10f);
            return;
        }

        if (celebrating && speed < 0.5f)
        {
            // Happy hops and arms up.
            float hop = Mathf.Abs(Mathf.Sin(stateTimer * 6f)) * 0.45f;
            model.localPosition = new Vector3(0f, hop, 0f);
            model.localRotation = Quaternion.Euler(0f, stateTimer * 140f, 0f);
            float wave = Mathf.Sin(stateTimer * 12f) * 15f;
            SetLimb(leftArm, -165f + wave); SetLimb(rightArm, -165f - wave);
            SetLimb(leftLeg, 0f); SetLimb(rightLeg, 0f);
            return;
        }

        float lean = Mathf.Clamp(-lateral * 10f, -20f, 20f);

        if (!grounded)
        {
            model.localRotation = Quaternion.Euler(10f, 0f, lean);
            model.localPosition = Vector3.zero;
            SetLimb(leftArm, -130f); SetLimb(rightArm, -130f);
            SetLimb(leftLeg, -45f); SetLimb(rightLeg, 25f);
        }
        else if (speed > 0.5f)
        {
            runCycle += dt * speed * 1.25f;
            float s = Mathf.Sin(runCycle);
            float bounce = Mathf.Abs(Mathf.Cos(runCycle)) * 0.12f;
            model.localPosition = new Vector3(0f, bounce, 0f);
            model.localRotation = Quaternion.Euler(12f, 0f, lean);
            SetLimb(leftArm, s * 60f); SetLimb(rightArm, -s * 60f);
            SetLimb(leftLeg, -s * 55f); SetLimb(rightLeg, s * 55f);
        }
        else
        {
            float breathe = Mathf.Sin(Time.time * 2.2f);
            model.localPosition = new Vector3(0f, breathe * 0.02f, 0f);
            model.localRotation = Quaternion.Euler(0f, 0f, 0f);
            SetLimb(leftArm, breathe * 4f); SetLimb(rightArm, -breathe * 4f);
            SetLimb(leftLeg, 0f); SetLimb(rightLeg, 0f);
        }

        float squash = 1f + jumpSquash * 0.15f;
        model.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));

        if (head != null)
        {
            head.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 1.3f) * (speed > 0.5f ? 3f : 10f), 0f);
        }
    }
}
