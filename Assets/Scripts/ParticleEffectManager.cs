using UnityEngine;

/// <summary>
/// Creates and manages all particle effects via code. No prefabs or textures needed.
/// Attach to an empty GameObject in the scene, or let GameManager create it.
/// </summary>
public class ParticleEffectManager : MonoBehaviour
{
    public static ParticleEffectManager Instance { get; private set; }

    // Enemy death pools (need multiple since enemies can die in quick succession)
    ParticleSystem[] enemyDeathFlash;
    ParticleSystem[] enemyDeathSparks;
    int enemyDeathIndex;
    const int EnemyDeathPoolSize = 4;

    // Muzzle flash (single — shoot has cooldown)
    ParticleSystem muzzleFlash;

    // Bullet impact (2 instances for rapid hits)
    ParticleSystem[] bulletImpact;
    int bulletImpactIndex;
    const int BulletImpactPoolSize = 2;

    // Coin collection (3 instances for closely spaced coins)
    ParticleSystem[] coinFlash;
    ParticleSystem[] coinSparkles;
    int coinIndex;
    const int CoinPoolSize = 3;

    // Health pickup (2 instances)
    ParticleSystem[] healthFlash;
    ParticleSystem[] healthSparkles;
    int healthIndex;
    const int HealthPoolSize = 2;

    // Player damage (single)
    ParticleSystem damageFlash;

    // Shield activation (single — cooldown prevents overlap)
    ParticleSystem shieldRipple;

    // Shield block (2 instances for multiple projectiles)
    ParticleSystem[] shieldBlock;
    int shieldBlockIndex;
    const int ShieldBlockPoolSize = 2;

    // Game over disintegration (single)
    ParticleSystem gameOverFlash;
    ParticleSystem gameOverFragments;
    ParticleSystem gameOverEmbers;

    // Shared materials
    Material additiveMat;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        additiveMat = CreateAdditiveMaterial();
        if (additiveMat == null)
        {
            Debug.LogError("[ParticleEffectManager] Failed to create material — particle effects disabled.");
            Destroy(gameObject);
            return;
        }

        CreateAllSystems();
        Instance = this; // Only set AFTER everything is created successfully
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    Material CreateAdditiveMaterial()
    {
        // Try particle-specific shaders first (best visual results)
        var shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Additive");
        // Sprites/Default is always included in Unity builds
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            Debug.LogError("[ParticleEffectManager] No suitable particle shader found!");
            return null;
        }

        var mat = new Material(shader);
        mat.SetFloat("_Mode", 1f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetColor("_Color", Color.white);
        mat.renderQueue = 3100;
        return mat;
    }

    Material CloneMaterial(Color color)
    {
        var mat = new Material(additiveMat);
        mat.SetColor("_Color", color);
        return mat;
    }

    void CreateAllSystems()
    {
        CreateEnemyDeathSystems();
        CreateMuzzleFlash();
        CreateBulletImpact();
        CreateCoinCollection();
        CreateHealthPickup();
        CreateDamageFlash();
        CreateShieldRipple();
        CreateShieldBlock();
        CreateGameOverDisintegration();
    }

    // ======== SYSTEM CREATION ========

    void CreateEnemyDeathSystems()
    {
        enemyDeathFlash = new ParticleSystem[EnemyDeathPoolSize];
        enemyDeathSparks = new ParticleSystem[EnemyDeathPoolSize];

        for (int i = 0; i < EnemyDeathPoolSize; i++)
        {
            // Inner flash
            enemyDeathFlash[i] = MakeSystem($"EnemyDeathFlash_{i}");
            var main = enemyDeathFlash[i].main;
            main.startLifetime = 0.15f;
            main.startSpeed = 0f;
            main.startSize = 0.5f;
            main.startColor = new Color(0f, 1f, 1f, 0.8f);
            main.maxParticles = 1;

            var emission = enemyDeathFlash[i].emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var sol = enemyDeathFlash[i].sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 6f));

