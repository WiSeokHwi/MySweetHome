using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.UI;
using System.Linq; // LINQ를 사용하기 위해 추가

/// <summary>
/// VR 제작 UI - 3x3 제작 그리드와 결과 슬롯을 제공하는 VR 인터페이스
/// </summary>
public class VRCraftingUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("제작 그리드 슬롯들이 배치될 부모 Transform")]
    [SerializeField] private Transform craftingGridContainer;
    [Tooltip("제작 결과 슬롯이 배치될 부모 Transform")]
    [SerializeField] private Transform resultSlotContainer;
    [Tooltip("인벤토리/제작 슬롯 UI 프리팹 (InventorySlotUI 컴포넌트 포함)")]
    [SerializeField] private GameObject slotPrefab;
    [Tooltip("제작 UI의 제목 텍스트")]
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("현재 매칭된 레시피의 이름을 표시할 텍스트")]
    [SerializeField] private TextMeshProUGUI recipeNameText;

    [Header("VR Interaction")]
    [Tooltip("VR UI 자체를 잡고 이동할 수 있게 하는 XRGrabInteractable")]
    [SerializeField] private XRGrabInteractable grabInteractable;
    [Tooltip("UI 렌더링을 위한 Canvas 컴포넌트")]
    [SerializeField] private Canvas craftingCanvas;

    [Header("Layout Settings")]
    [Tooltip("슬롯 간의 가로/세로 간격 (픽셀 단위, RectTransform 기준)")]
    [SerializeField] private float slotSpacing = 120f;
    [Tooltip("게임 시작 시 제작 UI를 바로 표시할지 여부")]
    [SerializeField] private bool startVisible = false;

    private InventorySlotUI[] craftingGridSlots = new InventorySlotUI[9];
    private InventorySlotUI resultSlot;
    private CraftingManager craftingManager;
    private PlayerInventory playerInventory;
    private CraftingRecipe currentRecipe; // 현재 그리드에 매칭된 레시피
    private bool isCraftingOpen = false;

    // 이벤트 (VRUIManager 등에서 구독)
    public System.Action OnCraftingCompleted; // 제작이 완료될 때 발생
    public System.Action OnCraftingToggled; // 제작 UI 열림/닫힘 상태가 변경될 때 발생

    public bool IsOpen => isCraftingOpen;

    void Awake()
    {
        // UI 컴포넌트 참조 자동 할당 (없을 경우)
        if (craftingCanvas == null)
            craftingCanvas = GetComponentInChildren<Canvas>();

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        SetVisibility(startVisible); // 초기 가시성 설정
    }

    void Start()
    {
        craftingManager = CraftingManager.Instance;
        playerInventory = PlayerInventory.Instance;

        if (craftingManager == null)
        {
            Debug.LogError("VRCraftingUI: CraftingManager 인스턴스를 찾을 수 없습니다! 제작 UI가 제대로 작동하지 않을 수 있습니다.");
        }

        if (playerInventory == null)
        {
            Debug.LogError("VRCraftingUI: PlayerInventory 인스턴스를 찾을 수 없습니다! 제작 UI가 제대로 작동하지 않을 수 있습니다.");
        }

        InitializeCraftingGrid(); // 제작 그리드 슬롯 UI 생성 및 초기화
        InitializeResultSlot(); // 결과 슬롯 UI 생성 및 초기화

        // 초기 레시피 확인 (시작 시 그리드가 비어있을 수 있지만, 안전을 위해)
        CheckForRecipe();
    }

    /// <summary>
    /// 3x3 제작 그리드 슬롯 UI들을 생성하고 초기화합니다.
    /// </summary>
    private void InitializeCraftingGrid()
    {
        if (craftingGridContainer == null || slotPrefab == null)
        {
            Debug.LogError("VRCraftingUI: craftingGridContainer 또는 slotPrefab이 설정되지 않았습니다! 제작 그리드를 생성할 수 없습니다.");
            return;
        }

        // 기존 슬롯들 제거 (재초기화 시)
        foreach (Transform child in craftingGridContainer)
        {
            DestroyImmediate(child.gameObject); // 에디터 모드에서도 안전하게 제거
        }

        // 3x3 그리드 생성
        for (int i = 0; i < 9; i++)
        {
            GameObject slotObj = Instantiate(slotPrefab, craftingGridContainer);
            InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();

            if (slotUI == null)
            {
                Debug.LogWarning($"VRCraftingUI: slotPrefab에 InventorySlotUI 컴포넌트가 없습니다. {slotObj.name}에 추가합니다.");
                slotUI = slotObj.AddComponent<InventorySlotUI>();
            }

            // 그리드 위치 설정 (중앙 정렬)
            RectTransform rectTransform = slotObj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                int row = i / 3; // 0, 1, 2
                int col = i % 3; // 0, 1, 2

                // 3x3 그리드를 중앙에 배치하기 위한 오프셋
                float startX = -slotSpacing; // (3-1)/2 * slotSpacing
                float startY = slotSpacing;  // (3-1)/2 * slotSpacing (위에서 아래로)

                float x = startX + col * slotSpacing;
                float y = startY - row * slotSpacing;

                rectTransform.anchoredPosition = new Vector2(x, y);
                // RectTransform의 pivot과 anchor를 (0.5, 0.5)로 설정하는 것이 좋습니다.
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            }

            // 슬롯 초기화 및 이벤트 연결
            slotUI.Initialize(i);
            slotUI.OnSlotClicked += OnCraftingSlotClicked; // 제작 그리드 슬롯 클릭 이벤트 구독
            slotUI.OnSlotHovered += OnCraftingSlotHovered; // 제작 그리드 슬롯 호버 이벤트 구독
            slotUI.OnSlotContentChanged += OnCraftingGridContentChanged; // 슬롯 내용 변경 시 레시피 확인 트리거

            craftingGridSlots[i] = slotUI;
        }

        if (titleText != null)
            titleText.text = "제작대";

        Debug.Log("VRCraftingUI: 3x3 제작 그리드가 생성되었습니다.");
    }

    /// <summary>
    /// 결과 슬롯 UI를 생성하고 초기화합니다.
    /// </summary>
    private void InitializeResultSlot()
    {
        if (resultSlotContainer == null || slotPrefab == null)
        {
            Debug.LogError("VRCraftingUI: resultSlotContainer 또는 slotPrefab이 설정되지 않았습니다! 결과 슬롯을 생성할 수 없습니다.");
            return;
        }

        // 기존 슬롯 제거 (재초기화 시)
        foreach (Transform child in resultSlotContainer)
        {
            DestroyImmediate(child.gameObject);
        }

        // 결과 슬롯 생성
        GameObject resultSlotObj = Instantiate(slotPrefab, resultSlotContainer);
        resultSlot = resultSlotObj.GetComponent<InventorySlotUI>();

        if (resultSlot == null)
        {
            Debug.LogWarning($"VRCraftingUI: slotPrefab에 InventorySlotUI 컴포넌트가 없습니다. {resultSlotObj.name}에 추가합니다.");
            resultSlot = resultSlotObj.AddComponent<InventorySlotUI>();
        }

        resultSlot.Initialize(-1); // 결과 슬롯은 특별한 인덱스 (-1) 사용
        resultSlot.OnSlotClicked += OnResultSlotClicked; // 결과 슬롯 클릭 이벤트 구독
                                                         // 결과 슬롯은 아이템을 놓을 수 없으므로 OnSlotContentChanged는 구독하지 않습니다.

        Debug.Log("VRCraftingUI: 결과 슬롯이 생성되었습니다.");
    }

    /// <summary>
    /// 제작 그리드 슬롯 클릭 처리.
    /// 이 메서드는 주로 그리드에 이미 있는 아이템을 인벤토리로 되돌릴 때 사용됩니다.
    /// (아이템을 그리드에 놓는 것은 XRSocketInteractor를 통해 처리됩니다.)
    /// </summary>
    /// <param name="slotIndex">클릭된 슬롯의 인덱스</param>
    /// <param name="itemStack">클릭된 슬롯의 아이템 스택</param>
    private void OnCraftingSlotClicked(int slotIndex, ItemStack itemStack)
    {
        Debug.Log($"VRCraftingUI: 제작 슬롯 {slotIndex} 클릭됨.");

        // 아이템이 있는 슬롯을 클릭하면 인벤토리로 반환
        if (itemStack != null && !itemStack.IsEmpty)
        {
            ReturnItemToInventory(slotIndex);
        }
        else
        {
            Debug.Log("VRCraftingUI: 빈 제작 슬롯이 클릭되었습니다. 아이템을 드래그하여 배치하세요.");
        }

        // 슬롯 내용이 변경되었을 수 있으므로 레시피 다시 확인
        CheckForRecipe();
    }

    /// <summary>
    /// 제작 그리드 슬롯 호버 처리
    /// </summary>
    /// <param name="slotIndex">호버된 슬롯의 인덱스</param>
    private void OnCraftingSlotHovered(int slotIndex)
    {
        // 모든 슬롯의 하이라이트 제거
        foreach (var slot in craftingGridSlots)
        {
            slot.SetHighlighted(false);
        }

        // 호버된 슬롯 하이라이트
        if (slotIndex >= 0 && slotIndex < craftingGridSlots.Length)
        {
            craftingGridSlots[slotIndex].SetHighlighted(true);
        }
    }

    /// <summary>
    /// 제작 그리드의 슬롯 내용이 변경될 때 호출됩니다.
    /// (XRSocketInteractor를 통해 아이템이 놓이거나 제거될 때)
    /// </summary>
    private void OnCraftingGridContentChanged()
    {
        Debug.Log("VRCraftingUI: 제작 그리드 내용 변경 감지. 레시피 확인을 시작합니다.");
        CheckForRecipe();
    }

    /// <summary>
    /// 결과 슬롯 클릭 처리 (제작 실행)
    /// </summary>
    /// <param name="slotIndex">클릭된 슬롯의 인덱스 (결과 슬롯은 -1)</param>
    /// <param name="itemStack">클릭된 슬롯의 아이템 스택 (제작 결과물)</param>
    private void OnResultSlotClicked(int slotIndex, ItemStack itemStack)
    {
        if (currentRecipe == null || itemStack == null || itemStack.IsEmpty)
        {
            Debug.Log("VRCraftingUI: 제작할 수 있는 아이템이 없거나 결과물이 없습니다.");
            return;
        }

        // 제작 실행
        ExecuteCrafting();
    }

    /// <summary>
    /// 현재 제작 그리드에 있는 아이템들을 기반으로 매칭되는 레시피를 찾고 UI를 업데이트합니다.
    /// </summary>
    private void CheckForRecipe()
    {
        if (craftingManager == null) return;

        // 제작 그리드를 ItemStack 배열로 변환
        ItemStack[] gridItems = new ItemStack[9];
        for (int i = 0; i < 9; i++)
        {
            gridItems[i] = craftingGridSlots[i].ItemStack;
        }

        // 매칭되는 레시피 찾기
        currentRecipe = craftingManager.FindMatchingRecipe(gridItems);

        // UI 업데이트
        UpdateResultSlot();
        UpdateRecipeInfo();
    }

    /// <summary>
    /// 결과 슬롯을 현재 레시피의 결과물로 업데이트합니다.
    /// </summary>
    private void UpdateResultSlot()
    {
        if (resultSlot == null) return;

        if (currentRecipe != null)
        {
            // 결과 아이템 표시
            var resultItem = new ItemStack(currentRecipe.resultMaterial, currentRecipe.resultQuantity);
            resultSlot.SetItem(resultItem);
            Debug.Log($"VRCraftingUI: 결과 슬롯 업데이트 - {resultItem.GetDebugInfo()}");
        }
        else
        {
            // 결과 슬롯 비우기
            resultSlot.ClearSlot();
            Debug.Log("VRCraftingUI: 결과 슬롯 비움.");
        }
    }

    /// <summary>
    /// 레시피 이름 텍스트를 업데이트합니다.
    /// </summary>
    private void UpdateRecipeInfo()
    {
        if (recipeNameText != null)
        {
            if (currentRecipe != null)
            {
                recipeNameText.text = currentRecipe.recipeName;
                recipeNameText.color = Color.green; // 매칭 성공 시 녹색
            }
            else
            {
                recipeNameText.text = "레시피 없음";
                recipeNameText.color = Color.gray; // 매칭 실패 시 회색
            }
        }
    }

    /// <summary>
    /// 현재 매칭된 레시피를 기반으로 제작을 실행합니다.
    /// PlayerInventory를 통해 재료 소모 및 결과물 추가를 처리합니다.
    /// </summary>
    public void ExecuteCrafting()
    {
        if (currentRecipe == null || playerInventory == null)
        {
            Debug.LogWarning("VRCraftingUI: 제작을 실행할 수 없습니다. 레시피가 없거나 인벤토리 참조가 없습니다.");
            return;
        }

        // PlayerInventory를 통한 제작 실행 (재료 소모 및 결과물 추가)
        bool success = playerInventory.ExecuteRecipe(currentRecipe);

        if (success)
        {
            Debug.Log($"VRCraftingUI: '{currentRecipe.recipeName}' 제작 완료!");

            // 제작 그리드 비우기 (재료 소모 후)
            ClearCraftingGrid();

            // UI 새로고침 (결과 슬롯 및 레시피 정보 업데이트)
            currentRecipe = null; // 레시피 초기화
            UpdateResultSlot();
            UpdateRecipeInfo();

            OnCraftingCompleted?.Invoke(); // 제작 완료 이벤트 발생 (VRUIManager에서 사용)
        }
        else
        {
            Debug.LogWarning("VRCraftingUI: 제작에 실패했습니다. 재료가 부족하거나 인벤토리가 가득 찼을 수 있습니다.");
        }
    }

    /// <summary>
    /// 제작 그리드의 모든 슬롯을 비웁니다.
    /// </summary>
    private void ClearCraftingGrid()
    {
        foreach (var slot in craftingGridSlots)
        {
            slot.ClearSlot();
        }
        Debug.Log("VRCraftingUI: 제작 그리드가 비워졌습니다.");
    }

    /// <summary>
    /// 제작 그리드의 특정 슬롯에 있는 아이템을 인벤토리로 반환합니다.
    /// </summary>
    /// <param name="slotIndex">반환할 아이템이 있는 제작 그리드 슬롯의 인덱스</param>
    private void ReturnItemToInventory(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= craftingGridSlots.Length || playerInventory == null)
        {
            Debug.LogWarning($"VRCraftingUI: 유효하지 않은 슬롯 인덱스 ({slotIndex})이거나 PlayerInventory가 없습니다.");
            return;
        }

        var slot = craftingGridSlots[slotIndex];
        if (slot.IsEmpty) return; // 슬롯이 비어있으면 아무것도 하지 않음

        var itemStack = slot.ItemStack;
        bool success = playerInventory.AddItem(itemStack.material, itemStack.Quantity);

        if (success)
        {
            slot.ClearSlot(); // 인벤토리로 성공적으로 이동했으면 제작 그리드 슬롯 비움
            Debug.Log($"VRCraftingUI: {itemStack.material.materialName} x{itemStack.Quantity}을 인벤토리로 반환했습니다.");
        }
        else
        {
            Debug.LogWarning($"VRCraftingUI: 인벤토리가 가득 차서 '{itemStack.material.materialName}'을(를) 반환할 수 없습니다.");
        }
    }

    /// <summary>
    /// 제작 UI의 표시/숨김 상태를 토글합니다.
    /// </summary>
    public void ToggleCrafting()
    {
        SetVisibility(!isCraftingOpen);
    }

    /// <summary>
    /// 제작 UI의 표시 상태를 설정합니다.
    /// </summary>
    /// <param name="visible">true면 표시, false면 숨김</param>
    public void SetVisibility(bool visible)
    {
        isCraftingOpen = visible;
        gameObject.SetActive(visible); // GameObject 활성화/비활성화

        if (craftingCanvas != null)
        {
            craftingCanvas.enabled = visible; // Canvas 컴포넌트 활성화/비활성화 (렌더링 제어)
        }

        OnCraftingToggled?.Invoke(); // 상태 변경 이벤트 발생

        Debug.Log($"VRCraftingUI: 제작 UI {(visible ? "열림" : "닫힘")}");
    }

    /// <summary>
    /// 인벤토리에서 제작 그리드로 아이템을 추가합니다.
    /// 이 메서드는 InventorySlotUI의 XRSocketInteractor를 통한 드래그 앤 드롭이 아닌,
    /// 인벤토리 UI에서 선택된 아이템을 제작 그리드로 '전송'하는 등의 추가적인 상호작용에 사용될 수 있습니다.
    /// (현재는 직접 사용되지 않을 수 있지만, 확장성을 위해 유지)
    /// </summary>
    /// <param name="material">추가할 재료</param>
    /// <param name="quantity">추가할 수량 (주로 1)</param>
    /// <param name="targetSlotIndex">아이템을 놓을 제작 그리드 슬롯의 인덱스</param>
    /// <returns>아이템 추가 성공 여부</returns>
    public bool AddItemToCraftingGrid(CraftingMaterial material, int quantity, int targetSlotIndex)
    {
        if (targetSlotIndex < 0 || targetSlotIndex >= craftingGridSlots.Length)
        {
            Debug.LogWarning($"VRCraftingUI: AddItemToCraftingGrid: 유효하지 않은 슬롯 인덱스: {targetSlotIndex}");
            return false;
        }
        if (material == null || playerInventory == null)
        {
            Debug.LogWarning("VRCraftingUI: AddItemToCraftingGrid: 재료 또는 인벤토리 참조가 없습니다.");
            return false;
        }

        // 인벤토리에 해당 아이템이 충분히 있는지 확인
        if (!playerInventory.HasEnoughItems(material, quantity))
        {
            Debug.LogWarning($"VRCraftingUI: AddItemToCraftingGrid: 인벤토리에 {material.materialName} x{quantity}가 부족합니다.");
            return false;
        }

        // 제작 그리드 슬롯이 비어있는지 확인 (또는 스택 가능 여부 확인)
        var targetSlot = craftingGridSlots[targetSlotIndex];
        if (!targetSlot.IsEmpty && (targetSlot.ItemStack.material != material || !material.isStackable))
        {
            Debug.LogWarning($"VRCraftingUI: AddItemToCraftingGrid: 슬롯 {targetSlotIndex}가 이미 차 있거나 다른 아이템이 있습니다.");
            return false; // 슬롯이 비어있지 않고, 같은 아이템이 아니거나 스택 불가능
        }

        // 인벤토리에서 아이템 제거
        int removed = playerInventory.RemoveItem(material, quantity);
        if (removed > 0)
        {
            // 제작 그리드에 아이템 추가 (기존 스택에 추가 또는 새 스택 생성)
            if (targetSlot.IsEmpty)
            {
                targetSlot.SetItem(new ItemStack(material, removed));
            }
            else
            {
                targetSlot.ItemStack.AddItems(removed);
                targetSlot.UpdateVisuals(); // 수량 변경 시 시각 업데이트
            }

            Debug.Log($"VRCraftingUI: {material.materialName} x{removed}을(를) 제작 그리드 슬롯 {targetSlotIndex}에 추가했습니다.");
            CheckForRecipe(); // 그리드 내용 변경 시 레시피 확인
            return true;
        }

        return false;
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
        if (graphicRaycaster == null && craftingCanvas != null)
        {
            graphicRaycaster = craftingCanvas.gameObject.AddComponent<GraphicRaycaster>();
            Debug.Log("VRCraftingUI: GraphicRaycaster가 Canvas에 추가되었습니다.");
        }

        // XRRayInteractor의 raycastMask 등을 설정할 수 있습니다.
        // rayInteractor.raycastMask = LayerMask.GetMask("UI"); // 예시: UI 레이어에만 레이캐스트
    }

    /// <summary>
    /// 제작 UI 상태 디버그 출력
    /// </summary>
    [ContextMenu("제작 UI 상태 출력")]
    public void PrintCraftingStatus()
    {
        Debug.Log("=== VR 제작 UI 상태 ===");
        Debug.Log($"열림 상태: {isCraftingOpen}");
        Debug.Log($"현재 레시피: {(currentRecipe != null ? currentRecipe.recipeName : "없음")}");

        Debug.Log("제작 그리드 내용:");
        for (int i = 0; i < craftingGridSlots.Length; i++)
        {
            var slot = craftingGridSlots[i];
            Debug.Log($"  슬롯 {i}: {(slot.IsEmpty ? "비어있음" : slot.GetDebugInfo())}");
        }

        Debug.Log("====================");
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        foreach (var slotUI in craftingGridSlots)
        {
            if (slotUI != null)
            {
                slotUI.OnSlotClicked -= OnCraftingSlotClicked;
                slotUI.OnSlotHovered -= OnCraftingSlotHovered;
                slotUI.OnSlotContentChanged -= OnCraftingGridContentChanged;
            }
        }
        if (resultSlot != null)
        {
            resultSlot.OnSlotClicked -= OnResultSlotClicked;
        }
    }
}
