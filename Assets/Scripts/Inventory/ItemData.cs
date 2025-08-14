using UnityEngine;

[CreateAssetMenu(menuName = "Item/Item")]
public class ItemData : ScriptableObject
{
    public string itemName; // 아이템 이름

    public string description;

    // 썸네일
    public Sprite thumbnail;

    // 월드에 생성오브젝트 프리팹
    public GameObject gameModel;
    
    // 최대 스택 크기
    public int maxStackSize = 1;
    
    // 스택 가능한지 여부
    public bool isStackable = false;
}
