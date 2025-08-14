/// <summary>
/// ==================== INVENTORY MANAGEMENT SYSTEM ====================
/// 
/// 【 시스템 개요 】
/// 게임 내 모든 인벤토리 데이터와 UI를 통합 관리하는 싱글톤 클래스입니다.
/// VR 환경에서의 아이템 저장, 이동, 스택 관리 등을 담당합니다.
/// 
/// 【 주요 책임 】
/// 1. 인벤토리 슬롯 데이터 관리 (SlotData 리스트)
/// 2. UI 아이콘 오브젝트 생성 및 동기화
/// 3. 아이템 추가/제거/이동 로직
/// 4. 스택 시스템 (같은 아이템 묶음 관리)
/// 
/// 【 연동 시스템 】
/// - GrabbableItem.TryAddToInventory(): 월드 아이템을 인벤토리로 이동
/// - VRUIItemIcon: 각 슬롯의 시각적 표현 및 드래그 앤 드롭
/// - InventoryUIManager: 인벤토리 창 열기/닫기 및 아이템 정보 표시
/// - VRUISlot: 각 인벤토리 슬롯의 UI 컨테이너
/// 
/// 【 데이터 저장 위치 】
/// - inventorySlots: 모든 슬롯의 아이템 데이터 (이 클래스에서 관리)
/// - iconObjects: 각 슬롯의 UI 아이콘 오브젝트 참조 (이 클래스에서 관리)
/// - Instance: 싱글톤 인스턴스 (이 클래스에서 관리)
/// 
/// 【 주요 호출 흐름 】
/// 1. 게임 시작 → Awake() → 슬롯 초기화 → UI 아이콘 생성
/// 2. 아이템 획득 → AddItemToInventory() → 스택/빈슬롯 찾기 → UI 업데이트
/// 3. 아이템 이동 → MoveItem() → 슬롯 간 데이터 교환 → UI 동기화
/// </summary>
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    // ========== 싱글톤 패턴 ==========
    /// <summary>
    /// 【접근 위치】GrabbableItem, InventoryUIManager, VRUIItemIcon 등에서 접근
    /// 【용도】전역에서 인벤토리 시스템에 접근하기 위한 싱글톤 인스턴스
    /// 【초기화】Awake()에서 설정
    /// </summary>
    public static InventoryManager Instance;

    // ========== 인벤토리 설정 ==========
    [Header("📦 인벤토리 기본 설정")]
    [Tooltip("인벤토리 최대 슬롯 수 - 이 값에 따라 slotUIList와 iconObjects 배열 크기 결정")]
    public int maxSlots = 10;
    
    /// <summary>
    /// 【데이터 저장】각 슬롯의 아이템 정보 (ItemData, quantity)
    /// 【업데이트 위치】AddItemToInventory(), MoveItem(), RemoveItemFromSlot()
    /// 【참조 위치】모든 인벤토리 관련 메서드에서 참조
    /// </summary>
    public List<SlotData> inventorySlots = new List<SlotData>();

    // ========== UI 연결 설정 ==========
    [Header("🎨 UI 연결 설정")]
    [Tooltip("인벤토리 UI의 각 슬롯들 - Inspector에서 순서대로 할당")]
    public VRUISlot[] slotUIList;

    [Tooltip("아이템 아이콘 UI 프리팹 - VRUIItemIcon 컴포넌트가 있어야 함")]
    public GameObject itemIconPrefab;

    [Tooltip("생성된 아이콘들의 부모 Transform - 보통 Canvas 하위의 Panel")]
    public Transform iconParent;

    /// <summary>
    /// 【데이터 저장】각 슬롯에 대응하는 UI 아이콘 오브젝트들
    /// 【생성 위치】Awake()에서 itemIconPrefab으로부터 Instantiate
    /// 【업데이트 위치】UpdateSlotUI()에서 아이템 정보에 따라 표시/숨김 처리
    /// </summary>
    private VRUIItemIcon[] iconObjects;

    [Header("🎮 게임 시작 설정")]
    [Tooltip("게임 시작 시 자동으로 인벤토리에 추가할 기본 아이템들")]
    public ItemData[] defaultItem;

    // ==========================================================================
    // UNITY 생명주기 및 초기화
    // ==========================================================================
    
    /// <summary>
    /// 【Unity 생명주기】GameObject 생성 시 최초 1회 호출
    /// 【처리 내용】싱글톤 패턴 적용, 인벤토리 슬롯 및 UI 초기화
    /// </summary>
    void Awake()
    {
        // 싱글톤 패턴 구현 - 중복 인스턴스 방지
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 인벤토리 슬롯 데이터 초기화 (빈 슬롯으로 설정)
        inventorySlots.Clear();
        for (int i = 0; i < maxSlots; i++)
        {
            inventorySlots.Add(new SlotData()
            {
                item = null,      // 아이템 없음
                quantity = 0      // 수량 0
            });
        }

        // UI 아이콘 오브젝트 배열 초기화
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
