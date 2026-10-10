using UnityEngine;

/// <summary>
/// Steers the character: A/D (left stick X) rotates him around the Y axis.
/// Reads MoveInput from PlayerMovementInput. Forward/back movement itself comes
/// from the walk/run root-motion clips in the blend tree.
/// </summary>
[RequireComponent(typeof(PlayerMovementInput))]
public class PlayerTurn : MonoBehaviour
{
    [Tooltip("Degrees per second at full left/right input.")]
    [SerializeField] private float turnSpeed = 120f;

    private PlayerMovementInput input;

    private void Awake()
    {
        input = GetComponent<PlayerMovementInput>();
    }

    private void Update()
    {
        transform.Rotate(0f, input.MoveInput.x * turnSpeed * Time.deltaTime, 0f, Space.World);
    }
}
