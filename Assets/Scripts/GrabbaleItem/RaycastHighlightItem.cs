using UnityEngine;

/// <summary>
/// ==================== 레이캐스트 하이라이트 시스템 ====================
/// 
/// 【 시스템 개요 】
/// GrabbableItem을 상속받아 레이캐스트 기반 하이라이트 기능을 제공하는 추상 기본 클래스입니다.
/// ToolItem과 SeedItem 등에서 공통으로 사용하는 하이라이트 로직을 추상화합니다.
/// 
/// 【 공통 기능 】
/// 1. 잡힌 상태에서만 레이캐스트 감지
/// 2. Update에서 지속적인 타겟 감지 및 하이라이트 관리
/// 3. 타겟 변경 시 이전 오브젝트 하이라이트 자동 해제
/// 4. 도구 놓을 때 하이라이트 자동 해제
/// 5. 디버그 기즈모 표시 (레이캐스트 범위)
/// 
/// 【 상속 클래스에서 구현해야 할 메서드 】
/// - DetectTarget(): 레이캐스트로 타겟 오브젝트 감지
/// - SetHighlight(target, state): 타겟 오브젝트 하이라이트 설정/해제
/// - OnTargetChanged(oldTarget, newTarget): 타겟 변경 시 추가 처리
/// 
/// 【 사용 예시 】
/// - ToolItem: InteractionObject 감지 및 Interact(true/false) 호출
/// - SeedItem: FarmTile 감지 및 심기 가능 여부에 따른 하이라이트
/// </summary>
public abstract class RaycastHighlightItem : GrabbableItem
{
    [Header("레이캐스트 하이라이트 설정")]
    [Tooltip("레이캐스트 감지 범위")]
    public float detectionRange = 2.0f;
    
    [Tooltip("레이캐스트 감지용 레이어 마스크")]
    public LayerMask detectionLayerMask = -1;
    
    [Tooltip("레이캐스트 시작점 (없으면 transform 사용)")]
    public Transform raycastOrigin;
    
    [Header("디버그")]
    [SerializeField] protected bool enableDebugLogs = true;
    [SerializeField] protected bool showDebugGizmos = true;
    
    // 현재 하이라이트된 타겟 오브젝트
    protected GameObject currentTarget;
    
    /// <summary>
    /// Unity Update - 잡힌 상태에서만 타겟 감지 및 하이라이트 관리
    /// </summary>
    private void Update()
    {
        if (!isGrabbed)
        {
            // 도구를 놓았을 때 하이라이트 해제
            ClearHighlight();
            return;
        }
        
        // 레이캐스트로 타겟 감지 및 하이라이트 관리
        UpdateHighlight();
    }
    
    /// <summary>
    /// 타겟 감지 및 하이라이트 업데이트 처리
    /// </summary>
    private void UpdateHighlight()
    {
        // 상속 클래스에서 구현한 타겟 감지 로직 호출
        GameObject newTarget = DetectTarget();
        
        // 타겟이 변경된 경우
        if (currentTarget != newTarget)
        {
            GameObject oldTarget = currentTarget;
            
            // 이전 타겟 하이라이트 해제
            if (currentTarget != null)
            {
                SetHighlight(currentTarget, false);
            }
            
            // 새 타겟 하이라이트 활성화
            if (newTarget != null)
            {
                SetHighlight(newTarget, true);
            }
            
            currentTarget = newTarget;
            
            // 상속 클래스에서 추가 처리가 필요한 경우
            OnTargetChanged(oldTarget, newTarget);
        }
    }
    
    /// <summary>
    /// 현재 하이라이트를 해제합니다
    /// </summary>
    protected void ClearHighlight()
    {
        if (currentTarget != null)
        {
            SetHighlight(currentTarget, false);
            currentTarget = null;
        }
    }
    
    /// <summary>
    /// 레이캐스트 원점 위치 반환
    /// </summary>
    protected Vector3 GetRaycastOrigin()
    {
        return raycastOrigin != null ? raycastOrigin.position : transform.position;
    }
    
    /// <summary>
    /// 레이캐스트 방향 반환
    /// </summary>
    protected virtual Vector3 GetRaycastDirection()
    {
        return raycastOrigin != null ? raycastOrigin.forward : transform.forward;
    }
    
    /// <summary>
    /// 현재 타겟 오브젝트 반환
    /// </summary>
    public GameObject GetCurrentTarget()
    {
        return currentTarget;
    }
    
    /// <summary>
    /// 컴포넌트 정리 시 하이라이트 해제
    /// </summary>
    protected virtual void OnDestroy()
    {
        ClearHighlight();
    }
    
    /// <summary>
    /// 디버그 기즈모 그리기 - 레이캐스트 범위 표시
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        Vector3 origin = GetRaycastOrigin();
        Vector3 direction = GetRaycastDirection();
        
        // 레이캐스트 방향과 범위 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(origin, direction * detectionRange);
        
        // 감지 범위 끝점 표시
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin + direction * detectionRange, 0.1f);
        
        // 상속 클래스별 추가 기즈모
        DrawCustomGizmos();
    }
    
    // ========== 상속 클래스에서 구현해야 할 추상 메서드 ==========
    
    /// <summary>
    /// 레이캐스트로 타겟 오브젝트를 감지합니다
    /// </summary>
    /// <returns>감지된 타겟 오브젝트 (없으면 null)</returns>
    protected abstract GameObject DetectTarget();
    
    /// <summary>
    /// 타겟 오브젝트의 하이라이트를 설정/해제합니다
    /// </summary>
    /// <param name="target">타겟 오브젝트</param>
    /// <param name="highlight">하이라이트 활성화 여부</param>
    protected abstract void SetHighlight(GameObject target, bool highlight);
    
    // ========== 상속 클래스에서 선택적으로 오버라이드할 가상 메서드 ==========
    
    /// <summary>
    /// 타겟이 변경되었을 때 호출됩니다 (선택적 오버라이드)
    /// </summary>
    /// <param name="oldTarget">이전 타겟</param>
    /// <param name="newTarget">새로운 타겟</param>
    protected virtual void OnTargetChanged(GameObject oldTarget, GameObject newTarget)
    {
        if (enableDebugLogs && newTarget != null)
        {
            Debug.Log($"[{GetType().Name}] 타겟 변경: {newTarget.name}");
        }
    }
    
    /// <summary>
    /// 커스텀 디버그 기즈모 그리기 (선택적 오버라이드)
    /// </summary>
    protected virtual void DrawCustomGizmos()
    {
        // 상속 클래스에서 필요시 구현
    }
}