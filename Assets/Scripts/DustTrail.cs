using UnityEngine;

/// <summary>
/// LAB 4 - spawns a dust puff at the character's feet while he is moving fast.
/// Speed is measured from how far the character actually moved this frame (root motion),
/// so it works with the walk/run blend tree: walk is about 1.4 m/s, run about 3.8 m/s.
/// </summary>
public class DustTrail : MonoBehaviour
{
    [Header("Dust")]
    [Tooltip("Drag the DustPuff prefab here.")]
    [SerializeField] private ParticleSystem dustPrefab;
    [Tooltip("Offset from the character's pivot (his feet) where the dust appears.")]
    [SerializeField] private Vector3 feetOffset = new Vector3(0f, 0.03f, 0f);

    [Header("Trigger")]
    [Tooltip("Speed in metres per second above which dust spawns. Between walk (~1.4) and run (~3.8).")]
    [SerializeField] private float speedThreshold = 2.5f;
    [Tooltip("Minimum seconds between puffs while above the threshold.")]
    [SerializeField] private float spawnInterval = 0.18f;
    [Tooltip("How quickly the measured speed settles (higher = snappier).")]
    [SerializeField] private float speedSmoothing = 15f;

    /// <summary>Current ground speed in metres per second.</summary>
    public float Speed { get; private set; }

    private Vector3 lastPosition;
    private float cooldown;

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f; // ground speed only
        lastPosition = transform.position;

        float instant = delta.magnitude / dt;
        Speed = Mathf.Lerp(Speed, instant, 1f - Mathf.Exp(-speedSmoothing * dt));

        cooldown -= dt;
        if (Speed > speedThreshold && cooldown <= 0f && dustPrefab != null)
        {
            Instantiate(dustPrefab, transform.position + feetOffset, Quaternion.identity);
            cooldown = spawnInterval;
        }
    }
}
