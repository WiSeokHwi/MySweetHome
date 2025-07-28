using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// PlacableItem - VR 환경에서 그리드 기반 배치 가능한 아이템
/// 
/// == 주요 기능 ==
/// 1. 그리드 배치 시스템과의 연동 (크기, 위치 정보 제공)
/// 2. VR 인터랙션 지원 (XRGrabInteractable 통합)
/// 3. 물리 시뮬레이션 상태 관리 (배치 전/후 Rigidbody 제어)
/// 4. 배치 상태 추적 및 시각적 피드백
/// 5. 에디터 도구 (Gizmo를 통한 그리드 영역 시각화)
/// 
/// == 생명주기 ==
/// 1. 초기화 (Awake): 필수 컴포넌트 확인 및 자동 추가
/// 2. 자유 상태: 물리 시뮬레이션 활성화, VR로 잡기 가능
/// 3. 잡힌 상태: 물리 시뮬레이션 비활성화, 플레이어가 조작
/// 4. 배치 상태: 그리드에 고정, 물리 시뮬레이션 비활성화, 재배치 가능
/// 
/// == VR 특화 기능 ==
/// - XRGrabInteractable과 완전 통합
/// - 배치 전후 상태 전환 자동화
/// - Rigidbody 속성 동적 조절로 자연스러운 물리 반응
/// </summary>
public class PlacableItem : MonoBehaviour
{
    // === 그리드 시스템 연동 프로퍼티 ===
    [Header("Grid Properties")]
    [Tooltip("이 아이템이 차지할 그리드 셀의 크기 (x, z). Y축은 높이 관련이므로 무시.")]
    public Vector3Int itemGridSize = new Vector3Int(1, 1, 1);    // 그리드상 점유 크기 (예: 2x3 소파)
    
    [Header("Item Properties")]
    [Tooltip("이 아이템의 실제 월드 높이.")]
    [SerializeField] private float itemWorldHeight = 1.0f;       // 월드 공간에서의 높이 (충돌 검사용)
    
    // === 외부 접근용 프로퍼티 ===
    public float ItemWorldHeight => itemWorldHeight;             // 읽기 전용 높이 정보
    public bool IsPlaced { get; private set; } = false;          // 현재 그리드에 배치된 상태인지 여부

    // === 필수 컴포넌트 참조 ===
    private XRGrabInteractable grabInteractable;                 // VR 잡기 인터랙션 컴포넌트
    private Rigidbody itemRigidbody;                             // 물리 시뮬레이션 컴포넌트 (자동 추가)
    
    // === 내부 상태 관리 ===
    private bool isInitialized = false;                          // 초기화 완료 플래그 (안전성 검사용)

    /// <summary>
    /// Unity Awake: 컴포넌트 초기화 및 종속성 검증
    /// 
    /// 호출 순서: Awake → Start 이전에 실행
    /// 목적: 다른 스크립트에서 이 컴포넌트를 사용하기 전에 완전히 초기화
    /// </summary>
    void Awake()
    {
        InitializeComponents();
    }

    /// <summary>
    /// 필수 컴포넌트 초기화 및 설정
    /// 
    /// 초기화 단계:
    /// 1. XRGrabInteractable 확인 (VR 잡기 기능의 핵심)
    /// 2. Rigidbody 확인 및 자동 추가 (물리 시뮬레이션용)
    /// 3. 그리드 크기 유효성 검사 (잘못된 설정 방지)
    /// 4. 초기 상태 설정 (VR 인터랙션 준비)
    /// 5. 초기화 완료 플래그 설정
    /// 
    /// 실패 처리:
    /// - XRGrabInteractable 없으면 컴포넌트 비활성화
    /// - Rigidbody 없으면 자동 추가 후 경고 로그
    /// - 잘못된 그리드 크기면 기본값(1x1x1)으로 수정
    /// </summary>
    private void InitializeComponents()
    {
        // XRGrabInteractable 컴포넌트 확인
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
        {
            Debug.LogError($"PlacableItem: '{gameObject.name}'에 XRGrabInteractable 컴포넌트가 필요합니다.", this);
            // Graceful degradation: 기본 상호작용이라도 제공
            enabled = false;
            return;
        }

        // Rigidbody 컴포넌트 확인 및 추가
        itemRigidbody = GetComponent<Rigidbody>();
        if (itemRigidbody == null)
        {
            itemRigidbody = gameObject.AddComponent<Rigidbody>();
            Debug.LogWarning($"PlacableItem: '{gameObject.name}'에 Rigidbody가 없어 자동으로 추가했습니다.", this);
        }

        // 그리드 크기 유효성 검사
        ValidateGridSize();

        // 초기 상태 설정
        SetInitialState();
        isInitialized = true;
    }

