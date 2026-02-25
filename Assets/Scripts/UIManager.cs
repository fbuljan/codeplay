using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages all UI: menu screen, in-game HUD, game over screen.
/// Creates its own Canvas and TMP elements programmatically.
/// Futuristic theme with glow effects, fade transitions, and animated elements.
/// </summary>
public class UIManager : MonoBehaviour
{
    // ---- Serialized Theme Fields ----

    [Header("Typography")]
    [Tooltip("Assign a futuristic SDF font (e.g. Orbitron, Rajdhani, Share Tech Mono). " +
             "Generate via Window > TextMeshPro > Font Asset Creator.")]
    [SerializeField] TMP_FontAsset customFont;
    [SerializeField] float headerLetterSpacing = 12f;
    [SerializeField] float bodyLetterSpacing = 4f;

    [Header("Color Palette")]
    [SerializeField] Color accentPrimary = new Color(0f, 0.85f, 1f, 1f);
    [SerializeField] Color accentGold = new Color(1f, 0.8f, 0f, 1f);
    [SerializeField] Color panelBackground = new Color(0.02f, 0.05f, 0.12f, 0.85f);
    [SerializeField] Color panelBorder = new Color(0f, 0.85f, 1f, 0.4f);
    [SerializeField] Color textPrimary = new Color(0.9f, 0.95f, 1f, 1f);
    [SerializeField] Color textSecondary = new Color(0.5f, 0.6f, 0.7f, 1f);
    [SerializeField] Color textSuccess = new Color(0.3f, 1f, 0.5f, 1f);
    [SerializeField] Color textDanger = new Color(1f, 0.35f, 0.35f, 1f);

    [Header("Text Effects")]
    [SerializeField] float titleGlowPower = 0.6f;
    [SerializeField] float titleOutlineWidth = 0.15f;
    [SerializeField] float hudOutlineWidth = 0.05f;
    [SerializeField] Color titleGlowColor = new Color(0f, 0.85f, 1f, 0.5f);

    [Header("Animations")]
    [SerializeField] float panelFadeDuration = 0.3f;
    [SerializeField] float pulseSpeed = 2f;
    [SerializeField] float pulseMinAlpha = 0.4f;
    [SerializeField] float scoreCountSpeed = 500f;

    [Header("Overlay Effects")]
    [Tooltip("Optional: assign a tiled scanline sprite for CRT effect.")]
    [SerializeField] Sprite scanlineTexture;
    [SerializeField] float scanlineAlpha = 0.03f;

    // ---- Private State ----

    GameObject menuPanel;
    GameObject hudPanel;
    GameObject gameOverPanel;
    GameObject pausePanel;

    CanvasGroup menuCanvasGroup;
    CanvasGroup hudCanvasGroup;
    CanvasGroup gameOverCanvasGroup;
    CanvasGroup pauseCanvasGroup;

    TextMeshProUGUI scoreText;
    TextMeshProUGUI healthText;
    TextMeshProUGUI shieldText;
    TextMeshProUGUI multiplierText;
    TextMeshProUGUI gameOverScoreText;
    TextMeshProUGUI gameOverHighScoreText;
    TextMeshProUGUI newHighScoreText;
    TextMeshProUGUI menuHighScoreText;
    TextMeshProUGUI restartPromptText;

    TextMeshProUGUI trainingLabel;
    TextMeshProUGUI trainingExitText;
    TextMeshProUGUI controllerLetterLabel;
    TextMeshProUGUI controllerLetterText;

    TextMeshProUGUI pauseText;

    TMP_InputField portInputField;
    TMP_InputField baudInputField;
    TextMeshProUGUI connectionStatusText;

    // Fade coroutine tracking — prevents race conditions on rapid state changes
    Dictionary<CanvasGroup, Coroutine> activeFades = new Dictionary<CanvasGroup, Coroutine>();

    // Score animation
    GameObject scoreContainer;
    GameObject healthContainer;
    int displayedScore;
    int targetScore;
    Coroutine scoreCountCoroutine;

