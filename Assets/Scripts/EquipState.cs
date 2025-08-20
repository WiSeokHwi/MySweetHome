using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// ==================== VR INPUT MANAGEMENT SYSTEM ====================
/// 
/// 【 시스템 개요 】
/// VR 환경에서 플레이어의 손에 들린 아이템들을 추적하고, 모든 입력을 중앙에서 관리하는 시스템입니다.
/// IInteractable 인터페이스를 통해 통합된 상호작용 시스템을 제공합니다.
/// 
/// 【 주요 책임 】
/// 1. 좌/우 손별 현재 장착된 아이템 추적 (equippedItems Dictionary)
/// 2. Unity Input System을 통한 VR 입력 처리 (인벤토리 추가, 도구 사용, 배치 모드 등)
/// 3. IInteractable 호버 감지 및 통합 상호작용 처리
/// 4. 입력 이벤트를 적절한 아이템/오브젝트로 전달
/// 
/// 【 연동 시스템 】
/// - IInteractable: 모든 상호작용 가능한 오브젝트의 통합 인터페이스
/// - GrabbableItem: 모든 잡을 수 있는 아이템의 기본 클래스 (IInteractable 구현)
/// - InteractionObject: 도구 상호작용 오브젝트 기본 클래스 (IInteractable 구현)
/// - VRPlacementController: 아이템 배치 모드 관리
/// - NearFarInteractor: Unity XR의 VR 컨트롤러 상호작용
/// 
/// 【 데이터 저장 위치 】
/// - equippedItems: 각 손(NearFarInteractor)별 현재 장착 아이템 (Dictionary)
/// - currentHoveredInteractable: 현재 호버 중인 IInteractable 오브젝트
/// - 입력 액션들: Unity Input System의 InputActionReference들
/// 
/// 【 입력 처리 흐름 】
/// 1. VR 컨트롤러 호버 → IInteractable 감지 → OnHoverEnter/Exit
/// 2. VR 컨트롤러 입력 발생 → Unity Input System
/// 3. EquipState에서 입력 감지 → 우선순위에 따른 상호작용 처리:
///    - 우선순위 1: 호버 중인 IInteractable 오브젝트와 상호작용
///    - 우선순위 2: 들고 있는 아이템의 기능 실행
/// </summary>
public class EquipState : MonoBehaviour
{
    [Header("입력 설정")]
    [Tooltip("인벤토리에 아이템 추가 입력 (예: 반대 손 트리거)")]
    public InputActionReference inventoryAddAction;
    
    [Tooltip("도구 사용 입력 (예: 트리거 버튼)")]
    public InputActionReference toolUseAction;
    
    [Tooltip("배치 모드 토글 입력")]
    public InputActionReference placementModeToggleAction;
    
    [Tooltip("배치 확정 입력 (예: 트리거)")]
    public InputActionReference placementConfirmAction;
    
    [Header("XR 설정")]
    [Tooltip("왼손 Near Far Interactor")]
    public NearFarInteractor leftHandInteractor;
    
    [Tooltip("오른손 Near Far Interactor")]
    public NearFarInteractor rightHandInteractor;
    
