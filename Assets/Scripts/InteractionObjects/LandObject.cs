using UnityEngine;

public enum LandType
{
    Grass,
    Dirt,
    Water,
}

/// <summary>
/// 농지/흙과 같은 땅 관련 상호작용 오브젝트
/// 괭이로 갈아엎거나 경작할 수 있습니다.
/// </summary>
public class LandObject : InteractionObject
{
    [Header("땅 설정")]
    [Tooltip("현재 땅의 상태")]
    public LandType landType = LandType.Grass;
    
    [Header("땅 머터리얼")]
    public Material grass, dirt, water;
    
    [Header("시각적 효과")]
    public GameObject selectObject;
    
    private Renderer landRenderer;
    
    protected virtual void Start()
    {
        landRenderer = GetComponent<Renderer>();
        
        // 기본값을 Grass로 설정
        SwitchLandStatus(LandType.Grass);
        
        // 호환 가능한 도구 타입을 괭이로 설정 (Inspector에서 설정하지 않은 경우)
        if (compatibleToolTypes == null || compatibleToolTypes.Length == 0)
        {
            compatibleToolTypes = new EquipmentData.ToolType[] { EquipmentData.ToolType.Hoe };
        }
    }
    
    public override void InteractWithTool(EquipmentData.ToolType toolType)
    {
        switch (toolType)
        {
            case EquipmentData.ToolType.Hoe:
                ProcessWithHoe();
                break;
            default:
                if (enableDebugLogs)
                    Debug.LogWarning($"[LandObject] {gameObject.name}: {toolType} 도구로는 상호작용할 수 없습니다.");
                break;
        }
    }
    
    /// <summary>
    /// 괭이로 땅을 갈아엎는 처리
    /// </summary>
    protected virtual void ProcessWithHoe()
    {
        LandType newLandType = LandType.Dirt; // 괭이로 갈면 항상 흙으로 변경
        
        if (enableDebugLogs)
            Debug.Log($"[LandObject] {gameObject.name}: 괭이로 땅을 갈아엎고 있습니다. ({landType} → {newLandType})");
        
        SwitchLandStatus(newLandType);
    }
    
    public void SwitchLandStatus(LandType newLandType)
    {
        landType = newLandType;
        Material newMaterial = grass;

        switch (landType)
        {
            case LandType.Grass:
                newMaterial = grass;
                break;
            case LandType.Dirt:
                newMaterial = dirt;
                break;
            case LandType.Water:
                newMaterial = water;
                break;
        }

        if (GetComponent<Renderer>() != null)
        {
            GetComponent<Renderer>().material = newMaterial;
        }
        else
        {
            Debug.LogWarning("Renderer is not assigned on " + gameObject.name);
        }
    }

    public virtual void GizmosSelected(bool select)
    {
        if (selectObject != null)
        {
            selectObject.SetActive(select);
        }
    }

    public override void Interact(bool isStart)
    {
        base.Interact(isStart); // 부모 클래스의 기본 동작 호출
        GizmosSelected(isStart);
    }
    
    /// <summary>
    /// 현재 땅 상태 반환
    /// </summary>
    public LandType GetCurrentLandType()
    {
        return landType;
    }
    
    /// <summary>
    /// 땅 상태를 수동으로 설정 (외부에서 호출 가능)
    /// </summary>
    public void SetLandType(LandType newType)
    {
        SwitchLandStatus(newType);
    }
}