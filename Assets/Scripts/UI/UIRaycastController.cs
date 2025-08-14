using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// UI 상호작용을 위한 레이캐스트 컨트롤러
/// 아이템 선택, UI 요소 클릭 등을 관리합니다.
/// </summary>
public class UIRaycastController : MonoBehaviour
{
    [Header("입력 설정")]
    [Tooltip("아이템 선택/UI 클릭을 위한 입력 액션")]
    public InputActionReference selectAction;
    
    [Header("레이캐스트 설정")]
    [Tooltip("레이캐스트 거리")]
    public float raycastDistance = 10.0f;
    
    [Tooltip("아이템 선택 가능한 레이어 마스크")]
    public LayerMask itemSelectionLayerMask = -1;
    
    [Header("UI 연결")]
    [Tooltip("인벤토리 UI 매니저")]
    public InventoryUIManager inventoryUIManager;
    
    [Tooltip("배치 컨트롤러 (배치 모드 확인용)")]
    public VRPlacementController placementController;
    
    [Header("시각적 피드백")]
    [Tooltip("활성화/비활성화할 레이캐스트 인터랙터 오브젝트 (이 스크립트가 있는 오브젝트와 다른 경우)")]
    public GameObject raycastInteractorObject;
    
    
    [Header("디버그")]
    [SerializeField] private bool enableDebugLogs = true;
    
    void Awake()
    {
        ValidateReferences();
    }
    
    private void ValidateReferences()
    {
        // InventoryUIManager 자동 찾기
        if (inventoryUIManager == null)
        {
            inventoryUIManager = FindAnyObjectByType<InventoryUIManager>();
            if (inventoryUIManager == null)
            {
                Debug.LogWarning("[UIRaycastController] InventoryUIManager를 찾을 수 없습니다.");
            }
        }
        
        // VRPlacementController 자동 찾기
        if (placementController == null)
        {
            placementController = FindAnyObjectByType<VRPlacementController>();
            if (placementController == null)
            {
                Debug.LogWarning("[UIRaycastController] VRPlacementController를 찾을 수 없습니다.");
            }
        }
        
        // 레이캐스트 인터랙터 오브젝트 설정
        if (raycastInteractorObject == null)
        {
            // 자식 오브젝트 중에서 XRRayInteractor가 있는 것을 찾기
            var rayInteractor = GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
            if (rayInteractor != null)
            {
                raycastInteractorObject = rayInteractor.gameObject;
                if (enableDebugLogs)
                    Debug.Log($"[UIRaycastController] 자동으로 찾은 레이캐스트 인터랙터: {raycastInteractorObject.name}");
            }
            else
            {
                raycastInteractorObject = gameObject;
                if (enableDebugLogs)
                    Debug.LogWarning("[UIRaycastController] XRRayInteractor를 찾을 수 없어 현재 GameObject를 사용합니다.");
            }
        }
    }
    
    void Start()
    {
        // InventoryUIManager의 인벤토리 상태 변화 이벤트 구독
        if (inventoryUIManager != null)
        {
            // 초기 상태 설정
            SetRaycastActive(inventoryUIManager.IsInventoryOpen());
        }
        else
        {
            // InventoryUIManager가 없으면 비활성화
            SetRaycastActive(false);
        }
    }
    
    void OnEnable()
    {
        // 오브젝트가 활성화될 때 입력 액션 활성화
        if (selectAction != null)
        {
            selectAction.action.Enable();
            selectAction.action.performed += OnSelectPerformed;
        }
    }
    
    void OnDisable()
    {
        // 오브젝트가 비활성화될 때 입력 액션 비활성화
        if (selectAction != null)
        {
            selectAction.action.performed -= OnSelectPerformed;
            selectAction.action.Disable();
        }
    }
    
    void Update()
    {
        // 인벤토리 상태 변화 감지
        CheckInventoryStateChange();
    }
    
