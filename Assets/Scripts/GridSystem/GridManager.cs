/// <summary>
/// ==================== 그리드 배치 시스템 ====================
/// 
/// 【 시스템 개요 】
/// VR 환경에서 PlacableItem을 그리드 기반으로 배치하기 위한 코어 시스템입니다.
/// 실시간 월드-그리드 좌표 변환, 셀 점유 관리, 시각적 로컬 그리드 제공
/// 
/// 【 주요 기능 】
/// 1. 그리드 시스템: 월드 좌표 ↔ 그리드 좌표 변환
/// 2. 셀 점유 관리: PlacableItem별 점유 셀 추적
/// 3. 로컬 그리드: 배치 모드에서 플레이어 주변에만 그리드 표시
/// 4. 배치 유효성 검사: 범위, 셀 점유, 충돌 검사
/// 5. 시각적 피드백: 배치 가능/불가 머티리얼
/// 
/// 【 연동 시스템 】
/// - VRPlacementController: 배치 모드 제어, 레이캠스트 처리, 배치 실행
/// - PlacableItem: 아이템 크기, 배치 상태, 그리드 소속 정보
/// - PreviewCollisionDetector: 3D 충돌 검사 및 실시간 피드백
/// 
/// 【 데이터 저장 위치 】
/// - occupiedCells: 셀 점유 정보 Dictionary<Vector3Int, PlacableItem> (이 클래스에서 관리)
/// - localGridCells: 로컬 그리드 UI 오브젝트들 List<GameObject> (이 클래스에서 관리)
/// - 그리드 설정: cellSize, gridWidth, gridHeight (Inspector 설정)
/// 
/// 【 로컬 그리드 시스템 】
/// 1. 배치 모드 시작 → 로컬 그리드 활성화
/// 2. 플레이어 이동 → 주변 그리드 셀 재생성
/// 3. 셀 점유 상태에 따른 색상 변경 (초록/빨간색)
/// 4. 배치 모드 종료 → 로컬 그리드 비활성화
/// </summary>
using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    // ========== 그리드 설정 ==========
    [Header("Grid Settings")]
    [SerializeField] private float cellSize = 1.0f;        // 그리드 셀 한 장의 크기 (월드 단위)
    [SerializeField] private int gridWidth = 10;           // 전체 그리드의 너비 (셀 개수)
    [SerializeField] private int gridHeight = 10;          // 전체 그리드의 높이 (셀 개수)
    [SerializeField] private float localGridRadius = 5.0f; // 로컬 그리드 표시 반경 (월드 단위)

    // ========== 시각적 머티리얼 ==========
    [Header("Materials")]
    [SerializeField] private Material validPlacementMaterial;    // 배치 가능 셀 머티리얼 (초록색)
    [SerializeField] private Material invalidPlacementMaterial;  // 배치 불가 셀 머티리얼 (빨간색)
    [SerializeField] private Material runtimeDefaultGridMaterial;// 기본 그리드 머티리얼

    // ========== 로컬 그리드 시스템 ==========
    /// <summary>
    /// 【데이터 저장】로컬 그리드 UI들의 부모 오브젝트
    /// 【생성 위치】CreateLocalGridParent()에서 자동 생성
    /// 【사용 위치】StartPlacementMode()에서 활성화, EndPlacementMode()에서 비활성화
    /// </summary>
    private GameObject localGridParent;
    
    /// <summary>
    /// 【데이터 저장】현재 표시된 로컬 그리드 셀 UI 오브젝트들
    /// 【업데이트 위치】GenerateLocalGridCells()에서 생성, ClearLocalGridCells()에서 제거
    /// 【내용】Plane 프리미티브로 만든 그리드 셀 오브젝트들
    /// </summary>
    private List<GameObject> localGridCells = new List<GameObject>();
    
    private Vector3 lastPlayerPosition;                    // 로컬 그리드 업데이트 최적화용
    private bool isPlacementMode = false;                  // 현재 배치 모드 여부

    // ========== 그리드 상태 관리 ==========
    /// <summary>
    /// 【데이터 저장】그리드 셀별 점유 PlacableItem 정보
    /// 【업데이트 위치】OccupyCells(추가), ReleaseCells(제거), ForceReleaseCellsForItem(강제 제거)
    /// 【참조 위치】CanPlaceItem()에서 배치 가능 여부 확인 시
    /// 【Key】Vector3Int 그리드 좌표 (x, 0, z)
    /// 【Value】해당 셀을 점유한 PlacableItem 참조
    /// </summary>
    private Dictionary<Vector3Int, PlacableItem> occupiedCells = new Dictionary<Vector3Int, PlacableItem>();
    
    /// <summary>
    /// 【상태 저장】현재 배치 중인 PlacableItem 참조
    /// 【설정 위치】SetCurrentPlacingItem()에서 배치 시작 시 설정
    /// 【사용 위치】현재는 사용되지 않지만 디버깅용으로 유지
    /// </summary>
    private PlacableItem currentPlacingItem;

    // ========== 공개 프로퍼티 ==========
    /// <summary>그리드 셀 한 장의 크기 (월드 단위)</summary>
    public float CellSize => cellSize;
    /// <summary>전체 그리드의 너비 (셀 개수)</summary>
    public int GridWidth => gridWidth;
    /// <summary>전체 그리드의 높이 (셀 개수)</summary>
    public int GridHeight => gridHeight;
    /// <summary>기본 그리드 머티리얼 참조</summary>
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
        if (isPlacementMode && localGridParent != null && localGridParent.activeInHierarchy)
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
        if (cellsToRemove.Count > 0 && isPlacementMode && localGridParent != null && localGridParent.activeInHierarchy)
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
        
        if (localGridParent != null)
        {
            localGridParent.SetActive(true);
        }
    }

    public void EndPlacementMode()
    {
        currentPlacingItem = null;
        isPlacementMode = false;
        
        if (localGridParent != null)
        {
            localGridParent.SetActive(false);
            ClearLocalGridCells();
        }
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

        if (isPlacementMode && localGridParent != null && localGridParent.activeInHierarchy)
            RefreshLocalGridCells();
    }

    void OnDestroy()
    {
        ClearLocalGridCells();
    }
}