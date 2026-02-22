using UnityEngine;

/// <summary>
/// Controls player movement: forward movement, jumping, and sliding.
/// Movement can be enabled/disabled by GameManager for state transitions.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float forwardSpeed = 10f;

    [Header("Jump")]
    [SerializeField] float jumpForce = 10f;

    [Header("Slide")]
    [SerializeField] float slideDuration = 0.5f;
    [SerializeField] float slideHeightScale = 0.25f;
    [SerializeField] float slideTransitionSpeed = 20f;

    [Header("Shooting")]
    [SerializeField] GameObject projectilePrefab;
    [SerializeField] float shootCooldown = 0.3f;
    [SerializeField] int projectilePoolSize = 10;
    [SerializeField] Vector3 shootOffset = new(0f, 0.5f, 1f);

    [Header("Ground Detection")]
    [SerializeField] float groundTolerance = 0.05f;

    [Header("References")]
    [SerializeField] InputProcessor inputProcessor;

    Rigidbody rb;

    float groundY;
    bool isGrounded;
    bool jumpRequested;
    bool movementEnabled;

    // Slide state
    float slideTimer;
    bool isSliding;
    float targetScaleY = 1f;

    // Shooting state
    float shootCooldownTimer;
    GameObject[] projectilePool;

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
        inputProcessor.OnSlidePressed += OnSlidePressed;
        inputProcessor.OnShootPressed += OnShootPressed;

        InitProjectilePool();
    }

    void OnDestroy()
    {
        if (inputProcessor != null)
        {
            inputProcessor.OnJumpPressed -= OnJumpPressed;
            inputProcessor.OnSlidePressed -= OnSlidePressed;
            inputProcessor.OnShootPressed -= OnShootPressed;
        }
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;

        if (!enabled)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
            jumpRequested = false;
            if (isSliding) EndSlide();
        }
        else
        {
            rb.isKinematic = false;
        }
    }

    void OnJumpPressed()
    {
        if (movementEnabled && !isSliding)
            jumpRequested = true;
    }

    void OnSlidePressed()
    {
        if (movementEnabled && isGrounded && !isSliding)
            StartSlide();
    }

    void Update()
    {
        if (!movementEnabled) return;

        CheckGrounded();

        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0f)
                EndSlide();
        }

        UpdateSlideScale();

        if (shootCooldownTimer > 0f)
            shootCooldownTimer -= Time.deltaTime;
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

    void StartSlide()
    {
        isSliding = true;
        slideTimer = slideDuration;
        targetScaleY = slideHeightScale;
    }

    void EndSlide()
    {
        isSliding = false;
        targetScaleY = 1f;
    }

    void UpdateSlideScale()
    {
        float currentY = transform.localScale.y;
        if (Mathf.Approximately(currentY, targetScaleY)) return;

        float newScaleY = Mathf.MoveTowards(currentY, targetScaleY, slideTransitionSpeed * Time.deltaTime);

        Vector3 scale = transform.localScale;
        scale.y = newScaleY;
        transform.localScale = scale;

        Vector3 pos = transform.position;
        pos.y = groundY * newScaleY;
        transform.position = pos;
    }

    void CheckGrounded()
    {
        isGrounded = transform.position.y <= groundY + groundTolerance;
    }

    // ---- Shooting ----

    void OnShootPressed()
    {
        if (!movementEnabled || shootCooldownTimer > 0f) return;

        Shoot();
        shootCooldownTimer = shootCooldown;
    }

    void Shoot()
    {
        if (projectilePool == null) return;

        GameObject obj = GetInactiveProjectile();
        if (obj == null) return;

        var projectile = obj.GetComponent<Projectile>();
        projectile.Fire(transform.position + shootOffset);
    }

    void InitProjectilePool()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[PlayerController] Projectile prefab not assigned — shooting disabled.");
            return;
        }

        projectilePool = new GameObject[projectilePoolSize];
        for (int i = 0; i < projectilePoolSize; i++)
        {
            var obj = Instantiate(projectilePrefab);
            obj.name = $"Projectile_{i}";
            obj.SetActive(false);
            projectilePool[i] = obj;
        }
    }

    GameObject GetInactiveProjectile()
    {
        for (int i = 0; i < projectilePool.Length; i++)
        {
            if (!projectilePool[i].activeSelf)
                return projectilePool[i];
        }
        return null;
    }
}
