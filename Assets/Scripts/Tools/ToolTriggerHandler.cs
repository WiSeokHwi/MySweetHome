using UnityEngine;

/// <summary>
/// 도구의 트리거 콜라이더에 부착되어 ToolItem에 트리거 이벤트를 전달하는 헬퍼 컴포넌트
/// 
/// 상호작용 구분:
/// 1. 닿았을 때: Interact(true/false) - 시각적 피드백용 (하이라이트, 선택 표시 등)
/// 2. 입력했을 때: InteractWithTool() - 실제 도구 사용 동작
/// </summary>
public class ToolTriggerHandler : MonoBehaviour
{
    private ToolItem parentTool;
    
    /// <summary>
    /// 부모 ToolItem 설정
    /// </summary>
    public void Initialize(ToolItem tool)
    {
        parentTool = tool;
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (parentTool != null)
        {
            InteractionObject interactionObject = other.GetComponent<InteractionObject>();
            if (interactionObject != null)
            {
                // 도구를 들고 있을 때만 시각적 피드백 표시
                if (parentTool.IsGrabbed)
                {
                    interactionObject.Interact(true);
                }
            }

            // ToolItem에 트리거 진입 알림
            parentTool.OnToolTriggerEnter(other, interactionObject);
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (parentTool != null)
        {
            InteractionObject interactionObject = other.GetComponent<InteractionObject>();
            if (interactionObject != null)
            {
                // 도구를 들고 있을 때만 시각적 피드백 제거
                if (parentTool.IsGrabbed)
                {
                    interactionObject.Interact(false);
                }
            }

            // ToolItem에 트리거 종료 알림
            parentTool.OnToolTriggerExit(other);
        }
    }
    
    public void Cleanup()
    {
        parentTool = null;
    }
}