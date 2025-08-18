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
public class SeedItem : GrabbableItem
{
    [Header("Seed Settings")]
    [Tooltip("이 씨앗의 데이터")]
    public SeedData seedData;
    
    [Header("Planting System")]
    [Tooltip("씨앗 심기 감지 범위")]
    public float plantingRange = 2.0f;
    
    [Tooltip("레이캐스트 레이어 마스크 (FarmTile만 감지)")]
    public LayerMask farmTileLayerMask = -1;
    
    // 현재 타겟 농장 타일
    private FarmTile currentTargetTile;
    
    // 마지막으로 하이라이트된 타일 (중복 처리 방지)
    private FarmTile lastHighlightedTile;
    
    void Start()
    {
        if (grabInteractable != null)
        {
            // 잡았을 때와 놓았을 때 이벤트 등록
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }
        
        // 씨앗 데이터 검증
        if (seedData == null)
        {
            Debug.LogError($"[SeedItem] {gameObject.name}: SeedData가 할당되지 않았습니다!");
        }
    }
    
    void Update()
    {
        // 씨앗이 잡혀있을 때만 농장 타일 감지
        if (IsGrabbed)
        {
            DetectFarmTile();
        }
    }
    
    /// <summary>
    /// 씨앗이 잡혔을 때 호출
    /// </summary>
    /// <param name="args">선택 이벤트 인자</param>
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        Debug.Log($"[SeedItem] {seedData?.itemName} 씨앗을 잡았습니다. 농장을 찾아 심어보세요!");
    }
    
    /// <summary>
    /// 씨앗을 놓았을 때 호출
    /// </summary>
    /// <param name="args">선택 해제 이벤트 인자</param>
    private void OnReleased(SelectExitEventArgs args)
    {
        // 하이라이트 해제
        ClearHighlight();
        
        Debug.Log($"[SeedItem] {seedData?.itemName} 씨앗을 놓았습니다.");
    }
    
    /// <summary>
    /// 농장 타일을 감지하고 하이라이트 처리
    /// </summary>
    private void DetectFarmTile()
    {
        // 씨앗에서 아래쪽으로 레이캐스트
        Ray ray = new Ray(transform.position, Vector3.down);
        RaycastHit hit;
        
        FarmTile detectedTile = null;
        
        if (Physics.Raycast(ray, out hit, plantingRange, farmTileLayerMask))
        {
            detectedTile = hit.collider.GetComponent<FarmTile>();
        }
        
        // 타겟 타일이 변경되었으면 하이라이트 업데이트
        if (detectedTile != currentTargetTile)
        {
            // 이전 타일 하이라이트 해제
            if (currentTargetTile != null)
            {
                SetTileHighlight(currentTargetTile, false);
            }
            
            currentTargetTile = detectedTile;
            
            // 새 타일 하이라이트 설정
            if (currentTargetTile != null)
            {
                bool canPlant = currentTargetTile.CanPlantSeed(seedData);
                SetTileHighlight(currentTargetTile, canPlant);
                
                if (currentTargetTile != lastHighlightedTile)
                {
                    string status = canPlant ? "심기 가능" : "심기 불가능";
                    Debug.Log($"[SeedItem] 농장 타일 감지: {currentTargetTile.name} ({status})");
                }
            }
            
            lastHighlightedTile = currentTargetTile;
        }
    }
    
    /// <summary>
    /// 농장 타일 하이라이트 설정/해제
    /// </summary>
    /// <param name="tile">타겟 타일</param>
    /// <param name="canPlant">심기 가능 여부</param>
    private void SetTileHighlight(FarmTile tile, bool canPlant)
    {
        if (tile == null) return;
        
        // FarmTile의 전용 씨앗 심기 하이라이트 사용
        tile.ShowPlantingHighlight(true, canPlant);
    }
    
    /// <summary>
    /// 모든 하이라이트 해제
    /// </summary>
    private void ClearHighlight()
    {
        if (currentTargetTile != null)
        {
            currentTargetTile.ShowPlantingHighlight(false);
            currentTargetTile = null;
        }
        lastHighlightedTile = null;
    }
    
    
    /// <summary>
    /// 씨앗을 심는 실제 로직
    /// </summary>
    private void PlantSeed()
    {
        if (currentTargetTile == null || seedData == null)
            return;
        
        // 농장 타일에 씨앗 심기
        bool planted = currentTargetTile.PlantSeed(seedData);
        
        if (planted)
        {
            Debug.Log($"[SeedItem] {seedData.itemName} 씨앗을 성공적으로 심었습니다!");
            
            // 하이라이트 해제
            ClearHighlight();
            
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
    
    /// <summary>
    /// 디버그용 기즈모 그리기
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 씨앗 심기 감지 범위 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, plantingRange);
        
        // 아래쪽으로 레이 표시
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector3.down * plantingRange);
    }
    
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
        
        // 현재 타겟 농장 타일이 있고 심기 가능한 상태인지 확인
        if (currentTargetTile != null && seedData != null)
        {
            bool canPlant = currentTargetTile.CanPlantSeed(seedData);
            if (canPlant)
            {
                Debug.Log($"[SeedItem] ToolUseAction을 통해 {seedData.itemName} 씨앗 심기를 시도합니다.");
                PlantSeed();
            }
            else
            {
                Debug.LogWarning($"[SeedItem] {currentTargetTile.name}에 {seedData?.itemName} 씨앗을 심을 수 없습니다.");
            }
        }
        else
        {
            Debug.Log($"[SeedItem] 심을 수 있는 농장 타일을 찾지 못했습니다. 괭이로 갈은 땅 위에서 시도해보세요.");
        }
    }
    
    /// <summary>
    /// 컴포넌트 정리
    /// </summary>
    void OnDestroy()
    {
        ClearHighlight();
        
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }
}