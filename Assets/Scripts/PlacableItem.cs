using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// PlacableItem - VR 환경에서 그리드 기반 배치 가능한 아이템
/// GrabbableItem을 상속받아 잡기 기능을 재사용하고, 그리드 배치 기능을 특화합니다.
///
/// == 주요 기능 ==
/// 1. 그리드 배치 시스템과의 연동 (크기, 위치 정보 제공)
/// 2. 배치 상태 추적 및 시각적 피드백
/// 3. 배치 전/후 Rigidbody 물리 시뮬레이션 상태 제어
/// 4. 에디터 도구 (Gizmo를 통한 그리드 영역 시각화)
/// </summary>
// PlacableItem은 이제 GrabbableItem을 상속받으므로,
// XRGrabInteractable과 Rigidbody는 GrabbableItem에서 RequireComponent로 관리됩니다.
public class PlacableItem : GrabbableItem // GrabbableItem 상속
{
    // === 그리드 시스템 연동 프로퍼티 ===
    [Header("Grid Properties")]
    [Tooltip("이 아이템이 차지할 그리드 셀의 크기 (x, z). Y축은 높이 관련이므로 무시.")]
    public Vector3Int itemGridSize = new Vector3Int(1, 1, 1); // 그리드상 점유 크기 (예: 2x3 소파)

    [Header("Item Properties")]
    [Tooltip("이 아이템의 실제 월드 높이.")]
    [SerializeField] private float itemWorldHeight = 1.0f; // 월드 공간에서의 높이 (충돌 검사용)

    // === 외부 접근용 프로퍼티 ===
    public float ItemWorldHeight => itemWorldHeight; // 읽기 전용 높이 정보
    public bool IsPlaced { get; private set; } = false; // 현재 그리드에 배치된 상태인지 여부

    /// <summary>
    /// Unity Awake: 컴포넌트 초기화 및 종속성 검증
    /// </summary>
    protected override void Awake() // virtual Awake() 오버라이드
    {
        base.Awake(); // GrabbableItem의 Awake 호출 (XRGrabInteractable, Rigidbody 초기화)

        // 그리드 크기 유효성 검사
        ValidateGridSize();

        // PlacableItem은 초기에는 자유 상태 (물리 시뮬레이션 활성화)로 시작합니다.
        // 이 부분은 GrabbableItem의 SetPhysicsForUnGrabbed()에서 이미 처리됩니다.
    }

    /// <summary>
    /// 아이템이 잡혔을 때 호출됩니다. (GrabbableItem의 OnGrabStarted 오버라이드)
    /// 배치된 상태에서 잡혔다면 배치 상태를 해제합니다.
    /// </summary>
    protected override void OnGrabStarted(SelectEnterEventArgs args)
    {
        base.OnGrabStarted(args); // GrabbableItem의 OnGrabStarted 호출 (물리 속성 변경)

        // 만약 배치된 상태에서 다시 잡혔다면, 배치 상태를 해제합니다.
        if (IsPlaced)
        {
            SetPlaced(false); // 배치 해제
        }
    }

    /// <summary>
    /// 아이템이 놓였을 때 호출됩니다. (GrabbableItem의 OnGrabEnded 오버라이드)
    /// PlacableItem은 놓인 후 배치 시스템에 의해 배치될 수 있습니다.
    /// 여기서는 기본 물리 상태로 돌아가도록만 처리하고, 실제 배치는 외부 시스템(VRPlacementController)에서 담당합니다.
    /// </summary>
    protected override void OnGrabEnded(SelectExitEventArgs args)
    {
        base.OnGrabEnded(args); // GrabbableItem의 OnGrabEnded 호출 (물리 속성 복원)

        // 아이템이 놓였을 때, 배치 시스템이 이 아이템을 배치할지 결정합니다.
        // IsPlaced 상태는 VRPlacementController와 같은 외부 스크립트에서만 변경되어야 합니다.
        // 여기서 IsPlaced를 false로 설정하면, 배치된 아이템을 잡았다 놓았을 때
        // 다시 자유 상태가 됩니다.
        IsPlaced = false; // 놓이면 배치 상태는 일단 해제 (재배치 대기)
        Debug.Log($"PlacableItem: '{gameObject.name}'이 놓였습니다. 배치 상태 해제됨.");
    }

