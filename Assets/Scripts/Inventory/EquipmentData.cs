using UnityEngine;

[CreateAssetMenu(menuName = "Item/Equipment")]
public class EquipmentData : ItemData
{
    public enum ToolType
    {
        Hoe,
        WateringCan,
        Axe,
        Pickaxe,
    }
    public ToolType toolType;
}
