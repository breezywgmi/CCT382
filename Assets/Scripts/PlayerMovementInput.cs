using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// LAB 3 - GOAL 2 & GOAL 3
/// Reads the Move action (WASD / left stick) from PlayerControls.inputactions every frame
/// and feeds it to the Animator so the 2D Freeform Directional blend tree can blend.
/// Goes on the character next to its Animator. Drag PlayerControls.inputactions into the
/// "Input Actions" slot in the Inspector.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerMovementInput : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Drag PlayerControls.inputactions here.")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";

    [Header("Animator Parameters")]
    // Must exactly match the float parameters in the Animator (capitalization matters).
    [SerializeField] private string moveXParam = "MoveX";
    [SerializeField] private string moveYParam = "MoveY";

    [Header("Smoothing")]
    // This number is the response curve: small = fast attack, snappy blending;
    // bigger = slow attack, floatier blending.
    [SerializeField] private float dampTime = 0.1f;

    private InputAction moveAction;
    private Animator animator;

    /// <summary>The current input direction, updated every frame.</summary>
    public Vector2 MoveInput { get; private set; }

    // Awake() runs once at start: finds the "Player" map and the "Move" action,
    // and logs a clear Console error if something isn't wired up.
    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (inputActions == null)
        {
            Debug.LogError($"{name}: No InputActionAsset assigned to PlayerMovementInput. " +
                           "Drag PlayerControls.inputactions into the Input Actions field.");
            return;
        }

        var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
        if (map == null)
        {
            Debug.LogError($"{name}: Action map \"{actionMapName}\" not found in {inputActions.name}.");
            return;
        }

        moveAction = map.FindAction(moveActionName, throwIfNotFound: false);
        if (moveAction == null)
        {
            Debug.LogError($"{name}: Action \"{moveActionName}\" not found in map \"{actionMapName}\".");
        }
    }

    // Turn the action's listening on/off with the GameObject. Skipping this means
    // the action never fires - the classic Input System pitfall.
    private void OnEnable()
    {
        moveAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
    }

    // Update() runs once per frame.
    private void Update()
    {
        // Current WASD / left-stick value as a Vector2 ((0,0) = no input).
        MoveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        // Feed MoveInput into the Animator so the blend tree can use it.
        animator.SetFloat(moveXParam, MoveInput.x, dampTime, Time.deltaTime);
        animator.SetFloat(moveYParam, MoveInput.y, dampTime, Time.deltaTime);
    }
}
