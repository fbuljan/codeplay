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
    [SerializeField] float jumpForce = 14f;
    [SerializeField] float gravityMultiplier = 2f;

    [Header("Slide / Duck")]
    [SerializeField] float slideHeightScale = 0.25f;
    [SerializeField] float slideTransitionSpeed = 20f;
    [SerializeField] float slamDownSpeed = 30f;

    [Header("Shooting")]
    [SerializeField] float shootCooldown = 0.3f;
    [SerializeField] float maxShootRange = 100f;
    [SerializeField] Vector3 shootOffset = new(0f, 0.5f, 1f);
    [SerializeField] float tracerDuration = 0.08f;
    [SerializeField] float tracerStartWidth = 0.15f;
    [SerializeField] float tracerEndWidth = 0.05f;

    [Header("Aiming")]
    [SerializeField] float joystickAmplitude = 0.2f;
    [SerializeField] float joystickLaneThreshold = 0.4f;
    [SerializeField] float joystickHeightThreshold = 0.3f;
    [SerializeField] float reticleDistance = 15f;
    [SerializeField] float groundAimY = 1f;
    [SerializeField] float airAimY = 5f;
    [SerializeField] Material reticleMaterial;
    [SerializeField] Material reticleAimedMaterial;

    [Header("Lane Switching")]
    [SerializeField] float tiltAmplitude = 0.5f;
    [SerializeField] float tiltLaneThreshold = 0.5f;
    [SerializeField] float laneSwitchSpeed = 10f;
    [SerializeField] float laneSwitchCooldown = 0.3f;

    [Header("Shield")]
    [SerializeField] float shieldDuration = 1f;
    [SerializeField] float shieldCooldown = 3f;
    [SerializeField] Material shieldMaterial;

    [Header("Ground Detection")]
    [SerializeField] float groundTolerance = 0.05f;

    [Header("References")]
    [SerializeField] InputProcessor inputProcessor;

    Rigidbody rb;

    float groundY;
    bool isGrounded;
    bool jumpRequested;
    bool movementEnabled;

    // Duck / slide state
    bool isSliding;
    bool isSlamming;
    float targetScaleY = 1f;

    // Shooting state
    float shootCooldownTimer;
    LineRenderer tracerLine;
    float tracerTimer;

    // Aiming state
    int currentAimLane = 1; // 0=left, 1=center, 2=right
    bool aimingHigh;
    GameObject reticleVisual;

    // Lane switching state
    int currentLane = 1;
    float targetLaneX;
    float laneSwitchCooldownTimer;

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
        inputProcessor.OnShootPressed += OnShootPressed;
        inputProcessor.OnShieldPressed += OnShieldPressed;

        playerHealth = GetComponent<PlayerHealth>();
        uiManager = FindObjectOfType<UIManager>();
        CreateReticle();
        CreateTracer();
        CreateShieldVisual();
    }

    void OnDestroy()
    {
        if (inputProcessor != null)
        {
            inputProcessor.OnJumpPressed -= OnJumpPressed;
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
            isSlamming = false;
            if (isSliding) EndSlide();
            if (isShielded) DeactivateShield();
            if (reticleVisual != null) reticleVisual.SetActive(false);
            currentLane = 1;
            targetLaneX = 0f;
        }
        else
        {
            rb.isKinematic = false;
        }
    }

    void OnJumpPressed()
    {
        if (!movementEnabled) return;

        // Jump from duck — cancel duck and jump
        if (isSliding)
            EndSlide();

        // Cancel slam if mid-air
        if (isSlamming)
            isSlamming = false;

        jumpRequested = true;
    }

    void Update()
    {
        if (!movementEnabled) return;

        UpdateAim();
        UpdateLaneSwitching();
        CheckGrounded();

        // Slam landing — start duck when hitting the ground
        if (isSlamming && isGrounded)
        {
            isSlamming = false;
            StartSlide();
        }

        // Hold-based duck: down while held, up when released
        bool slideHeld = inputProcessor != null && inputProcessor.IsSlideHeld;

        if (slideHeld && !isSliding && !isSlamming)
        {
            if (isGrounded)
            {
                StartSlide();
            }
            else
            {
                // Slam down — cancel air time, duck on landing
                isSlamming = true;
                rb.velocity = new Vector3(rb.velocity.x, -slamDownSpeed, rb.velocity.z);
            }
        }
        else if (!slideHeld && isSliding)
        {
            EndSlide();
        }

        UpdateSlideScale();

        if (shootCooldownTimer > 0f)
            shootCooldownTimer -= Time.deltaTime;

        if (tracerTimer > 0f)
        {
            tracerTimer -= Time.deltaTime;
            if (tracerTimer <= 0f && tracerLine != null)
                tracerLine.enabled = false;
        }

        if (laneSwitchCooldownTimer > 0f)
            laneSwitchCooldownTimer -= Time.deltaTime;

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

        // Extra gravity for snappier jump arc
        if (!isGrounded)
            rb.AddForce(Vector3.up * Physics.gravity.y * (gravityMultiplier - 1f), ForceMode.Acceleration);

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
        velocity.x = (targetLaneX - transform.position.x) * laneSwitchSpeed;
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
        float aimX = GetAimLaneX();
        float aimY = GetAimY();
        Vector3 origin = transform.position + shootOffset;

        // Shoot a ray from origin directly toward the reticle position
        Vector3 reticlePos = new Vector3(aimX, aimY, transform.position.z + reticleDistance);
        Vector3 direction = (reticlePos - origin).normalized;

        RaycastHit[] hits = Physics.RaycastAll(
            origin, direction, maxShootRange,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide
        );
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 hitPos = origin + direction * maxShootRange;
        bool hitSomething = false;
        bool hitEnemy = false;

        foreach (var hit in hits)
        {
            var enemy = hit.collider.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage();
                hitPos = hit.point;
                hitSomething = true;
                hitEnemy = true;
                break;
            }

            var obstacle = hit.collider.GetComponent<Obstacle>();
            if (obstacle != null)
            {
                hitPos = hit.point;
                hitSomething = true;
                break;
            }
        }

        ShowTracer(origin, hitPos);

        // Particle effects
        var fx = ParticleEffectManager.Instance;
        if (fx != null)
        {
            fx.PlayMuzzleFlash(origin, direction);
            if (hitSomething)
                fx.PlayBulletImpact(hitPos, hitEnemy);
        }
    }

    void CreateTracer()
    {
        var obj = new GameObject("ShootTracer");
        tracerLine = obj.AddComponent<LineRenderer>();
        tracerLine.positionCount = 2;
        tracerLine.startWidth = tracerStartWidth;
        tracerLine.endWidth = tracerEndWidth;
        tracerLine.material = new Material(reticleVisual.GetComponent<Renderer>().sharedMaterial.shader);
        tracerLine.material.color = Color.yellow;
        tracerLine.enabled = false;
    }

    void ShowTracer(Vector3 from, Vector3 to)
    {
        if (tracerLine == null) return;
        tracerLine.SetPosition(0, from);
        tracerLine.SetPosition(1, to);
        tracerLine.enabled = true;
        tracerTimer = tracerDuration;
    }

    // ---- Aiming ----

    void UpdateAim()
    {
        if (inputProcessor == null) return;

        Vector2 joystick = inputProcessor.GetJoystick();
        float amp = Mathf.Max(joystickAmplitude, 0.01f);
        float nx = Mathf.Clamp(joystick.x / amp, -1f, 1f);
        float ny = Mathf.Clamp(joystick.y / amp, -1f, 1f);

        if (nx < -joystickLaneThreshold)
            currentAimLane = 0;
        else if (nx > joystickLaneThreshold)
            currentAimLane = 2;
        else
            currentAimLane = 1;

        aimingHigh = ny > joystickHeightThreshold;

        UpdateReticle();
    }

    float GetAimLaneX()
    {
        float[] lanes = GameManager.Instance.LanePositions;
        if (lanes == null || lanes.Length == 0) return 0f;
        return lanes[currentAimLane];
    }

    float GetAimY()
    {
        return aimingHigh ? airAimY : groundAimY;
    }

    void CreateReticle()
    {
        reticleVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
        reticleVisual.name = "AimReticle";

        var col = reticleVisual.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        reticleVisual.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        reticleVisual.transform.rotation = Quaternion.Euler(0, 0, 45); // diamond shape

        if (reticleMaterial != null)
            reticleVisual.GetComponent<Renderer>().material = reticleMaterial;

        reticleVisual.SetActive(false);
    }

    void UpdateReticle()
    {
        if (reticleVisual == null) return;

        if (!reticleVisual.activeSelf)
            reticleVisual.SetActive(true);

        float targetX = GetAimLaneX();
        float targetY = GetAimY();

        reticleVisual.transform.position = new Vector3(
            targetX,
            targetY,
            transform.position.z + reticleDistance
        );

        // Swap material: aimed vs default
        bool enemyAimed = IsEnemyInAimCorridor(targetX, targetY);
        var renderer = reticleVisual.GetComponent<Renderer>();
        if (renderer != null && reticleMaterial != null && reticleAimedMaterial != null)
        {
            renderer.material = enemyAimed ? reticleAimedMaterial : reticleMaterial;
        }
    }

    bool IsEnemyInAimCorridor(float aimX, float aimY)
    {
        Vector3 origin = transform.position + shootOffset;
        Vector3 reticlePos = new Vector3(aimX, aimY, transform.position.z + reticleDistance);
        Vector3 direction = (reticlePos - origin).normalized;

        RaycastHit[] hits = Physics.RaycastAll(
            origin, direction, maxShootRange,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide
        );

        foreach (var hit in hits)
        {
            if (hit.collider.GetComponent<Enemy>() != null)
                return true;
        }
        return false;
    }

    // ---- Lane Switching ----

    void UpdateLaneSwitching()
    {
        if (inputProcessor == null) return;

        Vector3 tilt = inputProcessor.GetTilt();
        float amp = Mathf.Max(tiltAmplitude, 0.01f);
        float nx = Mathf.Clamp(-tilt.x / amp, -1f, 1f);

        int newLane;
        if (nx < -tiltLaneThreshold)
            newLane = 0;
        else if (nx > tiltLaneThreshold)
            newLane = 2;
        else
            newLane = 1;

        if (newLane != currentLane && laneSwitchCooldownTimer <= 0f)
        {
            currentLane = newLane;
            float[] lanes = GameManager.Instance.LanePositions;
            if (lanes != null && lanes.Length > 0)
                targetLaneX = lanes[currentLane];
            laneSwitchCooldownTimer = laneSwitchCooldown;
        }
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

        var fx = ParticleEffectManager.Instance;
        if (fx != null)
            fx.PlayShieldActivation(transform.position);
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

        if (shieldMaterial != null)
            shieldVisual.GetComponent<Renderer>().material = shieldMaterial;

        shieldVisual.SetActive(false);
    }
}
