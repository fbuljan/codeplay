using UnityEngine;

/// <summary>
/// Owns scene setup: positions lanes, player, and camera programmatically.
/// Assign references in the inspector, then everything configures itself on Play.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] Transform playerTransform;
    [SerializeField] GameObject[] lanePlanes = new GameObject[3]; // Left, Center, Right
    [SerializeField] Camera mainCamera;

    [Header("Lane Settings")]
    [SerializeField] float laneWidth = 3f;
    [SerializeField] float segmentLength = 100f;
    [SerializeField] int segmentRows = 4;

    [Header("Camera")]
    [SerializeField] Vector3 cameraOffset = new(0f, 5f, -10f);

    [Header("Ground Layer")]
    [Tooltip("Layer index to assign to lane planes (6 = first unused user layer)")]
    [SerializeField] int groundLayerIndex = 6;

    /// <summary>
    /// Lane X positions, indexed 0=Left, 1=Center, 2=Right.
    /// </summary>
    public float[] LanePositions { get; private set; }

    void Awake()
    {
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
    }

    void SetupLanes()
    {
        // Unity's default plane is 10x10 units, so scale = desired size / 10
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

        // Get capsule height to position correctly on ground
        float playerHeight = 1f; // default half-height
        var capsule = playerTransform.GetComponent<CapsuleCollider>();
        if (capsule != null)
            playerHeight = capsule.height / 2f;

        // Center lane, sitting on ground, facing +Z
        playerTransform.position = new Vector3(0f, playerHeight, 0f);
        playerTransform.rotation = Quaternion.identity;
    }

    void SetupCamera()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            Debug.LogError("[GameManager] No camera found!");
            return;
        }

        // Position behind and above player
        mainCamera.transform.position = playerTransform.position + cameraOffset;
        mainCamera.transform.LookAt(playerTransform);
    }

    void LateUpdate()
    {
        // Simple camera follow
        if (mainCamera != null && playerTransform != null)
        {
            mainCamera.transform.position = playerTransform.position + cameraOffset;
            mainCamera.transform.LookAt(playerTransform);
        }
    }
}