    /// <summary>
    /// 그리드 크기 유효성 검사 및 자동 수정
    /// </summary>
    private void ValidateGridSize()
    {
        // 그리드 크기가 유효한지 확인
        if (itemGridSize.x <= 0 || itemGridSize.z <= 0)
        {
            Debug.LogWarning($"PlacableItem: '{gameObject.name}'의 itemGridSize가 유효하지 않습니다. (1,1,1)로 설정합니다.", this);
            itemGridSize = new Vector3Int(1, 1, 1);
        }

        // 너무 큰 아이템 경고
        if (itemGridSize.x > 10 || itemGridSize.z > 10)
        {
            Debug.LogWarning($"PlacableItem: '{gameObject.name}'의 크기가 매우 큽니다 ({itemGridSize.x}x{itemGridSize.z}). 그리드 범위를 벗어날 수 있습니다.", this);
        }
    }

    /// <summary>
    /// 아이템의 배치 상태 설정 (핵심 상태 전환 메서드)
    /// 이 메서드는 주로 VRPlacementController에서 호출됩니다.
    /// </summary>
    /// <param name="placed">true: 배치 상태, false: 자유 상태</param>
    public void SetPlaced(bool placed)
    {
        IsPlaced = placed;

        if (placed)
        {
            SetPhysicsForPlaced();
        }
        else
        {
            // 배치 해제 시, 현재 잡혀있지 않다면 물리 시뮬레이션 활성화
            if (!isGrabbed) // GrabbableItem의 isGrabbed 필드 사용
            {
                SetPhysicsForUnGrabbed(); // GrabbableItem의 메서드 호출
            }
            // 잡혀있는 상태라면 SetPhysicsForGrabbed() 상태를 유지 (컨트롤러에 따라 움직임)
        }

        Debug.Log($"PlacableItem: '{gameObject.name}' 배치 상태 변경 - IsPlaced: {IsPlaced}");
    }

    /// <summary>
    /// 배치 상태로 전환 - 그리드에 고정된 안정적인 상태
    /// (물리 시뮬레이션 비활성화)
    /// </summary>
    private void SetPhysicsForPlaced()
    {
        if (itemRigidbody == null) return;

        // 배치 상태: 물리 시뮬레이션 비활성화
        if (!itemRigidbody.isKinematic)
        {
            // Kinematic으로 변경하기 전에 속도 제거
            itemRigidbody.linearVelocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
        }

        itemRigidbody.isKinematic = true;
        itemRigidbody.useGravity = false;
    }

    /// <summary>
    /// 아이템의 현재 상태 정보를 문자열로 반환 (디버깅용)
    /// </summary>
    public string GetStateInfo()
    {
        string rigidbodyInfo = itemRigidbody != null ?
            $"RB(Kinematic: {itemRigidbody.isKinematic}, Gravity: {itemRigidbody.useGravity})" :
            "No Rigidbody";

        string grabInfo = grabInteractable != null ?
            $"Grab(Enabled: {grabInteractable.enabled}, Grabbed: {isGrabbed})" : // isGrabbed 정보 추가
            "No GrabInteractable";

        return $"Placed: {IsPlaced}, {rigidbodyInfo}, {grabInfo}";
    }

    /// <summary>
    /// Unity OnValidate: 에디터에서 Inspector 값 변경 시 호출
    /// </summary>
    void OnValidate()
    {
        // 에디터에서 값 변경 시 유효성 검사
        ValidateGridSize();

        if (itemWorldHeight <= 0)
        {
            itemWorldHeight = 1.0f;
            Debug.LogWarning($"PlacableItem: '{gameObject.name}'의 itemWorldHeight가 0 이하입니다. 1.0으로 설정합니다.");
        }
    }

    /// <summary>
    /// Unity Gizmo: 아이템이 차지할 그리드 영역을 에디터에서 시각화
    /// </summary>
    void OnDrawGizmosSelected()
    {
        // GridManager를 찾아서 셀 크기 정보 가져오기
        var gridManager = FindAnyObjectByType<GridManager>();
        if (gridManager == null) return;

        float cellSize = gridManager.CellSize;

        Gizmos.color = Color.yellow;
        Vector3 size = new Vector3(
            itemGridSize.x * cellSize,
            0.1f, // 그리드 평면 높이
            itemGridSize.z * cellSize
        );

        // 아이템 위치를 기준으로 그리드 영역 표시
        Vector3 center = transform.position;
        center.y = gridManager.transform.position.y + 0.05f; // 그리드 위에 표시

        Gizmos.DrawWireCube(center, size);

        // 각 셀 경계 표시
        Gizmos.color = Color.green;
        for (int x = 0; x <= itemGridSize.x; x++)
        {
            for (int z = 0; z <= itemGridSize.z; z++)
            {
                Vector3 cornerPos = center - size * 0.5f + new Vector3(x * cellSize, 0, z * cellSize);
                Gizmos.DrawWireSphere(cornerPos, 0.1f);
            }
        }
    }
}
