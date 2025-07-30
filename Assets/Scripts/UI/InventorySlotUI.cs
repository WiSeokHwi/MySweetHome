using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; // UI 이벤트 시스템 사용 (PointerClick, PointerEnter, PointerExit)
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors; // XR UI Interaction 및 XRSocketInteractor 사용

/// <summary>
/// InventorySlotUI - 인벤토리/제작대 UI의 개별 슬롯을 관리
/// VR 환경에서 3D 공간에 배치되는 인벤토리 슬롯
/// </summary>
// UI 이벤트 인터페이스 구현 (VR 레이캐스트 상호작용을 위해 필요)
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject emptySlotIndicator; // 빈 슬롯일 때 표시할 오브젝트 (선택 사항)

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Color selectedColor = Color.green;

    [Header("Interaction (VR Socket)")]
    [Tooltip("이 슬롯에 아이템을 드랍할 수 있는 XRSocketInteractor (VR 드래그 앤 드롭용)")]
    [SerializeField] private XRSocketInteractor socketInteractor; // XRSocketInteractor 필드 추가

    private ItemStack currentItemStack;
    private int slotIndex;
    private bool isSelected = false;
    private bool isHighlighted = false;

    // 이벤트 (VRInventoryUI, VRCraftingUI 등에서 구독)
    public System.Action<int, ItemStack> OnSlotClicked;
    public System.Action<int> OnSlotHovered;
    public System.Action OnSlotContentChanged; // 슬롯 내용 변경 시 알림 (예: 아이템 추가/제거/수량 변경)

    public ItemStack ItemStack => currentItemStack;
    public int SlotIndex => slotIndex;
    public bool IsEmpty => currentItemStack == null || currentItemStack.IsEmpty; // ItemStack의 IsEmpty 속성 활용

    void Awake() // Start 대신 Awake에서 초기화하여 다른 스크립트에서 참조 가능하도록
    {
        // UI 참조 자동 할당 (없을 경우)
        if (itemIcon == null) itemIcon = transform.Find("ItemIcon")?.GetComponent<Image>();
        if (quantityText == null) quantityText = transform.Find("QuantityText")?.GetComponent<TextMeshProUGUI>();
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (emptySlotIndicator == null) emptySlotIndicator = transform.Find("EmptySlotIndicator")?.gameObject;

        // XRSocketInteractor가 없으면 찾아서 할당 (에디터에서 수동 할당 권장)
        if (socketInteractor == null)
        {
            socketInteractor = GetComponent<XRSocketInteractor>();
        }

        ClearSlot(); // 초기 상태는 빈 슬롯으로 설정
    }

    void OnEnable()
    {
        // XRSocketInteractor 이벤트 리스너 등록
        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.AddListener(OnSocketItemPlaced);
            socketInteractor.selectExited.AddListener(OnSocketItemRemoved);
        }
    }

    void OnDisable()
    {
        // XRSocketInteractor 이벤트 리스너 해제
        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.RemoveListener(OnSocketItemPlaced);
            socketInteractor.selectExited.RemoveListener(OnSocketItemRemoved);
        }
    }

    /// <summary>
    /// 슬롯 초기화
    /// </summary>
    /// <param name="index">이 슬롯의 인덱스</param>
    public void Initialize(int index)
    {
        slotIndex = index;
        gameObject.name = $"Slot_{index}"; // 오브젝트 이름 설정 (디버깅 용이)
        UpdateVisuals(); // 초기 시각 업데이트
    }

    /// <summary>
    /// 슬롯에 아이템 설정
    /// </summary>
    /// <param name="itemStack">설정할 아이템 스택</param>
    public void SetItem(ItemStack itemStack)
    {
        // 변경 감지: 아이템 종류 및 수량이 같으면 리턴 (이벤트 호출 방지)
        bool sameMaterial = (currentItemStack?.material == itemStack?.material);
        bool sameQuantity = (currentItemStack?.Quantity == itemStack?.Quantity);
        if (sameMaterial && sameQuantity)
            return;

        currentItemStack = itemStack;
        UpdateVisuals(); // 시각 업데이트
        OnSlotContentChanged?.Invoke(); // 내용 변경 알림
    }

    /// <summary>
    /// 슬롯 비우기
    /// </summary>
    public void ClearSlot()
    {
        // 빈 아이템으로 세팅 (null 대신 빈 ItemStack 사용)
        SetItem(new ItemStack(null, 0));
    }

    /// <summary>
    /// 시각적 요소 업데이트 (아이콘, 수량 텍스트, 빈 슬롯 표시 등)
    /// </summary>
    public void UpdateVisuals()
    {
        bool hasItem = !IsEmpty; // ItemStack의 IsEmpty 속성 활용

        // 아이템 아이콘 활성화/비활성화 및 설정
        if (itemIcon != null)
        {
            itemIcon.gameObject.SetActive(hasItem);
            if (hasItem && currentItemStack.material != null && currentItemStack.material.icon != null)
            {
                itemIcon.sprite = currentItemStack.material.icon;
                itemIcon.color = Color.white; // 아이콘 색상 초기화
            }
        }

        // 수량 텍스트 활성화/비활성화 및 설정 (수량이 1보다 클 때만 표시)
        if (quantityText != null)
        {
            quantityText.gameObject.SetActive(hasItem && currentItemStack.Quantity > 1);
            if (hasItem)
            {
                quantityText.text = currentItemStack.Quantity.ToString();
            }
        }

        // 빈 슬롯 표시기 활성화/비활성화
        if (emptySlotIndicator != null)
        {
            emptySlotIndicator.SetActive(!hasItem);
        }

        // 배경색 업데이트 (하이라이트, 선택 상태 반영)
        UpdateBackgroundColor();
    }

    /// <summary>
    /// 배경색 업데이트 (normal, highlight, selected 색상 반영)
    /// </summary>
    private void UpdateBackgroundColor()
    {
        if (backgroundImage == null) return;

        Color targetColor = normalColor;

        if (isSelected)
            targetColor = selectedColor;
        else if (isHighlighted)
            targetColor = highlightColor;

        backgroundImage.color = targetColor;
    }

    /// <summary>
    /// 슬롯 선택 상태 설정
    /// </summary>
    /// <param name="selected">선택 여부</param>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateBackgroundColor();
    }

    /// <summary>
    /// 슬롯 하이라이트 상태 설정 (호버 시)
    /// </summary>
    /// <param name="highlighted">하이라이트 여부</param>
    public void SetHighlighted(bool highlighted)
    {
        isHighlighted = highlighted;
        UpdateBackgroundColor();
    }

    // --- UI Event System Handlers (IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler) ---
    // 이 함수들은 Event Trigger 컴포넌트를 통해 VR Ray Interactor에 의해 호출됩니다.

    /// <summary>
    /// 슬롯 클릭 처리 (VR 컨트롤러 레이캐스트 클릭 또는 마우스 클릭)
    /// </summary>
    /// <param name="eventData">클릭 이벤트 데이터</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        OnSlotClicked?.Invoke(slotIndex, currentItemStack);
        Debug.Log($"[InventorySlotUI] Slot {slotIndex} clicked. Item: {currentItemStack?.GetDebugInfo() ?? "Empty"}");
    }

    /// <summary>
    /// 슬롯 호버 시작 처리 (VR 레이캐스트 호버 또는 마우스 호버)
    /// </summary>
    /// <param name="eventData">호버 이벤트 데이터</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        OnSlotHovered?.Invoke(slotIndex);
        Debug.Log($"[InventorySlotUI] Slot {slotIndex} hovered.");
    }

    /// <summary>
    /// 슬롯 호버 종료 처리
    /// </summary>
    /// <param name="eventData">호버 이벤트 데이터</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighlighted(false); // 호버 해제 시 하이라이트 제거
        Debug.Log($"[InventorySlotUI] Slot {slotIndex} hover exited.");
    }

    /// <summary>
    /// 아이템을 다른 슬롯으로 이동 시도
    /// (이 메서드는 InventoryManagerUI 또는 VRCraftingUI에서 호출됩니다.)
    /// </summary>
    /// <param name="targetSlot">아이템을 이동할 대상 슬롯</param>
    /// <returns>이동 성공 여부</returns>
    public bool TryMoveItemTo(InventorySlotUI targetSlot)
    {
        if (IsEmpty || targetSlot == null) return false; // 현재 슬롯이 비어있거나 대상 슬롯이 없으면 이동 불가

        // 같은 아이템이고, 스택 가능한 아이템이면 스택 합치기 시도
        if (!targetSlot.IsEmpty &&
            targetSlot.ItemStack.material == currentItemStack.material &&
            currentItemStack.material.isStackable) // 현재 아이템이 스택 가능해야 합칠 수 있음
        {
            int maxStack = currentItemStack.material.maxStackSize;
            int availableSpace = maxStack - targetSlot.ItemStack.Quantity; // 대상 슬롯에 남은 공간

            if (availableSpace > 0)
            {
                int transferAmount = Mathf.Min(availableSpace, currentItemStack.Quantity); // 이동 가능한 수량

                // 아이템 이동 (대상 슬롯에 추가, 현재 슬롯에서 제거)
                targetSlot.ItemStack.AddItems(transferAmount);
                currentItemStack.RemoveItems(transferAmount);

                // 현재 슬롯이 비었으면 완전히 비움
                if (currentItemStack.IsEmpty)
                {
                    ClearSlot();
                }

                // 시각 업데이트
                UpdateVisuals();
                targetSlot.UpdateVisuals();

                return true;
            }
        }
        // 대상 슬롯이 비어있으면 현재 아이템을 통째로 이동
        else if (targetSlot.IsEmpty)
        {
            targetSlot.SetItem(currentItemStack); // 대상 슬롯에 현재 아이템 설정
            ClearSlot(); // 현재 슬롯 비움
            return true;
        }
        // 다른 아이템이면 서로 교환
        else
        {
            var tempItem = currentItemStack; // 현재 아이템 임시 저장
            SetItem(targetSlot.ItemStack); // 현재 슬롯에 대상 슬롯 아이템 설정
            targetSlot.SetItem(tempItem); // 대상 슬롯에 임시 저장된 아이템 설정
            return true;
        }

        return false; // 어떤 조건에도 해당하지 않으면 이동 실패
    }

    /// <summary>
    /// 디버그 정보 출력
    /// </summary>
    /// <returns>슬롯의 상세 정보 문자열</returns>
    public string GetDebugInfo()
    {
        if (IsEmpty)
            return $"Slot {slotIndex}: Empty";
        else
            return $"Slot {slotIndex}: {currentItemStack.material.materialName} x{currentItemStack.Quantity}";
    }

    // --- XR Socket Interactor Event Handlers ---
    // XRSocketInteractor가 아이템을 감지하거나 놓았을 때 호출됩니다.

    /// <summary>
    /// XRSocketInteractor에 PickableItem이 놓였을 때 호출됩니다.
    /// </summary>
    /// <param name="args">선택 이벤트 인자</param>
    private void OnSocketItemPlaced(SelectEnterEventArgs args)
    {
        // 놓인 오브젝트에서 PickableItem 컴포넌트 찾기
        PickableItem placedPickableItem = args.interactableObject.transform.GetComponent<PickableItem>();
        if (placedPickableItem != null && placedPickableItem.itemData != null)
        {
            // 슬롯에 아이템 스택 설정
            SetItem(new ItemStack(placedPickableItem.itemData, placedPickableItem.quantity));

            // 월드에 있는 PickableItem 오브젝트는 제거하지 않음 (외부에서 관리)

            Debug.Log($"[InventorySlotUI] Socket: {ItemStack.GetDebugInfo()} 배치됨.");
            OnSlotContentChanged?.Invoke();
        }
        else
        {
            Debug.LogWarning($"[InventorySlotUI] Socket: 유효하지 않은 아이템이 슬롯에 놓였습니다. {args.interactableObject.transform.name}", this);
        }
    }

    /// <summary>
    /// XRSocketInteractor에서 PickableItem이 제거되었을 때 호출됩니다.
    /// </summary>
    /// <param name="args">선택 이벤트 인자</param>
    private void OnSocketItemRemoved(SelectExitEventArgs args)
    {
        // 슬롯 내용은 바로 비우지 않음 (외부에서 ClearSlot 호출 권장)
        Debug.Log($"[InventorySlotUI] Socket: 아이템 제거됨. (슬롯 내용 유지, 외부 로직에서 ClearSlot 필요)");
    }
}
