// InventoryManager.cs
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("최대 슬롯수")]
    public int maxSlots = 10;
    public List<SlotData> inventorySlots = new List<SlotData>();

    [Header("UI 슬롯들 (배치 순서)")]
    public VRUISlot[] slotUIList;

    [Header("아이템 아이콘들")]
    public GameObject itemIconPrefab;

    [Header("아이콘 부모 (UI 캔버스 안)")]
    public Transform iconParent;

    // 슬롯별 아이콘 오브젝트 저장
    private VRUIItemIcon[] iconObjects;

    [Header("기본 아이템 (게임 시작 시 인벤토리에 넣을 아이템들)")]
    public ItemData[] defaultItem;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 슬롯 데이터 초기화
        inventorySlots.Clear();
        for (int i = 0; i < maxSlots; i++)
        {
            inventorySlots.Add(new SlotData()
            {
                item = null,
                quantity = 0
            });
        }

        // 아이콘 오브젝트 배열 초기화
        iconObjects = new VRUIItemIcon[maxSlots];

        // 슬롯 인덱스 및 아이콘 초기화
        for (int i = 0; i < maxSlots; i++)
        {
            if (i >= slotUIList.Length)
            {
                Debug.LogError($"slotUIList.Length ({slotUIList.Length}) < maxSlots ({maxSlots}) - 슬롯 UI가 부족합니다!");
                break;
            }

            slotUIList[i].slotIndex = i;

            if (iconObjects[i] == null)
            {
                GameObject iconObj = Instantiate(itemIconPrefab, iconParent);
                iconObj.name = $"ItemIcon_{i}";
                iconObjects[i] = iconObj.GetComponent<VRUIItemIcon>();

                if (iconObjects[i] == null)
                {
                    Debug.LogError("itemIconPrefab에 VRUIItemIcon 컴포넌트가 없습니다!");
                    break;
                }

                iconObjects[i].currentSlotIndex = i;
                iconObjects[i].SetSlotTransform(slotUIList[i].transform);
            }

            UpdateSlotUI(i);
        }
    }

    private void Start()
    {
        // 기본 아이템 인벤토리에 추가
        for (int i = 0; i < defaultItem.Length && i < maxSlots; i++)
        {
            AddItemToSlot(i, defaultItem[i], 1);
        }
    }

    public void AddItemToSlot(int slotIndex, ItemData item, int quantity)
    {
        AddItemToInventory(item, quantity, slotIndex);
    }

    public bool AddItemToInventory(ItemData item, int quantity, int preferredSlot = -1)
    {
        if (item == null || quantity <= 0) return false;

        int remainingQuantity = quantity;

        // 1단계: 선호 슬롯이 있으면 먼저 시도
        if (preferredSlot >= 0 && preferredSlot < inventorySlots.Count)
        {
            remainingQuantity = TryAddToSlot(preferredSlot, item, remainingQuantity);
        }

        // 2단계: 같은 아이템이 있는 기존 스택에 추가 시도
        if (remainingQuantity > 0 && item.isStackable)
        {
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                if (i == preferredSlot) continue; // 이미 시도한 슬롯 제외
                
                var slot = inventorySlots[i];
                if (slot.item == item && slot.quantity < item.maxStackSize)
                {
                    remainingQuantity = TryAddToSlot(i, item, remainingQuantity);
                    if (remainingQuantity <= 0) break;
                }
            }
        }

        // 3단계: 빈 슬롯에 추가
        if (remainingQuantity > 0)
        {
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                if (i == preferredSlot) continue; // 이미 시도한 슬롯 제외
                
                if (inventorySlots[i].item == null)
                {
                    remainingQuantity = TryAddToSlot(i, item, remainingQuantity);
                    if (remainingQuantity <= 0) break;
                }
            }
        }

        // 남은 아이템이 있으면 경고
        if (remainingQuantity > 0)
        {
            Debug.LogWarning($"인벤토리가 가득 참: {item.itemName} {remainingQuantity}개를 추가할 수 없습니다.");
        }

        return remainingQuantity == 0; // 모든 아이템이 추가되었으면 true
    }

    private int TryAddToSlot(int slotIndex, ItemData item, int quantity)
    {
        var slot = inventorySlots[slotIndex];

        // 같은 아이템이 있고 스택 가능한 경우
        if (slot.item == item && item.isStackable)
        {
            int availableSpace = item.maxStackSize - slot.quantity;
            int addAmount = Mathf.Min(quantity, availableSpace);
            slot.quantity += addAmount;
            
            UpdateSlotUI(slotIndex);
            return quantity - addAmount;
        }
        // 빈 슬롯인 경우
        else if (slot.item == null)
        {
            slot.item = item;
            
            if (item.isStackable)
            {
                int addAmount = Mathf.Min(quantity, item.maxStackSize);
                slot.quantity = addAmount;
                
                UpdateSlotUI(slotIndex);
                return quantity - addAmount;
            }
            else
            {
                slot.quantity = 1;
                
                UpdateSlotUI(slotIndex);
                return quantity - 1;
            }
        }

        return quantity; // 추가할 수 없으면 원래 수량 반환
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= inventorySlots.Count) return;
        if (toIndex < 0 || toIndex >= inventorySlots.Count) return;
        if (fromIndex == toIndex) return;

        var fromSlot = inventorySlots[fromIndex];
        var toSlot = inventorySlots[toIndex];

        if (fromSlot.item == null) return;

        // 같은 아이템이고 스택 가능한 경우
        if (toSlot.item != null && toSlot.item == fromSlot.item && fromSlot.item.isStackable)
        {
            int maxStack = fromSlot.item.maxStackSize;
            int availableSpace = maxStack - toSlot.quantity;
            
            if (availableSpace > 0)
            {
                int transferAmount = Mathf.Min(fromSlot.quantity, availableSpace);
                toSlot.quantity += transferAmount;
                fromSlot.quantity -= transferAmount;
                
                // 원본 슬롯이 비었으면 완전히 제거
                if (fromSlot.quantity <= 0)
                {
                    fromSlot.item = null;
                    fromSlot.quantity = 0;
                }
            }
            else
            {
                // 스택이 가득 찬 경우 교체
                SwapSlots(fromIndex, toIndex);
            }
        }
        else
        {
            // 다른 아이템이거나 빈 슬롯이면 자리 교체
            SwapSlots(fromIndex, toIndex);
        }

        // UI 업데이트
        UpdateSlotUI(fromIndex);
        UpdateSlotUI(toIndex);

        // 아이콘 위치 동기화
        SyncIconPositions();
    }

    private void SwapSlots(int fromIndex, int toIndex)
    {
        var tempItem = inventorySlots[toIndex].item;
        var tempQuantity = inventorySlots[toIndex].quantity;

        inventorySlots[toIndex].item = inventorySlots[fromIndex].item;
        inventorySlots[toIndex].quantity = inventorySlots[fromIndex].quantity;

        inventorySlots[fromIndex].item = tempItem;
        inventorySlots[fromIndex].quantity = tempQuantity;

        // 아이콘 인덱스 업데이트
        iconObjects[toIndex].currentSlotIndex = toIndex;
        iconObjects[fromIndex].currentSlotIndex = fromIndex;
    }

    private void SyncIconPositions()
    {
        for (int i = 0; i < iconObjects.Length; i++)
        {
            if (iconObjects[i] != null)
            {
                iconObjects[i].ResetPositionToSlot();
            }
        }
    }

    public void RemoveItemFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventorySlots.Count) return;

        inventorySlots[slotIndex].item = null;
        inventorySlots[slotIndex].quantity = 0;

        UpdateSlotUI(slotIndex);
    }

    public void UpdateSlotUI(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventorySlots.Count) return;

        var slotData = inventorySlots[slotIndex];
        var icon = iconObjects[slotIndex];

        icon.UpdateUI(slotData.item, slotData.quantity);
    }
}

[System.Serializable]
public class SlotData
{
    public ItemData item;
    public int quantity;
}