    /// <summary>
    /// 그리드 크기 유효성 검사 및 자동 수정
    /// 
    /// 검사 항목:
    /// 1. 크기가 0 이하인지 확인 (최소 1x1 보장)
    /// 2. 너무 큰 크기인지 확인 (10x10 초과 시 경고)
    /// 
    /// 자동 수정:
    /// - 유효하지 않은 크기 → (1,1,1)로 강제 설정
    /// - 과도한 크기 → 경고 로그만 출력 (개발자 판단에 맡김)
    /// 
    /// 호출 시점: 초기화 및 OnValidate (에디터 값 변경 시)
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
    /// 아이템의 초기 상태 설정 (자유 상태)
    /// 
    /// 물리 설정:
    /// - Rigidbody: 물리 시뮬레이션 활성화 (중력, 충돌 반응)
    /// - isKinematic = false: 외부 힘에 반응
    /// - useGravity = true: 자연스러운 낙하
    /// 
    /// VR 인터랙션 설정:
    /// - XRGrabInteractable 활성화
    /// - 위치 추적 활성화 (손 움직임에 따라 이동)
    /// - 회전 추적 활성화 (손 회전에 따라 회전)
    /// 
    /// 목적: 아이템이 자연스럽게 잡기 가능한 상태로 준비
    /// </summary>
    private void SetInitialState()
    {
        if (itemRigidbody == null) return;

        // 초기에는 물리 시뮬레이션 활성화
        itemRigidbody.isKinematic = false;
        itemRigidbody.useGravity = true;

        // XRGrabInteractable 설정
        if (grabInteractable != null)
        {
            grabInteractable.enabled = true;
            grabInteractable.trackPosition = true;
            grabInteractable.trackRotation = true;
        }
    }

