using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
    public static PlayerInventory Instance { get; private set; }
    
    [Header("인벤토리 설정")]
    [Tooltip("인벤토리 최대 슬롯 수")]
    [SerializeField] private int maxSlots = 36;
    [Tooltip("디버그 로그 출력 여부")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("현재 인벤토리 상태 (읽기 전용)")]
    [Tooltip("현재 인벤토리에 있는 아이템들")]
    [SerializeField] private List<ItemStack> inventorySlots = new List<ItemStack>();
    
    // 이벤트 시스템
    public System.Action<CraftingMaterial, int> OnItemAdded;      // 아이템 추가 시
    public System.Action<CraftingMaterial, int> OnItemRemoved;    // 아이템 제거 시
    public System.Action OnInventoryChanged;                      // 인벤토리 변경 시
    
    void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시에도 유지
            InitializeInventory();
            
            if (enableDebugLogs)
                Debug.Log("[PlayerInventory] 인벤토리 시스템이 초기화되었습니다.");
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning("[PlayerInventory] 인벤토리 인스턴스가 이미 존재합니다. 중복 오브젝트를 제거합니다.");
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// 인벤토리 초기화
    /// </summary>
    private void InitializeInventory()
    {
        inventorySlots.Clear();
        
        // 빈 슬롯들로 초기화
        for (int i = 0; i < maxSlots; i++)
        {
            inventorySlots.Add(null);
        }
    }
    
    /// <summary>
    /// 아이템을 인벤토리에 추가
    /// </summary>
    /// <param name="material">추가할 재료</param>
    /// <param name="quantity">추가할 개수</param>
    /// <returns>성공적으로 추가되었는지 여부</returns>
    public bool AddItem(CraftingMaterial material, int quantity)
    {
        if (material == null || quantity <= 0)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[PlayerInventory] 잘못된 아이템 추가 요청입니다.");
            return false;
        }
        
        int remainingQuantity = quantity;
        
        // 1단계: 기존 스택에 추가 시도
        for (int i = 0; i < inventorySlots.Count && remainingQuantity > 0; i++)
        {
            var slot = inventorySlots[i];
            if (slot != null && slot.material == material)
            {
                int addableAmount = Mathf.Min(remainingQuantity, material.maxStackSize - slot.quantity);
                if (addableAmount > 0)
                {
                    slot.quantity += addableAmount;
                    remainingQuantity -= addableAmount;
                    
                    if (enableDebugLogs)
                        Debug.Log($"[PlayerInventory] {material.materialName} x{addableAmount}을 기존 스택에 추가했습니다. (슬롯 {i})");
                }
            }
        }
        
        // 2단계: 새로운 슬롯에 추가
        while (remainingQuantity > 0)
        {
            int emptySlotIndex = FindEmptySlot();
            if (emptySlotIndex == -1)
            {
                // 인벤토리가 가득 참
                if (enableDebugLogs)
                    Debug.LogWarning($"[PlayerInventory] 인벤토리가 가득 차서 {material.materialName} x{remainingQuantity}을 추가할 수 없습니다.");
                return remainingQuantity == quantity ? false : true; // 일부라도 추가되었으면 true
            }
            
            int stackSize = Mathf.Min(remainingQuantity, material.maxStackSize);
            inventorySlots[emptySlotIndex] = new ItemStack(material, stackSize);
            remainingQuantity -= stackSize;
            
            if (enableDebugLogs)
                Debug.Log($"[PlayerInventory] {material.materialName} x{stackSize}을 새 슬롯에 추가했습니다. (슬롯 {emptySlotIndex})");
        }
        
        // 이벤트 발생
        OnItemAdded?.Invoke(material, quantity - remainingQuantity);
        OnInventoryChanged?.Invoke();
        
        return true;
    }
    
    /// <summary>
    /// 아이템을 인벤토리에서 제거
    /// </summary>
    /// <param name="material">제거할 재료</param>
    /// <param name="quantity">제거할 개수</param>
    /// <returns>실제로 제거된 개수</returns>
    public int RemoveItem(CraftingMaterial material, int quantity)
    {
        if (material == null || quantity <= 0)
            return 0;
        
        int remainingToRemove = quantity;
        int totalRemoved = 0;
        
        // 뒤에서부터 제거 (최신 추가된 것부터)
        for (int i = inventorySlots.Count - 1; i >= 0 && remainingToRemove > 0; i--)
        {
            var slot = inventorySlots[i];
            if (slot != null && slot.material == material)
            {
                int removeAmount = Mathf.Min(remainingToRemove, slot.Quantity);
                slot.Quantity -= removeAmount;
                remainingToRemove -= removeAmount;
                totalRemoved += removeAmount;
                
                // 스택이 비었으면 슬롯 정리
                if (slot.Quantity <= 0)
                {
                    inventorySlots[i] = null;
                }
                
                if (enableDebugLogs)
                    Debug.Log($"[PlayerInventory] {material.materialName} x{removeAmount}을 제거했습니다. (슬롯 {i})");
            }
        }
        
        // 이벤트 발생
        if (totalRemoved > 0)
        {
            OnItemRemoved?.Invoke(material, totalRemoved);
            OnInventoryChanged?.Invoke();
        }
        
        return totalRemoved;
    }
    
    /// <summary>
    /// 특정 아이템의 개수 확인
    /// </summary>
    /// <param name="material">확인할 재료</param>
    /// <returns>인벤토리에 있는 해당 아이템의 총 개수</returns>
    public int GetItemCount(CraftingMaterial material)
    {
        if (material == null) return 0;
        
        int totalCount = 0;
        foreach (var slot in inventorySlots)
        {
            if (slot != null && slot.material == material)
            {
                totalCount += slot.Quantity;
            }
        }
        
        return totalCount;
    }
    
    /// <summary>
    /// 특정 아이템이 충분한지 확인
    /// </summary>
    /// <param name="material">확인할 재료</param>
    /// <param name="requiredQuantity">필요한 개수</param>
    /// <returns>충분한 아이템이 있는지 여부</returns>
    public bool HasEnoughItems(CraftingMaterial material, int requiredQuantity)
    {
        return GetItemCount(material) >= requiredQuantity;
    }
    
    /// <summary>
    /// 제작 레시피 실행 가능 여부 확인
    /// </summary>
    /// <param name="recipe">확인할 레시피</param>
    /// <returns>제작 가능 여부</returns>
    public bool CanCraftRecipe(CraftingRecipe recipe)
    {
        if (recipe == null) return false;
        
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
    
    /// <summary>
    /// 제작 레시피 실행 (재료 소모 + 결과물 추가)
    /// </summary>
    /// <param name="recipe">실행할 레시피</param>
    /// <returns>제작 성공 여부</returns>
    public bool ExecuteRecipe(CraftingRecipe recipe)
    {
        if (!CanCraftRecipe(recipe)) return false;
        
        // 재료 소모
        var requiredMaterials = recipe.GetRequiredMaterials();
        foreach (var requirement in requiredMaterials)
        {
            RemoveItem(requirement.Key, requirement.Value);
        }
        
        // 결과물 추가
        bool success = AddItem(recipe.resultMaterial, recipe.resultQuantity);
        
        if (success && enableDebugLogs)
        {
            Debug.Log($"[PlayerInventory] 제작 성공: {recipe.recipeName} → {recipe.resultMaterial.materialName} x{recipe.resultQuantity}");
        }
        
        return success;
    }
    
    /// <summary>
    /// 빈 슬롯 찾기
    /// </summary>
    /// <returns>빈 슬롯의 인덱스, 없으면 -1</returns>
    private int FindEmptySlot()
    {
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (inventorySlots[i] == null)
                return i;
        }
        return -1;
    }
    
    /// <summary>
    /// 사용된 슬롯 개수 반환
    /// </summary>
    public int GetUsedSlotCount()
    {
        return inventorySlots.Count(slot => slot != null);
    }
    
    /// <summary>
    /// 인벤토리 정리 (빈 슬롯 제거)
    /// </summary>
    public void CompactInventory()
    {
        var compactedSlots = new List<ItemStack>();
        
        // null이 아닌 슬롯들만 추가
        foreach (var slot in inventorySlots)
        {
            if (slot != null)
                compactedSlots.Add(slot);
        }
        
        // 나머지는 null로 채움
        while (compactedSlots.Count < maxSlots)
        {
            compactedSlots.Add(null);
        }
        
        inventorySlots = compactedSlots;
        OnInventoryChanged?.Invoke();
        
        if (enableDebugLogs)
            Debug.Log("[PlayerInventory] 인벤토리가 정리되었습니다.");
    }
    
    /// <summary>
    /// 인벤토리 완전 초기화
    /// </summary>
    [ContextMenu("인벤토리 초기화")]
    public void ClearInventory()
    {
        inventorySlots.Clear();
        InitializeInventory();
        OnInventoryChanged?.Invoke();
        
        if (enableDebugLogs)
            Debug.Log("[PlayerInventory] 인벤토리가 초기화되었습니다.");
    }
    
    /// <summary>
    /// 현재 인벤토리 상태 출력 (디버그용)
    /// </summary>
    [ContextMenu("인벤토리 상태 출력")]
    public void PrintInventoryStatus()
    {
        Debug.Log("=== 인벤토리 상태 ===");
        Debug.Log($"사용 슬롯: {GetUsedSlotCount()}/{maxSlots}");
        
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            var slot = inventorySlots[i];
            if (slot != null)
            {
                Debug.Log($"슬롯 {i}: {slot.material.materialName} x{slot.quantity}");
            }
        }
        Debug.Log("==================");
    }
    
    /// <summary>
    /// 테스트용 아이템 추가
    /// </summary>
    [ContextMenu("테스트 아이템 추가")]
    public void AddTestItems()
    {
        // 이 메서드는 에디터에서 테스트용으로만 사용
        Debug.Log("[PlayerInventory] 테스트 아이템 추가 기능은 실제 아이템이 있을 때 구현됩니다.");
    }
    
    void OnDestroy()
    {
        // 싱글톤 정리
        if (Instance == this)
        {
            Instance = null;
        }
    }
}