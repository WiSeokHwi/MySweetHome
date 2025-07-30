using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 단순화된 GridManager - 배치 모드에서만 로컬 그리드 표시
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private float cellSize = 1.0f;
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;
    [SerializeField] private float localGridRadius = 5.0f;

    [Header("Materials")]
    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;
    [SerializeField] private Material runtimeDefaultGridMaterial;

    // 로컬 그리드 시스템
    private GameObject localGridParent;
    private List<GameObject> localGridCells = new List<GameObject>();
    private Vector3 lastPlayerPosition;
    private bool isPlacementMode = false;

    // 그리드 상태 관리
    private Dictionary<Vector3Int, PlacableItem> occupiedCells = new Dictionary<Vector3Int, PlacableItem>();
    private PlacableItem currentPlacingItem;

    public float CellSize => cellSize;
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public Material RuntimeDefaultGridMaterial => runtimeDefaultGridMaterial;

    void Awake()
    {
        CreateLocalGridParent();
        ValidateMaterials();
    }

    private void CreateLocalGridParent()
    {
        localGridParent = new GameObject("LocalGrid");
        localGridParent.transform.SetParent(transform);
        localGridParent.SetActive(false);
    }

    private void ValidateMaterials()
    {
        Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit") ??
                              Shader.Find("Standard") ??
                              Shader.Find("Unlit/Color");

        if (validPlacementMaterial == null)
        {
            validPlacementMaterial = new Material(defaultShader)
            {
                color = new Color(0.0f, 1.0f, 0.0f, 0.5f),
                name = "ValidPlacement"
            };
        }

        if (invalidPlacementMaterial == null)
        {
            invalidPlacementMaterial = new Material(defaultShader)
            {
                color = new Color(1.0f, 0.0f, 0.0f, 0.5f),
                name = "InvalidPlacement"
            };
        }

        if (runtimeDefaultGridMaterial == null)
        {
            runtimeDefaultGridMaterial = new Material(defaultShader)
            {
                color = new Color(0.5f, 0.8f, 1.0f, 0.3f),
                name = "RuntimeDefault"
            };
        }
    }

    public Vector3 SnapToGridForPlacement(Vector3 hitPoint, Vector3Int itemSize, float itemWorldHeight)
    {
        Vector3 relativeHitPos = hitPoint - transform.position;
        float halfWidth = (gridWidth * cellSize) * 0.5f;
        float halfHeight = (gridHeight * cellSize) * 0.5f;

        relativeHitPos.x += halfWidth;
        relativeHitPos.z += halfHeight;

        float snappedX = Mathf.Floor(relativeHitPos.x / cellSize) * cellSize;
        float snappedZ = Mathf.Floor(relativeHitPos.z / cellSize) * cellSize;

        Vector3 itemCenterOffset = new Vector3(
            (itemSize.x - 1) * 0.5f * cellSize,
            0,
            (itemSize.z - 1) * 0.5f * cellSize
        );

        return transform.position +
               new Vector3(snappedX - halfWidth, transform.position.y, snappedZ - halfHeight) +
               new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f) +
               itemCenterOffset;
    }

    public Vector3Int WorldToGridCoordinates(Vector3 worldPosition)
    {
        Vector3 relativePos = worldPosition - transform.position;
        float halfWidth = (gridWidth * cellSize) * 0.5f;
        float halfHeight = (gridHeight * cellSize) * 0.5f;

        relativePos.x += halfWidth;
        relativePos.z += halfHeight;

        return new Vector3Int(
            Mathf.FloorToInt(relativePos.x / cellSize),
            0,
            Mathf.FloorToInt(relativePos.z / cellSize)
        );
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
                Vector3Int currentCell = new Vector3Int(itemGridOrigin.x + x, 0, itemGridOrigin.z + z);
                if (!IsInGridBounds(currentCell) || occupiedCells.ContainsKey(currentCell))
                    return false;
            }
        }
        return true;
    }

    public void OccupyCells(PlacableItem item)
    {
        if (item == null) return;

        // 배치하기 전에 이미 점유된 셀이 있다면 먼저 해제
        ReleaseCells(item);

        Vector3Int itemGridOrigin = WorldToGridCoordinates(item.transform.position);
        Vector3Int itemSize = item.itemGridSize;

        for (int x = 0; x < itemSize.x; x++)
        {
            for (int z = 0; z < itemSize.z; z++)
            {
                Vector3Int cellToOccupy = new Vector3Int(itemGridOrigin.x + x, 0, itemGridOrigin.z + z);
                if (IsInGridBounds(cellToOccupy))
                    occupiedCells[cellToOccupy] = item;
            }
        }

        // 현재 배치 모드이고 로컬 그리드가 활성화되어 있다면 즉시 업데이트
        if (isPlacementMode && localGridParent.activeInHierarchy)
            RefreshLocalGridCells();
    }

    public void ReleaseCells(PlacableItem item)
    {
        if (item == null) return;

        var cellsToRemove = new List<Vector3Int>();
        foreach (var entry in occupiedCells)
        {
            if (entry.Value == item)
                cellsToRemove.Add(entry.Key);
        }

        foreach (Vector3Int cell in cellsToRemove)
            occupiedCells.Remove(cell);

        // 현재 배치 모드이고 로컬 그리드가 활성화되어 있다면 즉시 업데이트
        if (cellsToRemove.Count > 0 && isPlacementMode && localGridParent.activeInHierarchy)
            RefreshLocalGridCells();
    }

    // === VRPlacementController 호환 메서드들 ===
    public void SetCurrentPlacingItem(PlacableItem item)
    {
        StartPlacementMode(item);
    }

    public void ClearCurrentPlacingItem()
    {
        EndPlacementMode();
    }

    public void SetGridMaterial(Material material)
    {
        // 로컬 그리드에서는 개별 셀 머티리얼만 관리하므로 빈 구현
    }

    public void UpdateGridMaterialForPlacement(bool isValidPlacement)
    {
        // 현재 로컬 그리드 시스템에서는 각 셀이 개별적으로 머티리얼을 가지므로 빈 구현
    }

    // 아이템이 그냥 떨어뜨려질 때 호출 (배치 모드 없이)
    public void HandleItemDropped(PlacableItem item)
    {
        if (item == null) return;

        // 이미 배치된 아이템이었다면 그리드 점유 해제
        if (item.IsPlaced)
        {
            ReleaseCells(item);
            item.SetPlaced(false);
        }

        // 강제로 한 번 더 해제 시도 (안전장치)
        ForceReleaseCellsForItem(item);
    }

    public void StartPlacementMode(PlacableItem item)
    {
        currentPlacingItem = item;
        isPlacementMode = true;
        localGridParent.SetActive(true);
    }

    public void EndPlacementMode()
    {
        currentPlacingItem = null;
        isPlacementMode = false;
        localGridParent.SetActive(false);
        ClearLocalGridCells();
    }

    public void UpdateLocalGrid(Vector3 playerPosition)
    {
        if (!isPlacementMode) return;

        if (Vector3.Distance(playerPosition, lastPlayerPosition) < 1.0f && localGridCells.Count > 0)
            return;

        lastPlayerPosition = playerPosition;
        ClearLocalGridCells();
        GenerateLocalGridCells(playerPosition);
    }

    private void ClearLocalGridCells()
    {
        foreach (GameObject cell in localGridCells)
        {
            if (cell != null)
                Destroy(cell);
        }
        localGridCells.Clear();
    }

    // 로컬 그리드 셀들을 즉시 새로고침 (점유 상태 변경 시 사용)
    private void RefreshLocalGridCells()
    {
        if (!isPlacementMode || localGridCells.Count == 0) return;

        // 기존 셀들의 머티리얼만 업데이트 (재생성하지 않음)
        foreach (GameObject cell in localGridCells)
        {
            if (cell == null) continue;

            // 셀 이름에서 그리드 좌표 추출
            string[] parts = cell.name.Split('_');
            if (parts.Length >= 3 &&
                int.TryParse(parts[1], out int x) &&
                int.TryParse(parts[2], out int z))
            {
                Vector3Int gridPos = new Vector3Int(x, 0, z);
                var renderer = cell.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    bool isOccupied = occupiedCells.ContainsKey(gridPos);
                    renderer.material = isOccupied ? invalidPlacementMaterial : validPlacementMaterial;
                }
            }
        }
    }

    private void GenerateLocalGridCells(Vector3 centerPosition)
    {
        Vector3Int centerGridPos = WorldToGridCoordinates(centerPosition);
        int cellRadius = Mathf.CeilToInt(localGridRadius / cellSize);

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int z = -cellRadius; z <= cellRadius; z++)
            {
                Vector3Int gridPos = new Vector3Int(centerGridPos.x + x, 0, centerGridPos.z + z);

                if (!IsInGridBounds(gridPos)) continue;

                Vector3 worldPos = GridToWorldCoordinates(gridPos);
                float distance = Vector3.Distance(new Vector3(centerPosition.x, worldPos.y, centerPosition.z), worldPos);
                if (distance > localGridRadius) continue;

                CreateGridCell(gridPos, worldPos);
            }
        }
    }

    private Vector3 GridToWorldCoordinates(Vector3Int gridCoordinates)
    {
        float halfWidth = (gridWidth * cellSize) * 0.5f;
        float halfHeight = (gridHeight * cellSize) * 0.5f;

        return new Vector3(
            gridCoordinates.x * cellSize + transform.position.x - halfWidth + cellSize * 0.5f,
            transform.position.y,
            gridCoordinates.z * cellSize + transform.position.z - halfHeight + cellSize * 0.5f
        );
    }

    private void CreateGridCell(Vector3Int gridPos, Vector3 worldPos)
    {
        GameObject cellObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
        cellObject.name = $"GridCell_{gridPos.x}_{gridPos.z}";
        cellObject.transform.SetParent(localGridParent.transform);
        cellObject.transform.position = worldPos;
        cellObject.transform.localScale = new Vector3(cellSize * 0.1f, 1f, cellSize * 0.1f);

        var renderer = cellObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            bool isOccupied = occupiedCells.ContainsKey(gridPos);
            renderer.material = isOccupied ? invalidPlacementMaterial : validPlacementMaterial;
        }

        if (cellObject.GetComponent<Collider>() != null)
            Destroy(cellObject.GetComponent<Collider>());

        localGridCells.Add(cellObject);
    }

    // 강제로 특정 아이템의 모든 점유를 해제 (안전장치)
    public void ForceReleaseCellsForItem(PlacableItem item)
    {
        if (item == null) return;

        Vector3Int itemGridOrigin = WorldToGridCoordinates(item.transform.position);
        Vector3Int itemSize = item.itemGridSize;

        for (int x = 0; x < itemSize.x; x++)
        {
            for (int z = 0; z < itemSize.z; z++)
            {
                Vector3Int cellToRelease = new Vector3Int(itemGridOrigin.x + x, 0, itemGridOrigin.z + z);
                if (occupiedCells.ContainsKey(cellToRelease))
                    occupiedCells.Remove(cellToRelease);
            }
        }

        ReleaseCells(item);
    }

    // 중복 점유 정리 (모든 아이템의 점유 상태를 재계산)
    [ContextMenu("중복 점유 정리")]
    public void CleanupOccupiedCells()
    {
        occupiedCells.Clear();

        PlacableItem[] allItems = FindObjectsByType<PlacableItem>(FindObjectsSortMode.None);
        foreach (PlacableItem item in allItems)
        {
            if (item != null && item.IsPlaced)
                OccupyCells(item);
        }

        if (isPlacementMode && localGridParent.activeInHierarchy)
            RefreshLocalGridCells();
    }

    void OnDestroy()
    {
        ClearLocalGridCells();
    }
}