    /// <summary>
    /// 아이템의 배치 상태 설정 (핵심 상태 전환 메서드)
    /// 
    /// 상태 전환:
    /// - placed = true: 그리드에 배치된 상태로 전환
    /// - placed = false: 자유 상태로 전환 (물리 시뮬레이션 복원)
    /// 
    /// 호출자:
    /// - VRPlacementController: 배치 성공/실패 시
    /// - GridManager: 그리드 점유 해제 시
    /// 
    /// 안전성:
    /// - 초기화 확인: isInitialized가 false면 무시
    /// - 상태별 세부 설정은 SetPlacedState() / SetUnplacedState()에서 처리
    /// 
    /// 디버깅: 상태 변경 시 로그 출력
    /// </summary>
    /// <param name="placed">true: 배치 상태, false: 자유 상태</param>
    public void SetPlaced(bool placed)
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"PlacableItem: '{gameObject.name}'이 초기화되지 않았습니다.", this);
            return;
        }

        IsPlaced = placed;

        if (placed)
        {
            SetPlacedState();
        }
        else
        {
            SetUnplacedState();
        }

        Debug.Log($"PlacableItem: '{gameObject.name}' 배치 상태 변경 - IsPlaced: {IsPlaced}");
    }

    /// <summary>
    /// 배치 상태로 전환 - 그리드에 고정된 안정적인 상태
    /// 
    /// 물리 설정:
    /// - isKinematic = true: 외부 힘에 영향받지 않음
    /// - useGravity = false: 중력 무시 (공중에 떠있어도 OK)
    /// - 속도 초기화: 급작스러운 움직임 방지
    /// 
    /// VR 인터랙션 유지:
    /// - 배치된 상태에서도 다시 잡기 가능
    /// - 재배치를 위한 상호작용 활성화 유지
    /// 
    /// 사용 시나리오:
    /// - 그리드 배치 성공 후
    /// - 아이템이 정확한 위치에 고정되어야 할 때
    /// </summary>
    private void SetPlacedState()
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

        // 배치된 상태에서도 상호작용 가능하도록 유지
        if (grabInteractable != null)
        {
            grabInteractable.trackPosition = true;
            grabInteractable.trackRotation = true;
        }
    }

    /// <summary>
    /// 배치 해제 상태로 전환 - 자유로운 물리 시뮬레이션 상태
    /// 
    /// 물리 설정:
    /// - isKinematic = false: 중력과 충돌에 반응
    /// - useGravity = true: 자연스러운 낙하
    /// 
    /// VR 인터랙션 복원:
    /// - 모든 상호작용 기능 활성화
    /// - 위치/회전 추적 활성화
    /// 
    /// 사용 시나리오:
    /// - 배치 실패 시 물리적으로 떨어뜨릴 때
    /// - 배치된 아이템을 다시 잡아서 재배치할 때
    /// - 초기 상태로 복원할 때
    /// </summary>
    private void SetUnplacedState()
    {
        if (itemRigidbody == null) return;

        // 배치 해제 상태: 물리 시뮬레이션 활성화
        itemRigidbody.isKinematic = false;
        itemRigidbody.useGravity = true;

        if (grabInteractable != null)
        {
            grabInteractable.enabled = true;
            grabInteractable.trackPosition = true;
            grabInteractable.trackRotation = true;
        }
    }

    /// <summary>
    /// Rigidbody 컴포넌트 유효성 검사
    /// 
    /// 용도:
    /// - 다른 스크립트에서 물리 연산 전 안전성 확인
    /// - 초기화 실패 시 대체 로직 실행 여부 판단
    /// 
    /// 반환: Rigidbody가 존재하고 null이 아니면 true
    /// </summary>
    /// <returns>Rigidbody 컴포넌트가 유효한지 여부</returns>
    public bool HasValidRigidbody()
    {
        return itemRigidbody != null;
    }

    /// <summary>
    /// XRGrabInteractable 컴포넌트 유효성 검사
    /// 
    /// 검사 조건:
    /// - 컴포넌트가 존재하는지
    /// - 컴포넌트가 활성화되어 있는지
    /// 
    /// 용도:
    /// - VR 상호작용 가능 여부 판단
    /// - 배치 시스템에서 잡기 가능 상태 확인
    /// </summary>
    /// <returns>VR 잡기 인터랙션이 가능한지 여부</returns>
    public bool HasValidGrabInteractable()
    {
        return grabInteractable != null && grabInteractable.enabled;
    }

    /// <summary>
    /// 아이템의 현재 상태 정보를 문자열로 반환 (디버깅용)
    /// 
    /// 포함 정보:
    /// - 초기화 상태
    /// - 배치 상태 (IsPlaced)
    /// - Rigidbody 상태 (Kinematic, Gravity)
    /// - XRGrabInteractable 상태 (Enabled)
    /// 
    /// 사용처:
    /// - 디버깅 로그
    /// - 에디터 Inspector 정보 표시
    /// - 개발 중 상태 추적
    /// </summary>
    /// <returns>현재 상태를 설명하는 문자열</returns>
    public string GetStateInfo()
    {
        if (!isInitialized) return "Not Initialized";
        
        string rigidbodyInfo = itemRigidbody != null ? 
            $"RB(Kinematic: {itemRigidbody.isKinematic}, Gravity: {itemRigidbody.useGravity})" : 
            "No Rigidbody";
            
        string grabInfo = grabInteractable != null ? 
            $"Grab(Enabled: {grabInteractable.enabled})" : 
            "No GrabInteractable";
            
        return $"Placed: {IsPlaced}, {rigidbodyInfo}, {grabInfo}";
    }

    /// <summary>
    /// Unity OnValidate: 에디터에서 Inspector 값 변경 시 호출
    /// 
    /// 검증 항목:
    /// 1. 그리드 크기 유효성 (ValidateGridSize 호출)
    /// 2. 아이템 높이 유효성 (0 이하면 1.0으로 수정)
    /// 
    /// 호출 시점:
    /// - Inspector에서 값 변경 시
    /// - 스크립트 컴파일 후
    /// - 프리팹 변경 시
    /// 
    /// 목적: 개발자 실수로 인한 잘못된 설정값 자동 수정
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
    /// 
    /// 표시 내용:
    /// 1. 노란색 와이어프레임 큐브: 전체 그리드 점유 영역
    /// 2. 초록색 구체들: 각 그리드 셀의 모서리 점들
    /// 
    /// 동적 계산:
    /// - GridManager에서 실제 cellSize 가져와서 정확한 크기 표시
    /// - 아이템 위치 기준으로 그리드 영역 계산
    /// - 그리드 평면 위에 시각화 (높이 +0.05f)
    /// 
    /// 사용법: 
    /// - 씬에서 아이템 선택 시 자동 표시
    /// - 그리드 배치 설계 시 시각적 참고용
    /// </summary>
    void OnDrawGizmosSelected()
    {
        // GridManager를 찾아서 셀 크기 정보 가져오기
        var gridManager = FindObjectOfType<GridManager>();
        if (gridManager == null) return;

        float cellSize = gridManager.CellSize;
        
        Gizmos.color = Color.yellow;
        Vector3 size = new Vector3(
            itemGridSize.x * cellSize, 
            0.1f, 
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