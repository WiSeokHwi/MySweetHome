using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// GridManager - VR 환경에서 오브젝트 배치를 위한 그리드 시스템 관리자
/// 
/// == 주요 기능 ==
/// 1. 전체 그리드 생성 및 관리 (전역적 배치 영역)
/// 2. 로컬 그리드 시스템 (배치 모드에서 플레이어 주변만 표시)
/// 3. 그리드 셀 점유 상태 추적 및 관리
/// 4. 좌표 변환 (월드 ↔ 그리드 좌표)
/// 5. 배치 유효성 검사
/// 
/// == 동작 순서 ==
/// 1. Awake() → 컴포넌트 초기화 및 전체 그리드 메시 생성
/// 2. 배치 모드 진입 → 로컬 그리드 활성화, 전체 그리드 숨김
/// 3. 플레이어 이동 → 주변 그리드만 동적 생성/업데이트
/// 4. 오브젝트 배치 → 그리드 셀 점유 상태 업데이트
/// 5. 배치 모드 종료 → 로컬 그리드 정리, 전체 그리드 복원
/// </summary>
public class GridManager : MonoBehaviour
{
    // === 그리드 기본 설정 ===
    [Header("Grid Settings")]
    [Tooltip("그리드의 한 칸의 크기 (예: 1이면 1x1 유닛).")]
    [SerializeField] private float cellSize = 1.0f;           // 각 그리드 셀의 월드 크기
    [Tooltip("그리드에서 가로 칸 개수.")]
    [SerializeField] private int gridWidth = 10;              // X축 셀 개수 (전체 그리드 너비)
    [Tooltip("그리드에서 세로 칸 개수.")]
    [SerializeField] private int gridHeight = 10;             // Z축 셀 개수 (전체 그리드 높이)

    // === 배치 시각화 머티리얼 ===
    [Header("Placement Materials")]
    [Tooltip("배치 가능한 위치에서 쓸 표시용 그리드 비주얼 Material.")]
    [SerializeField] private Material previewGridMaterial;    // 배치 가능할 때 초록색 표시
    [Tooltip("배치 불가능한 위치에 표시용 그리드 비주얼 Material.")]
    [SerializeField] private Material invalidGridMaterial;    // 배치 불가능할 때 빨간색 표시
    [Tooltip("배치 모드가 아닌 때 그리드 배경에 표시용 기본 Material.")]
    [SerializeField] private Material defaultGridVisualMaterial; // 평상시 그리드 표시용

    // === 로컬 그리드 최적화 설정 ===
    [Header("Local Grid Settings")]
    [Tooltip("배치 모드에서 플레이어 주변에 표시할 그리드 반경 (미터)")]
    [SerializeField] private float localGridRadius = 5.0f;    // 플레이어 중심 5미터 반경만 표시
    [Tooltip("로컬 그리드를 업데이트할 최소 거리 (미터)")]
    [SerializeField] private float updateThreshold = 1.0f;    // 1미터 이상 움직일 때만 업데이트

    // === 전체 그리드 렌더링 컴포넌트 ===
    private MeshRenderer gridMeshRenderer;          // 전체 그리드 메시 렌더러
    private MeshFilter gridMeshFilter;              // 전체 그리드 메시 필터
    private Material runtimeDefaultGridMaterial;    // 런타임에서 사용할 기본 머티리얼
    private Material currentGridMaterial;           // 현재 적용된 머티리얼 (중복 설정 방지용)

    // === 로컬 그리드 시스템 (성능 최적화용) ===
    private GameObject localGridParent;             // 로컬 그리드 셀들의 부모 오브젝트
    private List<GameObject> localGridCells = new List<GameObject>(); // 현재 생성된 로컬 그리드 셀들
    private Vector3 lastPlayerPosition;             // 마지막 플레이어 위치 (업데이트 최적화용)
    private bool isLocalGridActive = false;         // 로컬 그리드 활성화 상태

    // === 외부 접근용 프로퍼티 ===
    public Material RuntimeDefaultGridMaterial => runtimeDefaultGridMaterial;
    public float CellSize => cellSize;
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public float LocalGridRadius => localGridRadius;

    // === 그리드 상태 관리 ===
    private Dictionary<Vector3Int, PlacableItem> occupiedCells = new Dictionary<Vector3Int, PlacableItem>(); // 점유된 셀과 아이템 매핑
    private PlacableItem currentPlacingItem;        // 현재 배치 중인 아이템 (배치 모드용)

