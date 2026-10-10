using System;
using UnityEngine;

/// <summary>
/// Third-person character motor.
/// - Walk/run: camera-relative input, the character turns toward it, and the walk/run root-motion clips
///   provide the real speed (so feet never slide).
/// - Jump: code-driven (variable height, coyote time, buffered input, heavier fall).
/// - Crouch: shrinks the capsule, slows the animation, lowers the pose (see CrouchPoseIK) and
///   refuses to stand up under a low ceiling.
/// OnAnimatorMove is defined here, so this script applies root motion through the CharacterController.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    [Header("Locomotion")]
    [SerializeField] private float turnSpeed = 840f;
    [SerializeField] private float airTurnSpeed = 300f;
    [SerializeField] private float speedDamp = 0.07f;
    [SerializeField] private float runSpeedReference = 3.8f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -24f;
    [SerializeField] private float fallMultiplier = 1.6f;
    [SerializeField] private float jumpCutMultiplier = 2.4f;
    [SerializeField] private float minJumpTime = 0.12f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float airAcceleration = 9f;
    [SerializeField] private float maxFallSpeed = 32f;
    [SerializeField] private float groundedStick = -3f;

    [Header("Crouch")]
    [SerializeField] private float standingHeight = 1.6f;
    [SerializeField] private float crouchHeight = 0.95f;
    [SerializeField] private float capsuleResizeSpeed = 9f;
    [SerializeField] private float crouchPoseSpeed = 7f;
    [Tooltip("True when the character has a real crouch animation. The IK crouch pose is then only used for the landing dip.")]
    [SerializeField] private bool animatedCrouch = true;

    public event Action Jumped;
    public event Action<float> Landed;
    public event Action<bool> CrouchChanged;

    public bool IsGrounded { get; private set; }
    public bool IsCrouching { get { return crouching; } }
    public float HorizontalSpeed { get; private set; }
    public float VerticalVelocity { get { return verticalVelocity; } }
    /// <summary>0 = standing, 1 = fully crouched. Also dips briefly when landing hard.</summary>
    public float CrouchPose { get { return animatedCrouch ? landingDip : Mathf.Max(crouchPose, landingDip); } }

    private Animator animator;
    private CharacterController cc;
    private PlayerInputReader input;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int VerticalHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int CrouchingHash = Animator.StringToHash("Crouching");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private float verticalVelocity;
    private Vector3 airVelocity, groundVelocity, desiredDir;
    private float lastGroundedTime = -10f, jumpStartTime = -10f;
    private bool jumpedSinceGround, crouching;
    private float crouchPose, landingDip;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        cc = GetComponent<CharacterController>();
        input = GetComponent<PlayerInputReader>();
        animator.applyRootMotion = true;
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        cc.height = standingHeight;
        cc.center = new Vector3(0f, standingHeight * 0.5f, 0f);
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        cc.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        cc.enabled = true;
        verticalVelocity = 0f; airVelocity = Vector3.zero; groundVelocity = Vector3.zero;
    }

    private bool CanStand()
    {
        float r = cc.radius * 0.95f;
        Vector3 p = transform.position;
        Vector3 bottom = p + Vector3.up * (r + 0.12f);
        Vector3 top = p + Vector3.up * (standingHeight - r);
        var hits = Physics.OverlapCapsule(bottom, top, r, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (!h.transform.IsChildOf(transform)) return false;
        return true;
    }

    private bool ProbeGround()
    {
        Vector3 origin = transform.position + Vector3.up * (cc.radius + 0.05f);
        RaycastHit hit;
        return Physics.SphereCast(origin, cc.radius * 0.9f, Vector3.down, out hit, 0.35f, ~0, QueryTriggerInteraction.Ignore);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        bool groundedNow = cc.isGrounded;       // result of the last Move() call
        IsGrounded = groundedNow;
        if (groundedNow)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity <= 0f) jumpedSinceGround = false;
        }

        // ---- camera-relative desired direction ----
        Vector2 m = input.Move;
        float mag = m.magnitude;
        Vector3 camF = Vector3.forward;
        if (cameraTransform != null)
        {
            camF = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (camF.sqrMagnitude < 0.001f) camF = Vector3.forward;
            camF.Normalize();
        }
        Vector3 camR = Vector3.Cross(Vector3.up, camF);
        desiredDir = mag > 0.01f ? (camF * m.y + camR * m.x).normalized : Vector3.zero;

        // ---- crouch (hold) ----
        bool wantCrouch = input.CrouchHeld;
        if (wantCrouch && groundedNow && !crouching)
        {
            crouching = true;
            if (CrouchChanged != null) CrouchChanged(true);
        }
        else if (!wantCrouch && crouching && CanStand())
        {
            crouching = false;
            if (CrouchChanged != null) CrouchChanged(false);
        }

        // ---- jump (buffered + coyote time) ----
        if (input.HasBufferedJump && !jumpedSinceGround && (Time.time - lastGroundedTime) <= coyoteTime)
        {
            if (!crouching || CanStand())
            {
                if (crouching) { crouching = false; if (CrouchChanged != null) CrouchChanged(false); }
                DoJump();
            }
        }

        // ---- turn toward the input direction ----
        if (mag > 0.05f)
        {
            float rs = groundedNow ? turnSpeed : airTurnSpeed;
            Quaternion target = Quaternion.LookRotation(desiredDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rs * dt);
        }

        // ---- animator parameters ----
        float align = mag > 0.05f ? Vector3.Dot(transform.forward, desiredDir) : 1f;
        float alignFactor = Mathf.InverseLerp(-0.2f, 0.85f, align);   // slows through sharp turns instead of skating
        float speedParam = mag * alignFactor;
        if (crouching) speedParam = Mathf.Min(speedParam, 0.5f);
        animator.SetFloat(SpeedHash, speedParam, speedDamp, dt);

        bool animGrounded = groundedNow || (verticalVelocity <= 0.5f && ProbeGround());
        animator.SetBool(GroundedHash, animGrounded);
        animator.SetFloat(VerticalHash, verticalVelocity);
        animator.SetBool(CrouchingHash, crouching);

        // ---- capsule + crouch pose ----
        float targetH = crouching ? crouchHeight : standingHeight;
        cc.height = Mathf.MoveTowards(cc.height, targetH, capsuleResizeSpeed * dt);
        cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
        crouchPose = Mathf.MoveTowards(crouchPose, crouching ? 1f : 0f, crouchPoseSpeed * dt);
        landingDip = Mathf.MoveTowards(landingDip, 0f, 3.5f * dt);
    }

    private void DoJump()
    {
        verticalVelocity = Mathf.Sqrt(2f * -gravity * jumpHeight);
        airVelocity = groundVelocity;           // carry the run speed into the air
        jumpedSinceGround = true;
        jumpStartTime = Time.time;
        lastGroundedTime = -10f;
        input.ConsumeJump();
        animator.SetBool(GroundedHash, false);
        animator.SetTrigger(JumpHash);
        if (Jumped != null) Jumped();
    }

    private void OnAnimatorMove()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        bool wasGrounded = cc.isGrounded;

        // vertical: gravity with variable jump height and a heavier fall
        if (wasGrounded && verticalVelocity <= 0f)
        {
            verticalVelocity = groundedStick;
        }
        else
        {
            float g = gravity;
            if (verticalVelocity > 0f && !input.JumpHeld && Time.time - jumpStartTime > minJumpTime) g *= jumpCutMultiplier;
            else if (verticalVelocity < 0f) g *= fallMultiplier;
            verticalVelocity = Mathf.Max(verticalVelocity + g * dt, -maxFallSpeed);
        }
        float vyUsed = verticalVelocity;

        // horizontal: root motion on the ground, carried momentum + air control in the air
        Vector3 flat;
        if (wasGrounded)
        {
            Vector3 d = animator.deltaPosition; d.y = 0f;
            flat = d * (1f - landingDip * 0.6f);
            groundVelocity = d / dt;
        }
        else
        {
            float mag = input.Move.magnitude;
            if (mag > 0.1f)
            {
                float speed = Mathf.Max(airVelocity.magnitude, mag * runSpeedReference * 0.75f);
                airVelocity = Vector3.MoveTowards(airVelocity, desiredDir * speed, airAcceleration * dt);
            }
            flat = airVelocity * dt;
        }

        Vector3 move = flat;
        move.y = vyUsed * dt;
        cc.Move(move);

        HorizontalSpeed = Mathf.Lerp(HorizontalSpeed, flat.magnitude / dt, 1f - Mathf.Exp(-15f * dt));

        if ((cc.collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

        if (!wasGrounded && cc.isGrounded)
        {
            float impact = Mathf.Abs(vyUsed);
            if (impact > 3f)
            {
                if (impact > 6f) landingDip = Mathf.Clamp(impact / 18f, 0.2f, 0.65f);
                if (Landed != null) Landed(impact);
            }
            verticalVelocity = groundedStick;
        }
    }
}
