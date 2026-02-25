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

    // Serial event codes sent to controller
    const int EventCoinCollected = 1;
    const int EventHealthPickup = 2;
    const int EventEnemyKilled = 3;
    const int EventDamageTaken = 4;
    const int EventPlayerDied = 5;

    PlayerController playerController;
    InputReader inputReader;
    int score;
    int highScore;
    float lastScoredZ;
    float scoreMultiplier = 1f;
    bool isTrainingMode;
    bool isPaused;

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

        // Find InputReader for reconnect and letter events
        if (inputProcessor != null)
            inputReader = FindObjectOfType<InputReader>();

        if (inputReader != null)
            inputReader.OnLetterReceived += OnControllerLetter;

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
        // Quit — works anytime
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Application.Quit();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
            return;
        }

        // Pause toggle — works during Playing state
        if (Input.GetKeyDown(KeyCode.P) && State == GameState.Playing)
        {
            isPaused = !isPaused;
            Time.timeScale = isPaused ? 0f : 1f;
            if (uiManager != null)
                uiManager.SetPaused(isPaused);
            return;
        }

        if (isPaused) return;

        switch (State)
        {
            case GameState.Menu:
                if (inputProcessor != null)
                {
                    if (inputProcessor.IsJumpHeld)
                    {
                        isTrainingMode = false;
                        SetState(GameState.Playing);
                    }
                    else if (inputProcessor.IsSlideHeld)
                    {
                        isTrainingMode = true;
                        SetState(GameState.Playing);
                    }
                }
                break;

            case GameState.Playing:
                if (!isTrainingMode)
                    UpdateDistanceScore();
                if (Input.GetKeyDown(KeyCode.K))
                    RestartGame();
                break;

            case GameState.GameOver:
                if (AnyButtonPressed())
                    RestartGame();
                break;
        }
    }

    public void SetState(GameState newState)
    {
        // Unpause whenever state changes
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = 1f;
            if (uiManager != null)
                uiManager.SetPaused(false);
        }

        State = newState;

        switch (newState)
        {
            case GameState.Menu:
                if (playerController != null)
                    playerController.SetMovementEnabled(false);
                break;

            case GameState.Playing:
                ApplySerialSettings();
                if (uiManager != null)
                    uiManager.SetTrainingMode(isTrainingMode);
                if (playerController != null)
                    playerController.SetMovementEnabled(true);
                if (playerHealth != null)
                {
                    playerHealth.ResetHealth();
                    playerHealth.PermanentlyInvincible = isTrainingMode;
                    if (uiManager != null)
                        uiManager.UpdateHealth(playerHealth.CurrentHealth);
                }
                if (spawnManager != null)
                {
                    spawnManager.SetSpawningEnabled(true);
                    if (isTrainingMode)
                    {
                        spawnManager.SetSpawnIntervals(3f, 5f);
                        spawnManager.SetEnemyChances(0.15f, 0.05f);
                        spawnManager.SetCollectibleChances(0.2f, 0f);
                    }
                }
                if (difficultyManager != null && !isTrainingMode)
                    difficultyManager.SetActive(true);
                if (!isTrainingMode)
                {
                    score = 0;
                    scoreMultiplier = 1f;
                    lastScoredZ = playerTransform.position.z;
                    if (uiManager != null)
                    {
                        uiManager.UpdateScore(score);
                        uiManager.UpdateMultiplier(scoreMultiplier);
                    }
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
        SendGameEvent(EventPlayerDied);
        if (isTrainingMode)
        {
            if (playerHealth != null)
                playerHealth.ResetHealth();
            return;
        }
        SetState(GameState.GameOver);
    }

    void OnHealthChanged(int currentHealth)
    {
        if (uiManager != null)
            uiManager.UpdateHealth(currentHealth);

        // Reset multiplier on damage
        SendGameEvent(EventDamageTaken);
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

        if (inputReader != null)
            inputReader.OnLetterReceived -= OnControllerLetter;

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
        SendGameEvent(EventEnemyKilled);
        if (State != GameState.Playing || isTrainingMode) return;
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
        SendGameEvent(EventCoinCollected);
        if (State != GameState.Playing || isTrainingMode) return;
        score += Mathf.RoundToInt(coinScoreBase * scoreMultiplier);
        scoreMultiplier += multiplierIncreasePerCoin;
        if (uiManager != null)
        {
            uiManager.UpdateScore(score);
            uiManager.UpdateMultiplier(scoreMultiplier);
        }
    }

    public void OnHealthPickup()
    {
        SendGameEvent(EventHealthPickup);
    }

    // ---- Serial Events ----

    void SendGameEvent(int eventCode)
    {
        if (inputReader != null)
            inputReader.SendEvent(eventCode);
    }

    void OnControllerLetter(char letter)
    {
        if (uiManager != null)
            uiManager.UpdateControllerLetter(letter.ToString());
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
