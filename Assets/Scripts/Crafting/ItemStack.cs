using UnityEngine;
using System;

/// <summary>
/// ItemStack - 재료/아이템의 종류와 수량을 나타내는 클래스
/// </summary>
[Serializable] // 인스펙터에서 보이도록 직렬화 가능하도록 설정
public class ItemStack
{
    public CraftingMaterial material;

    // Quantity 필드를 속성(Property)으로 변경하여 수량의 유효성을 보장합니다.
    // _quantity는 실제 값을 저장하는 private 필드입니다.
    private int _quantity;
    [Range(0, 999)] // 수량 범위 설정
    public int Quantity
    {
        get { return _quantity; }
        set
        {
            // 수량이 항상 0 이상이 되도록 보장합니다.
            _quantity = Mathf.Max(0, value);
            // 만약 수량이 0이 되면 아이템 종류도 null로 설정하여 빈 슬롯으로 만듭니다.
            // 이는 RemoveItems 메서드에서도 처리되지만, 직접 Quantity를 0으로 설정하는 경우를 대비합니다.
            if (_quantity == 0)
            {
                material = null;
            }
        }
    }

    public bool IsEmpty => material == null || Quantity <= 0;

    public ItemStack(CraftingMaterial material, int quantity)
    {
        this.material = material;
        this.Quantity = quantity; // 속성을 통해 값 할당
    }

    /// <summary>
    /// 아이템 수량을 증가시킵니다.
    /// </summary>
    /// <param name="amount">증가시킬 수량</param>
    public void AddItems(int amount)
    {
        if (amount <= 0) return;
        Quantity += amount; // 속성을 통해 값 증가
        // 최대 스택 크기 제한은 PlayerInventory에서 처리하는 것이 일반적입니다.
        // 여기서는 단순히 수량만 증가시킵니다.
    }

    /// <summary>
    /// 아이템 수량을 감소시킵니다.
    /// </summary>
    /// <param name="amount">감소시킬 수량</param>
    /// <returns>실제로 감소된 수량</returns>
    public int RemoveItems(int amount)
    {
        if (amount <= 0) return 0;

        int actualRemoved = Mathf.Min(Quantity, amount);
        Quantity -= actualRemoved; // 속성을 통해 값 감소
        // Quantity 속성의 setter에서 material = null; 및 Quantity = 0; 처리가 포함되어 있습니다.
        return actualRemoved;
    }

    /// <summary>
    /// 아이템 정보를 디버그 문자열로 반환
    /// </summary>
    public string GetDebugInfo()
    {
        return $"{material?.materialName ?? "NULL"} x{Quantity}";
    }
}