    /// <summary>
    /// 초기화 단계 - 컴포넌트 설정 및 그리드 생성
    /// 호출 순서: InitializeComponents → ValidateMaterials → GenerateGridMesh → CreateLocalGridParent
    /// </summary>
    void Awake()
    {
        InitializeComponents();                // 1. MeshRenderer, MeshFilter 컴포넌트 확보
        ValidateMaterials();                  // 2. 머티리얼 유효성 검사 및 기본값 설정
        GenerateGridMesh();                   // 3. 전체 그리드 메시 생성
        SetGridMaterial(runtimeDefaultGridMaterial); // 4. 기본 머티리얼 적용
        CreateLocalGridParent();              // 5. 로컬 그리드 부모 오브젝트 생성
    }

    /// <summary>
    /// 로컬 그리드 시스템용 부모 오브젝트 생성
    /// 배치 모드에서 동적으로 생성되는 그리드 셀들을 관리하기 위한 컨테이너
    /// </summary>
    private void CreateLocalGridParent()
    {
        localGridParent = new GameObject("LocalGrid");
        localGridParent.transform.SetParent(transform);
        localGridParent.SetActive(false);    // 초기에는 비활성화 상태
    }

    /// <summary>
    /// 필수 컴포넌트 초기화 - MeshRenderer와 MeshFilter 확보
    /// 만약 컴포넌트가 없다면 자동으로 추가함
    /// </summary>
    private void InitializeComponents()
    {
        gridMeshRenderer = GetComponent<MeshRenderer>();
        gridMeshFilter = GetComponent<MeshFilter>();

        // 필수 컴포넌트가 없으면 자동 추가
        if (gridMeshRenderer == null)
            gridMeshRenderer = gameObject.AddComponent<MeshRenderer>();
        if (gridMeshFilter == null)
            gridMeshFilter = gameObject.AddComponent<MeshFilter>();
    }

    /// <summary>
    /// 머티리얼 유효성 검사 및 기본값 설정
    /// Inspector에서 할당하지 않은 머티리얼이 있으면 기본 URP 머티리얼로 대체
    /// </summary>
    private void ValidateMaterials()
    {
        if (defaultGridVisualMaterial == null)
        {
            Debug.LogWarning("GridManager: Default Grid Visual Material이 할당되지 않았습니다.", this);
            defaultGridVisualMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }
        
        runtimeDefaultGridMaterial = defaultGridVisualMaterial;
        currentGridMaterial = runtimeDefaultGridMaterial;
    }

