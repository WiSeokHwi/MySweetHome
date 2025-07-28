using UnityEngine;

/// <summary>
/// ItemStack - 제작 재료의 스택 관리 클래스
/// 
/// == 주요 기능 ==
/// 1. CraftingMaterial과 수량을 함께 관리
/// 2. 스택 합치기, 나누기, 비교 기능
/// 3. 유효성 검사 및 디버깅 지원
/// 4. 직렬화 가능한 구조체 형태
/// 
/// == 사용 예시 ==
/// - 나무 64개 스택
/// - 철 광석 32개 스택
/// - 검 1개 (스택 불가)
/// </summary>
[System.Serializable]
public class ItemStack
{
    [Header("Stack Content")]
    [Tooltip("스택에 들어있는 재료 타입")]
    public CraftingMaterial material;
    
    [Tooltip("현재 스택에 있는 수량")]
    [SerializeField] private int quantity;
    
    /// <summary>
    /// 현재 수량 (외부에서 직접 수정 불가)
    /// </summary>
    public int Quantity 
    { 
        get => quantity; 
        private set => quantity = Mathf.Clamp(value, 0, MaxStackSize); 
    }
    
    /// <summary>
    /// 이 스택의 최대 크기
    /// </summary>
    public int MaxStackSize => material != null ? material.maxStackSize : 1;
    
    /// <summary>
    /// 스택이 비어있는지 여부
    /// </summary>
    public bool IsEmpty => material == null || quantity <= 0;
    
    /// <summary>
    /// 스택이 가득 찼는지 여부
    /// </summary>
    public bool IsFull => material != null && quantity >= MaxStackSize;
    
    /// <summary>
    /// 스택 가능한 재료인지 여부
    /// </summary>
    public bool IsStackable => material != null && material.isStackable;
    
    /// <summary>
    /// 빈 스택 생성자
    /// </summary>
    public ItemStack()
    {
        material = null;
        quantity = 0;
    }
    
    /// <summary>
    /// 재료와 수량으로 스택 생성
    /// </summary>
    /// <param name="mat">재료 타입</param>
    /// <param name="qty">수량</param>
    public ItemStack(CraftingMaterial mat, int qty)
    {
        material = mat;
        Quantity = qty;
    }
    
    /// <summary>
    /// 다른 스택을 복사하여 생성
    /// </summary>
    /// <param name="other">복사할 스택</param>
    public ItemStack(ItemStack other)
    {
        material = other.material;
        Quantity = other.quantity;
    }
    
    /// <summary>
    /// 스택에 아이템 추가
    /// </summary>
    /// <param name="amount">추가할 수량</param>
    /// <returns>실제로 추가된 수량</returns>
    public int AddItems(int amount)
    {
        if (material == null || amount <= 0)
            return 0;
        
        int maxCanAdd = MaxStackSize - quantity;
        int actualAdded = Mathf.Min(amount, maxCanAdd);
        
        Quantity += actualAdded;
        return actualAdded;
    }
    
    /// <summary>
    /// 스택에서 아이템 제거
    /// </summary>
    /// <param name="amount">제거할 수량</param>
    /// <returns>실제로 제거된 수량</returns>
    public int RemoveItems(int amount)
    {
        if (IsEmpty || amount <= 0)
            return 0;
        
        int actualRemoved = Mathf.Min(amount, quantity);
        Quantity -= actualRemoved;
        
        // 수량이 0이 되면 재료도 null로 설정
        if (quantity <= 0)
        {
            material = null;
        }
        
        return actualRemoved;
    }
    
    /// <summary>
    /// 다른 스택과 합치기 시도
    /// </summary>
    /// <param name="other">합칠 스택</param>
    /// <returns>합치기에 성공한 수량</returns>
    public int TryCombineWith(ItemStack other)
    {
        if (!CanCombineWith(other))
            return 0;
        
        int canAdd = MaxStackSize - quantity;
        int actualMoved = Mathf.Min(canAdd, other.quantity);
        
        if (actualMoved > 0)
        {
            Quantity += actualMoved;
            other.RemoveItems(actualMoved);
        }
        
        return actualMoved;
    }
    
    /// <summary>
    /// 스택을 분할하여 새로운 스택 생성
    /// </summary>
    /// <param name="amount">분할할 수량</param>
    /// <returns>분할된 새로운 스택 (분할 실패 시 null)</returns>
    public ItemStack SplitStack(int amount)
    {
        if (IsEmpty || amount <= 0 || amount >= quantity)
            return null;
        
        int splitAmount = Mathf.Min(amount, quantity);
        ItemStack newStack = new ItemStack(material, splitAmount);
        
        RemoveItems(splitAmount);
        
        return newStack;
    }
    
    /// <summary>
    /// 다른 스택과 합칠 수 있는지 확인
    /// </summary>
    /// <param name="other">확인할 스택</param>
    /// <returns>합칠 수 있으면 true</returns>
    public bool CanCombineWith(ItemStack other)
    {
        if (other == null || other.IsEmpty)
            return false;
        
        if (IsEmpty)
            return true;
        
        return material == other.material && 
               IsStackable && 
               !IsFull;
    }
    
    /// <summary>
    /// 스택을 비우기
    /// </summary>
    public void Clear()
    {
        material = null;
        quantity = 0;
    }
    
    /// <summary>
    /// 스택의 복사본 생성
    /// </summary>
    /// <returns>복사된 스택</returns>
    public ItemStack Clone()
    {
        return new ItemStack(this);
    }
    
    /// <summary>
    /// 두 스택이 같은 재료인지 비교
    /// </summary>
    /// <param name="other">비교할 스택</param>
    /// <returns>같은 재료면 true</returns>
    public bool IsSameMaterial(ItemStack other)
    {
        if (other == null)
            return false;
        
        return material == other.material;
    }
    
    /// <summary>
    /// 디버깅용 정보 문자열
    /// </summary>
    /// <returns>스택 정보 문자열</returns>
    public string GetDebugInfo()
    {
        if (IsEmpty)
            return "Empty Stack";
        
        return $"{material.materialName} x{quantity}/{MaxStackSize}";
    }
    
    /// <summary>
    /// ToString 오버라이드
    /// </summary>
    public override string ToString()
    {
        return GetDebugInfo();
    }
    
    /// <summary>
    /// 유효성 검사
    /// </summary>
    /// <returns>유효하면 true</returns>
    public bool IsValid()
    {
        if (quantity < 0)
        {
            Debug.LogError("ItemStack: 수량이 음수입니다.");
            return false;
        }
        
        if (material != null && quantity > MaxStackSize)
        {
            Debug.LogError($"ItemStack: 수량({quantity})이 최대 스택 크기({MaxStackSize})를 초과합니다.");
            return false;
        }
        
        return true;
    }
}