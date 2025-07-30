using UnityEngine;
using System.Collections.Generic;
using System; // Action 사용을 위해 추가

/// <summary>
/// PlayerInventory - 플레이어의 인벤토리 시스템
///
/// == 주요 기능 ==
/// 1. 아이템 추가/제거/검색
/// 2. 스택 관리 (같은 아이템 자동 합치기)
/// 3. 인벤토리 용량 제한
/// 4. 제작 시스템과 연동
/// 5. VR UI와 연동 준비
///
/// == 싱글톤 패턴 ==
/// - 게임 전체에서 하나의 인벤토리만 존재
/// - PlayerInventory.Instance로 어디서든 접근 가능
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    // 싱글톤 인스턴스
    private static PlayerInventory _instance;
    public static PlayerInventory Instance
    {
        get
        {
            if (_instance == null)
            {
                // 씬에서 PlayerInventory를 찾기
                _instance = FindAnyObjectByType<PlayerInventory>();

                if (_instance == null)
                {
                    // 없으면 새로 생성
                    GameObject inventoryObject = new GameObject("PlayerInventory");
                    _instance = inventoryObject.AddComponent<PlayerInventory>();
                    DontDestroyOnLoad(inventoryObject);
                    Debug.Log("[PlayerInventory] 자동으로 PlayerInventory 인스턴스를 생성했습니다.");
                }
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("인벤토리 설정")]
    [Tooltip("인벤토리 최대 슬롯 수")]
    [SerializeField] private int maxSlots = 36;
    [Tooltip("디버그 로그 출력 여부")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("현재 인벤토리 상태 (읽기 전용)")]
    [Tooltip("현재 인벤토리에 있는 아이템들")]
    [SerializeField] private List<ItemStack> inventorySlots = new List<ItemStack>();

    // 이벤트 시스템
    public Action<CraftingMaterial, int> OnItemAdded;    // 아이템 추가 시
    public Action<CraftingMaterial, int> OnItemRemoved;  // 아이템 제거 시
    public Action OnInventoryChanged;                    // 인벤토리 변경 시

    void Awake()
    {
        // 싱글톤 설정
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeInventory();

            if (enableDebugLogs)
                Debug.Log("[PlayerInventory] 인벤토리 시스템이 초기화되었습니다.");
        }
        else if (_instance != this)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[PlayerInventory] 인벤토리 인스턴스가 이미 존재합니다. 중복 오브젝트를 제거합니다.");
            Destroy(gameObject);
        }
    }

    private void InitializeInventory()
    {
        inventorySlots.Clear();

        for (int i = 0; i < maxSlots; i++)
        {
            inventorySlots.Add(null);
        }
    }

    public bool AddItem(CraftingMaterial material, int quantity)
    {
        if (material == null || quantity <= 0)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[PlayerInventory] 잘못된 아이템 추가 요청입니다: 재료가 없거나 수량이 0 이하입니다.");
            return false;
        }

        int remainingQuantity = quantity;

        if (material.isStackable)
        {
            for (int i = 0; i < inventorySlots.Count && remainingQuantity > 0; i++)
            {
                var slot = inventorySlots[i];
                if (slot != null && !slot.IsEmpty && slot.material == material && slot.Quantity < material.maxStackSize)
                {
                    int addableAmount = Mathf.Min(remainingQuantity, material.maxStackSize - slot.Quantity);
                    if (addableAmount > 0)
                    {
                        slot.AddItems(addableAmount);
                        remainingQuantity -= addableAmount;

                        if (enableDebugLogs)
                            Debug.Log($"[PlayerInventory] {material.materialName} x{addableAmount}을 기존 스택에 추가했습니다. (슬롯 {i}, 현재: {slot.Quantity})");
                    }
                }
            }
        }

        while (remainingQuantity > 0)
        {
            int emptySlotIndex = FindEmptySlot();
            if (emptySlotIndex == -1)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[PlayerInventory] 인벤토리가 가득 차서 {material.materialName} x{remainingQuantity}을 추가할 수 없습니다.");
                return remainingQuantity == quantity ? false : true;
            }

            int stackSize = material.isStackable ? Mathf.Min(remainingQuantity, material.maxStackSize) : 1;
            inventorySlots[emptySlotIndex] = new ItemStack(material, stackSize);
            remainingQuantity -= stackSize;

            if (enableDebugLogs)
                Debug.Log($"[PlayerInventory] {material.materialName} x{stackSize}을 새 슬롯에 추가했습니다. (슬롯 {emptySlotIndex})");
        }

        OnItemAdded?.Invoke(material, quantity - remainingQuantity);
        OnInventoryChanged?.Invoke();

        return true;
    }

    public int RemoveItem(CraftingMaterial material, int quantity)
    {
        if (material == null || quantity <= 0)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[PlayerInventory] 잘못된 아이템 제거 요청입니다: 재료가 없거나 수량이 0 이하입니다.");
            return 0;
        }

        int remainingToRemove = quantity;
        int totalRemoved = 0;

        for (int i = inventorySlots.Count - 1; i >= 0 && remainingToRemove > 0; i--)
        {
            var slot = inventorySlots[i];
            if (slot != null && !slot.IsEmpty && slot.material == material)
            {
                int removeAmount = Mathf.Min(remainingToRemove, slot.Quantity);
                int actualRemoved = slot.RemoveItems(removeAmount);
                remainingToRemove -= actualRemoved;
                totalRemoved += actualRemoved;

                if (slot.IsEmpty)
                {
                    inventorySlots[i] = null;
                }

                if (enableDebugLogs)
                    Debug.Log($"[PlayerInventory] {material.materialName} x{actualRemoved}을 제거했습니다. (슬롯 {i}, 남은 수량: {(slot != null ? slot.Quantity.ToString() : "0")})");
            }
        }

        if (totalRemoved > 0)
        {
            OnItemRemoved?.Invoke(material, totalRemoved);
            OnInventoryChanged?.Invoke();
        }

        if (remainingToRemove > 0 && enableDebugLogs)
        {
            Debug.LogWarning($"[PlayerInventory] {material.materialName} {remainingToRemove}개를 제거하지 못했습니다. 인벤토리에 부족합니다.");
        }

        return totalRemoved;
    }

    public int GetItemCount(CraftingMaterial material)
    {
        if (material == null) return 0;

        int totalCount = 0;
        foreach (var slot in inventorySlots)
        {
            if (slot != null && !slot.IsEmpty && slot.material == material)
            {
                totalCount += slot.Quantity;
            }
        }

        return totalCount;
    }

    public bool HasEnoughItems(CraftingMaterial material, int requiredQuantity)
    {
        return GetItemCount(material) >= requiredQuantity;
    }

    public bool CanCraftRecipe(CraftingRecipe recipe)
    {
        if (recipe == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[PlayerInventory] 제작 가능 여부 확인: 유효하지 않은 레시피입니다.");
            return false;
        }

        var requiredMaterials = recipe.GetRequiredMaterials();

        foreach (var requirement in requiredMaterials)
        {
            if (!HasEnoughItems(requirement.Key, requirement.Value))
            {
                if (enableDebugLogs)
                    Debug.Log($"[PlayerInventory] 제작 불가: {requirement.Key.materialName} x{requirement.Value} 부족 (보유: {GetItemCount(requirement.Key)})");
                return false;
            }
        }

        return true;
    }

    public bool ExecuteRecipe(CraftingRecipe recipe)
    {
        if (!CanCraftRecipe(recipe))
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[PlayerInventory] 제작 실패: 레시피 '{recipe?.recipeName ?? "NULL"}'를 제작할 수 없습니다 (재료 부족 등).");
            return false;
        }

        var requiredMaterials = recipe.GetRequiredMaterials();
        foreach (var requirement in requiredMaterials)
        {
            RemoveItem(requirement.Key, requirement.Value);
        }

        bool success = AddItem(recipe.resultMaterial, recipe.resultQuantity);

        if (success && enableDebugLogs)
        {
            Debug.Log($"[PlayerInventory] 제작 성공: {recipe.recipeName} → {recipe.resultMaterial.materialName} x{recipe.resultQuantity}");
        }
        else if (!success && enableDebugLogs)
        {
            Debug.LogError($"[PlayerInventory] 제작은 성공했으나 결과물 '{recipe.resultMaterial.materialName}'을(를) 인벤토리에 추가하지 못했습니다. 인벤토리가 가득 찼을 수 있습니다.");
        }

        return success;
    }

    private int FindEmptySlot()
    {
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (inventorySlots[i] == null || inventorySlots[i].IsEmpty)
                return i;
        }
        return -1;
    }

    // LINQ 대신 for문으로 메모리 누수 감소
    public int GetUsedSlotCount()
    {
        int count = 0;
        foreach (var slot in inventorySlots)
        {
            if (slot != null && !slot.IsEmpty)
                count++;
        }
        return count;
    }

    public void CompactInventory()
    {
        var compactedSlots = new List<ItemStack>();

        foreach (var slot in inventorySlots)
        {
            if (slot != null && !slot.IsEmpty)
                compactedSlots.Add(slot);
        }

        while (compactedSlots.Count < maxSlots)
        {
            compactedSlots.Add(null);
        }

        inventorySlots = compactedSlots;
        OnInventoryChanged?.Invoke();

        if (enableDebugLogs)
            Debug.Log("[PlayerInventory] 인벤토리가 정리되었습니다.");
    }

    [ContextMenu("인벤토리 초기화")]
    public void ClearInventory()
    {
        InitializeInventory();
        OnInventoryChanged?.Invoke();

        if (enableDebugLogs)
            Debug.Log("[PlayerInventory] 인벤토리가 완전히 초기화되었습니다.");
    }

    [ContextMenu("인벤토리 상태 출력")]
    public void PrintInventoryStatus()
    {
        Debug.Log("=== 인벤토리 상태 ===");
        Debug.Log($"사용 슬롯: {GetUsedSlotCount()}/{maxSlots}");

        for (int i = 0; i < inventorySlots.Count; i++)
        {
            var slot = inventorySlots[i];
            if (slot != null && !slot.IsEmpty)
            {
                Debug.Log($"슬롯 {i}: {slot.material.materialName} x{slot.Quantity}");
            }
            else
            {
                Debug.Log($"슬롯 {i}: 비어있음");
            }
        }
        Debug.Log("==================");
    }

    public ItemStack GetSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventorySlots.Count)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[PlayerInventory] 유효하지 않은 슬롯 인덱스 요청: {slotIndex}");
            return null;
        }

        return inventorySlots[slotIndex];
    }

    // 복사본 반환 대신 원본 리스트 반환 (외부 수정 주의)
    public List<ItemStack> GetAllSlots()
    {
        return inventorySlots;
    }

    [ContextMenu("테스트 아이템 추가")]
    public void AddTestItems()
    {
        if (enableDebugLogs)
            Debug.Log("[PlayerInventory] 테스트 아이템 추가 기능이 실행되었습니다. 실제 아이템 추가 로직을 구현해야 합니다.");
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