    // Pulse animations
    Coroutine newHighScorePulse;
    Coroutine restartPulse;
    Coroutine playTitlePulse;

    bool isTrainingMode;

    public event Action OnSettingsChanged;

    void Awake()
    {
        CreateCanvas();
    }

    // ================================================================
    // Canvas & Panel Construction
    // ================================================================

    void CreateCanvas()
    {
        var canvasObj = new GameObject("UICanvas");
        canvasObj.transform.SetParent(transform);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        if (FindObjectOfType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // ---- Menu Panel ----
        menuPanel = CreatePanel(canvasObj.transform, "MenuPanel");
        menuCanvasGroup = menuPanel.GetComponent<CanvasGroup>();

        var menuBox = CreateStyledPanel(menuPanel.transform, "MenuBox",
            new Vector2(500, 350), Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        var playTitle = CreateText(menuBox.transform, "[BUTTON4] PLAY", 48, TextAlignmentOptions.Center,
            new Vector2(0, 100), isHeader: true);
        ApplyTitleEffect(playTitle);
        playTitlePulse = StartCoroutine(PulseText(playTitle, accentPrimary, pulseSpeed * 0.5f, 0.6f));

        CreateText(menuBox.transform, "[BUTTON1] TRAINING", 32, TextAlignmentOptions.Center,
            new Vector2(0, 40), isHeader: true);

        menuHighScoreText = CreateText(menuBox.transform, "", 28, TextAlignmentOptions.Center,
            new Vector2(0, -40));
        menuHighScoreText.color = accentGold;

        // Settings area — bottom-left corner (on menuPanel, not menuBox)
        CreateText(menuPanel.transform, "Port:", 22, TextAlignmentOptions.MidlineLeft,
            new Vector2(20, 70), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0.5f));
        portInputField = CreateInputField(menuPanel.transform, "COM3",
            new Vector2(90, 70), 150, new Vector2(0, 0), new Vector2(0, 0.5f));

        CreateText(menuPanel.transform, "Baud:", 22, TextAlignmentOptions.MidlineLeft,
            new Vector2(20, 30), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0.5f));
        baudInputField = CreateInputField(menuPanel.transform, "115200",
            new Vector2(90, 30), 150, new Vector2(0, 0), new Vector2(0, 0.5f));

        connectionStatusText = CreateText(menuPanel.transform, "", 18, TextAlignmentOptions.MidlineLeft,
            new Vector2(250, 50), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0.5f));

        portInputField.onEndEdit.AddListener(_ => OnSettingsChanged?.Invoke());
        baudInputField.onEndEdit.AddListener(_ => OnSettingsChanged?.Invoke());

        // ---- HUD Panel ----
        hudPanel = CreatePanel(canvasObj.transform, "HUDPanel");
        hudCanvasGroup = hudPanel.GetComponent<CanvasGroup>();

