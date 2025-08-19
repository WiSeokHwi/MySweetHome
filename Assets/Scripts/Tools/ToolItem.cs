/// <summary>
/// ==================== VR 도구 아이템 시스템 ====================
/// 
/// 【 시스템 개요 】
/// VR 환경에서 사용할 수 있는 상호작용 도구들의 기본 클래스입니다.
/// GrabbableItem을 상속받아 잡기/놓기 기능과 도구 고유 기능을 모두 제공합니다.
/// 
/// 【 주요 기능 】
/// 1. 도구 타입별 기능 실행 (HOE, WATERING_CAN, AXE, PICKAXE)
/// 2. 트리거 콜라이더 기반 상호작용 감지
/// 3. InteractionObject와의 호환성 검사 및 상호작용
/// 4. 상태 기반 도구 사용 (잡은 상태에서만 사용 가능)
/// 5. 인벤토리 시스템과 연동 (도구도 인벤토리 저장 가능)
/// 
/// 【 연동 시스템 】
/// - GrabbableItem: 기본 VR 상호작용 및 인벤토리 기능
/// - EquipmentData: 도구 타입 및 메타데이터 정의
/// - InteractionObject: 도구와 상호작용할 수 있는 오브젝트들
/// - ToolTriggerHandler: 트리거 콜라이더 이벤트 처리
/// - EquipState: VR 컴트롤러 입력 처리 및 도구 사용 명령 전달
/// 
/// 【 데이터 저장 위치 】
/// - toolData: 도구 메타데이터 EquipmentData (이 클래스에서 관리, Inspector 설정)
/// - interactableObjects: 현재 상호작용 가능한 InteractionObject들 HashSet (이 클래스에서 관리)
/// - 기본 GrabbableItem 데이터: isGrabbed, currentDropZones 등 (부모 클래스에서 관리)
/// 
/// 【 도구 사용 플로우 】
/// 1. 도구 잡기 → isGrabbed = true → 트리거 콜라이더 활성화
/// 2. InteractionObject 감지 → OnToolTriggerEnter → 호환성 검사 → interactableObjects 추가
/// 3. 도구 사용 입력 → OnToolUseInput → UseTool → InteractionObject.InteractWithTool
/// 4. 도구 놓기 → isGrabbed = false → 상호작용 비활성화
/// </summary>
using UnityEngine;
using System.Collections.Generic;

public class ToolItem : RaycastHighlightItem
{
    // ========== 도구 데이터 ==========
    [Header("도구 데이터")]
    [Tooltip("이 도구의 타입과 메타데이터를 정의하는 ScriptableObject")]
    public EquipmentData toolData;
    
    // ========== 레이캐스트 설정 (기본 클래스에서 상속) ==========
    // raycastOrigin, detectionRange, detectionLayerMask는 부모 클래스에서 제공
    

    /// <summary>
    /// 【Unity 생명주기】컴포넌트 초기화
    /// 【처리 내용】부모 클래스 초기화, 트리거 핸들러 연결, 데이터 유효성 검사
    /// </summary>
    protected override void Awake()
    {
        base.Awake(); // GrabbableItem 초기화

        // 도구 데이터 유효성 검사
        if (toolData == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[ToolItem] {gameObject.name}: EquipmentData가 할당되지 않았습니다.");
        }
        
        // 레이캐스트 레이어 마스크를 interactionLayerMask로 설정
        detectionLayerMask = -1;
    }


    // Update 로직은 부모 클래스 RaycastHighlightItem에서 처리

    private void UseTool()
    {
        if (toolData == null) return;

        if (enableDebugLogs)
            Debug.Log($"[ToolItem] {toolData.toolType} 사용!");

        // 현재 타겟이 InteractionObject인지 확인하고 도구 사용
        InteractionObject targetObject = GetCurrentTarget()?.GetComponent<InteractionObject>();
        
        if (targetObject != null && targetObject.CanInteractWith(toolData.toolType))
        {
            targetObject.InteractWithTool(toolData.toolType);
            
            if (enableDebugLogs)
                Debug.Log($"[ToolItem] {toolData.toolType}로 {targetObject.name}과 상호작용했습니다.");
        }
        else
        {
            if (enableDebugLogs)
                Debug.Log($"[ToolItem] {toolData.toolType}: 상호작용할 수 있는 오브젝트가 없습니다.");
        }
    }
    
    // 레이캐스트 감지 로직은 부모 클래스의 DetectTarget()에서 처리
    
    // 디버그 기즈모는 부모 클래스에서 처리


    public override ItemData GetItemData()
    {
        return toolData;
    }

    public override int GetQuantity()
    {
        return 1; // 도구는 항상 1개
    }

    // OnInventoryAddInput: 부모 클래스의 기본 구현 사용 (드롭존에서 인벤토리 추가 가능)

    public override void OnToolUseInput()
    {
        // 잡힌 상태에서만 사용 가능
        if (!isGrabbed) 
        {
            if (enableDebugLogs)
                Debug.Log($"[ToolItem] {gameObject.name}: 잡지 않은 상태에서는 사용할 수 없습니다.");
            return;
        }
        
        UseTool();
    }
    
    // 하이라이트 해제는 부모 클래스에서 처리
    
    // ========== RaycastHighlightItem 추상 메서드 구현 ==========
    
    /// <summary>
    /// 레이캐스트로 InteractionObject 감지
    /// </summary>
    protected override GameObject DetectTarget()
    {
        Vector3 origin = GetRaycastOrigin();
        Vector3 direction = GetRaycastDirection();
        
        // 레이캐스트 실행
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, detectionRange, detectionLayerMask);
        
        // 첫 번째로 호환 가능한 InteractionObject 반환
        foreach (RaycastHit hit in hits)
        {
            InteractionObject interactionObj = hit.collider.GetComponent<InteractionObject>();
            if (interactionObj != null && interactionObj.CanInteractWith(toolData.toolType))
            {
                return hit.collider.gameObject;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// InteractionObject 하이라이트 설정/해제
    /// </summary>
    protected override void SetHighlight(GameObject target, bool highlight)
    {
        if (target == null) return;
        
        InteractionObject interactionObj = target.GetComponent<InteractionObject>();
        if (interactionObj != null)
        {
            interactionObj.Interact(highlight);
        }
    }
    
    /// <summary>
    /// 타겟 변경 시 로그 출력
    /// </summary>
    protected override void OnTargetChanged(GameObject oldTarget, GameObject newTarget)
    {
        if (enableDebugLogs)
        {
            if (newTarget != null)
            {
                Debug.Log($"[ToolItem] 새로운 상호작용 타겟: {newTarget.name}");
            }
            else if (oldTarget != null)
            {
                Debug.Log($"[ToolItem] 상호작용 타겟 해제: {oldTarget.name}");
            }
        }
    }
    
    
}