    private void GenerateGridMesh()
    {
        // 기존 메시 정리 (메모리 누수 방지)
        if (gridMeshFilter.mesh != null)
        {
            if (Application.isPlaying)
                Destroy(gridMeshFilter.mesh);
            else
                DestroyImmediate(gridMeshFilter.mesh);
        }

        Mesh mesh = new Mesh()
        {
            name = "GridMesh"
        };

        // 중앙 배치를 위한 오프셋 계산
        float halfWidth = (gridWidth * cellSize) * 0.5f;
        float halfHeight = (gridHeight * cellSize) * 0.5f;

        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-halfWidth, 0, -halfHeight), // 왼쪽하단
            new Vector3(halfWidth, 0, -halfHeight),  // 오른쪽하단
            new Vector3(-halfWidth, 0, halfHeight),  // 왼쪽상단
            new Vector3(halfWidth, 0, halfHeight)    // 오른쪽상단
        };

        int[] triangles = new int[] { 0, 2, 1, 1, 2, 3 };
        
        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(gridWidth, 0),
            new Vector2(0, gridHeight),
            new Vector2(gridWidth, gridHeight)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();

        gridMeshFilter.mesh = mesh;
        
        // 머티리얼 텍스처 스케일 설정
        SetMaterialTextureScale();
    }

    private void SetMaterialTextureScale()
    {
        Vector2 textureScale = new Vector2(gridWidth, gridHeight);
        
        if (previewGridMaterial != null)
            previewGridMaterial.mainTextureScale = textureScale;
        if (invalidGridMaterial != null)
            invalidGridMaterial.mainTextureScale = textureScale;
        if (defaultGridVisualMaterial != null)
            defaultGridVisualMaterial.mainTextureScale = textureScale;
    }

    /// <summary>
    /// 월드 좌표를 그리드에 스냅된 배치 위치로 변환
    /// VR에서 레이캐스트로 얻은 히트 포인트를 가장 가까운 그리드 위치로 스냅
    /// 
    /// 작동 원리:
    /// 1. 월드 좌표 → 그리드 중심 기준 상대 좌표로 변환
    /// 2. 셀 크기 단위로 Floor 스냅 (왼쪽 아래 모서리 기준)
    /// 3. 아이템 크기를 고려한 중심점 오프셋 적용
    /// 4. 다시 월드 좌표로 변환하여 반환
    /// </summary>
    /// <param name="hitPoint">VR 레이캐스트 히트 포인트</param>
    /// <param name="itemSize">배치할 아이템의 그리드 크기</param>
    /// <param name="itemWorldHeight">아이템의 월드 높이 (현재 미사용)</param>
    /// <returns>그리드에 스냅된 최종 배치 위치</returns>
    public Vector3 SnapToGridForPlacement(Vector3 hitPoint, Vector3Int itemSize, float itemWorldHeight)
    {
        Vector3 relativeHitPos = hitPoint - transform.position;

        // 그리드 중앙을 원점으로 좌표 변환
        float halfWidth = (gridWidth * cellSize) * 0.5f;
        float halfHeight = (gridHeight * cellSize) * 0.5f;

        relativeHitPos.x += halfWidth;   // 중앙 기준 → 왼쪽 하단 기준으로 변환
        relativeHitPos.z += halfHeight;

        // 그리드 셀로 스냅 (Floor 함수로 왼쪽 아래로 스냅)
        float snappedRelativeX = Mathf.Floor(relativeHitPos.x / cellSize) * cellSize;
        float snappedRelativeZ = Mathf.Floor(relativeHitPos.z / cellSize) * cellSize;

        // 아이템 크기에 따른 중심 오프셋 계산
        // 예: 2x2 아이템이면 0.5 셀만큼 오프셋하여 중앙에 배치
        Vector3 itemCenterOffset = new Vector3(
            (itemSize.x - 1) * 0.5f * cellSize,
            0,
            (itemSize.z - 1) * 0.5f * cellSize
        );

        // 최종 월드 위치 계산
        Vector3 finalWorldPosition = transform.position +
            new Vector3(snappedRelativeX - halfWidth, transform.position.y, snappedRelativeZ - halfHeight) +
            new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f) +  // 셀 중심으로 이동
            itemCenterOffset;

        return finalWorldPosition;
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
            Mathf.FloorToInt(relativePos.y / cellSize),
            Mathf.FloorToInt(relativePos.z / cellSize)
        );
    }

    public Vector3 GridToWorldCoordinates(Vector3Int gridCoordinates)
    {
        float halfWidth = (gridWidth * cellSize) * 0.5f;
        float halfHeight = (gridHeight * cellSize) * 0.5f;

        return new Vector3(
            gridCoordinates.x * cellSize + transform.position.x - halfWidth + cellSize * 0.5f,
            gridCoordinates.y * cellSize + transform.position.y,
            gridCoordinates.z * cellSize + transform.position.z - halfHeight + cellSize * 0.5f
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
        if (item == null)
        {
            Debug.LogError("[GridManager] Cannot occupy cells - item is null.");
            return;
        }

        Vector3Int itemGridOrigin = WorldToGridCoordinates(item.transform.position);
        Vector3Int itemSize = item.itemGridSize;

        // 전체 영역이 그리드 범위 내에 있는지 확인
        if (!ValidateItemPlacement(itemGridOrigin, itemSize, item.name))
            return;

        // 셀 점유 실행
        for (int x = 0; x < itemSize.x; x++)
        {
            for (int z = 0; z < itemSize.z; z++)
            {
                Vector3Int cellToOccupy = new Vector3Int(itemGridOrigin.x + x, itemGridOrigin.y, itemGridOrigin.z + z);
                occupiedCells[cellToOccupy] = item;
            }
        }

        Debug.Log($"[GridManager] Item '{item.name}' occupies {itemSize.x * itemSize.z} cells from {itemGridOrigin}");
    }

    private bool ValidateItemPlacement(Vector3Int itemGridOrigin, Vector3Int itemSize, string itemName)
    {
        if (!IsInGridBounds(itemGridOrigin))
        {
            Debug.LogError($"[GridManager] Item origin {itemGridOrigin} is outside grid bounds for '{itemName}'.");
            return false;
        }

        // 아이템이 차지할 모든 셀 검증
        for (int x = 0; x < itemSize.x; x++)
        {
            for (int z = 0; z < itemSize.z; z++)
            {
                Vector3Int cellToCheck = new Vector3Int(itemGridOrigin.x + x, itemGridOrigin.y, itemGridOrigin.z + z);
                if (!IsInGridBounds(cellToCheck))
                {
                    Debug.LogError($"[GridManager] Cell {cellToCheck} is outside grid bounds for '{itemName}'.");
                    return false;
                }
            }
        }
        
        return true;
    }

    public void ReleaseCells(PlacableItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("[GridManager] Cannot release cells - item is null.");
            return;
        }

        var cellsToRemove = new List<Vector3Int>();
        foreach (var entry in occupiedCells)
        {
            if (entry.Value == item)
                cellsToRemove.Add(entry.Key);
        }

        foreach (Vector3Int cell in cellsToRemove)
        {
            occupiedCells.Remove(cell);
        }

        if (cellsToRemove.Count > 0)
            Debug.Log($"[GridManager] Released {cellsToRemove.Count} cells for '{item.name}'");
    }

    public void SetCurrentPlacingItem(PlacableItem item)
    {
        currentPlacingItem = item;
        SetGridMaterial(previewGridMaterial);
        
        // 로컬 그리드 활성화
        EnableLocalGrid(true);
    }

    public void ClearCurrentPlacingItem()
    {
        currentPlacingItem = null;
        SetGridMaterial(runtimeDefaultGridMaterial);
        
        // 로컬 그리드 비활성화
        EnableLocalGrid(false);
    }

    public void SetGridMaterial(Material material)
    {
        if (gridMeshRenderer == null)
        {
            Debug.LogError("GridManager: MeshRenderer is null. Cannot set material.");
            return;
        }

        if (material == null)
        {
            Debug.LogWarning("GridManager: Material is null. Using default material.");
            material = runtimeDefaultGridMaterial ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }

        // 중복 설정 방지
        if (currentGridMaterial != material)
        {
            gridMeshRenderer.material = material;
            currentGridMaterial = material;
        }
    }

    public void UpdateGridMaterialForPlacement(bool isValidPlacement)
    {
        if (currentPlacingItem != null)
        {
            SetGridMaterial(isValidPlacement ? previewGridMaterial : invalidGridMaterial);
        }
    }

    /// <summary>
    /// 로컬 그리드 시스템 활성화/비활성화
    /// 
    /// VR 성능 최적화를 위해 배치 모드에서만 플레이어 주변 그리드를 표시
    /// - 활성화: 전체 그리드 숨김 → 로컬 그리드 표시 준비
    /// - 비활성화: 로컬 그리드 정리 → 전체 그리드 복원
    /// </summary>
    /// <param name="enable">true: 로컬 그리드 활성화, false: 전체 그리드 복원</param>
    public void EnableLocalGrid(bool enable)
    {
        isLocalGridActive = enable;
        
        if (enable)
        {
            // 전체 그리드 숨기기 (성능 최적화)
            if (gridMeshRenderer != null)
                gridMeshRenderer.enabled = false;
            
            // 로컬 그리드 부모 활성화 (동적 셀 생성 준비)
            if (localGridParent != null)
                localGridParent.SetActive(true);
        }
        else
        {
            // 전체 그리드 다시 보이기 (평상시 상태 복원)
            if (gridMeshRenderer != null)
                gridMeshRenderer.enabled = true;
            
            // 로컬 그리드 비활성화 및 정리
            if (localGridParent != null)
                localGridParent.SetActive(false);
                
            ClearLocalGridCells();  // 메모리 정리
        }
    }

    public void UpdateLocalGrid(Vector3 playerPosition)
    {
        if (!isLocalGridActive) return;

        // 플레이어가 충분히 움직였는지 확인
        if (Vector3.Distance(playerPosition, lastPlayerPosition) < updateThreshold && localGridCells.Count > 0)
            return;

        lastPlayerPosition = playerPosition;
        
        // 기존 로컬 그리드 셀들 정리
        ClearLocalGridCells();
        
        // 플레이어 주변의 그리드 셀들 생성
        GenerateLocalGridCells(playerPosition);
    }

    private void ClearLocalGridCells()
    {
        foreach (GameObject cell in localGridCells)
        {
            if (cell != null)
            {
                if (Application.isPlaying)
                    Destroy(cell);
                else
                    DestroyImmediate(cell);
            }
        }
        localGridCells.Clear();
    }

    private void GenerateLocalGridCells(Vector3 centerPosition)
    {
        // 플레이어 위치를 그리드 좌표로 변환
        Vector3Int centerGridPos = WorldToGridCoordinates(centerPosition);
        
        // 반경 내의 셀 개수 계산
        int cellRadius = Mathf.CeilToInt(localGridRadius / cellSize);
        
        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int z = -cellRadius; z <= cellRadius; z++)
            {
                Vector3Int gridPos = new Vector3Int(centerGridPos.x + x, 0, centerGridPos.z + z);
                
                // 그리드 범위 내에 있는지 확인
                if (!IsInGridBounds(gridPos)) continue;
                
                // 실제 거리 확인 (원형 범위)
                Vector3 worldPos = GridToWorldCoordinates(gridPos);
                float distance = Vector3.Distance(new Vector3(centerPosition.x, worldPos.y, centerPosition.z), worldPos);
                if (distance > localGridRadius) continue;
                
                // 그리드 셀 생성
                CreateGridCell(gridPos, worldPos);
            }
        }
    }

    private void CreateGridCell(Vector3Int gridPos, Vector3 worldPos)
    {
        GameObject cellObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
        cellObject.name = $"GridCell_{gridPos.x}_{gridPos.z}";
        cellObject.transform.SetParent(localGridParent.transform);
        
        // 위치와 크기 설정
        cellObject.transform.position = worldPos;
        cellObject.transform.localScale = new Vector3(cellSize * 0.1f, 1f, cellSize * 0.1f);
        
        // 머티리얼 설정
        var renderer = cellObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            // 점유된 셀인지 확인
            bool isOccupied = occupiedCells.ContainsKey(gridPos);
            Material materialToUse = isOccupied ? invalidGridMaterial : previewGridMaterial;
            renderer.material = materialToUse ?? runtimeDefaultGridMaterial;
            
            // 반투명하게 설정
            if (renderer.material.HasProperty("_Color"))
            {
                Color color = renderer.material.color;
                color.a = 0.3f;
                renderer.material.color = color;
            }
        }
        
        // 콜라이더 제거 (시각적 목적만)
        var collider = cellObject.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);
        }
        
        localGridCells.Add(cellObject);
    }

    void OnDrawGizmos()
    {
        // 에디터와 플레이 모드 모두에서 그리드 표시
        Gizmos.color = Color.blue;
        Vector3 origin = transform.position;
        Vector3 size = new Vector3(gridWidth * cellSize, 0.1f, gridHeight * cellSize);
        Gizmos.DrawWireCube(origin, size);

        // 플레이 모드일 때만 점유된 셀 표시
        if (Application.isPlaying && occupiedCells != null)
        {
            Gizmos.color = Color.red;
            foreach (var entry in occupiedCells)
            {
                Vector3 cellWorldPos = GridToWorldCoordinates(entry.Key);
                Gizmos.DrawWireCube(cellWorldPos, new Vector3(cellSize, 0.1f, cellSize));
            }
        }
        
        // 에디터에서 그리드 셀 구분선 표시
        if (!Application.isPlaying)
        {
            DrawGridLines();
        }
    }

    private void DrawGridLines()
    {
        Gizmos.color = Color.gray;
        float halfWidth = (gridWidth * cellSize) * 0.5f;
        float halfHeight = (gridHeight * cellSize) * 0.5f;
        
        // 세로선
        for (int i = 0; i <= gridWidth; i++)
        {
            Vector3 start = transform.position + new Vector3(-halfWidth + i * cellSize, 0, -halfHeight);
            Vector3 end = transform.position + new Vector3(-halfWidth + i * cellSize, 0, halfHeight);
            Gizmos.DrawLine(start, end);
        }
        
        // 가로선
        for (int i = 0; i <= gridHeight; i++)
        {
            Vector3 start = transform.position + new Vector3(-halfWidth, 0, -halfHeight + i * cellSize);
            Vector3 end = transform.position + new Vector3(halfWidth, 0, -halfHeight + i * cellSize);
            Gizmos.DrawLine(start, end);
        }
    }

    void OnDestroy()
    {
        // 메모리 정리
        if (gridMeshFilter != null && gridMeshFilter.mesh != null)
        {
            if (Application.isPlaying)
                Destroy(gridMeshFilter.mesh);
            else
                DestroyImmediate(gridMeshFilter.mesh);
        }
        
        // 로컬 그리드 정리
        ClearLocalGridCells();
        
        if (localGridParent != null)
        {
            if (Application.isPlaying)
                Destroy(localGridParent);
            else
                DestroyImmediate(localGridParent);
        }
    }
}