    /// <summary>
    /// 선택 입력이 들어왔을 때 호출
    /// </summary>
    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        PerformRaycast();
    }
    
    /// <summary>
    /// 레이캐스트를 수행하여 선택 가능한 오브젝트를 찾습니다.
    /// </summary>
    private void PerformRaycast()
    {
        // 레이캐스트가 비활성화되어 있으면 실행하지 않음
        if (!IsRaycastActive())
        {
            return;
        }
        
        // 배치 모드 중이면 아이템 선택 비활성화
        if (IsInPlacementMode())
        {
            if (enableDebugLogs)
                Debug.Log("[UIRaycastController] 배치 모드 중이므로 아이템 선택이 비활성화됩니다.");
            return;
        }
        
        // 레이캐스트 실행
        RaycastHit hit;
        Vector3 rayOrigin = GetRayOrigin();
        Vector3 rayDirection = GetRayDirection();
        
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, raycastDistance, itemSelectionLayerMask))
        {
            ProcessRaycastHit(hit);
        }
        else
        {
            if (enableDebugLogs)
                Debug.Log("[UIRaycastController] 레이캐스트가 아무것도 감지하지 못했습니다.");
        }
    }
    
    /// <summary>
    /// 레이캐스트 히트를 처리합니다.
    /// </summary>
    private void ProcessRaycastHit(RaycastHit hit)
    {
        // GrabbableItem 찾기
        GrabbableItem grabbableItem = hit.collider.GetComponent<GrabbableItem>();
        if (grabbableItem == null)
        {
            grabbableItem = hit.collider.GetComponentInParent<GrabbableItem>();
        }
        
        if (grabbableItem != null)
        {
            HandleItemSelection(grabbableItem);
        }
        else
        {
            // 다른 상호작용 가능한 오브젝트 처리 (확장 가능)
            HandleOtherInteraction(hit);
        }
    }
    
    /// <summary>
    /// 아이템 선택을 처리합니다.
    /// </summary>
    private void HandleItemSelection(GrabbableItem item)
    {
        if (inventoryUIManager == null || item == null)
            return;
        
        ItemData itemData = item.GetItemData();
        if (itemData == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[UIRaycastController] {item.gameObject.name}에 유효한 ItemData가 없습니다.");
            return;
        }
        
        // InventoryUIManager에 아이템 정보 전달
        inventoryUIManager.ShowSelectedItemInfo(itemData);
        
        if (enableDebugLogs)
            Debug.Log($"[UIRaycastController] 아이템 선택: {itemData.itemName}");
    }
    
    /// <summary>
    /// 기타 상호작용을 처리합니다 (확장 가능).
    /// </summary>
    private void HandleOtherInteraction(RaycastHit hit)
    {
        // 추후 UI 버튼, 스위치, 문 등의 상호작용 오브젝트 처리 가능
        if (enableDebugLogs)
            Debug.Log($"[UIRaycastController] 상호작용 불가능한 오브젝트: {hit.collider.gameObject.name}");
    }
    
    /// <summary>
    /// 현재 배치 모드인지 확인합니다.
    /// </summary>
    private bool IsInPlacementMode()
    {
        if (placementController == null)
            return false;
        
        // VRPlacementController의 배치 모드 상태 확인
        // (VRPlacementController에 public 프로퍼티가 있다고 가정)
        return placementController.IsInPlacementMode();
    }
    
    /// <summary>
    /// 레이 시작점을 반환합니다.
    /// </summary>
    private Vector3 GetRayOrigin()
    {
        return transform.position;
    }
    
    /// <summary>
    /// 레이 방향을 반환합니다.
    /// </summary>
    private Vector3 GetRayDirection()
    {
        return transform.forward;
    }
    
    
    /// <summary>
    /// 수동으로 레이캐스트 실행 (외부에서 호출 가능)
    /// </summary>
    public void TriggerRaycast()
    {
        PerformRaycast();
    }
    
    // 레이캐스트 활성화 상태 추적
    private bool isRaycastActive = false;
    private bool lastInventoryState = false;
    
    /// <summary>
    /// 인벤토리 상태 변화를 감지합니다.
    /// </summary>
    private void CheckInventoryStateChange()
    {
        if (inventoryUIManager == null)
            return;
        
        bool currentInventoryState = inventoryUIManager.IsInventoryOpen();
        
        // 인벤토리 상태가 변경되었을 때만 처리
        if (currentInventoryState != lastInventoryState)
        {
            SetRaycastActive(currentInventoryState);
            lastInventoryState = currentInventoryState;
            
            if (enableDebugLogs)
                Debug.Log($"[UIRaycastController] 인벤토리 상태 변경: {(currentInventoryState ? "열림" : "닫힘")} - 레이캐스트: {(isRaycastActive ? "활성화" : "비활성화")}");
        }
    }
    
    /// <summary>
    /// 레이캐스트 활성화 상태를 설정합니다.
    /// </summary>
    private void SetRaycastActive(bool active)
    {
        isRaycastActive = active;
        
        // 레이캐스트 인터랙터 오브젝트 활성화/비활성화
        if (raycastInteractorObject != null)
        {
            raycastInteractorObject.SetActive(active);
            
            if (enableDebugLogs)
                Debug.Log($"[UIRaycastController] 레이캐스트 인터랙터 오브젝트 {raycastInteractorObject.name}: {(active ? "활성화" : "비활성화")}");
        }
        
        // 입력 액션은 오브젝트가 활성화될 때 자동으로 처리되므로 별도 관리 불필요
        // (GameObject가 비활성화되면 이 스크립트도 비활성화되어 입력이 자동으로 차단됨)
    }
    
    /// <summary>
    /// 현재 레이캐스트가 활성화되어 있는지 반환합니다.
    /// </summary>
    private bool IsRaycastActive()
    {
        return isRaycastActive;
    }
    
    /// <summary>
    /// 레이캐스트 상태를 외부에서 확인할 수 있습니다.
    /// </summary>
    public bool GetRaycastActiveState()
    {
        return isRaycastActive;
    }
}