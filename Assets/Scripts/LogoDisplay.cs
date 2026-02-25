using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays a competition logo in the corner of the screen.
/// Rotates based on tilt Y and Z axes from the controller.
/// Only visible during gameplay (Playing state).
/// </summary>
public class LogoDisplay : MonoBehaviour
{
    [Header("Logo")]
    [SerializeField] Sprite logoSprite;
    [SerializeField] float logoSize = 100f;
    [SerializeField] Vector2 logoOffset = new(20f, 20f);

    [Header("Tilt Rotation")]
    [SerializeField] float smoothSpeed = 10f;

    [Header("References")]
    [SerializeField] InputProcessor inputProcessor;

    RectTransform logoRect;
    GameObject canvasObj;
    TextMeshProUGUI debugText; // TODO: remove debug text later

    void Start()
    {
        if (inputProcessor == null)
            inputProcessor = FindObjectOfType<InputProcessor>();

        CreateLogoUI();
    }

    void CreateLogoUI()
    {
        canvasObj = new GameObject("LogoCanvas");
        canvasObj.transform.SetParent(transform);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // Logo image — bottom-left corner
        var logoObj = new GameObject("Logo", typeof(RectTransform));
        logoObj.transform.SetParent(canvasObj.transform, false);

        logoRect = logoObj.GetComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0, 0);
        logoRect.anchorMax = new Vector2(0, 0);
        logoRect.pivot = new Vector2(0.5f, 0.5f);
        logoRect.anchoredPosition = logoOffset + new Vector2(logoSize / 2f, logoSize / 2f);
        logoRect.sizeDelta = new Vector2(logoSize, logoSize);

        var image = logoObj.AddComponent<Image>();
        if (logoSprite != null)
            image.sprite = logoSprite;
        else
            image.color = new Color(1f, 1f, 1f, 0.3f);
        image.preserveAspect = true;

        // TODO: remove debug text later
        var debugObj = new GameObject("TiltDebug", typeof(RectTransform));
        debugObj.transform.SetParent(canvasObj.transform, false);
        var debugRect = debugObj.GetComponent<RectTransform>();
        debugRect.anchorMin = new Vector2(0, 0);
        debugRect.anchorMax = new Vector2(0, 0);
        debugRect.pivot = new Vector2(0, 0);
        debugRect.anchoredPosition = new Vector2(20, logoOffset.y + logoSize + 10);
        debugRect.sizeDelta = new Vector2(300, 30);
        debugText = debugObj.AddComponent<TextMeshProUGUI>();
        debugText.fontSize = 18;
        debugText.color = Color.yellow;

        // Hidden until gameplay starts
        canvasObj.SetActive(false);
    }

    void Update()
    {
        if (canvasObj == null) return;

        // Only show during gameplay
        bool playing = GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
        if (canvasObj.activeSelf != playing)
            canvasObj.SetActive(playing);

        if (!playing || logoRect == null || inputProcessor == null) return;

        Vector3 tilt = inputProcessor.GetTilt();

        float pitch = Mathf.Atan2(tilt.y, tilt.z) * Mathf.Rad2Deg;
        float roll  = Mathf.Atan2(tilt.x, tilt.z) * Mathf.Rad2Deg;

        float targetX = Mathf.Clamp(-pitch, -90f, 90f);
        float targetY = Mathf.Clamp(-roll, -90f, 90f);

        float curX = logoRect.localEulerAngles.x > 180 ? logoRect.localEulerAngles.x - 360 : logoRect.localEulerAngles.x;
        float curY = logoRect.localEulerAngles.y > 180 ? logoRect.localEulerAngles.y - 360 : logoRect.localEulerAngles.y;

        float rotX = Mathf.Lerp(curX, targetX, smoothSpeed * Time.deltaTime);
        float rotY = Mathf.Lerp(curY, targetY, smoothSpeed * Time.deltaTime);

        logoRect.localRotation = Quaternion.Euler(rotX, rotY, 0f);

        if (debugText != null)
            debugText.text = $"tilt raw: ({tilt.x:F0}, {tilt.y:F0}, {tilt.z:F0})  rot: ({rotX:F1}, {rotY:F1})";
    }
}
