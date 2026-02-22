using UnityEngine;

/// <summary>
/// Controls player movement: automatic forward movement and jumping.
/// Movement can be enabled/disabled by GameManager for state transitions.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float forwardSpeed = 10f;

    [Header("Jump")]
    [SerializeField] float jumpForce = 10f;

    [Header("Ground Detection")]
    [SerializeField] float groundTolerance = 0.05f;

    [Header("References")]
    [SerializeField] InputProcessor inputProcessor;

    Rigidbody rb;

    float groundY;
    bool isGrounded;
    bool jumpRequested;
    bool movementEnabled;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        groundY = transform.position.y;
    }

    void Start()
    {
        if (inputProcessor == null)
            inputProcessor = FindObjectOfType<InputProcessor>();

        if (inputProcessor == null)
        {
            Debug.LogError("[PlayerController] No InputProcessor found in scene!");
            return;
        }

        inputProcessor.OnJumpPressed += OnJumpPressed;
    }

    void OnDestroy()
    {
        if (inputProcessor != null)
            inputProcessor.OnJumpPressed -= OnJumpPressed;
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;

        if (!enabled)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
            jumpRequested = false;
        }
        else
        {
            rb.isKinematic = false;
        }
    }

    void OnJumpPressed()
    {
        if (movementEnabled)
            jumpRequested = true;
    }

    void Update()
    {
        if (movementEnabled)
            CheckGrounded();
    }

    void FixedUpdate()
    {
        if (!movementEnabled) return;

        MoveForward();

        if (jumpRequested && isGrounded)
            Jump();

        jumpRequested = false;
    }

    void MoveForward()
    {
        Vector3 velocity = rb.velocity;
        velocity.z = forwardSpeed;
        rb.velocity = velocity;
    }

    void Jump()
    {
        Vector3 velocity = rb.velocity;
        velocity.y = 0f;
        rb.velocity = velocity;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    void CheckGrounded()
    {
        isGrounded = transform.position.y <= groundY + groundTolerance;
    }
}
