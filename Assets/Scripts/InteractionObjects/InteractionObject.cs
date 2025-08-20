using UnityEngine;

/// <summary>
/// ==================== TOOL INTERACTION SYSTEM ====================
/// 
/// 【 시스템 개요 】
/// 도구(ToolItem)와 상호작용할 수 있는 모든 오브젝트들의 기본 클래스입니다.
/// 기존 태그 기반 시스템을 대체하는 객체지향적 상호작용 시스템의 핵심입니다.
/// IInteractable 인터페이스를 구현하여 통합 상호작용 시스템을 지원합니다.
/// 
/// 【 설계 철학 】
/// - 각 오브젝트가 자신만의 상호작용 로직을 정의 (캡슐화)
/// - 도구는 오브젝트 타입을 체크할 필요 없이 InteractWithTool() 호출만 하면 됨
/// - 새로운 상호작용 오브젝트 추가가 기존 코드에 영향을 주지 않음 (개방-폐쇄 원칙)
/// 
/// 【 상속 구조 】
/// InteractionObject (추상 기본 클래스, IInteractable 구현)
/// └── LandObject (땅 - 괭이로 갈기)
/// └── TreeObject (나무 - 도끼로 벌목) [미구현]
/// └── CropObject (작물 - 물뿌리개로 물주기) [미구현]
/// └── RockObject (돌 - 곡괭이로 채굴) [미구현]
/// 
/// 【 연동 시스템 】
/// - ToolItem: 도구 사용 시 InteractWithTool() 호출
/// - ToolTriggerHandler: 도구가 오브젝트에 닿았을 때 Interact() 호출
/// - EquipState: IInteractable을 통한 통합 상호작용 관리
/// 
/// 【 호출 흐름 】
/// 1. VR 컨트롤러 호버 → OnHoverEnter() → 시각적 피드백 시작
/// 2. 도구 사용 버튼 누름 → OnInteract() → InteractWithTool() → 실제 상호작용 수행
/// 3. 호버 종료 → OnHoverExit() → 시각적 피드백 종료
/// </summary>
public abstract class InteractionObject : MonoBehaviour, IInteractable
{
    [Header("상호작용 설정")]
    [Tooltip("이 오브젝트와 상호작용할 수 있는 도구 타입들")]
    public EquipmentData.ToolType[] compatibleToolTypes;
    
    [Header("디버그")]
    [SerializeField] protected bool enableDebugLogs = true;
    
    /// <summary>
    /// 상호작용 시작/종료 시 호출됩니다.
    /// </summary>
    /// <param name="isStart">true: 상호작용 시작, false: 상호작용 종료</param>
    public virtual void Interact(bool isStart)
    {
        if (isStart)
        {
            OnInteractionStart();
        }
        else
        {
            OnInteractionEnd();
        }
    }
    
    /// <summary>
    /// 특정 도구 타입과 실제 상호작용을 수행합니다.
    /// </summary>
    /// <param name="toolType">사용된 도구 타입</param>
    public abstract void InteractWithTool(EquipmentData.ToolType toolType);
    
    /// <summary>
    /// 특정 도구 타입과 상호작용 가능한지 확인합니다.
    /// </summary>
    /// <param name="toolType">확인할 도구 타입</param>
    /// <returns>상호작용 가능 여부</returns>
    public virtual bool CanInteractWith(EquipmentData.ToolType toolType)
    {
        if (compatibleToolTypes == null || compatibleToolTypes.Length == 0)
            return false;
            
        foreach (var compatibleType in compatibleToolTypes)
        {
            if (compatibleType == toolType)
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 상호작용 시작 시 호출되는 가상 메서드 (선택적 오버라이드)
    /// </summary>
    protected virtual void OnInteractionStart()
    {
        if (enableDebugLogs)
            Debug.Log($"[InteractionObject] {gameObject.name}: 상호작용 시작");
    }
    
    /// <summary>
    /// 상호작용 종료 시 호출되는 가상 메서드 (선택적 오버라이드)
    /// </summary>
    protected virtual void OnInteractionEnd()
    {
        if (enableDebugLogs)
            Debug.Log($"[InteractionObject] {gameObject.name}: 상호작용 종료");
    }
    
    #region IInteractable Implementation
    
    /// <summary>
    /// 상호작용 가능 여부 확인 - 호환 가능한 도구가 있는지 확인
    /// </summary>
    public virtual bool CanInteract()
    {
        return compatibleToolTypes != null && compatibleToolTypes.Length > 0;
    }
    
    /// <summary>
    /// 호버 시작 - 기본 상호작용 시작 호출
    /// </summary>
    public virtual void OnHoverEnter()
    {
        Interact(true);
    }
    
    /// <summary>
    /// 호버 종료 - 기본 상호작용 종료 호출
    /// </summary>
    public virtual void OnHoverExit()
    {
        Interact(false);
    }
    
    /// <summary>
    /// 상호작용 실행 - 기본 도구 타입으로 상호작용 수행
    /// </summary>
    public virtual void OnInteract()
    {
        // 호환 가능한 첫 번째 도구 타입으로 상호작용 수행
        if (compatibleToolTypes != null && compatibleToolTypes.Length > 0)
        {
            InteractWithTool(compatibleToolTypes[0]);
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning("[InteractionObject] 상호작용할 수 있는 도구가 설정되지 않았습니다.");
        }
    }
    
    /// <summary>
    /// GameObject 반환 (IInteractable 인터페이스 요구사항)
    /// </summary>
    public GameObject GetGameObject()
    {
        return gameObject;
    }
    
    #endregion
}
