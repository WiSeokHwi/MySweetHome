// VRUIItemIcon.cs
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VRUIItemIcon : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public int currentSlotIndex;
    private Transform slotTransform; // 슬롯의 Transform 저장

    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text quantityText;

    private Vector3 originalPosition;
    private bool isDragging;

    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Start()
    {
        // 초기 originalPosition을 현재 위치로 설정하는 Start에서는 하지 않고
        // SetSlotTransform에서 위치 설정 후 originalPosition 저장하도록 변경
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        originalPosition = rectTransform.position; // 드래그 시작 위치 저장
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

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
            vrCamera = FindObjectOfType<Camera>();
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
}
