using UnityEngine;

[CreateAssetMenu(menuName = "Item/Placable")]
public class PlacableItemData : ItemData
{
    [Header("아이템 그리드 사이즈")]
    [SerializeField] public Vector3 gridSize;
}
