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

public class ToolItem : GrabbableItem
{
    // ========== 도구 데이터 ==========
    [Header("도구 데이터")]
    [Tooltip("이 도구의 타입과 메타데이터를 정의하는 ScriptableObject")]
    public EquipmentData toolData;
    
    // ========== 상호작용 설정 ==========
    [Header("도구 상호작용")]
    [Tooltip("도구 끝부분의 트리거 콜라이더 - 이 콜라이더에 ToolTriggerHandler 컴포넌트 필요")]
    public Collider toolTriggerCollider;
    
    [Header("디버그")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // ========== 상태 추적 ==========
    /// <summary>
    /// 【데이터 저장】현재 상호작용 가능한 InteractionObject들의 집합
    /// 【업데이트 위치】OnToolTriggerEnter(추가), OnToolTriggerExit(제거)
    /// 【참조 위치】UseTool()에서 상호작용할 대상 오브젝트 확인
    /// 【자료형】HashSet으로 중복 방지 및 빠른 검색
    /// </summary>
    private HashSet<InteractionObject> interactableObjects = new HashSet<InteractionObject>();

    /// <summary>
    /// 【Unity 생명주기】컴포넌트 초기화
    /// 【처리 내용】부모 클래스 초기화, 트리거 핸들러 연결, 데이터 유효성 검사
    /// </summary>
    protected override void Awake()
    {
        base.Awake(); // GrabbableItem 초기화

        // 트리거 콜라이더에 ToolTriggerHandler 연결
        if (toolTriggerCollider != null)
        {
            var handler = toolTriggerCollider.GetComponent<ToolTriggerHandler>();
            if (handler != null)
            {
                handler.Initialize(this); // ToolTriggerHandler에 이 ToolItem 참조 전달
            }
            else
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[ToolItem] {gameObject.name}: 트리거 콜라이더에 ToolTriggerHandler 컴포넌트가 없습니다.");
            }
        }

        // 도구 데이터 유효성 검사
        if (toolData == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[ToolItem] {gameObject.name}: EquipmentData가 할당되지 않았습니다.");
        }
        
        // 트리거 콜라이더 유효성 검사
        if (toolTriggerCollider == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[ToolItem] {gameObject.name}: 도구 트리거 콜라이더가 할당되지 않았습니다.");
        }
    }


    private void UseTool()
    {
        if (toolData == null) return;

        if (enableDebugLogs)
            Debug.Log($"[ToolItem] {toolData.toolType} 사용!");

        // 현재 상호작용 가능한 오브젝트들이 있는 경우에만 도구 사용
        if (interactableObjects.Count > 0)
        {
            // 모든 상호작용 가능한 오브젝트에게 도구 사용 요청
            foreach (InteractionObject interactionObj in interactableObjects)
            {
                if (interactionObj != null)
                {
                    interactionObj.InteractWithTool(toolData.toolType);
                }
            }
        }
        else
        {
            if (enableDebugLogs)
                Debug.Log($"[ToolItem] {toolData.toolType}: 상호작용할 오브젝트가 없습니다.");
        }
    }


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
    
    /// <summary>
    /// 트리거 콜라이더에 오브젝트가 들어왔을 때 호출 (외부에서 호출됨)
    /// </summary>
    public void OnToolTriggerEnter(Collider other, InteractionObject interactionObject)
    {
        if (!isGrabbed) return; // 잡힌 상태에서만 상호작용
        
        // InteractionObject가 있고 이 도구와 상호작용 가능한지 확인
        if (interactionObject != null && interactionObject.CanInteractWith(toolData.toolType))
        {
            interactableObjects.Add(interactionObject);
            
            if (enableDebugLogs)
                Debug.Log($"[ToolItem] {toolData.toolType} 도구가 {interactionObject.gameObject.name}과(와) 상호작용 가능합니다.");
        }
    }
    
    /// <summary>
    /// 트리거 콜라이더에서 오브젝트가 나갔을 때 호출 (외부에서 호출됨)
    /// </summary>
    public void OnToolTriggerExit(Collider other)
    {
        InteractionObject interactionObject = other.GetComponent<InteractionObject>();
        
        if (interactionObject != null && interactableObjects.Contains(interactionObject))
        {
            interactableObjects.Remove(interactionObject);
            
            if (enableDebugLogs)
                Debug.Log($"[ToolItem] {toolData.toolType} 도구가 {interactionObject.gameObject.name}과(와) 상호작용을 중단했습니다.");
        }
    }
    
}