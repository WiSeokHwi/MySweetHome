using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;

/// <summary>
/// ==================== GRABBABLE ITEM SYSTEM ====================
/// 
/// 【 시스템 개요 】
/// VR 환경에서 플레이어가 손으로 잡을 수 있는 모든 오브젝트의 기본 클래스입니다.
/// Unity XR Interaction Toolkit을 기반으로 하며, 모든 상호작용 가능한 아이템의 토대가 됩니다.
/// 
/// 【 상속 구조 】
/// GrabbableItem (기본 클래스)
/// ├── ToolItem (도구류: 괭이, 물뿌리개 등)
/// ├── PickableItem (수집 아이템: 씨앗, 재료 등)
/// └── PlacableItem (배치 아이템: 가구, 건물 등)
/// 
/// 【 주요 책임 】
/// 1. VR 잡기 상호작용 (XRGrabInteractable 통합 관리)
/// 2. 물리 시뮬레이션 제어 (잡힌 상태/놓인 상태 전환)
/// 3. 인벤토리 드롭존 감지 및 추가 로직
/// 4. 입력 이벤트 추상화 (하위 클래스에서 구현)
/// 
/// 【 연동 시스템 】
/// - EquipState: 현재 플레이어가 들고 있는 아이템 추적
/// - InventoryManager: 인벤토리 추가/제거 처리
/// - InventoryDropZone: 드롭존 진입/탈출 감지
/// 
/// 【 데이터 저장 위치 】
/// - isGrabbed: 현재 잡힌 상태 (이 클래스에서 관리)
/// - currentDropZones: 현재 감지된 드롭존들 (이 클래스에서 관리)
/// - grabInteractable, itemRigidbody: Unity 컴포넌트 참조 (이 클래스에서 캐싱)
/// 
/// 【 주요 호출 흐름 】
/// 1. 플레이어가 아이템 잡기 → OnGrabStarted() 호출 → SetPhysicsForGrabbed()
/// 2. 플레이어가 아이템 놓기 → OnGrabEnded() 호출 → SetPhysicsForUnGrabbed()
/// 3. 드롭존 진입 → InventoryDropZone에서 OnEnteredDropZone() 호출
/// 4. 인벤토리 입력 → EquipState에서 OnInventoryAddInput() 호출
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))] // VR에서 잡을 수 있도록 XRGrabInteractable 필수
[RequireComponent(typeof(Rigidbody))] // 물리 동작을 위해 Rigidbody 필수
public abstract class GrabbableItem : MonoBehaviour
{
    // ========== 컴포넌트 참조 (Awake에서 자동 초기화) ==========
    /// <summary>Unity XR Interaction Toolkit의 잡기 컴포넌트 - Awake()에서 GetComponent로 자동 할당</summary>
    protected XRGrabInteractable grabInteractable;
    
    /// <summary>Unity 물리 시스템 컴포넌트 - Awake()에서 GetComponent로 자동 할당</summary>
    protected Rigidbody itemRigidbody;

    // ========== 상태 관리 ==========
    /// <summary>현재 플레이어가 이 아이템을 잡고 있는지 여부 - OnGrabStarted/OnGrabEnded에서 업데이트</summary>
    protected bool isGrabbed = false;

    /// <summary>현재 이 아이템이 들어가 있는 드롭존들 - InventoryDropZone에서 OnEnteredDropZone/OnExitedDropZone 호출 시 업데이트</summary>
    private HashSet<InventoryDropZone> currentDropZones = new HashSet<InventoryDropZone>();

    // ========== 외부 접근용 프로퍼티 ==========
    /// <summary>
    /// 【호출 위치】ToolTriggerHandler.cs, VRPlacementController.cs 등에서 호출
    /// 【용도】현재 아이템이 잡힌 상태인지 확인하여 상호작용 가능 여부 판단
    /// </summary>
    public bool IsGrabbed => isGrabbed;

    // ==========================================================================
    // UNITY 생명주기 메서드들
    // ==========================================================================
    
    /// <summary>
    /// 【Unity 생명주기】GameObject가 생성될 때 최초 1회 호출
    /// 【처리 내용】필수 컴포넌트들(XRGrabInteractable, Rigidbody) 자동 참조 및 초기화
    /// 【호출 시점】Start() 이전, OnEnable() 이전
    /// </summary>
    protected virtual void Awake()
    {
        InitializeComponents();
    }

    /// <summary>
    /// 【Unity 생명주기】GameObject가 활성화될 때마다 호출
    /// 【처리 내용】XRGrabInteractable 이벤트 리스너 등록
    /// 【연동 이벤트】selectEntered(잡기), selectExited(놓기)
    /// </summary>
    protected virtual void OnEnable()
    {
        if (grabInteractable != null)
        {
            // 플레이어가 아이템을 잡을 때 OnGrabStarted 호출
            grabInteractable.selectEntered.AddListener(OnGrabStarted);
            // 플레이어가 아이템을 놓을 때 OnGrabEnded 호출
            grabInteractable.selectExited.AddListener(OnGrabEnded);
        }
    }