    [Header("컨트롤러 참조")]
    [Tooltip("배치 시스템 컨트롤러")]
    public VRPlacementController placementController;
    
    
    [Header("디버그")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // 현재 들고 있는 아이템들 추적 (손별로)
    private Dictionary<NearFarInteractor, GrabbableItem> equippedItems = new Dictionary<NearFarInteractor, GrabbableItem>();
    
    // 일반 상호작용 오브젝트 감지
    private IInteractable currentHoveredInteractable = null;
    
    void Awake()
    {
        InitializeInteractors();
    }
    
    void OnEnable()
    {
        // 입력 액션 활성화
        if (inventoryAddAction != null)
        {
            inventoryAddAction.action.Enable();
            inventoryAddAction.action.performed += OnInventoryAddPerformed;
        }
        
        if (toolUseAction != null)
        {
            toolUseAction.action.Enable();
            toolUseAction.action.performed += OnToolUsePerformed;
        }
        
        if (placementModeToggleAction != null)
        {
            placementModeToggleAction.action.Enable();
            placementModeToggleAction.action.performed += OnPlacementModeTogglePerformed;
        }
        
        if (placementConfirmAction != null)
        {
            placementConfirmAction.action.Enable();
            placementConfirmAction.action.performed += OnPlacementConfirmPerformed;
        }
        
        // 인터랙터 이벤트 구독
        if (leftHandInteractor != null)
        {
            leftHandInteractor.selectEntered.AddListener(OnItemGrabbed);
            leftHandInteractor.selectExited.AddListener(OnItemReleased);
            leftHandInteractor.hoverEntered.AddListener(OnHoverEntered);
            leftHandInteractor.hoverExited.AddListener(OnHoverExited);
        }
        
        if (rightHandInteractor != null)
        {
            rightHandInteractor.selectEntered.AddListener(OnItemGrabbed);
            rightHandInteractor.selectExited.AddListener(OnItemReleased);
            rightHandInteractor.hoverEntered.AddListener(OnHoverEntered);
            rightHandInteractor.hoverExited.AddListener(OnHoverExited);
        }
    }
    
    void OnDisable()
    {
        // 입력 액션 비활성화
        if (inventoryAddAction != null)
        {
            inventoryAddAction.action.performed -= OnInventoryAddPerformed;
            inventoryAddAction.action.Disable();
        }
        
        if (toolUseAction != null)
        {
            toolUseAction.action.performed -= OnToolUsePerformed;
            toolUseAction.action.Disable();
        }
        
        if (placementModeToggleAction != null)
        {
            placementModeToggleAction.action.performed -= OnPlacementModeTogglePerformed;
            placementModeToggleAction.action.Disable();
        }
        
        if (placementConfirmAction != null)
        {
            placementConfirmAction.action.performed -= OnPlacementConfirmPerformed;
            placementConfirmAction.action.Disable();
        }
        
        // 인터랙터 이벤트 구독 해제
        if (leftHandInteractor != null)
        {
            leftHandInteractor.selectEntered.RemoveListener(OnItemGrabbed);
            leftHandInteractor.selectExited.RemoveListener(OnItemReleased);
            leftHandInteractor.hoverEntered.RemoveListener(OnHoverEntered);
            leftHandInteractor.hoverExited.RemoveListener(OnHoverExited);
        }
        
        if (rightHandInteractor != null)
        {
            rightHandInteractor.selectEntered.RemoveListener(OnItemGrabbed);
            rightHandInteractor.selectExited.RemoveListener(OnItemReleased);
            rightHandInteractor.hoverEntered.RemoveListener(OnHoverEntered);
            rightHandInteractor.hoverExited.RemoveListener(OnHoverExited);
        }
    }
    
    private void InitializeInteractors()
    {
        // 인터랙터들을 딕셔너리에 초기화
        if (leftHandInteractor != null)
            equippedItems[leftHandInteractor] = null;
        
        if (rightHandInteractor != null)
            equippedItems[rightHandInteractor] = null;
        
        if (enableDebugLogs)
            Debug.Log("[EquipState] 인터랙터 초기화 완료");
    }
    
    /// <summary>
    /// 아이템이 잡혔을 때 호출
    /// </summary>
    private void OnItemGrabbed(SelectEnterEventArgs args)
    {
        NearFarInteractor interactor = args.interactorObject as NearFarInteractor;
        GrabbableItem item = args.interactableObject.transform.GetComponent<GrabbableItem>();
        
        if (interactor != null && item != null)
        {
            equippedItems[interactor] = item;
            
            if (enableDebugLogs)
                Debug.Log($"[EquipState] {GetHandName(interactor)}가 {item.gameObject.name}을(를) 잡았습니다.");
        }
    }
    
    /// <summary>
    /// 아이템이 놓여졌을 때 호출
    /// </summary>
    private void OnItemReleased(SelectExitEventArgs args)
    {
        NearFarInteractor interactor = args.interactorObject as NearFarInteractor;
        GrabbableItem item = args.interactableObject.transform.GetComponent<GrabbableItem>();
        
        if (interactor != null && item != null)
        {
            if (enableDebugLogs)
                Debug.Log($"[EquipState] {GetHandName(interactor)}가 {item.gameObject.name}을(를) 놓았습니다.");
            
            equippedItems[interactor] = null;
        }
    }
    
    /// <summary>
    /// 인벤토리 추가 입력이 들어왔을 때 호출
    /// </summary>
    private void OnInventoryAddPerformed(InputAction.CallbackContext context)
    {
        if (enableDebugLogs)
            Debug.Log("[EquipState] 인벤토리 추가 입력이 들어왔습니다.");
        
        // 현재 들고 있는 모든 아이템에게 입력 전달
        foreach (var kvp in equippedItems)
        {
            GrabbableItem item = kvp.Value;
            if (item != null)
            {
                item.OnInventoryAddInput();
                break; // 첫 번째 아이템에서만 처리
            }
        }
    }
    
    /// <summary>
    /// 도구 사용 입력이 들어왔을 때 호출
    /// </summary>
    private void OnToolUsePerformed(InputAction.CallbackContext context)
    {
        if (enableDebugLogs)
            Debug.Log("[EquipState] 도구 사용 입력이 들어왔습니다.");
        
        // 우선순위 1: 현재 호버 중인 IInteractable 오브젝트와 상호작용
        if (currentHoveredInteractable != null && currentHoveredInteractable.CanInteract())
        {
            currentHoveredInteractable.OnInteract();
            return;
        }
        
        // 우선순위 2: 현재 들고 있는 아이템의 도구 사용 기능 (IInteractable로 통합 처리)
        foreach (var kvp in equippedItems)
        {
            GrabbableItem item = kvp.Value;
            if (item != null)
            {
                // GrabbableItem도 이제 IInteractable이므로 통합된 방식으로 처리
                IInteractable itemInteractable = item as IInteractable;
                if (itemInteractable != null && itemInteractable.CanInteract())
                {
                    itemInteractable.OnInteract();
                }
                break; // 첫 번째 아이템에서만 처리
            }
        }
    }
    
    /// <summary>
    /// 배치 모드 토글 입력이 들어왔을 때 호출
    /// </summary>
    private void OnPlacementModeTogglePerformed(InputAction.CallbackContext context)
    {
        if (enableDebugLogs)
            Debug.Log("[EquipState] 배치 모드 토글 입력이 들어왔습니다.");
        
        if (placementController != null)
        {
            // 현재 들고 있는 PlacableItem이 있는지 확인
            PlacableItem placableItem = GetCurrentPlacableItem();
            if (placableItem != null)
            {
                placementController.TogglePlacementMode(placableItem);
            }
            else
            {
                if (enableDebugLogs)
                    Debug.Log("[EquipState] 배치 가능한 아이템을 들고 있지 않습니다.");
            }
        }
    }
    
    /// <summary>
    /// 배치 확정 입력이 들어왔을 때 호출
    /// </summary>
    private void OnPlacementConfirmPerformed(InputAction.CallbackContext context)
    {
        if (enableDebugLogs)
            Debug.Log("[EquipState] 배치 확정 입력이 들어왔습니다.");
        
        if (placementController != null)
        {
            placementController.ConfirmPlacement();
        }
    }
    
    /// <summary>
    /// 인터랙터의 손 이름 반환
    /// </summary>
    private string GetHandName(NearFarInteractor interactor)
    {
        if (interactor == leftHandInteractor) return "왼손";
        if (interactor == rightHandInteractor) return "오른손";
        return "알 수 없는 손";
    }
    
    /// <summary>
    /// 현재 특정 손에 들고 있는 아이템 반환
    /// </summary>
    public GrabbableItem GetEquippedItem(NearFarInteractor interactor)
    {
        return equippedItems.ContainsKey(interactor) ? equippedItems[interactor] : null;
    }
    
    /// <summary>
    /// 현재 들고 있는 모든 아이템 반환
    /// </summary>
    public List<GrabbableItem> GetAllEquippedItems()
    {
        List<GrabbableItem> items = new List<GrabbableItem>();
        foreach (var item in equippedItems.Values)
        {
            if (item != null)
                items.Add(item);
        }
        return items;
    }
    
    /// <summary>
    /// 현재 들고 있는 PlacableItem 반환 (첫 번째로 찾은 것)
    /// </summary>
    private PlacableItem GetCurrentPlacableItem()
    {
        foreach (var item in equippedItems.Values)
        {
            if (item != null && item is PlacableItem placableItem)
                return placableItem;
        }
        return null;
    }
    
    /// <summary>
    /// 호버 시작 이벤트 처리
    /// </summary>
    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        // IInteractable 컴포넌트 확인
        IInteractable interactable = args.interactableObject.transform.GetComponent<IInteractable>();
        if (interactable != null && interactable.CanInteract())
        {
            // 이전 호버 오브젝트 정리
            if (currentHoveredInteractable != null)
            {
                currentHoveredInteractable.OnHoverExit();
            }
            
            // 새로운 호버 오브젝트 설정
            currentHoveredInteractable = interactable;
            currentHoveredInteractable.OnHoverEnter();
            
            if (enableDebugLogs)
                Debug.Log($"[EquipState] 상호작용 가능한 오브젝트 호버: {interactable.GetGameObject().name}");
        }
    }
    
    /// <summary>
    /// 호버 종료 이벤트 처리
    /// </summary>
    private void OnHoverExited(HoverExitEventArgs args)
    {
        // 현재 호버 중인 오브젝트와 같은지 확인
        IInteractable interactable = args.interactableObject.transform.GetComponent<IInteractable>();
        if (interactable != null && currentHoveredInteractable == interactable)
        {
            currentHoveredInteractable.OnHoverExit();
            currentHoveredInteractable = null;
            
            if (enableDebugLogs)
                Debug.Log($"[EquipState] 상호작용 오브젝트 호버 종료: {interactable.GetGameObject().name}");
        }
    }
}