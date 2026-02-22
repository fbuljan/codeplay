using System;
using UnityEngine;

/// <summary>
/// Processes raw input from InputReader and provides game-specific input events.
/// Handles press detection (not hold) so consumers get clean one-shot events.
/// </summary>
public class InputProcessor : MonoBehaviour
{
    [Header("Button Mapping - Phase 1")]
    [SerializeField] int jumpButtonIndex = 0;     // btn1 - Jump
    [SerializeField] int shieldButtonIndex = 1;   // btn2 - Shield
    [SerializeField] int slideButtonIndex = 2;    // btn3 - Slide
    [SerializeField] int shootButtonIndex = 3;    // btn4 - Shoot

    [Header("References")]
    [SerializeField] InputReader inputReader;

    public event Action OnJumpPressed;
    public event Action OnSlidePressed;
    public event Action OnShootPressed;
    public event Action OnShieldPressed;

    bool[] previousButtonStates;

    void Awake()
    {
        if (inputReader == null)
            inputReader = FindObjectOfType<InputReader>();

        if (inputReader == null)
            Debug.LogError("[InputProcessor] No InputReader found!");
    }

    void Start()
    {
        if (inputReader != null && inputReader.FaceButtons != null)
            previousButtonStates = new bool[inputReader.FaceButtons.Length];
    }

    void Update()
    {
        if (inputReader == null || inputReader.FaceButtons == null) return;

        if (previousButtonStates == null || previousButtonStates.Length != inputReader.FaceButtons.Length)
            previousButtonStates = new bool[inputReader.FaceButtons.Length];

        ProcessButton(jumpButtonIndex, OnJumpPressed);
        ProcessButton(slideButtonIndex, OnSlidePressed);
        ProcessButton(shootButtonIndex, OnShootPressed);
        ProcessButton(shieldButtonIndex, OnShieldPressed);

        Array.Copy(inputReader.FaceButtons, previousButtonStates, inputReader.FaceButtons.Length);
    }

    void ProcessButton(int index, Action onPressed)
    {
        if (index < 0 || index >= inputReader.FaceButtons.Length) return;

        bool isPressed = inputReader.FaceButtons[index];
        bool wasPressed = previousButtonStates[index];

        if (isPressed && !wasPressed)
            onPressed?.Invoke();
    }

    // Held-state accessors (useful for shield, etc.)
    public bool IsJumpHeld => GetButtonState(jumpButtonIndex);
    public bool IsSlideHeld => GetButtonState(slideButtonIndex);
    public bool IsShootHeld => GetButtonState(shootButtonIndex);
    public bool IsShieldHeld => GetButtonState(shieldButtonIndex);

    bool GetButtonState(int index)
    {
        if (inputReader == null || inputReader.FaceButtons == null) return false;
        if (index < 0 || index >= inputReader.FaceButtons.Length) return false;
        return inputReader.FaceButtons[index];
    }

    // Phase 2+
    public Vector2 GetJoystick()
    {
        if (inputReader == null) return Vector2.zero;
        return inputReader.JoystickPosition;
    }

    // Phase 3
    public Vector3 GetTilt()
    {
        if (inputReader == null) return Vector3.zero;
        return inputReader.Tilt;
    }
}