    /// <summary>
    /// 【Unity 생명주기】GameObject가 비활성화될 때마다 호출
    /// 【처리 내용】등록된 이벤트 리스너들 제거 (메모리 누수 방지)
    /// </summary>
    protected virtual void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabStarted);
            grabInteractable.selectExited.RemoveListener(OnGrabEnded);
        }
    }

    // ==========================================================================
    // 컴포넌트 초기화 메서드들
    // ==========================================================================
    
    /// <summary>
    /// 【호출 위치】Awake()에서 호출
    /// 【처리 내용】RequireComponent로 지정된 필수 컴포넌트들 참조 및 초기 설정
    /// 【에러 처리】컴포넌트가 없으면 자동 추가 또는 스크립트 비활성화
    /// </summary>
    private void InitializeComponents()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
        {
            Debug.LogError($"GrabbableItem: '{gameObject.name}'에 XRGrabInteractable 컴포넌트가 필요합니다.", this);
            enabled = false; // 필수 컴포넌트가 없으므로 스크립트 비활성화
            return;
        }

        itemRigidbody = GetComponent<Rigidbody>();
        if (itemRigidbody == null)
        {
            itemRigidbody = gameObject.AddComponent<Rigidbody>();
            Debug.LogWarning($"GrabbableItem: '{gameObject.name}'에 Rigidbody가 없어 자동으로 추가했습니다.", this);
        }

        // 초기 상태 설정 (잡히지 않은 상태의 물리 옵션 적용)
        SetPhysicsForUnGrabbed();
    }

    // ==========================================================================
    // VR 상호작용 이벤트 처리기들 
    // ==========================================================================
    
    /// <summary>
    /// 【호출 위치】XRGrabInteractable.selectEntered 이벤트에서 자동 호출
    /// 【트리거】플레이어가 VR 컴트롤러로 이 아이템을 잡을 때
    /// 【처리 내용】잡힌 상태로 전환, 물리 속성 변경 (Kinematic 모드)
    /// 【연동 호출】EquipState에서 이 이벤트를 감지하여 equippedItems 업데이트
    /// </summary>
    protected virtual void OnGrabStarted(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        SetPhysicsForGrabbed();
        Debug.Log($"GrabbableItem: '{gameObject.name}'을(를) 잡았습니다.");
    }

    /// <summary>
    /// 【호출 위치】XRGrabInteractable.selectExited 이벤트에서 자동 호출
    /// 【트리거】플레이어가 VR 컴트롤러에서 손을 떼거나 다른 아이템을 잡을 때
    /// 【처리 내용】놓인 상태로 전환, 물리 속성 복원 (중력 영향 받음)
    /// 【연동 호출】EquipState에서 이 이벤트를 감지하여 equippedItems 업데이트
    /// </summary>
    protected virtual void OnGrabEnded(SelectExitEventArgs args)
    {
        isGrabbed = false;
        SetPhysicsForUnGrabbed();
        Debug.Log($"GrabbableItem: '{gameObject.name}'을(를) 놓았습니다.");
    }

    // ==========================================================================
    // 물리 시스템 제어 메서드들
    // ==========================================================================
    
    /// <summary>
    /// 【호출 위치】OnGrabStarted()에서 호출
    /// 【처리 내용】아이템을 잡았을 때의 물리 속성 설정
    /// 【주요 변경】
    /// - isKinematic = true: 물리 엔진에 의한 움직임 비활성화 (플레이어가 제어)
    /// - useGravity = false: 중력 영향 비활성화 (떨어지지 않음)
    /// - 속도 초기화: 기존 가속도 제거
    /// </summary>
    protected virtual void SetPhysicsForGrabbed()
    {
        if (itemRigidbody == null) return;

        itemRigidbody.isKinematic = true;
        itemRigidbody.useGravity = false;
        itemRigidbody.linearVelocity = Vector3.zero;
        itemRigidbody.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// 【호출 위치】OnGrabEnded()에서 호출
    /// 【처리 내용】아이템을 놓았을 때의 물리 속성 복원
    /// 【주요 변경】
    /// - isKinematic = false: 물리 엔진이 다시 오브젝트를 제어
    /// - useGravity = true: 중력 영향 활성화 (자연스럽게 떨어짐)
    /// 【결과】아이템이 다른 콜라이더와 충돌하고 중력의 영향을 받음
    /// </summary>
    protected virtual void SetPhysicsForUnGrabbed()
    {
        if (itemRigidbody == null) return;

        itemRigidbody.isKinematic = false;
        itemRigidbody.useGravity = true;
    }

    /// <summary>
    /// Rigidbody 컴포넌트 유효성 확인
    /// </summary>
    public bool HasValidRigidbody()
    {
        return itemRigidbody != null;
    }

    /// <summary>
    /// XRGrabInteractable 컴포넌트 유효성 확인
    /// </summary>
    public bool HasValidGrabInteractable()
    {
        return grabInteractable != null && grabInteractable.enabled;
    }

    /// <summary>
    /// 이 아이템의 ItemData를 가져옵니다. 하위 클래스에서 구현해야 합니다.
    /// </summary>
    public abstract ItemData GetItemData();

    /// <summary>
    /// 이 아이템의 수량을 가져옵니다. 하위 클래스에서 구현해야 합니다.
    /// </summary>
    public abstract int GetQuantity();

    // ==========================================================================
    // 인벤토리 시스템 연동 메서드들
    // ==========================================================================
    
    /// <summary>
    /// 【호출 위치】EquipState.OnInventoryAddPerformed()에서 호출
    /// 【트리거】플레이어가 인벤토리 추가 버튼 누름 (예: 반대 손 트리거)
    /// 【처리 내용】드롭존 내부에 있을 때만 인벤토리에 아이템 추가 시도
    /// 【보안 기능】드롭존 외부에서는 인벤토리 추가 불가능 (오류 방지)
    /// </summary>
    public virtual void OnInventoryAddInput()
    {
        if (currentDropZones.Count > 0)
        {
            TryAddToInventory();
        }
        else
        {
            Debug.Log($"[GrabbableItem] {gameObject.name}: 드롭존 내부에 있지 않아 인벤토리에 추가할 수 없습니다.");
        }
    }

    /// <summary>
    /// 【호출 위치】EquipState.OnToolUsePerformed()에서 호출
    /// 【트리거】플레이어가 도구 사용 버튼 누름 (예: 트리거 버튼)
    /// 【구현 필수】하위 클래스에서 반드시 구현해야 하는 추상 메서드
    /// 【구현 예시】
    /// - ToolItem: 도구 사용 로직 (UseTool() 호출)
    /// - PickableItem: 보통 아무 동작 안 함 (Debug 로그만 출력)
    /// - PlacableItem: 배치 불가능 메시지 출력
    /// </summary>
    public abstract void OnToolUseInput();

    /// <summary>
    /// 【호출 위치】InventoryDropZone.OnTriggerEnter()에서 호출
    /// 【트리거】이 아이템의 Collider가 InventoryDropZone의 Trigger Collider와 충돌
    /// 【처리 내용】currentDropZones HashSet에 드롭존 추가
    /// 【사용】OnInventoryAddInput()에서 드롭존 내부 여부 확인용
    /// </summary>
    public virtual void OnEnteredDropZone(InventoryDropZone dropZone)
    {
        currentDropZones.Add(dropZone);
        Debug.Log($"GrabbableItem: '{gameObject.name}'이(가) 드롭존에 진입했습니다.");
    }

    /// <summary>
    /// 【호출 위치】InventoryDropZone.OnTriggerExit()에서 호출
    /// 【트리거】이 아이템의 Collider가 InventoryDropZone의 Trigger Collider에서 벗어남
    /// 【처리 내용】currentDropZones HashSet에서 드롭존 제거
    /// </summary>
    public virtual void OnExitedDropZone(InventoryDropZone dropZone)
    {
        currentDropZones.Remove(dropZone);
        Debug.Log($"GrabbableItem: '{gameObject.name}'이(가) 드롭존에서 나갔습니다.");
    }

    /// <summary>
    /// 【호출 위치】OnInventoryAddInput()에서 호출
    /// 【처리 내용】실제 인벤토리 추가 로직 수행
    /// 【처리 순서】
    /// 1. 하위 클래스에서 ItemData 및 수량 정보 수집
    /// 2. InventoryManager.AddItemToInventory() 호출
    /// 3. 성공 시 GameObject.Destroy() 호출로 월드에서 제거
    /// 【에러 처리】ItemData 또는 인벤토리 공간 부족 시 경고 에리
    /// </summary>
    protected virtual void TryAddToInventory()
    {
        ItemData itemData = GetItemData();
        int quantity = GetQuantity();

        if (itemData == null)
        {
            Debug.LogWarning($"[GrabbableItem] {gameObject.name}에 유효한 ItemData가 없습니다.");
            return;
        }

        bool success = InventoryManager.Instance.AddItemToInventory(itemData, quantity);

        if (success)
        {
            Debug.Log($"[GrabbableItem] {itemData.itemName} x{quantity}을(를) 인벤토리에 추가했습니다.");
            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning($"[GrabbableItem] 인벤토리가 가득 차서 {itemData.itemName}을(를) 추가할 수 없습니다.");
        }
    }
}
