using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Menu, Playing, GameOver }

/// <summary>
/// Owns scene setup and game state transitions.
/// Menu → any button → Playing → health=0 → GameOver → any button → restart.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] Transform playerTransform;
    [SerializeField] GameObject[] lanePlanes = new GameObject[3];
    [SerializeField] Camera mainCamera;
    [SerializeField] InputProcessor inputProcessor;
    [SerializeField] UIManager uiManager;
    [SerializeField] PlayerHealth playerHealth;
    [SerializeField] SpawnManager spawnManager;
    [SerializeField] DifficultyManager difficultyManager;

    [Header("Lane Settings")]
    [SerializeField] float laneWidth = 3f;
    [SerializeField] float segmentLength = 100f;
    [SerializeField] int segmentRows = 4;

    [Header("Camera")]
    [SerializeField] Vector3 cameraOffset = new(0f, 5f, -10f);

    [Header("Score")]
    [SerializeField] int enemyKillScore = 100;
    [SerializeField] int pointsPerMeter = 1;
    [SerializeField] int coinScoreBase = 50;

    [Header("Multiplier")]
    [SerializeField] float multiplierIncreasePerCoin = 0.1f;
    [SerializeField] float multiplierIncreasePerKill = 0.25f;

    [Header("Ground Layer")]
    [Tooltip("Layer index to assign to lane planes (6 = first unused user layer)")]
    [SerializeField] int groundLayerIndex = 6;

    public static GameManager Instance { get; private set; }
    public GameState State { get; private set; }
    public float[] LanePositions { get; private set; }

    PlayerController playerController;
    InputReader inputReader;
    int score;
    int highScore;
    float lastScoredZ;
    float scoreMultiplier = 1f;

    void Awake()
    {
        Instance = this;

        LanePositions = new float[]
        {
            -laneWidth,
            0f,
            laneWidth
        };

        SetupLanes();
        SetupGroundScroller();
        SetupPlayer();
        SetupCamera();
        SetupSpawnManager();

        playerController = playerTransform.GetComponent<PlayerController>();
        SetupDifficultyManager();
    }

    void Start()
    {
        if (playerHealth == null)
            playerHealth = playerTransform.GetComponent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += OnHealthChanged;
            playerHealth.OnDied += OnPlayerDied;
        }

        // Find InputReader for reconnect
        if (inputProcessor != null)
            inputReader = FindObjectOfType<InputReader>();

        // Load saved settings
        highScore = PlayerPrefs.GetInt("HighScore", 0);
        string savedPort = PlayerPrefs.GetString("PortName", "COM3");
        int savedBaud = PlayerPrefs.GetInt("BaudRate", 115200);

        if (uiManager != null)
        {
            uiManager.SetPortName(savedPort);
            uiManager.SetBaudRate(savedBaud);
            uiManager.UpdateHighScore(highScore);
            uiManager.OnSettingsChanged += TryReconnectSerial;
        }

        // Try connecting with saved settings on startup
        TryReconnectSerial();

        SetState(GameState.Menu);
    }

    void Update()
    {
        switch (State)
        {
            case GameState.Menu:
                if (AnyButtonPressed())
                    SetState(GameState.Playing);
                break;

            case GameState.Playing:
                UpdateDistanceScore();
                break;

            case GameState.GameOver:
                if (AnyButtonPressed())
                    RestartGame();
                break;
        }
    }

    public void SetState(GameState newState)
    {
        State = newState;

        switch (newState)
        {
            case GameState.Menu:
                if (playerController != null)
                    playerController.SetMovementEnabled(false);
                break;

            case GameState.Playing:
                ApplySerialSettings();
                if (playerController != null)
                    playerController.SetMovementEnabled(true);
                if (playerHealth != null)
                {
                    playerHealth.ResetHealth();
                    if (uiManager != null)
                        uiManager.UpdateHealth(playerHealth.CurrentHealth);
                }
                if (spawnManager != null)
                    spawnManager.SetSpawningEnabled(true);
                if (difficultyManager != null)
                    difficultyManager.SetActive(true);
                score = 0;
                scoreMultiplier = 1f;
                lastScoredZ = playerTransform.position.z;
                if (uiManager != null)
                {
                    uiManager.UpdateScore(score);
                    uiManager.UpdateMultiplier(scoreMultiplier);
                }
                break;

            case GameState.GameOver:
                if (playerController != null)
                    playerController.SetMovementEnabled(false);
                if (spawnManager != null)
                    spawnManager.SetSpawningEnabled(false);
                if (difficultyManager != null)
                    difficultyManager.SetActive(false);
                CheckHighScore();
                break;
        }

        if (uiManager != null)
            uiManager.OnGameStateChanged(newState);
    }

    public void OnPlayerDied()
    {
        SetState(GameState.GameOver);
    }

    void OnHealthChanged(int currentHealth)
    {
        if (uiManager != null)
            uiManager.UpdateHealth(currentHealth);

        // Reset multiplier on damage
        scoreMultiplier = 1f;
        if (uiManager != null)
            uiManager.UpdateMultiplier(scoreMultiplier);
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnHealthChanged;
            playerHealth.OnDied -= OnPlayerDied;
        }

        if (uiManager != null)
            uiManager.OnSettingsChanged -= TryReconnectSerial;
    }

    void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    bool AnyButtonPressed()
    {
        if (inputProcessor == null) return false;

        // Check if any face button was just pressed this frame
        if (inputProcessor.IsJumpHeld || inputProcessor.IsSlideHeld ||
            inputProcessor.IsShootHeld || inputProcessor.IsShieldHeld)
            return true;

        return false;
    }

    // ---- Score ----

    void UpdateDistanceScore()
    {
        float currentZ = playerTransform.position.z;
        int meters = Mathf.FloorToInt(currentZ - lastScoredZ);
        if (meters > 0)
        {
            score += Mathf.RoundToInt(meters * pointsPerMeter * scoreMultiplier);
            lastScoredZ += meters;
            if (uiManager != null)
                uiManager.UpdateScore(score);
        }
    }

    public void OnEnemyKilled(Enemy enemy)
    {
        if (State != GameState.Playing) return;
        score += Mathf.RoundToInt(enemyKillScore * scoreMultiplier);
        scoreMultiplier += multiplierIncreasePerKill;
        if (uiManager != null)
        {
            uiManager.UpdateScore(score);
            uiManager.UpdateMultiplier(scoreMultiplier);
        }
    }

    public void OnCoinCollected(Coin coin)
    {
        if (State != GameState.Playing) return;
        score += Mathf.RoundToInt(coinScoreBase * scoreMultiplier);
        scoreMultiplier += multiplierIncreasePerCoin;
        if (uiManager != null)
        {
            uiManager.UpdateScore(score);
            uiManager.UpdateMultiplier(scoreMultiplier);
        }
    }

    // ---- Settings & High Score ----

    void TryReconnectSerial()
    {
        if (uiManager == null || inputReader == null) return;

        string port = uiManager.GetPortName();
        int baud = uiManager.GetBaudRate();

        if (string.IsNullOrWhiteSpace(port) || baud <= 0) return;

        PlayerPrefs.SetString("PortName", port);
        PlayerPrefs.SetInt("BaudRate", baud);
        PlayerPrefs.Save();

        var (success, message) = inputReader.Reconnect(port, baud);
        uiManager.UpdateConnectionStatus(message, success);
    }

    void ApplySerialSettings()
    {
        TryReconnectSerial();
    }

    void CheckHighScore()
    {
        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt("HighScore", highScore);
            PlayerPrefs.Save();

            if (uiManager != null)
            {
                uiManager.UpdateHighScore(highScore);
                uiManager.ShowNewHighScore();
            }
        }
    }

    // ---- Scene Setup (unchanged) ----

    void SetupLanes()
    {
        Vector3 planeScale = new(laneWidth / 10f, 1f, segmentLength / 10f);

        for (int i = 0; i < lanePlanes.Length; i++)
        {
            if (lanePlanes[i] == null)
            {
                Debug.LogError($"[GameManager] Lane plane [{i}] is not assigned!");
                continue;
            }

            Transform t = lanePlanes[i].transform;
            t.position = new Vector3(LanePositions[i], 0f, segmentLength / 2f);
            t.rotation = Quaternion.identity;
            t.localScale = planeScale;

            lanePlanes[i].layer = groundLayerIndex;
        }
    }

    void SetupGroundScroller()
    {
        var scroller = gameObject.AddComponent<GroundScroller>();
        scroller.Initialize(playerTransform, lanePlanes, LanePositions, segmentLength, segmentRows, groundLayerIndex);
    }

    void SetupPlayer()
    {
        if (playerTransform == null)
        {
            Debug.LogError("[GameManager] Player is not assigned!");
            return;
        }

        float playerHeight = 1f;
        var capsule = playerTransform.GetComponent<CapsuleCollider>();
        if (capsule != null)
            playerHeight = capsule.height / 2f;

        playerTransform.position = new Vector3(0f, playerHeight, 0f);
        playerTransform.rotation = Quaternion.identity;
    }

    void SetupSpawnManager()
    {
        if (spawnManager == null)
            spawnManager = GetComponent<SpawnManager>();

        if (spawnManager != null)
            spawnManager.Initialize(playerTransform);
    }

    void SetupDifficultyManager()
    {
        difficultyManager.Initialize(playerTransform, playerController, spawnManager);
    }

    void SetupCamera()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("[GameManager] No camera found!");
            return;
        }

        mainCamera.transform.position = playerTransform.position + cameraOffset;
        mainCamera.transform.LookAt(playerTransform);
    }

    void LateUpdate()
    {
        if (mainCamera != null && playerTransform != null)
        {
            mainCamera.transform.position = playerTransform.position + cameraOffset;
            mainCamera.transform.LookAt(playerTransform);
        }
    }
}
