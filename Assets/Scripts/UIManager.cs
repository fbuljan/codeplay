using UnityEngine;
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
    TextMeshProUGUI gameOverScoreText;

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
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode =
            UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Menu Panel
        menuPanel = CreatePanel(canvasObj.transform, "MenuPanel");
        CreateText(menuPanel.transform, "PRESS ANY BUTTON TO START", 48, TextAlignmentOptions.Center);

        // HUD Panel
        hudPanel = CreatePanel(canvasObj.transform, "HUDPanel");
        scoreText = CreateText(hudPanel.transform, "Score: 0", 32, TextAlignmentOptions.TopLeft,
            new Vector2(20, -20), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        healthText = CreateText(hudPanel.transform, "HP: 3", 32, TextAlignmentOptions.TopRight,
            new Vector2(-20, -20), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));

        // Game Over Panel
        gameOverPanel = CreatePanel(canvasObj.transform, "GameOverPanel");
        CreateText(gameOverPanel.transform, "GAME OVER", 64, TextAlignmentOptions.Center,
            new Vector2(0, 40));
        gameOverScoreText = CreateText(gameOverPanel.transform, "Score: 0", 36, TextAlignmentOptions.Center,
            new Vector2(0, -30));
        CreateText(gameOverPanel.transform, "Press any button to restart", 24, TextAlignmentOptions.Center,
            new Vector2(0, -80));
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

    public void OnGameStateChanged(GameState state)
    {
        menuPanel.SetActive(state == GameState.Menu);
        hudPanel.SetActive(state == GameState.Playing);
        gameOverPanel.SetActive(state == GameState.GameOver);
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
}
