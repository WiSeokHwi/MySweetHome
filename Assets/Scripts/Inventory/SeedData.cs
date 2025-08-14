using UnityEngine;

[CreateAssetMenu(menuName = "Item/Seed")]
public class SeedData : ItemData
{
    
    public int daysToGrow; // 작물이 자라는데 걸리는 시간

    public ItemData cropItem; // 수확 시 생성되는 작물 아이템

}