            var col = enemyDeathFlash[i].colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(0.8f, 0f));

            SetRenderer(enemyDeathFlash[i], ParticleSystemRenderMode.Billboard);

            // Outer sparks
            enemyDeathSparks[i] = MakeSystem($"EnemyDeathSparks_{i}");
            main = enemyDeathSparks[i].main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startColor = new Color(0f, 1f, 1f, 1f);
            main.maxParticles = 30;

            emission = enemyDeathSparks[i].emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 30) });

            var shape = enemyDeathSparks[i].shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            sol = enemyDeathSparks[i].sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            col = enemyDeathSparks[i].colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(1f, 0f));

            var vel = enemyDeathSparks[i].velocityOverLifetime;
            vel.enabled = true;
            vel.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            SetRenderer(enemyDeathSparks[i], ParticleSystemRenderMode.Stretch, 3f, 0.1f);
        }
    }

    void CreateMuzzleFlash()
    {
        muzzleFlash = MakeSystem("MuzzleFlash");
        var main = muzzleFlash.main;
        main.startLifetime = 0.1f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 20f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = new Color(1f, 0.9f, 0.2f, 1f);
        main.maxParticles = 12;

        var emission = muzzleFlash.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 12) });

        var shape = muzzleFlash.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.1f;

        var sol = muzzleFlash.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var col = muzzleFlash.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        SetRenderer(muzzleFlash, ParticleSystemRenderMode.Stretch, 2f, 0.05f);
    }

    void CreateBulletImpact()
    {
        bulletImpact = new ParticleSystem[BulletImpactPoolSize];
        for (int i = 0; i < BulletImpactPoolSize; i++)
        {
            bulletImpact[i] = MakeSystem($"BulletImpact_{i}");
            var main = bulletImpact[i].main;
            main.startLifetime = 0.3f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new Color(0f, 1f, 1f, 1f);
            main.gravityModifier = 0.5f;
            main.maxParticles = 15;

            var emission = bulletImpact[i].emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 15) });

            var shape = bulletImpact[i].shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.2f;

            var sol = bulletImpact[i].sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var col = bulletImpact[i].colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(1f, 0f));

            SetRenderer(bulletImpact[i], ParticleSystemRenderMode.Stretch, 2f, 0.05f);
        }
    }

    void CreateCoinCollection()
    {
        coinFlash = new ParticleSystem[CoinPoolSize];
        coinSparkles = new ParticleSystem[CoinPoolSize];

        for (int i = 0; i < CoinPoolSize; i++)
        {
            // Central flash (small pop)
            coinFlash[i] = MakeSystem($"CoinFlash_{i}");
            var main = coinFlash[i].main;
            main.startLifetime = 0.12f;
            main.startSpeed = 0f;
            main.startSize = 0.3f;
            main.startColor = new Color(1f, 0.85f, 0f, 0.7f);
            main.maxParticles = 1;

            var emission = coinFlash[i].emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var sol = coinFlash[i].sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.3f, 2f), new Keyframe(1f, 0f)
            ));

            var col = coinFlash[i].colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(0.7f, 0f));

            SetRenderer(coinFlash[i], ParticleSystemRenderMode.Billboard);

            // Small upward sparkles
            coinSparkles[i] = MakeSystem($"CoinSparkles_{i}");
            main = coinSparkles[i].main;
            main.startLifetime = 0.3f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0f, 1f),
                new Color(1f, 1f, 0.8f, 1f)
            );
            main.gravityModifier = -0.3f;
            main.maxParticles = 8;

            emission = coinSparkles[i].emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5, 8) });

            var shape = coinSparkles[i].shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var vel = coinSparkles[i].velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalY = 2f;

            sol = coinSparkles[i].sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            col = coinSparkles[i].colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(1f, 0f));

            SetRenderer(coinSparkles[i], ParticleSystemRenderMode.Billboard);
        }
    }

    void CreateHealthPickup()
    {
        healthFlash = new ParticleSystem[HealthPoolSize];
        healthSparkles = new ParticleSystem[HealthPoolSize];

        for (int i = 0; i < HealthPoolSize; i++)
        {
            // Central flash (green-white pop)
            healthFlash[i] = MakeSystem($"HealthFlash_{i}");
            var main = healthFlash[i].main;
            main.startLifetime = 0.15f;
            main.startSpeed = 0f;
            main.startSize = 0.4f;
            main.startColor = new Color(0.2f, 1f, 0.3f, 0.7f);
            main.maxParticles = 1;

            var emission = healthFlash[i].emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var sol = healthFlash[i].sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.3f, 2.5f), new Keyframe(1f, 0f)
            ));

            var col = healthFlash[i].colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(0.7f, 0f));

            SetRenderer(healthFlash[i], ParticleSystemRenderMode.Billboard);

            // Upward sparkles (green/white healing feel)
            healthSparkles[i] = MakeSystem($"HealthSparkles_{i}");
            main = healthSparkles[i].main;
            main.startLifetime = 0.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.2f, 1f, 0.3f, 1f),
                new Color(0.8f, 1f, 0.8f, 1f)
            );
            main.gravityModifier = -0.4f;
            main.maxParticles = 10;

            emission = healthSparkles[i].emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 10) });

            var shape = healthSparkles[i].shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var vel = healthSparkles[i].velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalY = 2.5f;

            sol = healthSparkles[i].sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            col = healthSparkles[i].colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(1f, 0f));

            SetRenderer(healthSparkles[i], ParticleSystemRenderMode.Billboard);
        }
    }

    void CreateDamageFlash()
    {
        damageFlash = MakeSystem("DamageFlash");
        var main = damageFlash.main;
        main.startLifetime = 0.35f;
        main.startSpeed = -6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startColor = new Color(1f, 0.15f, 0.1f, 0.9f);
        main.maxParticles = 25;

        var emission = damageFlash.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 25) });

        var shape = damageFlash.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 2.5f;

        var sol = damageFlash.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        var col = damageFlash.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.15f, 0.1f), 0f), new GradientColorKey(new Color(0.5f, 0f, 0f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        SetRenderer(damageFlash, ParticleSystemRenderMode.Billboard);
    }

    void CreateShieldRipple()
    {
        shieldRipple = MakeSystem("ShieldRipple");
        var main = shieldRipple.main;
        main.startLifetime = 0.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = new Color(0f, 0.9f, 1f, 0.9f);
        main.maxParticles = 40;

        var emission = shieldRipple.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30, 40) });

        var shape = shieldRipple.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.8f;

        var sol = shieldRipple.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var col = shieldRipple.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.8f, 0.95f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var vel = shieldRipple.velocityOverLifetime;
        vel.enabled = true;
        vel.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        SetRenderer(shieldRipple, ParticleSystemRenderMode.Stretch, 1.5f, 0.05f);
    }

    void CreateShieldBlock()
    {
        shieldBlock = new ParticleSystem[ShieldBlockPoolSize];
        for (int i = 0; i < ShieldBlockPoolSize; i++)
        {
            shieldBlock[i] = MakeSystem($"ShieldBlock_{i}");
            var main = shieldBlock[i].main;
            main.startLifetime = 0.25f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new Color(0.3f, 1f, 1f, 1f);
            main.maxParticles = 20;

            var emission = shieldBlock[i].emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15, 20) });

            var shape = shieldBlock[i].shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.3f;

            var sol = shieldBlock[i].sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var col = shieldBlock[i].colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(1f, 0f));

            SetRenderer(shieldBlock[i], ParticleSystemRenderMode.Stretch, 2f, 0.05f);
        }
    }

    void CreateGameOverDisintegration()
    {
        // Layer 1: Central white flash
        gameOverFlash = MakeSystem("GameOverFlash");
        var main = gameOverFlash.main;
        main.startLifetime = 0.3f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.startColor = new Color(1f, 1f, 1f, 0.9f);
        main.maxParticles = 1;

        var emission = gameOverFlash.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var sol = gameOverFlash.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 5f));

        var col = gameOverFlash.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(0.9f, 0f));

        SetRenderer(gameOverFlash, ParticleSystemRenderMode.Billboard);

        // Layer 2: Fragment burst
        gameOverFragments = MakeSystem("GameOverFragments");
        main = gameOverFragments.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 20f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0f, 1f, 1f, 1f),
            new Color(1f, 0f, 1f, 1f)
        );
        main.gravityModifier = 1f;
        main.maxParticles = 70;

        emission = gameOverFragments.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 50, 70) });

        var shape = gameOverFragments.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        sol = gameOverFragments.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        col = gameOverFragments.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(1f, 0f, 0.7f));

        var rot = gameOverFragments.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

        SetRenderer(gameOverFragments, ParticleSystemRenderMode.Stretch, 2f, 0.05f);

        // Layer 3: Lingering embers
        gameOverEmbers = MakeSystem("GameOverEmbers");
        main = gameOverEmbers.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0f, 0.5f, 0.5f, 0.6f),
            new Color(0.5f, 0f, 0.5f, 0.6f)
        );
        main.gravityModifier = -0.3f;
        main.maxParticles = 30;

        emission = gameOverEmbers.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.1f, 20, 30) });

        shape = gameOverEmbers.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.5f;

        sol = gameOverEmbers.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        col = gameOverEmbers.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(0.6f, 0f));

        SetRenderer(gameOverEmbers, ParticleSystemRenderMode.Billboard);
    }

    // ======== PUBLIC PLAY METHODS ========

    public void PlayEnemyDeath(Vector3 position, bool isAirEnemy)
    {
        int idx = enemyDeathIndex % EnemyDeathPoolSize;
        enemyDeathIndex++;

        Color color = isAirEnemy ? new Color(1f, 0f, 1f, 0.8f) : new Color(0f, 1f, 1f, 0.8f);
        Color sparkColor = isAirEnemy ? new Color(1f, 0f, 1f, 1f) : new Color(0f, 1f, 1f, 1f);

        var flashMain = enemyDeathFlash[idx].main;
        flashMain.startColor = color;
        PlayAt(enemyDeathFlash[idx], position);

        var sparksMain = enemyDeathSparks[idx].main;
        sparksMain.startColor = sparkColor;
        PlayAt(enemyDeathSparks[idx], position);
    }

    public void PlayMuzzleFlash(Vector3 position, Vector3 direction)
    {
        muzzleFlash.transform.position = position;
        muzzleFlash.transform.forward = direction;
        muzzleFlash.Play(true);
    }

    public void PlayBulletImpact(Vector3 position, bool isEnemy)
    {
        int idx = bulletImpactIndex % BulletImpactPoolSize;
        bulletImpactIndex++;

        Color color = isEnemy ? new Color(0f, 1f, 1f, 1f) : new Color(1f, 0.6f, 0.1f, 1f);
        var main = bulletImpact[idx].main;
        main.startColor = color;
        PlayAt(bulletImpact[idx], position);
    }

    public void PlayCoinCollect(Vector3 position)
    {
        int idx = coinIndex % CoinPoolSize;
        coinIndex++;

        PlayAt(coinFlash[idx], position);
        PlayAt(coinSparkles[idx], position);
    }

    public void PlayHealthPickup(Vector3 position)
    {
        int idx = healthIndex % HealthPoolSize;
        healthIndex++;

        PlayAt(healthFlash[idx], position);
        PlayAt(healthSparkles[idx], position);
    }

    public void PlayDamageFlash(Vector3 playerPosition)
    {
        PlayAt(damageFlash, playerPosition + Vector3.up);
    }

    public void PlayShieldActivation(Vector3 playerPosition)
    {
        PlayAt(shieldRipple, playerPosition);
    }

    public void PlayShieldBlock(Vector3 contactPoint, Vector3 playerCenter)
    {
        int idx = shieldBlockIndex % ShieldBlockPoolSize;
        shieldBlockIndex++;

        var sys = shieldBlock[idx];
        sys.transform.position = contactPoint;
        // Orient hemisphere away from player center (sparks fly outward)
        sys.transform.forward = (contactPoint - playerCenter).normalized;
        sys.Play(true);
    }

    public void PlayGameOverDisintegration(Vector3 playerPosition)
    {
        Vector3 pos = playerPosition + Vector3.up;
        PlayAt(gameOverFlash, pos);
        PlayAt(gameOverFragments, pos);
        PlayAt(gameOverEmbers, pos);
    }

    // ======== HELPERS ========

    ParticleSystem MakeSystem(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // Disable default emission
        var emission = ps.emission;
        emission.rateOverTime = 0;

        // Disable shape by default (individual setup will re-enable)
        var shape = ps.shape;
        shape.enabled = false;

        return ps;
    }

    void SetRenderer(ParticleSystem ps, ParticleSystemRenderMode mode, float lengthScale = 0f, float speedScale = 0f)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = additiveMat;
        renderer.renderMode = mode;
        if (mode == ParticleSystemRenderMode.Stretch)
        {
            renderer.lengthScale = lengthScale;
            renderer.velocityScale = speedScale;
        }
    }

    void PlayAt(ParticleSystem ps, Vector3 position)
    {
        ps.transform.position = position;
        ps.Play(true);
    }

    Gradient AlphaGradient(float startAlpha, float endAlpha, float holdUntil = 0f)
    {
        var grad = new Gradient();
        if (holdUntil > 0f)
        {
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha, holdUntil), new GradientAlphaKey(endAlpha, 1f) }
            );
        }
        else
        {
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(endAlpha, 1f) }
            );
        }
        return grad;
    }
}
