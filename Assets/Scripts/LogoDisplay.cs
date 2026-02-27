using UnityEngine;

/// <summary>
/// Displays a 3D logo model that rotates to match the controller's tilt.
/// Uses a second camera with a viewport rect in the bottom-left corner.
/// Only visible during gameplay (Playing state).
/// </summary>
public class LogoDisplay : MonoBehaviour
{
    [Header("Logo")]
    [SerializeField] GameObject logoObject;
    [SerializeField] float viewportSize = 0.15f;
    [SerializeField] Vector2 viewportOffset = new(0.01f, 0.01f);
    [SerializeField] Vector3 cameraOffset = new(0f, 0f, -3f);

    [Header("Tilt Rotation")]
    [SerializeField] Vector3 baseRotation = new(-90f, -90f, 0f);
    [SerializeField] float smoothSpeed = 10f;

    [Header("References")]
    [SerializeField] InputProcessor inputProcessor;

    Vector3 logoPos = new(500f, 500f, 500f);
    Camera logoCamera;
    Camera mainCam;
    Quaternion currentRotation = Quaternion.identity;
    bool visible;

    void Start()
    {
        if (inputProcessor == null)
            inputProcessor = FindObjectOfType<InputProcessor>();

        SetupLogoScene();
    }

    void SetupLogoScene()
    {
        if (logoObject == null)
        {
            Debug.LogWarning("[LogoDisplay] No logo object assigned.");
            return;
        }

        // Move the scene object far away so it doesn't interfere with the game scene
        logoObject.transform.position = logoPos;
        logoObject.transform.rotation = Quaternion.Euler(baseRotation);
        currentRotation = Quaternion.Euler(baseRotation);

        // Put logo on a dedicated layer so only our camera sees it
        int layer = LayerMask.NameToLayer("UI");
        if (layer < 0) layer = 5; // fallback to built-in UI layer
        SetLayerRecursive(logoObject, layer);

        // Exclude this layer from main camera
        mainCam = Camera.main;
        if (mainCam != null)
            mainCam.cullingMask &= ~(1 << layer);

        // Create dedicated camera looking at the logo
        var camObj = new GameObject("LogoDisplay_Camera");
        logoCamera = camObj.AddComponent<Camera>();
        logoCamera.transform.position = logoPos + cameraOffset;
        logoCamera.transform.LookAt(logoPos);
        logoCamera.clearFlags = CameraClearFlags.SolidColor;
        logoCamera.backgroundColor = mainCam != null ? mainCam.backgroundColor : Color.clear;
        logoCamera.cullingMask = 1 << layer;
        logoCamera.fieldOfView = 30f;
        logoCamera.nearClipPlane = 0.1f;
        logoCamera.farClipPlane = 50f;
        logoCamera.depth = 100; // render on top of main camera
        logoCamera.rect = new Rect(viewportOffset.x, viewportOffset.y, viewportSize, viewportSize);
        logoCamera.enabled = false;

        SetVisible(false);
    }

    void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    void SetVisible(bool show)
    {
        visible = show;
        if (logoCamera != null) logoCamera.enabled = show;
        if (logoObject != null) logoObject.SetActive(show);
    }

    void Update()
    {
        if (logoObject == null) return;

        bool playing = GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
        if (visible != playing)
            SetVisible(playing);

        // Sync logo camera background color with main camera
        if (logoCamera != null && mainCam != null)
            logoCamera.backgroundColor = mainCam.backgroundColor;

        if (!playing || inputProcessor == null) return;

        Vector3 tilt = inputProcessor.GetTilt();
        // tilt is a raw gravity vector (~0,0,100 when flat). Derive roll/pitch via atan2.
        float roll = Mathf.Atan2(tilt.x, tilt.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Atan2(tilt.y, tilt.z) * Mathf.Rad2Deg;

        Quaternion targetRotation = Quaternion.Euler(baseRotation) * Quaternion.Euler(-pitch, 0f, roll);
        currentRotation = Quaternion.Slerp(currentRotation, targetRotation, smoothSpeed * Time.deltaTime);
        logoObject.transform.rotation = currentRotation;
    }

    void OnDestroy()
    {
        if (logoCamera != null) Destroy(logoCamera.gameObject);
    }
}
