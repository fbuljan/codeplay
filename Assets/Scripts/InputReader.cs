using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

/// <summary>
/// Reads binary input packets from a microcontroller over serial.
/// Expected struct (12 bytes, little-endian):
///   int16 buttons   - bitfield: bit0=btn1, bit1=btn2, bit2=btn3, bit3=btn4, bit4=joystickBtn
///   int16 joy_x     - joystick X (normalized by MCU)
///   int16 joy_y     - joystick Y (normalized by MCU)
///   int16 tilt_x    - accelerometer X
///   int16 tilt_y    - accelerometer Y
///   int16 tilt_z    - accelerometer Z
/// </summary>
public class InputReader : MonoBehaviour
{
    [Header("Serial Settings")]
    [SerializeField] string portName = "COM3";
    [SerializeField] int baudRate = 115200;
    [SerializeField] int readTimeoutMs = 100;

    [Header("Joystick Normalization")]
    [Tooltip("Max absolute value the MCU sends for joystick axes. Used to normalize to -1..1")]
    [SerializeField] int joystickRange = 100;

    const int PacketSize = 12; // 6 x int16
    const int FaceButtonCount = 4;

    public bool[] FaceButtons { get; private set; }
    public Vector2 JoystickPosition { get; private set; }
    public bool JoystickButton { get; private set; }
    public Vector3 Tilt { get; private set; }

    public event Action OnInputReceived;

    SerialPort serial;
    Thread readThread;
    volatile bool running;

    // Thread-safe packet transfer
    byte[] latestPacket;
    readonly object packetLock = new();

    void Awake()
    {
        FaceButtons = new bool[FaceButtonCount];
    }

    void OnEnable()
    {
        try
        {
            serial = new SerialPort(portName, baudRate)
            {
                ReadTimeout = readTimeoutMs,
                DtrEnable = true,
                RtsEnable = true
            };
            serial.Open();

            // Discard any stale data in the buffer
            serial.DiscardInBuffer();

            running = true;
            readThread = new Thread(ReadLoop) { IsBackground = true };
            readThread.Start();

            Debug.Log($"[InputReader] Opened {portName} @ {baudRate}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[InputReader] Failed to open {portName}: {e.Message}");
        }
    }

    void OnDisable()
    {
        running = false;
        readThread?.Join(500);

        if (serial is { IsOpen: true })
        {
            serial.Close();
            Debug.Log("[InputReader] Port closed.");
        }
    }

    void ReadLoop()
    {
        byte[] buffer = new byte[PacketSize];

        while (running)
        {
            try
            {
                // Read exactly PacketSize bytes
                int bytesRead = 0;
                while (bytesRead < PacketSize && running)
                {
                    int read = serial.Read(buffer, bytesRead, PacketSize - bytesRead);
                    bytesRead += read;
                }

                if (bytesRead == PacketSize)
                {
                    lock (packetLock)
                    {
                        latestPacket ??= new byte[PacketSize];
                        Buffer.BlockCopy(buffer, 0, latestPacket, 0, PacketSize);
                    }
                }
            }
            catch (TimeoutException) { }
            catch (Exception e)
            {
                if (running) Debug.LogWarning($"[InputReader] Read error: {e.Message}");
            }
        }
    }

    void Update()
    {
        byte[] packet;
        lock (packetLock)
        {
            if (latestPacket == null) return;
            packet = latestPacket;
            latestPacket = null;
        }

        ParsePacket(packet);
        OnInputReceived?.Invoke();
    }

    void ParsePacket(byte[] packet)
    {
        // All values are int16 little-endian (matches Arduino + Windows)
        short buttons = BitConverter.ToInt16(packet, 0);
        short joyX = BitConverter.ToInt16(packet, 2);
        short joyY = BitConverter.ToInt16(packet, 4);
        short tiltX = BitConverter.ToInt16(packet, 6);
        short tiltY = BitConverter.ToInt16(packet, 8);
        short tiltZ = BitConverter.ToInt16(packet, 10);

        // Unpack button bitfield
        FaceButtons[0] = (buttons & (1 << 0)) != 0; // btn1
        FaceButtons[1] = (buttons & (1 << 1)) != 0; // btn2
        FaceButtons[2] = (buttons & (1 << 2)) != 0; // btn3
        FaceButtons[3] = (buttons & (1 << 3)) != 0; // btn4
        JoystickButton = (buttons & (1 << 4)) != 0;  // joystick press

        // Normalize joystick to -1..1
        float range = Mathf.Max(joystickRange, 1);
        JoystickPosition = new Vector2(
            Mathf.Clamp(joyX / range, -1f, 1f),
            Mathf.Clamp(joyY / range, -1f, 1f)
        );

        // Raw tilt values (normalization TBD for Phase 3)
        Tilt = new Vector3(tiltX, tiltY, tiltZ);
    }

    public bool IsConnected => serial is { IsOpen: true };

    public (bool success, string message) Reconnect(string newPort, int newBaud)
    {
        // Stop existing connection
        running = false;
        readThread?.Join(500);

        if (serial is { IsOpen: true })
            serial.Close();

        // Apply new settings
        portName = newPort;
        baudRate = newBaud;

        // Reopen
        try
        {
            serial = new SerialPort(portName, baudRate)
            {
                ReadTimeout = readTimeoutMs,
                DtrEnable = true,
                RtsEnable = true
            };
            serial.Open();
            serial.DiscardInBuffer();

            running = true;
            readThread = new Thread(ReadLoop) { IsBackground = true };
            readThread.Start();

            Debug.Log($"[InputReader] Reconnected to {portName} @ {baudRate}");
            return (true, $"Connected to {portName} @ {baudRate}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[InputReader] Failed to open {portName}: {e.Message}");
            return (false, $"Failed: {e.Message}");
        }
    }

    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();
}