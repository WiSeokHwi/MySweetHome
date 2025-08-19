using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 씨앗 아이템 - VR에서 씨앗을 들고 농장에 심을 수 있는 상호작용 오브젝트
/// 
/// == 주요 기능 ==
/// 1. 씨앗을 손에 들었을 때 심을 수 있는 농장 타일 하이라이트
/// 2. 트리거 입력으로 씨앗 심기
/// 3. Raycast 기반 농장 타일 감지
/// 4. 시각적 피드백 (심기 가능/불가능)
/// </summary>
public class SeedItem : RaycastHighlightItem
{
    [Header("Seed Settings")]
    [Tooltip("이 씨앗의 데이터")]
    public SeedData seedData;
    
    [Header("Planting System")]
    // plantingRange, detectionLayerMask는 부모 클래스에서 상속
    // 마지막으로 하이라이트된 타일 (중복 처리 방지)
    private FarmTile lastHighlightedTile;
    
    protected override void Awake()
    {
        base.Awake(); // RaycastHighlightItem 초기화
        
        // 씨앗 데이터 검증
        if (seedData == null)
        {
            Debug.LogError($"[SeedItem] {gameObject.name}: SeedData가 할당되지 않았습니다!");
        }
        
        // 레이캐스트 방향을 아래쪽으로 설정하기 위해 detectionLayerMask 설정
        detectionLayerMask = -1; // FarmTile 레이어 마스크
    }
    
    // Update 로직은 부모 클래스 RaycastHighlightItem에서 처리
    
    // 잡기/놓기 이벤트는 부모 클래스에서 처리
    
    // ========== RaycastHighlightItem 추상 메서드 구현 ==========
    
    /// <summary>
    /// 레이캐스트로 FarmTile 감지 (아래쪽 방향)
    /// </summary>
    protected override GameObject DetectTarget()
    {
        // 씨앗에서 아래쪽으로 레이캐스트
        Ray ray = new Ray(transform.position, Vector3.down);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, detectionRange, detectionLayerMask))
        {
            FarmTile farmTile = hit.collider.GetComponent<FarmTile>();
            if (farmTile != null)
            {
                return hit.collider.gameObject;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// FarmTile 하이라이트 설정/해제 (심기 가능 여부에 따라)
    /// </summary>
    protected override void SetHighlight(GameObject target, bool highlight)
    {
        if (target == null) return;
        
        FarmTile farmTile = target.GetComponent<FarmTile>();
        if (farmTile != null)
        {
            if (highlight)
            {
                // 심기 가능 여부에 따라 하이라이트 색상 결정
                bool canPlant = farmTile.CanPlantSeed(seedData);
                farmTile.ShowPlantingHighlight(true, canPlant);
            }
            else
            {
                farmTile.ShowPlantingHighlight(false);
            }
        }
    }
    
    /// <summary>
    /// 레이캐스트 방향을 아래쪽으로 오버라이드
    /// </summary>
    protected override Vector3 GetRaycastDirection()
    {
        return Vector3.down;
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
                FarmTile farmTile = newTarget.GetComponent<FarmTile>();
                if (farmTile != null)
                {
                    bool canPlant = farmTile.CanPlantSeed(seedData);
                    string status = canPlant ? "심기 가능" : "심기 불가능";
                    Debug.Log($"[SeedItem] 농장 타일 감지: {farmTile.name} ({status})");
                }
            }
        }
        
        lastHighlightedTile = newTarget?.GetComponent<FarmTile>();
    }
    
    /// <summary>
    /// 커스텀 디버그 기즈모 - 아래쪽 레이캐스트 표시
    /// </summary>
    protected override void DrawCustomGizmos()
    {
        // 씨앗 심기 감지 범위 표시 (구체)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
    
    
    /// <summary>
    /// 씨앗을 심는 실제 로직
    /// </summary>
    private void PlantSeedOnTile(FarmTile targetTile)
    {
        if (targetTile == null || seedData == null)
            return;
        
        // 농장 타일에 씨앗 심기
        bool planted = targetTile.PlantSeed(seedData);
        
        if (planted)
        {
            Debug.Log($"[SeedItem] {seedData.itemName} 씨앗을 성공적으로 심었습니다!");
            
            // 씨앗 아이템 제거 (또는 인벤토리에서 차감)
            DestroySeedItem();
        }
        else
        {
            Debug.LogWarning($"[SeedItem] {seedData.itemName} 씨앗을 심는데 실패했습니다.");
        }
    }
    
    /// <summary>
    /// 씨앗 아이템을 제거 (심은 후)
    /// </summary>
    private void DestroySeedItem()
    {
        // 잡고 있는 상태를 해제
        if (IsGrabbed && grabInteractable != null && grabInteractable.isSelected)
        {
            grabInteractable.interactionManager.SelectExit(
                grabInteractable.interactorsSelecting[0], 
                grabInteractable
            );
        }
        
        // 오브젝트 제거
        Destroy(gameObject);
    }
    
    // 디버그 기즈모는 부모 클래스에서 처리
    
    /// <summary>
    /// GrabbableItem 추상 메서드 구현 - 아이템 데이터 반환
    /// </summary>
    public override ItemData GetItemData()
    {
        return seedData;
    }
    
    /// <summary>
    /// GrabbableItem 추상 메서드 구현 - 수량 반환
    /// </summary>
    public override int GetQuantity()
    {
        return 1; // 씨앗은 개별 아이템
    }
    
    /// <summary>
    /// GrabbableItem 추상 메서드 구현 - 도구 사용 입력 처리
    /// EquipState의 ToolUseAction을 통해 씨앗 심기 실행
    /// </summary>
    public override void OnToolUseInput()
    {
        // 씨앗이 잡힌 상태에서만 심기 가능
        if (!IsGrabbed)
        {
            Debug.Log($"[SeedItem] {seedData?.itemName}: 씨앗을 잡지 않은 상태에서는 심을 수 없습니다.");
            return;
        }
        
        // 현재 타겟이 FarmTile인지 확인하고 씨앗 심기
        FarmTile targetTile = GetCurrentTarget()?.GetComponent<FarmTile>();
        
        if (targetTile != null && seedData != null)
        {
            bool canPlant = targetTile.CanPlantSeed(seedData);
            if (canPlant)
            {
                Debug.Log($"[SeedItem] ToolUseAction을 통해 {seedData.itemName} 씨앗 심기를 시도합니다.");
                PlantSeedOnTile(targetTile);
            }
            else
            {
                Debug.LogWarning($"[SeedItem] {targetTile.name}에 {seedData?.itemName} 씨앗을 심을 수 없습니다.");
            }
        }
        else
        {
            Debug.Log($"[SeedItem] 심을 수 있는 농장 타일을 찾지 못했습니다. 괭이로 갈은 땅 위에서 시도해보세요.");
        }
    }
    
    // 컴포넌트 정리는 부모 클래스에서 처리
}