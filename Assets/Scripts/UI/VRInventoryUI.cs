using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.UI;

/// <summary>
/// VR 인벤토리 UI - VR 환경에서 3D 공간에 배치되는 인벤토리 패널
/// </summary>
public class VRInventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("인벤토리 슬롯들이 배치될 부모 Transform")]
    [SerializeField] private Transform slotContainer;
    [Tooltip("인벤토리 슬롯 UI 프리팹 (InventorySlotUI 컴포넌트 포함)")]
    [SerializeField] private GameObject slotPrefab;
    [Tooltip("인벤토리 UI의 제목 텍스트")]
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("인벤토리 상태를 표시할 텍스트 (예: '사용 중: 10/36')")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("VR Interaction")]
    [Tooltip("VR UI 자체를 잡고 이동할 수 있게 하는 XRGrabInteractable")]
    [SerializeField] private XRGrabInteractable grabInteractable;
    [Tooltip("UI 렌더링을 위한 Canvas 컴포넌트")]
    [SerializeField] private Canvas inventoryCanvas;

    [Header("Layout Settings")]
    [Tooltip("한 줄에 표시될 슬롯의 개수")]
    [SerializeField] private int slotsPerRow = 6;
    [Tooltip("슬롯 간의 가로 간격 (픽셀 단위, RectTransform 기준)")]
    [SerializeField] private float slotSpacing = 120f;
    [Tooltip("줄 간의 세로 간격 (픽셀 단위, RectTransform 기준)")]
    [SerializeField] private float rowSpacing = 120f;

    [Header("Visibility Settings")]
    [Tooltip("게임 시작 시 인벤토리를 바로 표시할지 여부")]
    [SerializeField] private bool startVisible = false;
    [Tooltip("인벤토리 UI를 토글할 키보드 키 (디버그/테스트용)")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
    private PlayerInventory playerInventory;
    private int selectedSlotIndex = -1; // 현재 선택된 슬롯의 인덱스
    private bool isInventoryOpen = false;

    // 이벤트 (VRUIManager 등에서 구독)
    public System.Action<int> OnSlotSelected; // 슬롯이 선택될 때 발생
    public System.Action OnInventoryToggled; // 인벤토리 열림/닫힘 상태가 변경될 때 발생

    public bool IsOpen => isInventoryOpen;
    public int SelectedSlotIndex => selectedSlotIndex;

    void Awake()
    {
        // UI 컴포넌트 참조 자동 할당 (없을 경우)
        if (inventoryCanvas == null)
            inventoryCanvas = GetComponentInChildren<Canvas>();

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        SetVisibility(startVisible); // 초기 가시성 설정
    }

    void Start()
    {
        playerInventory = PlayerInventory.Instance;
        if (playerInventory != null)
        {
            // PlayerInventory의 변경 이벤트를 구독하여 UI 새로고침
            playerInventory.OnInventoryChanged += RefreshUI;
            InitializeSlots(); // 인벤토리 슬롯 UI 생성 및 초기화
            RefreshUI(); // 초기 UI 상태 새로고침
        }
        else
        {
            Debug.LogError("VRInventoryUI: PlayerInventory 인스턴스를 찾을 수 없습니다! 인벤토리 UI가 제대로 작동하지 않을 수 있습니다.");
        }
    }

    void Update()
    {
        // 키보드 토글 (테스트/디버그용, 실제 VR에서는 VRUIManager에서 컨트롤러 입력 처리)
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleInventory();
        }
    }

    /// <summary>
    /// 인벤토리 슬롯 UI들을 생성하고 초기화합니다.
    /// PlayerInventory의 maxSlots에 따라 슬롯 개수를 결정합니다.
    /// </summary>
    private void InitializeSlots()
    {
        if (slotContainer == null || slotPrefab == null)
        {
            Debug.LogError("VRInventoryUI: slotContainer 또는 slotPrefab이 설정되지 않았습니다! 슬롯을 생성할 수 없습니다.");
            return;
        }

        // 기존 슬롯들 제거 (재초기화 시)
        foreach (Transform child in slotContainer)
        {
            DestroyImmediate(child.gameObject); // 에디터 모드에서도 안전하게 제거
        }
        slotUIs.Clear(); // 리스트 비우기

        // PlayerInventory의 최대 슬롯 개수 가져오기
        int actualMaxSlots = playerInventory != null ? playerInventory.GetAllSlots().Count : 36; // PlayerInventory에서 가져오거나 기본값 사용

        for (int i = 0; i < actualMaxSlots; i++)
        {
            GameObject slotObj = Instantiate(slotPrefab, slotContainer);
            InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();

            if (slotUI == null)
            {
                // 프리팹에 InventorySlotUI가 없을 경우 경고 및 추가 시도
                Debug.LogWarning($"VRInventoryUI: slotPrefab에 InventorySlotUI 컴포넌트가 없습니다. {slotObj.name}에 추가합니다.");
                slotUI = slotObj.AddComponent<InventorySlotUI>();
            }

            // 슬롯 위치 설정 (수동 그리드 레이아웃)
            RectTransform rectTransform = slotObj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                int row = i / slotsPerRow;
                int col = i % slotsPerRow;

                // 중앙 정렬을 위해 오프셋 계산 (슬롯 컨테이너의 크기에 따라 조정 필요)
                float startX = -(slotsPerRow - 1) * slotSpacing / 2f;
                float startY = (actualMaxSlots / slotsPerRow / 2f - 0.5f) * rowSpacing; // 대략적인 Y 시작점

                float x = startX + col * slotSpacing;
                float y = startY - row * rowSpacing; // 위에서 아래로

                rectTransform.anchoredPosition = new Vector2(x, y);
                // RectTransform의 pivot과 anchor를 (0.5, 0.5)로 설정하는 것이 좋습니다.
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            }

            // 슬롯 초기화 및 이벤트 연결
            slotUI.Initialize(i);
            slotUI.OnSlotClicked += OnSlotClicked; // 슬롯 클릭 이벤트 구독
            slotUI.OnSlotHovered += OnSlotHovered; // 슬롯 호버 이벤트 구독
            slotUI.OnSlotContentChanged += RefreshUI; // 슬롯 내용 변경 시 UI 새로고침 (레시피 확인 등)

            slotUIs.Add(slotUI);
        }

        if (titleText != null)
            titleText.text = "인벤토리";

        Debug.Log($"VRInventoryUI: {actualMaxSlots}개의 슬롯이 생성되었습니다.");
    }

    /// <summary>
    /// 인벤토리 UI를 PlayerInventory의 현재 상태에 맞춰 새로고침합니다.
    /// </summary>
    private void RefreshUI()
    {
        if (playerInventory == null || slotUIs.Count == 0)
        {
            Debug.LogWarning("VRInventoryUI: RefreshUI 실패 - playerInventory 또는 slotUIs가 null입니다.");
            return;
        }

        Debug.Log("VRInventoryUI: UI 새로고침 시작");

        // PlayerInventory에서 모든 슬롯 데이터 가져오기
        var allPlayerSlots = playerInventory.GetAllSlots(); // List<ItemStack> 반환

        // 모든 슬롯 UI 업데이트
        for (int i = 0; i < slotUIs.Count; i++) // slotUIs.Count를 기준으로 반복
        {
            // PlayerInventory의 슬롯 개수보다 UI 슬롯이 많을 경우를 대비
            ItemStack itemInSlot = (i < allPlayerSlots.Count && allPlayerSlots[i] != null) ? allPlayerSlots[i] : new ItemStack(null, 0);

            slotUIs[i].SetItem(itemInSlot);

        
        }

        // 상태 텍스트 업데이트
        if (statusText != null && playerInventory != null)
        {
            int usedSlots = playerInventory.GetUsedSlotCount();
            statusText.text = $"사용 중: {usedSlots}/{playerInventory.GetAllSlots().Count}"; // PlayerInventory의 실제 슬롯 개수 사용
        }
        Debug.Log("VRInventoryUI: UI 새로고침 완료");
    }

    /// <summary>
    /// 인벤토리 UI의 표시/숨김 상태를 토글합니다.
    /// </summary>
    public void ToggleInventory()
    {
        SetVisibility(!isInventoryOpen);
    }

    /// <summary>
    /// 인벤토리 UI의 표시 상태를 설정합니다.
    /// </summary>
    /// <param name="visible">true면 표시, false면 숨김</param>
    public void SetVisibility(bool visible)
    {
        isInventoryOpen = visible;
        gameObject.SetActive(visible); // GameObject 활성화/비활성화

        if (inventoryCanvas != null)
        {
            inventoryCanvas.enabled = visible; // Canvas 컴포넌트 활성화/비활성화 (렌더링 제어)
        }

        OnInventoryToggled?.Invoke(); // 상태 변경 이벤트 발생

        Debug.Log($"VRInventoryUI: 인벤토리 {(visible ? "열림" : "닫힘")}");
    }

    /// <summary>
    /// 슬롯 클릭 이벤트 핸들러
    /// </summary>
    /// <param name="slotIndex">클릭된 슬롯의 인덱스</param>
    /// <param name="itemStack">클릭된 슬롯의 아이템 스택</param>
    private void OnSlotClicked(int slotIndex, ItemStack itemStack)
    {
        // 현재 선택된 슬롯이 이미 클릭된 슬롯이라면 선택 해제
        if (selectedSlotIndex == slotIndex)
        {
            SetSelectedSlot(-1); // 선택 해제
        }
        else
        {
            SetSelectedSlot(slotIndex); // 새로운 슬롯 선택
        }

        Debug.Log($"VRInventoryUI: 슬롯 {slotIndex} 클릭됨 - {(itemStack != null ? itemStack.GetDebugInfo() : "빈 슬롯")}");
    }

    /// <summary>
    /// 슬롯 호버 이벤트 핸들러
    /// </summary>
    /// <param name="slotIndex">호버된 슬롯의 인덱스</param>
    private void OnSlotHovered(int slotIndex)
    {
        // 모든 슬롯의 하이라이트 제거
        foreach (var slot in slotUIs)
        {
            // 현재 선택된 슬롯이 아니면서 하이라이트 상태인 경우에만 해제
            if (slot.SlotIndex != selectedSlotIndex)
            {
                slot.SetHighlighted(false);
            }
        }

        // 호버된 슬롯 하이라이트 (선택된 슬롯이 아닐 경우에만)
        if (slotIndex >= 0 && slotIndex < slotUIs.Count)
        {
            if (slotUIs[slotIndex].SlotIndex != selectedSlotIndex)
            {
                slotUIs[slotIndex].SetHighlighted(true);
            }
        }
    }

    /// <summary>
    /// 선택된 슬롯을 설정하고 시각적 피드백을 업데이트합니다.
    /// </summary>
    /// <param name="newSlotIndex">새로 선택할 슬롯의 인덱스 (-1은 선택 해제)</param>
    private void SetSelectedSlot(int newSlotIndex)
    {
        // 이전 선택된 슬롯의 시각적 상태 초기화
        if (selectedSlotIndex >= 0 && selectedSlotIndex < slotUIs.Count)
        {
            slotUIs[selectedSlotIndex].SetSelected(false);
            slotUIs[selectedSlotIndex].SetHighlighted(false); // 선택 해제 시 하이라이트도 해제
        }

        selectedSlotIndex = newSlotIndex; // 새 선택 인덱스 설정

        // 새로운 슬롯의 시각적 상태 업데이트
        if (selectedSlotIndex >= 0 && selectedSlotIndex < slotUIs.Count)
        {
            slotUIs[selectedSlotIndex].SetSelected(true);
            slotUIs[selectedSlotIndex].SetHighlighted(true); // 선택된 슬롯은 하이라이트도 유지
        }

        OnSlotSelected?.Invoke(selectedSlotIndex); // 슬롯 선택 이벤트 발생
    }

    /// <summary>
    /// 현재 선택된 아이템을 사용합니다.
    /// (예: 음식이면 체력 회복, 도구면 장비 등)
    /// </summary>
    public void UseSelectedItem()
    {
        if (selectedSlotIndex < 0 || selectedSlotIndex >= slotUIs.Count)
        {
            Debug.LogWarning("VRInventoryUI: 선택된 슬롯이 없습니다.");
            return;
        }

        var selectedSlot = slotUIs[selectedSlotIndex];
        if (selectedSlot.IsEmpty)
        {
            Debug.LogWarning("VRInventoryUI: 선택된 슬롯이 비어있습니다.");
            return;
        }

        var itemStack = selectedSlot.ItemStack;

        // 실제 아이템 사용 로직을 여기에 구현합니다.
        // 예: itemStack.material.UseItem(); (CraftingMaterial에 UseItem 메서드 추가)
        // 또는 특정 아이템 타입에 따라 다른 로직 실행

        if (itemStack.material.isConsumable) // 소모성 아이템인 경우
        {
            playerInventory.RemoveItem(itemStack.material, 1); // 1개 소모
            Debug.Log($"VRInventoryUI: {itemStack.material.materialName} 1개 사용됨.");
        }
        else
        {
            Debug.Log($"VRInventoryUI: {itemStack.material.materialName} 사용됨 (비소모성).");
        }

        // 아이템 사용 후 선택 해제
        SetSelectedSlot(-1);
    }

    /// <summary>
    /// 현재 선택된 아이템을 월드에 드롭합니다.
    /// </summary>
    public void DropSelectedItem()
    {
        if (selectedSlotIndex < 0 || selectedSlotIndex >= slotUIs.Count)
        {
            Debug.LogWarning("VRInventoryUI: 선택된 슬롯이 없습니다.");
            return;
        }

        var selectedSlot = slotUIs[selectedSlotIndex];
        if (selectedSlot.IsEmpty)
        {
            Debug.LogWarning("VRInventoryUI: 선택된 슬롯이 비어있습니다.");
            return;
        }

        var itemStack = selectedSlot.ItemStack;

        // 월드에 PickableItem 프리팹을 생성
        if (itemStack.material.worldPrefab != null)
        {
            // 플레이어 앞쪽에 드롭 위치 계산 (VRUIManager의 playerHead를 참조하거나 직접 계산)
            Vector3 dropPosition = transform.position + transform.forward * 0.5f + transform.up * -0.2f; // 인벤토리 UI 기준으로 앞쪽 아래
            GameObject droppedItemGO = Instantiate(itemStack.material.worldPrefab, dropPosition, Quaternion.identity);

            // 생성된 오브젝트에 PickableItem 컴포넌트가 있다면 데이터 설정
            PickableItem droppedPickableItem = droppedItemGO.GetComponent<PickableItem>();
            if (droppedPickableItem != null)
            {
                droppedPickableItem.itemData = itemStack.material;
                droppedPickableItem.quantity = itemStack.Quantity; // 슬롯의 모든 수량 드롭
                // Rigidbody가 있다면 물리 활성화 (PickableItem Awake에서 isKinematic = true, useGravity = false 설정했으니 여기서 변경)
                Rigidbody rb = droppedItemGO.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    // 드롭 시 약간의 힘을 가하여 자연스럽게 떨어지도록
                    rb.AddForce(transform.forward * 0.5f + Vector3.up * 0.5f, ForceMode.Impulse);
                }
            }
            Debug.Log($"VRInventoryUI: {itemStack.material.materialName} x{itemStack.Quantity}을 월드에 드롭했습니다.");
        }
        else
        {
            Debug.LogWarning($"VRInventoryUI: '{itemStack.material.materialName}'의 worldPrefab이 할당되지 않아 월드에 드롭할 수 없습니다.");
        }

        // 인벤토리에서 아이템 제거
        if (playerInventory != null)
        {
            playerInventory.RemoveItem(itemStack.material, itemStack.Quantity); // 슬롯의 모든 수량 제거
        }

        // 드롭 후 선택 해제
        SetSelectedSlot(-1);
    }

    /// <summary>
    /// VR Ray Interactor를 통한 UI 상호작용 설정을 돕습니다.
    /// (일반적으로 VRUIManager에서 호출되어 GraphicRaycaster를 설정합니다.)
    /// </summary>
    /// <param name="rayInteractor">사용할 XR Ray Interactor</param>
    public void SetupVRInteraction(XRRayInteractor rayInteractor)
    {
        if (rayInteractor == null) return;

        // Canvas에 GraphicRaycaster가 없으면 추가 (VR UI 상호작용의 필수 조건)
        var graphicRaycaster = GetComponentInChildren<GraphicRaycaster>();
        if (graphicRaycaster == null && inventoryCanvas != null)
        {
            graphicRaycaster = inventoryCanvas.gameObject.AddComponent<GraphicRaycaster>();
            Debug.Log("VRInventoryUI: GraphicRaycaster가 Canvas에 추가되었습니다.");
        }

        // XRRayInteractor의 raycastMask 등을 설정할 수 있습니다.
        // rayInteractor.raycastMask = LayerMask.GetMask("UI"); // 예시: UI 레이어에만 레이캐스트
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= RefreshUI;
        }
        // 슬롯 UI 이벤트도 해제
        foreach (var slotUI in slotUIs)
        {
            if (slotUI != null)
            {
                slotUI.OnSlotClicked -= OnSlotClicked;
                slotUI.OnSlotHovered -= OnSlotHovered;
                slotUI.OnSlotContentChanged -= RefreshUI;
            }
        }
    }

    /// <summary>
    /// 인벤토리 상태 디버그 출력
    /// </summary>
    [ContextMenu("인벤토리 상태 출력")]
    public void PrintInventoryStatus()
    {
        Debug.Log("=== VR 인벤토리 UI 상태 ===");
        Debug.Log($"열림 상태: {isInventoryOpen}");
        Debug.Log($"선택된 슬롯: {selectedSlotIndex}");
        Debug.Log($"슬롯 UI 개수: {slotUIs.Count}");

        if (playerInventory != null)
        {
            playerInventory.PrintInventoryStatus(); // PlayerInventory의 상세 상태 출력
        }

        Debug.Log("========================");
    }
}
