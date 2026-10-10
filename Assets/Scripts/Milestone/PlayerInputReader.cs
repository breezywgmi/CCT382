using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads Move / Jump / Crouch / Sprint from PlayerControls.inputactions (keyboard + gamepad).
/// Movement is run-only: any move input runs at full speed (no walk).
/// Jump presses are buffered for a short time so a press just before landing still counts.
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [Tooltip("Keyboard walks at this strength unless Sprint is held.")]
    [SerializeField, Range(0.2f, 1f)] private float keyboardWalkScale = 0.5f;
    [SerializeField] private float jumpBufferTime = 0.15f;

    private InputActionMap map;
    private InputAction move, jump, crouch, sprint;
    private float jumpBufferedUntil = -1f;

    public Vector2 Move { get; private set; }
    public bool SprintHeld { get { return sprint != null && sprint.IsPressed(); } }
    public bool CrouchHeld { get { return crouch != null && crouch.IsPressed(); } }
    public bool JumpHeld { get { return jump != null && jump.IsPressed(); } }
    public bool HasBufferedJump { get { return Time.time <= jumpBufferedUntil; } }
    public void ConsumeJump() { jumpBufferedUntil = -1f; }

    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogError(name + ": assign PlayerControls.inputactions to PlayerInputReader.");
            return;
        }
        map = inputActions.FindActionMap(actionMapName, false);
        if (map == null) { Debug.LogError(name + ": action map not found: " + actionMapName); return; }
        move = map.FindAction("Move", false);
        jump = map.FindAction("Jump", false);
        crouch = map.FindAction("Crouch", false);
        sprint = map.FindAction("Sprint", false);
        if (move == null || jump == null || crouch == null || sprint == null)
            Debug.LogError(name + ": PlayerControls needs Move, Jump, Crouch and Sprint actions.");
    }

    private void OnEnable() { if (map != null) map.Enable(); }
    private void OnDisable() { if (map != null) map.Disable(); }

    private void Update()
    {
        Vector2 raw = move != null ? move.ReadValue<Vector2>() : Vector2.zero;
        // Always run: any input above the dead zone moves at full speed (W/S forward and back, A/D strafe-turn).
        Move = raw.sqrMagnitude > 0.04f ? raw.normalized : Vector2.zero;

        if (jump != null && jump.WasPressedThisFrame()) jumpBufferedUntil = Time.time + jumpBufferTime;
    }
}
