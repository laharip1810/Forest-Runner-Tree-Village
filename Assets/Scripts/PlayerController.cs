using UnityEngine;

/// <summary>
/// Auto-running player: lane-based left/right movement, jumping, gravity,
/// ground detection, obstacle collision and fall detection.
/// Uses a CharacterController (no Rigidbody physics needed).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Running")]
    public float startSpeed = 10f;
    public float maxSpeed = 12.5f;
    public float acceleration = 0.08f;

    [Header("Lanes")]
    public float laneDistance = 2f;
    public float laneChangeSpeed = 13f;

    [Header("Jumping")]
    public float jumpVelocity = 9f;
    public float gravity = 25f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.15f;

    [Header("Falling")]
    public float fallDeathY = -2f;

    [Header("References")]
    public PlayerAnimator animator;
    public AudioManager audioManager;

    public float CurrentSpeed { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsDead { get; private set; }
    public bool HasFallen { get; private set; }
    public float VerticalVelocity { get { return verticalVelocity; } }

    CharacterController controller;
    Vector3 startPosition;
    int lane;
    float verticalVelocity;
    float lastGroundedTime = -10f;
    float lastJumpPressTime = -10f;
    bool finished;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        startPosition = transform.position;
        CurrentSpeed = startSpeed;
    }

    public void ResetPlayer()
    {
        controller.enabled = false;
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;
        controller.enabled = true;
        lane = 0;
        verticalVelocity = 0f;
        lastGroundedTime = -10f;
        lastJumpPressTime = -10f;
        CurrentSpeed = startSpeed;
        IsDead = false;
        HasFallen = false;
        finished = false;
        if (animator != null) animator.ResetPose();
    }

    public void Finish()
    {
        finished = true;
        if (animator != null) animator.Celebrate();
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || !controller.enabled) return;
        float dt = Time.deltaTime;
        bool playing = gm.State == GameState.Playing && !IsDead;

        // ---------- Input ----------
        if (playing && Time.frameCount != gm.StartFrame)
        {
            if (InputHelper.LeftPressed()) lane = Mathf.Max(-1, lane - 1);
            if (InputHelper.RightPressed()) lane = Mathf.Min(1, lane + 1);
            if (InputHelper.JumpPressed()) lastJumpPressTime = Time.time;
        }

        // ---------- Ground & gravity ----------
        IsGrounded = controller.isGrounded;
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f) verticalVelocity = -2f;
        }

        if (playing
            && Time.time - lastJumpPressTime <= jumpBufferTime
            && Time.time - lastGroundedTime <= coyoteTime
            && verticalVelocity <= 0f)
        {
            verticalVelocity = jumpVelocity;
            lastGroundedTime = -10f;
            lastJumpPressTime = -10f;
            if (animator != null) animator.OnJump();
            if (audioManager != null) audioManager.PlayJump();
        }

        verticalVelocity -= gravity * dt;
        verticalVelocity = Mathf.Max(verticalVelocity, -40f);

        // ---------- Forward speed ----------
        float forward = 0f;
        if (playing)
        {
            CurrentSpeed = Mathf.Min(maxSpeed, CurrentSpeed + acceleration * dt);
            forward = CurrentSpeed;
        }
        else if (finished)
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, 14f * dt);
            forward = CurrentSpeed;
        }
        else if (HasFallen)
        {
            forward = CurrentSpeed * 0.5f;
        }
        else if (IsDead)
        {
            CurrentSpeed = 0f;
        }

        // ---------- Lateral (lane) movement ----------
        float targetX = lane * laneDistance;
        float x = transform.position.x;
        float newX = Mathf.MoveTowards(x, targetX, laneChangeSpeed * dt);
        if (IsDead && !HasFallen) newX = x;

        Vector3 move = new Vector3(newX - x, verticalVelocity * dt, forward * dt);
        controller.Move(move);

        if (animator != null)
        {
            animator.SetState(forward, IsGrounded, targetX - transform.position.x);
        }

        // ---------- Fall detection ----------
        if (gm.State == GameState.Playing && !IsDead && transform.position.y < fallDeathY)
        {
            IsDead = true;
            HasFallen = true;
            gm.GameOver("You fell off the broken bridge!", true);
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing || IsDead) return;

        var obstacle = hit.collider.GetComponentInParent<Obstacle>();
        if (obstacle == null) return;

        // Landing on top of an obstacle is allowed; hitting its side is not.
        if (hit.normal.y > 0.55f) return;

        IsDead = true;
        if (animator != null) animator.Die();
        gm.GameOver(obstacle.hitMessage, false);
    }
}
