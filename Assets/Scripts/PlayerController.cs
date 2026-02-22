using UnityEngine;

/// <summary>
/// Controls player movement: forward movement, jumping, and sliding.
/// Movement can be enabled/disabled by GameManager for state transitions.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    float forwardSpeed = 10f;

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

    [Header("Shield")]
    [SerializeField] float shieldDuration = 1f;
    [SerializeField] float shieldCooldown = 3f;
    [SerializeField] Color shieldColor = new(0f, 0.5f, 1f, 0.3f);

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

    // Shield state
    float shieldTimer;
    float shieldCooldownTimer;
    bool isShielded;
    GameObject shieldVisual;
    PlayerHealth playerHealth;
    UIManager uiManager;

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
        inputProcessor.OnShieldPressed += OnShieldPressed;

        playerHealth = GetComponent<PlayerHealth>();
        uiManager = FindObjectOfType<UIManager>();
        InitProjectilePool();
        CreateShieldVisual();
    }

    void OnDestroy()
    {
        if (inputProcessor != null)
        {
            inputProcessor.OnJumpPressed -= OnJumpPressed;
            inputProcessor.OnSlidePressed -= OnSlidePressed;
            inputProcessor.OnShootPressed -= OnShootPressed;
            inputProcessor.OnShieldPressed -= OnShieldPressed;
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
            if (isShielded) DeactivateShield();
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

        if (shieldCooldownTimer > 0f)
            shieldCooldownTimer -= Time.deltaTime;

        if (isShielded)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer <= 0f)
                DeactivateShield();
        }

        if (uiManager != null)
            uiManager.UpdateShield(isShielded, shieldCooldownTimer);
    }

    void FixedUpdate()
    {
        if (!movementEnabled) return;

        MoveForward();

        if (jumpRequested && isGrounded)
            Jump();

        jumpRequested = false;
    }

    public void SetForwardSpeed(float speed)
    {
        forwardSpeed = speed;
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

    // ---- Shield ----

    void OnShieldPressed()
    {
        if (!movementEnabled || isShielded || shieldCooldownTimer > 0f) return;

        ActivateShield();
    }

    void ActivateShield()
    {
        isShielded = true;
        shieldTimer = shieldDuration;

        if (playerHealth != null)
            playerHealth.SetShielded(true);

        if (shieldVisual != null)
            shieldVisual.SetActive(true);
    }

    void DeactivateShield()
    {
        isShielded = false;
        shieldCooldownTimer = shieldCooldown;

        if (playerHealth != null)
            playerHealth.SetShielded(false);

        if (shieldVisual != null)
            shieldVisual.SetActive(false);
    }

    void CreateShieldVisual()
    {
        shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shieldVisual.name = "ShieldBubble";
        shieldVisual.transform.SetParent(transform, false);
        shieldVisual.transform.localPosition = Vector3.zero;
        shieldVisual.transform.localScale = Vector3.one * 2f;

        // Remove collider so it doesn't interfere with physics
        var col = shieldVisual.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        // Semi-transparent material
        var renderer = shieldVisual.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.SetFloat("_Mode", 3); // Transparent mode
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.color = shieldColor;
        renderer.material = mat;

        shieldVisual.SetActive(false);
    }
}
