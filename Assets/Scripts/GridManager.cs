using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("그리드 셀 하나의 크기 (예: 1이면 1x1 유닛).")]
    [SerializeField] private float cellSize = 1.0f;
    [Tooltip("그리드의 가로 셀 개수.")]
    [SerializeField] private int gridWidth = 10;
    [Tooltip("그리드의 세로 셀 개수.")]
    [SerializeField] private int gridHeight = 10;

    [Header("Placement Settings")]
    [Tooltip("배치 가능한 아이템이 있을 때 표시될 그리드 평면의 Material.")]
    [SerializeField] private Material previewGridMaterial;
    [Tooltip("배치 불가능한 영역에 표시될 그리드 평면의 Material.")]
    [SerializeField] private Material invalidGridMaterial;
    [Tooltip("배치 모드가 아닐 때 그리드 평면에 표시될 기본 Material.")]
    [SerializeField] private Material defaultGridVisualMaterial;

    private MeshRenderer gridMeshRenderer;
    private MeshFilter gridMeshFilter;
    private Material runtimeDefaultGridMaterial; // 스크립트가 관리할 기본 Material

    // 외부에서 runtimeDefaultGridMaterial에 접근할 수 있도록 public 프로퍼티 추가
    public Material RuntimeDefaultGridMaterial => runtimeDefaultGridMaterial;
    // PlacableItem에 점유 셀 목록 추가
    public List<Vector3Int> OccupiedCells { get; } = new List<Vector3Int>();


    private Dictionary<Vector3Int, PlacableItem> occupiedCells = new Dictionary<Vector3Int, PlacableItem>();

    private PlacableItem currentPlacingItem;

    void Awake()
    {
        gridMeshRenderer = GetComponent<MeshRenderer>();
        gridMeshFilter = GetComponent<MeshFilter>();

        if (gridMeshRenderer == null)
        {
            gridMeshRenderer = gameObject.AddComponent<MeshRenderer>();
        }
        if (gridMeshFilter == null)
        {
            gridMeshFilter = gameObject.AddComponent<MeshFilter>();
        }

        if (defaultGridVisualMaterial == null)
        {
            Debug.LogWarning("GridManager: 'Default Grid Visual Material'이 할당되지 않았습니다. 그리드가 제대로 표시되지 않을 수 있습니다.", this);
        }
        runtimeDefaultGridMaterial = defaultGridVisualMaterial;

        GenerateGridMesh();

        // 초기 그리드 Material을 runtimeDefaultGridMaterial로 설정
        SetGridMaterial(runtimeDefaultGridMaterial);
    }

    private void GenerateGridMesh()
    {
        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(0, 0, 0);
        vertices[1] = new Vector3(gridWidth * cellSize, 0, 0);
        vertices[2] = new Vector3(0, 0, gridHeight * cellSize);
        vertices[3] = new Vector3(gridWidth * cellSize, 0, gridHeight * cellSize);

        int[] triangles = new int[]
        {
            0, 2, 1,
            1, 2, 3
        };

        Vector2[] uv = new Vector2[4];
        uv[0] = new Vector2(0, 0);
        uv[1] = new Vector2(gridWidth, 0);
        uv[2] = new Vector2(0, gridHeight);
        uv[3] = new Vector2(gridWidth, gridHeight);

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();

        gridMeshFilter.mesh = mesh;

        if (previewGridMaterial != null)
        {
            previewGridMaterial.mainTextureScale = new Vector2(gridWidth, gridHeight);
        }
        if (invalidGridMaterial != null)
        {
            invalidGridMaterial.mainTextureScale = new Vector2(gridWidth, gridHeight);
        }
        if (defaultGridVisualMaterial != null)
        {
            defaultGridVisualMaterial.mainTextureScale = new Vector2(gridWidth, gridHeight);
        }
    }

    public Vector3 SnapToGridForPlacement(Vector3 hitPoint, Vector3Int itemSize, float itemWorldHeight)
    {
        Vector3 relativeHitPos = hitPoint - transform.position;

        float snappedRelativeX = Mathf.Floor(relativeHitPos.x / cellSize) * cellSize;
        float snappedRelativeZ = Mathf.Floor(relativeHitPos.z / cellSize) * cellSize;

        Vector3 itemCenterOffset = new Vector3(itemSize.x * 0.5f * cellSize, 0, itemSize.z * 0.5f * cellSize);

        float snappedY = transform.position.y;

        Vector3 finalWorldPosition = transform.position + new Vector3(snappedRelativeX, snappedY, snappedRelativeZ) + itemCenterOffset;

        // Debug.Log들을 활성화하여 값 추적 (디버깅 시 유용)
        // Debug.Log($"[GridManager.SnapToGridForPlacement] Input hitPoint Y: {hitPoint.y}");
        // Debug.Log($"[GridManager.SnapToGridForPlacement] GridManager Y: {transform.position.y}");
        // Debug.Log($"[GridManager.SnapToGridForPlacement] Calculated snappedY: {snappedY}");
        // Debug.Log($"[GridManager.SnapToGridForPlacement] Final Calculated World Position Y (Item Pivot): {finalWorldPosition.y}");

        return finalWorldPosition;
    }

    public Vector3Int WorldToGridCoordinates(Vector3 worldPosition)
    {
        Vector3 relativePos = worldPosition - transform.position;

        int x = Mathf.FloorToInt(relativePos.x / cellSize);
        int y = Mathf.FloorToInt(relativePos.y / cellSize);
        int z = Mathf.FloorToInt(relativePos.z / cellSize);

        return new Vector3Int(x, y, z);
    }

    public Vector3 GridToWorldCoordinates(Vector3Int gridCoordinates)
    {
        float worldX = gridCoordinates.x * cellSize + transform.position.x;
        float worldY = gridCoordinates.y * cellSize + transform.position.y;
        float worldZ = gridCoordinates.z * cellSize + transform.position.z;

        worldX += cellSize * 0.5f;
        worldZ += cellSize * 0.5f;

        return new Vector3(worldX, worldY, worldZ);
    }

    public bool IsInGridBounds(Vector3Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.x < gridWidth &&
               gridPos.z >= 0 && gridPos.z < gridHeight;
    }

    public bool CanPlaceItem(Vector3Int itemGridOrigin, Vector3Int itemSize)
    {
        for (int x = 0; x < itemSize.x; x++)
        {
            for (int z = 0; z < itemSize.z; z++)
            {
                Vector3Int currentCell = new Vector3Int(itemGridOrigin.x + x, itemGridOrigin.y, itemGridOrigin.z + z);
                if (!IsInGridBounds(currentCell) || occupiedCells.ContainsKey(currentCell))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public void OccupyCells(PlacableItem item)
    {
        Vector3Int itemGridOrigin = WorldToGridCoordinates(item.transform.position);
        Vector3Int itemSize = item.itemGridSize;

        for (int x = 0; x < itemSize.x; x++)
        {
            for (int z = 0; z < itemSize.z; z++)
            {
                Vector3Int cellToOccupy = new Vector3Int(itemGridOrigin.x + x, itemGridOrigin.y, itemGridOrigin.z + z);
                if (IsInGridBounds(cellToOccupy))
                {
                    occupiedCells[cellToOccupy] = item;
                }
                else
                {
                    Debug.LogWarning($"[GridManager] Attempted to occupy cell {cellToOccupy} outside grid bounds for item {item.name}.");
                }
            }
        }
    }

    public void ReleaseCells(PlacableItem item)
    {
        List<Vector3Int> cellsToRemove = new List<Vector3Int>();
        foreach (var entry in occupiedCells)
        {
            if (entry.Value == item)
            {
                cellsToRemove.Add(entry.Key);
            }
        }

        foreach (Vector3Int cell in cellsToRemove)
        {
            occupiedCells.Remove(cell);
        }
    }

    public void SetCurrentPlacingItem(PlacableItem item)
    {
        currentPlacingItem = item;
        SetGridMaterial(previewGridMaterial);
    }

    public void ClearCurrentPlacingItem()
    {
        currentPlacingItem = null;
        SetGridMaterial(runtimeDefaultGridMaterial); // <--- runtimeDefaultGridMaterial 사용
    }

    public void SetGridMaterial(Material material)
    {
        if (gridMeshRenderer != null && material != null)
        {
            gridMeshRenderer.material = material;
        }
        else if (gridMeshRenderer != null && material == null)
        {
            // Material이 null일 경우 Debug.LogWarning을 띄우거나 Unity Default-Material로 대체
            Debug.LogWarning("GridManager: Material to set is null. Grid visibility might be affected.");
            gridMeshRenderer.material = new Material(Shader.Find("Standard")); // Unity 기본 Material
        }
    }

    public void UpdateGridMaterialForPlacement(bool isValidPlacement)
    {
        if (currentPlacingItem != null)
        {
            SetGridMaterial(isValidPlacement ? previewGridMaterial : invalidGridMaterial);
        }
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Vector3 origin = transform.position;
            Vector3 size = new Vector3(gridWidth * cellSize, 0.1f, gridHeight * cellSize);
            Gizmos.DrawWireCube(origin + size / 2f, size);

            Gizmos.color = Color.red;
            foreach (var entry in occupiedCells)
            {
                Vector3 cellWorldPos = GridToWorldCoordinates(entry.Key);
                Gizmos.DrawWireCube(cellWorldPos, new Vector3(cellSize, 0.1f, cellSize));
            }
        }
    }
}