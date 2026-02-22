using UnityEngine;

/// <summary>
/// Infinite ground scrolling. Clones the 3 lane planes into a grid of segments,
/// then recycles rows behind the player to the front as the player moves forward.
/// </summary>
public class GroundScroller : MonoBehaviour
{
    Transform playerTransform;

    float segmentLength;
    float[] laneXPositions;
    GameObject[,] segments; // [row, lane]
    int rowCount;

    float[] rowZPositions; // Z center of each row
    int rearRowIndex;      // which row index is currently the rearmost

    /// <summary>
    /// Called by GameManager to initialize the scroller with cloned segments.
    /// </summary>
    public void Initialize(Transform player, GameObject[] templatePlanes, float[] lanePositions, float segLength, int rows, int groundLayer)
    {
        playerTransform = player;
        segmentLength = segLength;
        laneXPositions = lanePositions;
        rowCount = rows;

        segments = new GameObject[rowCount, lanePositions.Length];
        rowZPositions = new float[rowCount];

        Vector3 planeScale = templatePlanes[0].transform.localScale;

        for (int row = 0; row < rowCount; row++)
        {
            float zCenter = segmentLength / 2f + row * segmentLength;
            rowZPositions[row] = zCenter;

            for (int lane = 0; lane < lanePositions.Length; lane++)
            {
                GameObject seg;
                if (row == 0)
                {
                    // First row: use the originals (already in scene)
                    seg = templatePlanes[lane];
                }
                else
                {
                    // Clone from the original
                    seg = Instantiate(templatePlanes[lane], transform);
                    seg.name = $"Lane{lane}_Row{row}";
                }

                seg.transform.position = new Vector3(lanePositions[lane], 0f, zCenter);
                seg.transform.localScale = planeScale;
                seg.layer = groundLayer;
                segments[row, lane] = seg;
            }
        }

        rearRowIndex = 0;
    }

    void Update()
    {
        if (playerTransform == null || segments == null) return;

        float playerZ = playerTransform.position.z;

        // Check if the player has moved past the center of the second row
        // If so, recycle the rear row to the front
        float rearZ = rowZPositions[rearRowIndex];
        float recycleThreshold = rearZ + segmentLength;

        if (playerZ > recycleThreshold)
        {
            RecycleRearRow();
        }
    }

    void RecycleRearRow()
    {
        // Find the current frontmost row Z
        float maxZ = float.MinValue;
        for (int i = 0; i < rowCount; i++)
        {
            if (rowZPositions[i] > maxZ)
                maxZ = rowZPositions[i];
        }

        // Move rear row ahead of the front
        float newZ = maxZ + segmentLength;
        rowZPositions[rearRowIndex] = newZ;

        for (int lane = 0; lane < laneXPositions.Length; lane++)
        {
            Vector3 pos = segments[rearRowIndex, lane].transform.position;
            pos.z = newZ;
            segments[rearRowIndex, lane].transform.position = pos;
        }

        // Advance rear row index to the next one
        rearRowIndex = (rearRowIndex + 1) % rowCount;
    }
}
