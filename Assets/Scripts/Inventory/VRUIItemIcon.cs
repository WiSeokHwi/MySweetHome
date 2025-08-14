/// <summary>
/// ==================== VR 인벤토리 UI 아이템 아이콘 시스템 ====================
/// 
/// 【 시스템 개요 】
/// VR 인벤토리에서 각 아이템을 시각적으로 표현하는 UI 컴포넌트입니다.
/// 드래그 앤 드롭, 클릭 상호작용, 월드 아이템 생성 등의 기능을 제공합니다.
/// 
/// 【 주요 기능 】
/// 1. 아이템 시각화 (아이콘 이미지, 수량 텍스트)
/// 2. 드래그 앤 드롭 (슬롯 간 이동)
/// 3. 월드 드롭 (인벤토리에서 월드로 아이템 생성)
/// 4. 클릭 상호작용 (아이템 정보 표시)
/// 
/// 【 연동 시스템 】
/// - InventoryManager: 슬롯 데이터 및 아이템 이동 처리
/// - VRUISlot: 드래그 앤 드롭 대상 슬롯 검색
/// - InventoryUIManager: 아이템 클릭 시 정보 표시
/// 
/// 【 데이터 저장 위치 】
/// - currentSlotIndex: 현재 소속 슬롯 번호 (이 클래스에서 관리)
/// - slotTransform: 소속 슬롯의 Transform 참조 (이 클래스에서 관리)
/// - 아이템 데이터: InventoryManager.inventorySlots[currentSlotIndex]에서 참조
/// 
/// 【 상호작용 흐름 】
/// 1. 클릭: OnPointerDown → OnPointerUp (드래그 없음) → OnItemClicked → InventoryUIManager.SelectSlot
/// 2. 슬롯 드래그: OnDrag (짧은 거리) → FindNearestSlot → InventoryManager.MoveItem
/// 3. 월드 드롭: OnDrag (긴 거리) → DropItemToWorld → CreateWorldItem → 월드에 GameObject 생성
/// </summary>
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VRUIItemIcon : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    // ========== 슬롯 연결 데이터 ==========
    /// <summary>
    /// 【데이터 저장】현재 이 아이콘이 속한 슬롯의 인덱스
    /// 【업데이트 위치】InventoryManager.SwapSlots()에서 슬롯 교체 시 업데이트
    /// 【참조 위치】OnItemClicked(), DropItemToWorld() 등에서 슬롯 데이터 접근 시 사용
    /// </summary>
    public int currentSlotIndex;
    
    /// <summary>
    /// 【데이터 저장】소속 슬롯의 Transform 참조 (위치 동기화용)
    /// 【설정 위치】InventoryManager.Awake()에서 SetSlotTransform() 호출 시
    /// 【사용 위치】ResetPositionToSlot()에서 아이콘 위치 복원 시
    /// </summary>
    private Transform slotTransform;

    // ========== UI 컴포넌트 ==========
    [SerializeField] private Image iconImage;        // 아이템 썸네일 이미지
    [SerializeField] private TMP_Text quantityText; // 아이템 수량 텍스트

    // ========== 드래그 상태 관리 ==========
    /// <summary>
    /// 【상태 저장】드래그 시작 시의 원래 위치 (복원용)
    /// 【업데이트 위치】OnPointerDown()에서 드래그 시작 시 현재 위치 저장
    /// </summary>
    private Vector3 originalPosition;
    
    /// <summary>
    /// 【상태 저장】현재 드래그 중인지 여부
    /// 【업데이트 위치】OnPointerDown(true), OnPointerUp(false)
    /// </summary>
    private bool isDragging;
    
    /// <summary>
    /// 【상태 저장】실제로 드래그가 발생했는지 여부 (클릭과 구분하기 위함)
    /// 【업데이트 위치】OnDrag()에서 true로 설정, OnPointerDown()에서 false로 초기화
    /// </summary>
    private bool wasDragged;

    private RectTransform rectTransform;

    /// <summary>
    /// 【Unity 생명주기】컴포넌트 초기화
    /// 【처리 내용】RectTransform 캐시
    /// </summary>
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// 【Unity 이벤트】마우스/터치 다운 시 호출
    /// 【처리 내용】드래그 상태 초기화 및 원래 위치 저장
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        wasDragged = false;
        originalPosition = rectTransform.position; // 드래그 시작 위치 저장
    }

    /// <summary>
    /// 【Unity 이벤트】드래그 중일 때 호출 (연속 호출)
    /// 【처리 내용】아이콘을 마우스 포인터 위치로 이동
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        wasDragged = true; // 드래그가 발생했음을 표시 (클릭과 구분)

        // 스크린 좌표를 월드 좌표로 변환하여 아이콘 이동
        Vector3 worldPos;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out worldPos))
        {
            rectTransform.position = worldPos;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;

        // 드래그하지 않고 단순 클릭인 경우
        if (!wasDragged)
        {
            OnItemClicked();
            return;
        }

        // 드래그 거리 계산 (월드 좌표에서 0.5 이상이면 월드 드롭)
        float dragDistance = Vector3.Distance(rectTransform.position, originalPosition);
        
        // 월드 좌표에서 0.5 이상 이동하면 월드 드롭으로 간주
        if (dragDistance > 0.5f)
        {
            DropItemToWorld();
            return;
        }

        // 가까운 거리 드래그면 슬롯 이동 시도
        VRUISlot nearestSlot = FindNearestSlot();
        if (nearestSlot != null)
        {
            int fromIndex = currentSlotIndex;
            int toIndex = nearestSlot.slotIndex;

            if (fromIndex != toIndex)
            {
                InventoryManager.Instance.MoveItem(fromIndex, toIndex);
            }
            else
            {
                ResetPositionToSlot();
            }
        }
        else
        {
            ResetPositionToSlot();
        }
    }

    VRUISlot FindNearestSlot()
    {
        VRUISlot[] slots = InventoryManager.Instance.slotUIList;
        VRUISlot nearest = null;
        float minDist = float.MaxValue;

        Vector2 iconScreenPos = RectTransformUtility.WorldToScreenPoint(null, rectTransform.position);

        foreach (var slot in slots)
        {
            Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(null, slot.transform.position);
            float dist = Vector2.Distance(iconScreenPos, slotScreenPos);

            if (dist < minDist && dist <= 5f) // 반경 30픽셀 이내의 슬롯만 고려 (정확한 슬롯 드롭만 허용)
            {
                minDist = dist;
                nearest = slot;
            }
        }
        return nearest;
    }

    public void UpdateUI(ItemData item, int quantity)
    {
        if (item == null)
        {
            iconImage.enabled = false;    // 이미지 비활성
            quantityText.text = "";       // 수량 텍스트 초기화
        }
        else
        {
            iconImage.enabled = true;
            iconImage.sprite = item.thumbnail;
            quantityText.text = quantity > 1 ? quantity.ToString() : "";
        }
    }

    public void SetSlotTransform(Transform slotTrans)
    {
        slotTransform = slotTrans;
        
        // rectTransform이 null이면 초기화
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
        
        ResetPositionToSlot();
        originalPosition = rectTransform.position; // 현재 위치를 원위치로 저장
    }

    public void ResetPositionToSlot()
    {
        if (slotTransform != null)
        {
            transform.position = slotTransform.position;
            transform.rotation = slotTransform.rotation;
        }
    }

    private void DropItemToWorld()
    {
        // 안전성 검사
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("InventoryManager.Instance가 null입니다.");
            ResetPositionToSlot();
            return;
        }

        if (currentSlotIndex < 0 || currentSlotIndex >= InventoryManager.Instance.inventorySlots.Count)
        {
            Debug.LogError($"잘못된 슬롯 인덱스: {currentSlotIndex}");
            ResetPositionToSlot();
            return;
        }

        var slotData = InventoryManager.Instance.inventorySlots[currentSlotIndex];
        
        if (slotData.item == null) 
        {
            Debug.LogWarning("슬롯에 아이템이 없습니다.");
            ResetPositionToSlot();
            return;
        }

        ItemData itemData = slotData.item;
        int quantity = slotData.quantity;

        // GameModel이 없으면 경고하고 원위치
        if (itemData.gameModel == null)
        {
            Debug.LogWarning($"아이템 {itemData.itemName}에 GameModel이 설정되지 않았습니다.");
            ResetPositionToSlot();
            return;
        }

        // 월드 위치 계산 (VR 카메라 앞쪽에 생성)
        Vector3 worldDropPosition = GetWorldDropPosition();

        // 스택이 가능한 아이템인 경우 각각 생성
        if (itemData.isStackable && quantity > 1)
        {
            for (int i = 0; i < quantity; i++)
            {
                CreateWorldItem(itemData, worldDropPosition + Vector3.right * i * 0.3f);
            }
        }
        else
        {
            CreateWorldItem(itemData, worldDropPosition);
        }

        // 인벤토리에서 아이템 제거
        InventoryManager.Instance.RemoveItemFromSlot(currentSlotIndex);

        Debug.Log($"{itemData.itemName} {quantity}개를 월드에 드롭했습니다.");
    }

    private Vector3 GetWorldDropPosition()
    {
        // VR 카메라 찾기
        Camera vrCamera = Camera.main;
        if (vrCamera == null)
        {
            vrCamera = FindAnyObjectByType<Camera>();
        }

        if (vrCamera != null)
        {
            // 카메라 앞쪽 2미터 지점에 생성
            return vrCamera.transform.position + vrCamera.transform.forward * 2f;
        }
        else
        {
            // VR 카메라를 찾을 수 없으면 월드 원점에 생성
            Debug.LogWarning("VR 카메라를 찾을 수 없어 원점에 아이템을 생성합니다.");
            return Vector3.zero;
        }
    }

    private void CreateWorldItem(ItemData itemData, Vector3 position)
    {
        if (itemData == null)
        {
            Debug.LogError("CreateWorldItem: itemData가 null입니다.");
            return;
        }

        if (itemData.gameModel == null)
        {
            Debug.LogError($"CreateWorldItem: {itemData.itemName}의 gameModel이 null입니다.");
            return;
        }

        try
        {
            GameObject worldItem = Instantiate(itemData.gameModel, position, Quaternion.identity);
            
            if (worldItem == null)
            {
                Debug.LogError($"CreateWorldItem: {itemData.itemName}의 gameModel 인스턴스화에 실패했습니다.");
                return;
            }
            
            // EquipmentData인지 확인하여 다른 컴포넌트 적용
            if (itemData is EquipmentData equipmentData)
            {
                // 장비 아이템은 ToolItem 사용 (도구 기능 + 집기)
                ToolItem toolComponent = worldItem.GetComponent<ToolItem>();
                if (toolComponent == null)
                {
                    toolComponent = worldItem.AddComponent<ToolItem>();
                    Debug.Log($"{itemData.itemName} (도구)에 ToolItem 컴포넌트를 자동으로 추가했습니다.");
                }
                
                // 데이터 할당
                toolComponent.toolData = equipmentData;
            }
            else
            {
                // 일반 아이템은 PlacableItem 사용 (집기 + 배치 가능)
                PlacableItem placableComponent = worldItem.GetComponent<PlacableItem>();
                if (placableComponent == null)
                {
                    placableComponent = worldItem.AddComponent<PlacableItem>();
                    Debug.Log($"{itemData.itemName} (일반 아이템)에 PlacableItem 컴포넌트를 자동으로 추가했습니다.");
                }
                placableComponent.itemData = itemData;
            }

            // 물리 효과를 위해 약간의 랜덤 힘 적용
            Rigidbody rb = worldItem.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 randomForce = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(0.5f, 1.5f),
                    Random.Range(-1f, 1f)
                );
                rb.AddForce(randomForce, ForceMode.Impulse);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CreateWorldItem 오류: {e.Message}");
        }
    }
    
    /// <summary>
    /// 아이템이 클릭되었을 때 호출 (드래그 없이 단순 클릭)
    /// </summary>
    private void OnItemClicked()
    {
        // InventoryManager에서 현재 슬롯의 아이템 정보 가져오기
        if (InventoryManager.Instance != null && 
            currentSlotIndex >= 0 && 
            currentSlotIndex < InventoryManager.Instance.inventorySlots.Count)
        {
            var slotData = InventoryManager.Instance.inventorySlots[currentSlotIndex];
            if (slotData.item != null)
            {
                // InventoryUIManager에 슬롯 선택 알림
                var inventoryUIManager = FindAnyObjectByType<InventoryUIManager>();
                if (inventoryUIManager != null)
                {
                    inventoryUIManager.SelectSlot(currentSlotIndex);
                    Debug.Log($"[VRUIItemIcon] 아이템 클릭: {slotData.item.itemName} (슬롯 {currentSlotIndex})");
                }
                else
                {
                    Debug.LogWarning("[VRUIItemIcon] InventoryUIManager를 찾을 수 없습니다.");
                }
            }
            else
            {
                Debug.Log($"[VRUIItemIcon] 빈 슬롯 클릭 (슬롯 {currentSlotIndex})");
            }
        }
    }
}