        // Score + Multiplier container (top-left)
        scoreContainer = CreateStyledPanel(hudPanel.transform, "ScoreContainer",
            new Vector2(280, 90), new Vector2(15, -15),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));

        scoreText = CreateText(scoreContainer.transform, "Score: 0", 32, TextAlignmentOptions.TopLeft,
            new Vector2(36, -8), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        ApplyHudOutline(scoreText);

        multiplierText = CreateText(scoreContainer.transform, "x1.0", 28, TextAlignmentOptions.TopLeft,
            new Vector2(12, -48), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        ApplyHudOutline(multiplierText);

        // Health container (top-right)
        healthContainer = CreateStyledPanel(hudPanel.transform, "HealthContainer",
            new Vector2(160, 55), new Vector2(-15, -60),
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));

        healthText = CreateText(healthContainer.transform, "HP: 3", 32, TextAlignmentOptions.Center,
            new Vector2(0, 0));
        ApplyHudOutline(healthText);

        // Shield container (bottom-center)
        var shieldContainer = CreateStyledPanel(hudPanel.transform, "ShieldContainer",
            new Vector2(300, 45), new Vector2(0, 15),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));

        shieldText = CreateText(shieldContainer.transform, "SHIELD READY", 24, TextAlignmentOptions.Center,
            new Vector2(0, 0));
        ApplyHudOutline(shieldText);

        // Training HUD (hidden by default)
        trainingLabel = CreateText(hudPanel.transform, "TRAINING MODE", 28, TextAlignmentOptions.TopLeft,
            new Vector2(20, -20), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), isHeader: true);
        trainingLabel.color = textSuccess;
        trainingLabel.gameObject.SetActive(false);

        trainingExitText = CreateText(hudPanel.transform, "Press K to exit", 22, TextAlignmentOptions.TopRight,
            new Vector2(-20, -20), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        trainingExitText.color = textSecondary;
        trainingExitText.gameObject.SetActive(false);

        // Controller letter display — bottom-right
        controllerLetterLabel = CreateText(hudPanel.transform, "Recognized letter:", 22, TextAlignmentOptions.BottomRight,
            new Vector2(-20, 90), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
        controllerLetterLabel.color = textSecondary;
        controllerLetterLabel.gameObject.SetActive(false);

        controllerLetterText = CreateText(hudPanel.transform, "", 64, TextAlignmentOptions.BottomRight,
            new Vector2(-20, 20), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
        controllerLetterText.color = textPrimary;

        // ---- Game Over Panel ----
        gameOverPanel = CreatePanel(canvasObj.transform, "GameOverPanel");
        gameOverCanvasGroup = gameOverPanel.GetComponent<CanvasGroup>();

        var gameOverBox = CreateStyledPanel(gameOverPanel.transform, "GameOverBox",
            new Vector2(500, 340), Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        var gameOverTitle = CreateText(gameOverBox.transform, "GAME OVER", 64, TextAlignmentOptions.Center,
            new Vector2(0, 80), isHeader: true);
        ApplyTitleEffect(gameOverTitle);

        gameOverScoreText = CreateText(gameOverBox.transform, "Score: 0", 36, TextAlignmentOptions.Center,
            new Vector2(0, 10));

        gameOverHighScoreText = CreateText(gameOverBox.transform, "", 28, TextAlignmentOptions.Center,
            new Vector2(0, -30));

        newHighScoreText = CreateText(gameOverBox.transform, "NEW HIGH SCORE!", 32, TextAlignmentOptions.Center,
            new Vector2(0, -70), isHeader: true);
        newHighScoreText.color = accentGold;
        newHighScoreText.gameObject.SetActive(false);

        restartPromptText = CreateText(gameOverBox.transform, "Press any button to restart", 24, TextAlignmentOptions.Center,
            new Vector2(0, -115));

        // ---- Pause Panel ----
        pausePanel = CreatePanel(canvasObj.transform, "PausePanel");
        pauseCanvasGroup = pausePanel.GetComponent<CanvasGroup>();
        var pauseBg = pausePanel.AddComponent<Image>();
        pauseBg.color = new Color(panelBackground.r, panelBackground.g, panelBackground.b, 0.85f);
        pauseText = CreateText(pausePanel.transform, "PAUSED\n\nPress P to unpause", 48, TextAlignmentOptions.Center,
            isHeader: true);
        ApplyTitleEffect(pauseText);
        pausePanel.SetActive(false);

        // ---- Hint text — always visible ----
        var hintText = CreateText(canvasObj.transform, "P: Pause | Q: Quit", 16, TextAlignmentOptions.BottomLeft,
            new Vector2(20, 93), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
        hintText.color = new Color(textSecondary.r, textSecondary.g, textSecondary.b, 0.55f);

        // ---- Scanline overlay (optional) ----
        if (scanlineTexture != null)
        {
            var scanObj = new GameObject("ScanlineOverlay", typeof(RectTransform));
            scanObj.transform.SetParent(canvasObj.transform, false);
            var scanRect = scanObj.GetComponent<RectTransform>();
            scanRect.anchorMin = Vector2.zero;
            scanRect.anchorMax = Vector2.one;
            scanRect.offsetMin = Vector2.zero;
            scanRect.offsetMax = Vector2.zero;
            var scanImg = scanObj.AddComponent<Image>();
            scanImg.sprite = scanlineTexture;
            scanImg.type = Image.Type.Tiled;
            scanImg.color = new Color(0f, 0f, 0f, scanlineAlpha);
            scanImg.raycastTarget = false;
        }
    }

    // ================================================================
    // Builder Helpers
    // ================================================================

    GameObject CreatePanel(Transform parent, string name)
    {
        var panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();
        return panel;
    }

    GameObject CreateStyledPanel(Transform parent, string name, Vector2 size,
        Vector2 anchoredPosition, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var bg = panel.AddComponent<Image>();
        bg.color = panelBackground;

        // Corner brackets for sci-fi framing
        CreateCornerBracket(panel.transform, "\u300C", new Vector2(0, 1), new Vector2(4, -4));
        CreateCornerBracket(panel.transform, "\u300D", new Vector2(1, 0), new Vector2(-4, 4));

        return panel;
    }

    void CreateCornerBracket(Transform parent, string character, Vector2 anchor, Vector2 offset)
    {
        var obj = new GameObject("Corner", typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = new Vector2(30, 30);

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = character;
        tmp.fontSize = 24;
        tmp.color = panelBorder;
        tmp.alignment = TextAlignmentOptions.Center;
        if (customFont != null) tmp.font = customFont;
    }

    TextMeshProUGUI CreateText(Transform parent, string text, int fontSize,
        TextAlignmentOptions alignment, Vector2? position = null,
        Vector2? anchorMin = null, Vector2? anchorMax = null, Vector2? pivot = null,
        bool isHeader = false)
    {
        var obj = new GameObject("Text", typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        var rect = obj.GetComponent<RectTransform>();
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchorMin = anchorMin ?? new Vector2(0.5f, 0.5f);
        rect.anchorMax = anchorMax ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position ?? Vector2.zero;
        rect.sizeDelta = new Vector2(800, fontSize + 20);

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = textPrimary;

        if (customFont != null)
            tmp.font = customFont;

        tmp.characterSpacing = isHeader ? headerLetterSpacing : bodyLetterSpacing;

        if (isHeader)
        {
            tmp.fontStyle = FontStyles.UpperCase;
            tmp.enableWordWrapping = false;
        }

        return tmp;
    }

    TMP_InputField CreateInputField(Transform parent, string defaultText, Vector2 position, float width,
        Vector2? anchor = null, Vector2? pivot = null)
    {
        var obj = new GameObject("InputField", typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        var rect = obj.GetComponent<RectTransform>();
        Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
        rect.anchorMin = a;
        rect.anchorMax = a;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, 36);

        // Background
        var bg = obj.AddComponent<Image>();
        bg.color = panelBackground;

        // Accent border
        var outline = obj.AddComponent<Outline>();
        outline.effectColor = panelBorder;
        outline.effectDistance = new Vector2(1, 1);

        // Text area
        var textArea = new GameObject("Text Area", typeof(RectTransform));
        textArea.transform.SetParent(obj.transform, false);
        var textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(10, 0);
        textAreaRect.offsetMax = new Vector2(-10, 0);
        textArea.AddComponent<RectMask2D>();

        // Input text
        var textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(textArea.transform, false);
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 24;
        tmp.color = textPrimary;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (customFont != null) tmp.font = customFont;

        var inputField = obj.AddComponent<TMP_InputField>();
        inputField.textComponent = tmp;
        inputField.textViewport = textAreaRect;
        inputField.text = defaultText;
        inputField.transition = Selectable.Transition.None;

        // Caret styling
        inputField.selectionColor = new Color(accentPrimary.r, accentPrimary.g, accentPrimary.b, 0.3f);
        inputField.caretColor = accentPrimary;
        inputField.caretWidth = 2;
        inputField.customCaretColor = true;

        // Focus color swap
        Color focusColor = Color.Lerp(panelBackground, accentPrimary, 0.2f);
        focusColor.a = 0.95f;
        Image bgRef = bg;
        Color normalColor = panelBackground;
        inputField.onSelect.AddListener(_ => bgRef.color = focusColor);
        inputField.onDeselect.AddListener(_ => bgRef.color = normalColor);

        return inputField;
    }

    // ================================================================
    // TMP Material Effects
    // ================================================================

    void ApplyTitleEffect(TextMeshProUGUI tmp)
    {
        Material mat = tmp.fontMaterial;
        mat.SetColor("_OutlineColor", accentPrimary);
        mat.SetFloat("_OutlineWidth", titleOutlineWidth);
        mat.SetColor("_GlowColor", titleGlowColor);
        mat.SetFloat("_GlowOffset", 0f);
        mat.SetFloat("_GlowInner", 0.1f);
        mat.SetFloat("_GlowOuter", 0.2f);
        mat.SetFloat("_GlowPower", titleGlowPower);
        tmp.fontMaterial = mat;
    }

    void ApplyHudOutline(TextMeshProUGUI tmp)
    {
        Material mat = tmp.fontMaterial;
        mat.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0.8f));
        mat.SetFloat("_OutlineWidth", hudOutlineWidth);
        mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.3f));
        mat.SetFloat("_UnderlayOffsetX", 0.5f);
        mat.SetFloat("_UnderlayOffsetY", -0.5f);
        mat.SetFloat("_UnderlayDilate", 0.1f);
        mat.SetFloat("_UnderlaySoftness", 0.2f);
        tmp.fontMaterial = mat;
    }

    // ================================================================
    // Animation Coroutines
    // ================================================================

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration, Action onComplete = null)
    {
        if (cg == null) yield break;
        cg.alpha = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
        if (to <= 0f) cg.gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    void ShowPanel(GameObject panel, CanvasGroup cg)
    {
        CancelFade(cg);
        panel.SetActive(true);
        activeFades[cg] = StartCoroutine(FadeCanvasGroup(cg, 0f, 1f, panelFadeDuration, () => activeFades.Remove(cg)));
    }

    void HidePanel(GameObject panel, CanvasGroup cg)
    {
        CancelFade(cg);
        if (panel.activeSelf)
            activeFades[cg] = StartCoroutine(FadeCanvasGroup(cg, cg.alpha, 0f, panelFadeDuration, () => activeFades.Remove(cg)));
    }

    void CancelFade(CanvasGroup cg)
    {
        if (activeFades.TryGetValue(cg, out var running) && running != null)
            StopCoroutine(running);
        activeFades.Remove(cg);
    }

    IEnumerator PulseText(TextMeshProUGUI tmp, Color baseColor, float speed, float minAlpha)
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * speed * Mathf.PI * 2f) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(minAlpha, 1f, t);
            tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }
    }

    IEnumerator AnimateScoreCounter()
    {
        while (displayedScore != targetScore)
        {
            displayedScore = (int)Mathf.MoveTowards(displayedScore, targetScore,
                scoreCountSpeed * Time.deltaTime);
            if (scoreText != null)
                scoreText.text = $"Score: {displayedScore}";
            yield return null;
        }
        scoreCountCoroutine = null;
    }

    // ================================================================
    // Public API (unchanged signatures)
    // ================================================================

    public void SetTrainingMode(bool training)
    {
        isTrainingMode = training;
    }

    public void OnGameStateChanged(GameState state)
    {
        // Hide non-active panels
        if (state != GameState.Menu) HidePanel(menuPanel, menuCanvasGroup);
        if (state != GameState.Playing) HidePanel(hudPanel, hudCanvasGroup);
        if (state != GameState.GameOver) HidePanel(gameOverPanel, gameOverCanvasGroup);

        // Show active panel
        switch (state)
        {
            case GameState.Menu:
                ShowPanel(menuPanel, menuCanvasGroup);
                break;

            case GameState.Playing:
                ShowPanel(hudPanel, hudCanvasGroup);
                scoreContainer.SetActive(!isTrainingMode);
                healthContainer.SetActive(!isTrainingMode);
                trainingLabel.gameObject.SetActive(isTrainingMode);
                trainingExitText.gameObject.SetActive(true);
                // Reset score counter
                displayedScore = 0;
                targetScore = 0;
                break;

            case GameState.GameOver:
                ShowPanel(gameOverPanel, gameOverCanvasGroup);
                newHighScoreText.gameObject.SetActive(false);
                // Start restart prompt breathing
                if (restartPulse != null) StopCoroutine(restartPulse);
                restartPulse = StartCoroutine(PulseText(restartPromptText, textSecondary, pulseSpeed * 0.6f, 0.3f));
                break;
        }
    }

    public void UpdateScore(int score)
    {
        targetScore = score;

        if (gameOverScoreText != null)
            gameOverScoreText.text = $"Score: {score}";

        if (scoreText != null && scoreCountCoroutine == null)
            scoreCountCoroutine = StartCoroutine(AnimateScoreCounter());
    }

    public void UpdateHealth(int health)
    {
        if (healthText != null)
            healthText.text = $"HP: {health}";
    }

    public void UpdateMultiplier(float multiplier)
    {
        if (multiplierText == null) return;

        multiplierText.text = $"x{multiplier:F1}";

        if (multiplier >= 2f)
            multiplierText.color = accentGold;
        else if (multiplier > 1f)
            multiplierText.color = textSuccess;
        else
            multiplierText.color = textPrimary;
    }

    public void UpdateShield(bool isActive, float cooldownRemaining)
    {
        if (shieldText == null) return;

        if (isActive)
        {
            shieldText.text = "SHIELD ACTIVE";
            shieldText.color = accentPrimary;
        }
        else if (cooldownRemaining > 0f)
        {
            shieldText.text = $"SHIELD {cooldownRemaining:F1}s";
            shieldText.color = textSecondary;
        }
        else
        {
            shieldText.text = "SHIELD READY";
            shieldText.color = textPrimary;
        }
    }

    // ---- Settings & High Score ----

    public void SetPortName(string port)
    {
        if (portInputField != null)
            portInputField.text = port;
    }

    public void SetBaudRate(int baud)
    {
        if (baudInputField != null)
            baudInputField.text = baud.ToString();
    }

    public string GetPortName()
    {
        return portInputField != null ? portInputField.text : "COM3";
    }

    public int GetBaudRate()
    {
        if (baudInputField != null && int.TryParse(baudInputField.text, out int baud))
            return baud;
        return 115200;
    }

    public void UpdateHighScore(int highScore)
    {
        string text = highScore > 0 ? $"High Score: {highScore}" : "";

        if (menuHighScoreText != null)
            menuHighScoreText.text = text;

        if (gameOverHighScoreText != null)
            gameOverHighScoreText.text = text;
    }

    public void UpdateConnectionStatus(string message, bool success)
    {
        if (connectionStatusText == null) return;
        connectionStatusText.text = message;
        connectionStatusText.color = success ? textSuccess : textDanger;
    }

    public void UpdateControllerLetter(string letter)
    {
        if (controllerLetterText != null)
            controllerLetterText.text = letter;

        if (controllerLetterLabel != null)
            controllerLetterLabel.gameObject.SetActive(!string.IsNullOrEmpty(letter));
    }

    public void ShowNewHighScore()
    {
        if (newHighScoreText != null)
        {
            newHighScoreText.gameObject.SetActive(true);
            if (newHighScorePulse != null) StopCoroutine(newHighScorePulse);
            newHighScorePulse = StartCoroutine(PulseText(newHighScoreText, accentGold, pulseSpeed, pulseMinAlpha));
        }
    }

    public void SetPaused(bool paused)
    {
        if (paused)
        {
            ShowPanel(pausePanel, pauseCanvasGroup);
            HidePanel(hudPanel, hudCanvasGroup);
        }
        else
        {
            HidePanel(pausePanel, pauseCanvasGroup);
            ShowPanel(hudPanel, hudCanvasGroup);
        }
    }
}
