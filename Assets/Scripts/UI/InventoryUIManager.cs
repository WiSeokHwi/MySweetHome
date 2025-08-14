/// <summary>
/// ==================== 인벤토리 UI 관리 시스템 ====================
/// 
/// 【 시스템 개요 】
/// VR 인벤토리 UI의 열기/닫기, 아이템 정보 표시, 사용량 표시 등을 총괄 관리하는 시스템입니다.
/// Unity Input System을 통해 VR 컴트롤러 입력을 처리하고 UI 업데이트를 담당합니다.
/// 
/// 【 주요 기능 】
/// 1. 인벤토리 창 열기/닫기 (입력 액션 기반)
/// 2. 인벤토리 사용량 표시 (0/16 식)
/// 3. 선택된 아이템 정보 표시 (이름, 설명)
/// 4. 슬롯 선택 및 아이템 자동 선택
/// 5. 월드 아이템 정보 표시
/// 
/// 【 연동 시스템 】
/// - InventoryManager: 인벤토리 데이터 및 슬롯 정보 연동
/// - VRUIItemIcon: 아이템 클릭 시 SelectSlot() 호출
/// - UIRaycastController: 인벤토리 열림 상태에 따른 레이캠스트 제어
/// - Unity Input System: VR 컴트롤러 입력 처리
/// 
/// 【 데이터 저장 위치 】
/// - isInventoryOpen: 인벤토리 열림 상태 (이 클래스에서 관리)
/// - selectedSlotIndex: 현재 선택된 슬롯 인덱스 (이 클래스에서 관리)
/// - 인벤토리 데이터: InventoryManager.inventorySlots에서 참조
/// 
/// 【 UI 업데이트 플로우 】
/// 1. 인벤토리 열기 → OpenInventory() → UpdateInventoryInfo() + SelectFirstAvailableItem()
/// 2. 아이템 선택 → SelectSlot() → UpdateItemInfo() → UI 텍스트 업데이트
/// 3. 인벤토리 닫기 → CloseInventory() → 선택 해제 및 UI 숨김
/// </summary>
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class InventoryUIManager : MonoBehaviour
{
    // ========== 입력 설정 ==========
    [Header("입력 설정")]
    [Tooltip("VR 컴트롤러에서 인벤토리 열기/닫기 입력 액션 (Unity Input System)")]
    public InputActionReference inventoryToggleAction;
    
    // ========== UI 참조 ==========
    [Header("UI 요소")]
    [Tooltip("인벤토리 전체 UI 패널 - 이 오브젝트를 통해 열기/닫기 제어")]
    public GameObject inventoryPanel;
    
    [Tooltip("인벤토리 사용량 표시 텍스트 (Used 슬롯/Max 슬롯 형태로 표시)")]
    public TextMeshProUGUI usedSlotsText;
    
    [Tooltip("선택된 아이템의 이름을 표시하는 UI 텍스트")]
    public TextMeshProUGUI itemNameText;
    
    [Tooltip("선택된 아이템의 상세 설명을 표시하는 UI 텍스트")]
    public TextMeshProUGUI itemDescriptionText;
    
    // ========== 시스템 연결 ==========
    [Header("인벤토리 설정")]
    [Tooltip("인벤토리 매니저 참조 - null이면 자동으로 Instance 찾기")]
    public InventoryManager inventoryManager;
    
    [Header("디버그")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // ========== 내부 상태 ==========
    /// <summary>
    /// 【상태 저장】현재 인벤토리 UI가 열려있는지 여부
    /// 【업데이트 위치】ToggleInventory(), OpenInventory(), CloseInventory()
    /// 【참조 위치】UIRaycastController에서 레이캠스트 활성화 결정 시
    /// </summary>
    private bool isInventoryOpen = false;
    
    /// <summary>
    /// 【상태 저장】현재 선택된 슬롯의 인덱스 (-1은 선택되지 않음)
    /// 【업데이트 위치】SelectSlot(), SelectFirstAvailableItem(), CloseInventory()
    /// 【참조 위치】UpdateItemInfo()에서 아이템 정보 표시 시
    /// </summary>
    private int selectedSlotIndex = -1;
    
    void Awake()
    {
        // InventoryManager 자동 찾기
        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null)
            {
                inventoryManager = FindAnyObjectByType<InventoryManager>();
            }
        }
        
        // 초기 상태 설정
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }
    
    void OnEnable()
    {
        if (inventoryToggleAction != null)
        {
            inventoryToggleAction.action.Enable();
            inventoryToggleAction.action.performed += OnInventoryTogglePerformed;
        }
    }
    
    void OnDisable()
    {
        if (inventoryToggleAction != null)
        {
            inventoryToggleAction.action.performed -= OnInventoryTogglePerformed;
            inventoryToggleAction.action.Disable();
        }
    }
    
    /// <summary>
    /// 인벤토리 토글 입력이 들어왔을 때 호출
    /// </summary>
    private void OnInventoryTogglePerformed(InputAction.CallbackContext context)
    {
        ToggleInventory();
    }
    
    /// <summary>
    /// 인벤토리 UI를 열거나 닫습니다.
    /// </summary>
    public void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(isInventoryOpen);
        }
        
        if (isInventoryOpen)
        {
            OpenInventory();
        }
        else
        {
            CloseInventory();
        }
        
        if (enableDebugLogs)
            Debug.Log("[InventoryUIManager] 인벤토리 " + (isInventoryOpen ? "열림" : "닫힘"));
    }
    
    /// <summary>
    /// 인벤토리를 엽니다.
    /// </summary>
    public void OpenInventory()
    {
        isInventoryOpen = true;
        
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
        }
        
        // 인벤토리 정보 업데이트
        UpdateInventoryInfo();
        
        // 첫 번째 아이템 선택 (있는 경우)
        SelectFirstAvailableItem();
    }
    
    /// <summary>
    /// 인벤토리를 닫습니다.
    /// </summary>
    public void CloseInventory()
    {
        isInventoryOpen = false;
        
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
        
        // 선택 해제
        selectedSlotIndex = -1;
        UpdateItemInfo(null);
    }
    
    /// <summary>
    /// 인벤토리 사용량 정보를 업데이트합니다.
    /// </summary>
    private void UpdateInventoryInfo()
    {
        if (inventoryManager == null) return;
        
        int usedSlots = GetUsedSlotsCount();
        int maxSlots = inventoryManager.maxSlots;
        
        if (usedSlotsText != null)
        {
            usedSlotsText.text = usedSlots + "/" + maxSlots;
        }
    }
    
    /// <summary>
    /// 사용 중인 슬롯 개수를 반환합니다.
    /// </summary>
    private int GetUsedSlotsCount()
    {
        if (inventoryManager == null || inventoryManager.inventorySlots == null)
            return 0;
        
        int usedCount = 0;
        foreach (var slot in inventoryManager.inventorySlots)
        {
            if (slot.item != null && slot.quantity > 0)
            {
                usedCount++;
            }
        }
        
        return usedCount;
    }
    
    /// <summary>
    /// 첫 번째 사용 가능한 아이템을 선택합니다.
    /// </summary>
    private void SelectFirstAvailableItem()
    {
        if (inventoryManager == null || inventoryManager.inventorySlots == null)
            return;
        
        for (int i = 0; i < inventoryManager.inventorySlots.Count; i++)
        {
            var slot = inventoryManager.inventorySlots[i];
            if (slot.item != null && slot.quantity > 0)
            {
                SelectSlot(i);
                return;
            }
        }
        
        // 아이템이 없으면 선택 해제
        selectedSlotIndex = -1;
        UpdateItemInfo(null);
    }
    
    /// <summary>
    /// 특정 슬롯을 선택합니다.
    /// </summary>
    /// <param name=\"slotIndex\">선택할 슬롯 인덱스</param>
    public void SelectSlot(int slotIndex)
    {
        if (inventoryManager == null || inventoryManager.inventorySlots == null)
            return;
        
        if (slotIndex < 0 || slotIndex >= inventoryManager.inventorySlots.Count)
            return;
        
        selectedSlotIndex = slotIndex;
        var selectedSlot = inventoryManager.inventorySlots[slotIndex];
        
        UpdateItemInfo(selectedSlot.item);
    }
    
    /// <summary>
    /// 선택된 아이템의 정보를 업데이트합니다.
    /// </summary>
    /// <param name=\"itemData\">표시할 아이템 데이터 (null이면 정보 숨김)</param>
    private void UpdateItemInfo(ItemData itemData)
    {
        if (itemData != null)
        {
            // 아이템 이름 표시
            if (itemNameText != null)
            {
                itemNameText.text = itemData.itemName;
            }
            
            // 아이템 설명 표시
            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = itemData.description;
            }
        }
        else
        {
            // 아이템 정보 숨김
            if (itemNameText != null)
            {
                itemNameText.text = "아이템 없음";
            }
            
            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = "선택된 아이템이 없습니다.";
            }
        }
    }
    
    /// <summary>
    /// 인벤토리가 열려있는지 반환합니다.
    /// </summary>
    public bool IsInventoryOpen()
    {
        return isInventoryOpen;
    }
    
    /// <summary>
    /// 현재 선택된 슬롯 인덱스를 반환합니다.
    /// </summary>
    public int GetSelectedSlotIndex()
    {
        return selectedSlotIndex;
    }
    
    /// <summary>
    /// 인벤토리 정보를 강제로 업데이트합니다 (외부에서 호출 가능).
    /// </summary>
    public void RefreshInventoryDisplay()
    {
        if (isInventoryOpen)
        {
            UpdateInventoryInfo();
            
            // 현재 선택된 슬롯이 유효한지 확인
            if (selectedSlotIndex >= 0 && selectedSlotIndex < inventoryManager.inventorySlots.Count)
            {
                var selectedSlot = inventoryManager.inventorySlots[selectedSlotIndex];
                UpdateItemInfo(selectedSlot.item);
            }
            else
            {
                SelectFirstAvailableItem();
            }
        }
    }
    
    /// <summary>
    /// 외부에서 선택된 아이템 정보를 표시합니다 (VRPlacementController에서 호출).
    /// </summary>
    /// <param name="itemData">표시할 아이템 데이터</param>
    public void ShowSelectedItemInfo(ItemData itemData)
    {
        // 인벤토리가 열려있지 않으면 일시적으로 아이템 정보만 표시
        if (!isInventoryOpen)
        {
            // 인벤토리 패널을 열지 않고 아이템 정보만 업데이트
            UpdateItemInfo(itemData);
            
            if (enableDebugLogs)
                Debug.Log($"[InventoryUIManager] 월드 아이템 정보 표시: {itemData.itemName}");
        }
        else
        {
            // 인벤토리가 열려있으면 해당 아이템을 찾아서 선택
            SelectItemInInventory(itemData);
        }
    }
    
    /// <summary>
    /// 인벤토리에서 특정 아이템을 찾아서 선택합니다.
    /// </summary>
    /// <param name="itemData">찾을 아이템 데이터</param>
    private void SelectItemInInventory(ItemData itemData)
    {
        if (inventoryManager == null || inventoryManager.inventorySlots == null)
            return;
        
        // 인벤토리에서 해당 아이템을 찾기
        for (int i = 0; i < inventoryManager.inventorySlots.Count; i++)
        {
            var slot = inventoryManager.inventorySlots[i];
            if (slot.item == itemData)
            {
                SelectSlot(i);
                return;
            }
        }
        
        // 인벤토리에 없는 아이템이면 직접 정보 표시
        UpdateItemInfo(itemData);
    }
}