using UnityEngine;

/// <summary>
/// Sets camera background color on start and cycles through colors on joystick button press.
/// </summary>
public class BackgroundManager : MonoBehaviour
{
    [SerializeField] Color defaultColor = new(0.05f, 0.05f, 0.15f);

    [SerializeField] Color[] backgroundColors = new[]
    {
        new Color(0.05f, 0.05f, 0.15f),  // Deep navy
        new Color(0.12f, 0.04f, 0.18f),  // Dark violet
        new Color(0.04f, 0.14f, 0.12f),  // Dark teal
        new Color(0.18f, 0.04f, 0.04f),  // Dark crimson
        new Color(0.04f, 0.1f, 0.2f),    // Midnight blue
        new Color(0.14f, 0.04f, 0.16f),  // Dark purple
        new Color(0.08f, 0.12f, 0.04f),  // Dark olive
        new Color(0.02f, 0.02f, 0.02f),  // Near black
    };

    [SerializeField] InputProcessor inputProcessor;

    Camera mainCamera;
    int currentColorIndex = -1;

    void Start()
    {
        mainCamera = Camera.main;

        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = defaultColor;
        }

        if (inputProcessor == null)
            inputProcessor = FindObjectOfType<InputProcessor>();

        if (inputProcessor != null)
            inputProcessor.OnJoystickButtonPressed += CycleBackground;
    }

    void OnDestroy()
    {
        if (inputProcessor != null)
            inputProcessor.OnJoystickButtonPressed -= CycleBackground;
    }

    void CycleBackground()
    {
        if (mainCamera == null || backgroundColors == null || backgroundColors.Length == 0) return;

        // Pick a random index that isn't the current one
        int newIndex;
        if (backgroundColors.Length == 1)
        {
            newIndex = 0;
        }
        else
        {
            do
            {
                newIndex = Random.Range(0, backgroundColors.Length);
            } while (newIndex == currentColorIndex);
        }

        currentColorIndex = newIndex;
        mainCamera.backgroundColor = backgroundColors[currentColorIndex];
    }
}
