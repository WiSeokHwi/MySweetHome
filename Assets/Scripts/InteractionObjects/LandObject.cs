using UnityEngine;

public enum LandType
{
    Grass,
    Dirt,
    Water,
}

/// <summary>
/// ==================== LAND OBJECT SYSTEM ====================
/// 
/// 【 시스템 개요 】
/// 농지/흙과 같은 땅 관련 상호작용 오브젝트입니다.
/// InteractionObject를 상속받아 기본 도구 상호작용 기능을 포함하며,
/// 땅 고유의 시각적 피드백과 상태 관리 기능을 추가로 제공합니다.
/// 
/// 【 상속 구조 】
/// MonoBehaviour → InteractionObject → LandObject
/// └── IInteractable (InteractionObject에서 구현, LandObject에서 확장)
/// 
/// 【 주요 기능 】
/// 1. 땅 상태 관리 (Grass/Dirt/Water)
/// 2. 괭이를 통한 경작 시스템
/// 3. 이중 하이라이트 시스템:
///    - InteractionObject 기본 상호작용 피드백
///    - LandObject 전용 selectObject 하이라이트
/// 4. 동적 머티리얼 교체 (상태별 시각적 변화)
/// 
/// 【 연동 시스템 】
/// - EquipState: VR 컨트롤러 통합 상호작용 관리
/// - ToolItem: 괭이 등의 도구와 상호작용
/// - FarmTile: 농장 시스템의 기본 클래스로 활용
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
    
    #region IInteractable Override (LandObject specific behavior)
    
    /// <summary>
    /// LandObject 전용 호버 시작 처리
    /// 기본 InteractionObject 동작에 추가로 LandObject 전용 하이라이트 적용
    /// </summary>
    public override void OnHoverEnter()
    {
        base.OnHoverEnter(); // InteractionObject의 기본 Interact(true) 호출
        GizmosSelected(true); // LandObject 전용 하이라이트 추가
        
        if (enableDebugLogs)
            Debug.Log($"[LandObject] {gameObject.name}: 땅 상호작용 가능 - 도구 사용 버튼을 눌러 경작하세요!");
    }
    
    /// <summary>
    /// LandObject 전용 호버 종료 처리
    /// 기본 InteractionObject 동작에 추가로 LandObject 전용 하이라이트 제거
    /// </summary>
    public override void OnHoverExit()
    {
        base.OnHoverExit(); // InteractionObject의 기본 Interact(false) 호출
        GizmosSelected(false); // LandObject 전용 하이라이트 제거
    }
    
    #endregion
}