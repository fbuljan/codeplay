using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages all UI: menu screen, in-game HUD, game over screen.
/// Creates its own Canvas and TMP elements programmatically.
/// </summary>
public class UIManager : MonoBehaviour
{
    GameObject menuPanel;
    GameObject hudPanel;
    GameObject gameOverPanel;

    TextMeshProUGUI scoreText;
    TextMeshProUGUI healthText;
    TextMeshProUGUI shieldText;
    TextMeshProUGUI multiplierText;
    TextMeshProUGUI gameOverScoreText;
    TextMeshProUGUI gameOverHighScoreText;
    TextMeshProUGUI newHighScoreText;
    TextMeshProUGUI menuHighScoreText;

    TextMeshProUGUI trainingLabel;
    TextMeshProUGUI trainingExitText;
    TextMeshProUGUI controllerLetterLabel;
    TextMeshProUGUI controllerLetterText;

    TMP_InputField portInputField;
    TMP_InputField baudInputField;
    TextMeshProUGUI connectionStatusText;

    bool isTrainingMode;

    public event Action OnSettingsChanged;

    void Awake()
    {
        CreateCanvas();
    }

    void CreateCanvas()
    {
        // Create Canvas
        var canvasObj = new GameObject("UICanvas");
        canvasObj.transform.SetParent(transform);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        // EventSystem (required for input fields to be clickable)
        if (FindObjectOfType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // Menu Panel
        menuPanel = CreatePanel(canvasObj.transform, "MenuPanel");
        CreateText(menuPanel.transform, "[BUTTON4] PLAY", 48, TextAlignmentOptions.Center,
            new Vector2(0, 100));
        CreateText(menuPanel.transform, "[BUTTON1] TRAINING", 32, TextAlignmentOptions.Center,
            new Vector2(0, 40));

        // Settings area — bottom-left corner
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

        menuHighScoreText = CreateText(menuPanel.transform, "", 28, TextAlignmentOptions.Center,
            new Vector2(0, -40));

        // HUD Panel
        hudPanel = CreatePanel(canvasObj.transform, "HUDPanel");
        scoreText = CreateText(hudPanel.transform, "Score: 0", 32, TextAlignmentOptions.TopLeft,
            new Vector2(20, -20), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        healthText = CreateText(hudPanel.transform, "HP: 3", 32, TextAlignmentOptions.TopRight,
            new Vector2(-20, -20), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        shieldText = CreateText(hudPanel.transform, "SHIELD READY", 24, TextAlignmentOptions.Bottom,
            new Vector2(0, 20), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        multiplierText = CreateText(hudPanel.transform, "x1.0", 28, TextAlignmentOptions.TopLeft,
            new Vector2(20, -60), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));

        // Training HUD (hidden by default)
        trainingLabel = CreateText(hudPanel.transform, "TRAINING MODE", 28, TextAlignmentOptions.TopLeft,
            new Vector2(20, -20), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        trainingLabel.color = new Color(0.5f, 1f, 0.5f);
        trainingLabel.gameObject.SetActive(false);

        trainingExitText = CreateText(hudPanel.transform, "Press K to exit", 22, TextAlignmentOptions.TopRight,
            new Vector2(-20, -20), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        trainingExitText.color = Color.gray;
        trainingExitText.gameObject.SetActive(false);

        // Controller letter display — bottom-right, visible in both modes
        controllerLetterLabel = CreateText(hudPanel.transform, "Recognized letter:", 22, TextAlignmentOptions.BottomRight,
            new Vector2(-20, 90), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
        controllerLetterLabel.color = new Color(1f, 1f, 1f, 0.5f);
        controllerLetterLabel.gameObject.SetActive(false);

        controllerLetterText = CreateText(hudPanel.transform, "", 64, TextAlignmentOptions.BottomRight,
            new Vector2(-20, 20), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
        controllerLetterText.color = new Color(1f, 1f, 1f, 0.8f);

        // Game Over Panel
        gameOverPanel = CreatePanel(canvasObj.transform, "GameOverPanel");
        CreateText(gameOverPanel.transform, "GAME OVER", 64, TextAlignmentOptions.Center,
            new Vector2(0, 60));
        gameOverScoreText = CreateText(gameOverPanel.transform, "Score: 0", 36, TextAlignmentOptions.Center,
            new Vector2(0, -10));
        gameOverHighScoreText = CreateText(gameOverPanel.transform, "", 28, TextAlignmentOptions.Center,
            new Vector2(0, -50));
        newHighScoreText = CreateText(gameOverPanel.transform, "NEW HIGH SCORE!", 32, TextAlignmentOptions.Center,
            new Vector2(0, -90));
        newHighScoreText.color = new Color(1f, 0.8f, 0f); // Gold
        newHighScoreText.gameObject.SetActive(false);
        CreateText(gameOverPanel.transform, "Press any button to restart", 24, TextAlignmentOptions.Center,
            new Vector2(0, -130));
    }

    GameObject CreatePanel(Transform parent, string name)
    {
        var panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return panel;
    }

    TextMeshProUGUI CreateText(Transform parent, string text, int fontSize,
        TextAlignmentOptions alignment, Vector2? position = null,
        Vector2? anchorMin = null, Vector2? anchorMax = null, Vector2? pivot = null)
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
        tmp.color = Color.white;

        return tmp;
    }

    TMP_InputField CreateInputField(Transform parent, string defaultText, Vector2 position, float width,
        Vector2? anchor = null, Vector2? pivot = null)
    {
        // Container
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
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

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
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Input field component
        var inputField = obj.AddComponent<TMP_InputField>();
        inputField.textComponent = tmp;
        inputField.textViewport = textAreaRect;
        inputField.text = defaultText;
        inputField.transition = Selectable.Transition.None;

        // Caret
        inputField.selectionColor = new Color(0.3f, 0.6f, 1f, 0.4f);
        inputField.caretColor = Color.white;
        inputField.caretWidth = 2;
        inputField.customCaretColor = true;

        // Explicit background color swap on focus
        Image bgRef = bg;
        inputField.onSelect.AddListener(_ => bgRef.color = new Color(0.15f, 0.25f, 0.4f, 0.95f));
        inputField.onDeselect.AddListener(_ => bgRef.color = new Color(0.15f, 0.15f, 0.15f, 0.9f));

        return inputField;
    }

    public void SetTrainingMode(bool training)
    {
        isTrainingMode = training;
    }

    public void OnGameStateChanged(GameState state)
    {
        menuPanel.SetActive(state == GameState.Menu);
        hudPanel.SetActive(state == GameState.Playing);
        gameOverPanel.SetActive(state == GameState.GameOver);

        if (state == GameState.Playing)
        {
            scoreText.gameObject.SetActive(!isTrainingMode);
            multiplierText.gameObject.SetActive(!isTrainingMode);
            healthText.gameObject.SetActive(!isTrainingMode);
            trainingLabel.gameObject.SetActive(isTrainingMode);
            trainingExitText.gameObject.SetActive(isTrainingMode);
        }

        if (state == GameState.GameOver)
            newHighScoreText.gameObject.SetActive(false);
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";

        if (gameOverScoreText != null)
            gameOverScoreText.text = $"Score: {score}";
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
            multiplierText.color = new Color(1f, 0.8f, 0f); // Gold
        else if (multiplier > 1f)
            multiplierText.color = new Color(0.5f, 1f, 0.5f); // Green
        else
            multiplierText.color = Color.white;
    }

    public void UpdateShield(bool isActive, float cooldownRemaining)
    {
        if (shieldText == null) return;

        if (isActive)
        {
            shieldText.text = "SHIELD ACTIVE";
            shieldText.color = new Color(0f, 0.8f, 1f);
        }
        else if (cooldownRemaining > 0f)
        {
            shieldText.text = $"SHIELD {cooldownRemaining:F1}s";
            shieldText.color = Color.gray;
        }
        else
        {
            shieldText.text = "SHIELD READY";
            shieldText.color = Color.white;
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
        connectionStatusText.color = success ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.4f, 0.4f);
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
            newHighScoreText.gameObject.SetActive(true);
    }
}
