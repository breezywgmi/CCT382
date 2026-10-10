using UnityEngine;

/// <summary>
/// Moves the character with the Animator's root motion THROUGH a CharacterController,
/// so walls, steps and ramps in the gym block him, and adds gravity so he follows the ground.
/// (Defining OnAnimatorMove takes over from Unity's default root motion handling.)
/// Needs: Animator with Apply Root Motion ON, plus a CharacterController.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public class RootMotionCharacterMover : MonoBehaviour
{
    [SerializeField] private float gravity = -20f;
    [Tooltip("Small downward push while grounded so he stays glued to ramps.")]
    [SerializeField] private float groundedStick = -2f;

    private Animator animator;
    private CharacterController controller;
    private float verticalVelocity;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
    }

    private void OnAnimatorMove()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        verticalVelocity = controller.isGrounded ? groundedStick : verticalVelocity + gravity * dt;

        Vector3 move = animator.deltaPosition;   // horizontal motion from the walk/run clips
        move.y = verticalVelocity * dt;          // vertical motion comes from gravity only
        controller.Move(move);

        transform.rotation *= animator.deltaRotation;
    }
}
