using UnityEngine;

/// <summary>
/// ==================== FARM TILE SYSTEM ====================
/// 
/// 【 시스템 개요 】
/// 씨앗을 심고 작물을 재배할 수 있는 농장 타일입니다.
/// LandObject를 상속받아 기본 땅 상호작용 기능을 포함하며,
/// IInteractable 인터페이스를 오버라이드하여 수확 기능을 제공합니다.
/// 
/// 【 상속 구조 】
/// MonoBehaviour → InteractionObject → LandObject → FarmTile
/// ├── IInteractable (LandObject에서 구현, FarmTile에서 오버라이드)
/// └── 그리드 배치 지원
/// 
/// 【 주요 기능 】
/// 1. 씨앗 심기 및 작물 성장 관리 (SeedData 기반)
/// 2. 실시간 성장 시간 추적 및 업데이트
/// 3. 이중 상호작용 시스템:
///    - 수확 가능 상태: VR 컨트롤러로 작물 수확
///    - 일반 상태: 괭이로 땅 갈기 (LandObject 기본 기능)
/// 4. 시각적 피드백 (성장 단계별 모델, 하이라이트)
/// 5. 그리드 기반 배치 지원
/// 
/// 【 상태 전환 흐름 】
/// Empty → Tilled (괭이 사용) → Growing (씨앗 심기) → ReadyToHarvest (수확 가능)
/// 
/// 【 연동 시스템 】
/// - SeedData: 작물 정보 및 성장 설정
/// - EquipState: VR 컨트롤러 상호작용 관리
/// - GridManager: 그리드 배치 시스템
/// - InventoryManager: 수확물 드롭 및 수집
/// </summary>
public class FarmTile : LandObject
{
    [Header("Farm Tile Settings")]
    [Tooltip("현재 심어진 씨앗 데이터")]
    public SeedData plantedSeed;
    
    [Tooltip("현재 작물 상태")]
    public FarmTileState currentState = FarmTileState.Empty;
    
    [Tooltip("씨앗을 심은 시간 (Time.time 기준)")]
    public float plantedTime = 0f;
    
    [Tooltip("현재 성장 가속 배율")]
    public float growthMultiplier = 1.0f;
    
    [Tooltip("현재 작물의 시각적 오브젝트")]
    public GameObject currentCropVisual;
    
    [Header("Visual Settings")]
    [Tooltip("씨앗 심은 후 표시할 흙더미 오브젝트")]
    public GameObject soilMoundObject;
    
    [Tooltip("완전 성장 시 생성될 CropItem 위치 (로컬 좌표)")]
    public Vector3 cropItemSpawnOffset = Vector3.up * 0.5f;
    
    [Header("Harvest Interaction")]
    [Tooltip("VR 컨트롤러 감지 거리")]
    public float harvestDetectionRadius = 0.5f;
    
    // 성장 업데이트 최적화를 위한 타이머
    private float lastGrowthUpdateTime = 0f;
    private const float GROWTH_UPDATE_INTERVAL = 1.0f; // 1초마다 성장 체크
    
    void Start()
    {
        base.Start();
        
        // 호환 가능한 도구 타입에 씨앗 심기 추가
        if (compatibleToolTypes == null || compatibleToolTypes.Length == 0)
        {
            compatibleToolTypes = new EquipmentData.ToolType[] { EquipmentData.ToolType.Hoe };
        }
        
        // FarmTile 레이어 설정 (씨앗 감지용)
        if (gameObject.layer == 0) // Default 레이어인 경우
        {
            gameObject.layer = LayerMask.NameToLayer("Default"); // 또는 FarmTile 전용 레이어 생성 가능
        }
    }
    
    void Update()
    {
        // 작물이 성장 중일 때만 업데이트
        if (currentState == FarmTileState.Growing && plantedSeed != null)
        {
            // 최적화: 일정 간격으로만 성장 체크
            if (Time.time - lastGrowthUpdateTime >= GROWTH_UPDATE_INTERVAL)
            {
                UpdateCropGrowth();
                lastGrowthUpdateTime = Time.time;
            }
        }
    }
    
    public override void InteractWithTool(EquipmentData.ToolType toolType)
    {
        switch (toolType)
        {
            case EquipmentData.ToolType.Hoe:
                if (currentState == FarmTileState.Empty && landType == LandType.Grass)
                {
                    ProcessWithHoe();
                }
                else if (enableDebugLogs)
                {
                    Debug.LogWarning($"[FarmTile] {gameObject.name}: 이미 경작된 땅이거나 적절하지 않은 상태입니다.");
                }
                break;
            default:
                base.InteractWithTool(toolType);
                break;
        }
    }
    
    /// <summary>
    /// 씨앗을 심는 메서드
    /// </summary>
    /// <param name="seedData">심을 씨앗 데이터</param>
    /// <returns>성공 여부</returns>
    public bool PlantSeed(SeedData seedData)
    {
        // 심기 조건 검사
        if (!CanPlantSeed(seedData))
        {
            return false;
        }
        
        // 씨앗 심기
        plantedSeed = seedData;
        currentState = FarmTileState.Growing;
        plantedTime = Time.time;
        
        // 흙더미 오브젝트 활성화
        if (soilMoundObject != null)
            soilMoundObject.SetActive(true);
        
        if (enableDebugLogs)
            Debug.Log($"[FarmTile] {gameObject.name}: {seedData.itemName} 씨앗을 심었습니다.");
        
        return true;
    }
    
    /// <summary>
    /// 씨앗을 심을 수 있는지 검사
    /// </summary>
    /// <param name="seedData">심을 씨앗 데이터</param>
    /// <returns>심기 가능 여부</returns>
    public bool CanPlantSeed(SeedData seedData)
    {
        if (seedData == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[FarmTile] 씨앗 데이터가 null입니다.");
            return false;
        }
        
        if (currentState != FarmTileState.Tilled)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[FarmTile] 땅이 갈아지지 않았습니다. 먼저 괭이로 갈아주세요.");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 작물을 수확하는 메서드
    /// </summary>
    /// <returns>수확 보상</returns>
    public HarvestReward HarvestCrop()
    {
        HarvestReward reward = new HarvestReward();
        
        if (currentState != FarmTileState.ReadyToHarvest || plantedSeed == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[FarmTile] 수확할 수 있는 작물이 없습니다.");
            return reward;
        }
        
        // 수확 보상 계산
        reward = plantedSeed.CalculateHarvestReward();
        
        if (enableDebugLogs)
            Debug.Log($"[FarmTile] {plantedSeed.itemName} 작물을 수확했습니다. " +
                     $"보상: {reward.mainItem?.itemName} x{reward.mainItemAmount}, 씨앗 x{reward.bonusSeeds}");
        
        // Crop 오브젝트를 월드에 드롭
        DropCropItem();
        
        // 수확 후 빈 타일로 변경
        ClearCrop();
        
        return reward;
    }
    
    /// <summary>
    /// 작물 성장 상태를 업데이트
    /// </summary>
    private void UpdateCropGrowth()
    {
        if (plantedSeed == null || currentState != FarmTileState.Growing)
            return;
        
        float growthTime = Time.time - plantedTime;
        float totalGrowthTimeInSeconds = plantedSeed.minutesToGrow * 60f;
        float effectiveGrowthTime = growthTime * growthMultiplier;
        
        // 완전 성장 체크
        if (effectiveGrowthTime >= totalGrowthTimeInSeconds)
        {
            currentState = FarmTileState.ReadyToHarvest;
            
            // Crop 오브젝트 생성
            CreateCropItem();
            
            if (enableDebugLogs)
                Debug.Log($"[FarmTile] {plantedSeed.itemName} 작물이 완전히 자랐습니다! 수확 가능.");
        }
    }
    
    
    /// <summary>
    /// 완전 성장 시 CropItem 생성
    /// </summary>
    private void CreateCropItem()
    {
        if (plantedSeed?.cropItem == null) return;
        
        // 이미 CropItem이 생성되어 있으면 건드리지 않음
        if (currentCropVisual != null) return;
        
        // CropItem의 gameModel을 사용하여 오브젝트 생성
        GameObject cropModel = plantedSeed.cropItem.gameModel;
        if (cropModel != null)
        {
            currentCropVisual = Instantiate(cropModel, transform);
            currentCropVisual.transform.localPosition = cropItemSpawnOffset;
            currentCropVisual.name = $"{plantedSeed.cropItem.itemName}_Crop";
            
            // Rigidbody가 있다면 kinematic으로 설정 (떨어지지 않도록)
            Rigidbody rb = currentCropVisual.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            
            // 작물은 순수 시각적 오브젝트로만 사용 (FarmTile에서 상호작용 처리)
            
            if (enableDebugLogs)
                Debug.Log($"[FarmTile] CropItem 생성: {plantedSeed.cropItem.itemName}");
        }
    }
    
    /// <summary>
    /// 수확 시 Crop 오브젝트를 월드에 드롭
    /// </summary>
    private void DropCropItem()
    {
        if (currentCropVisual == null || plantedSeed?.cropItem == null) return;
        
        // 현재 CropVisual을 부모에서 분리
        currentCropVisual.transform.SetParent(null);
        
        // 수확물은 이미 HarvestableItem(GrabbableItem)이므로 별도 컴포넌트 추가 불필요
        
        // Rigidbody kinematic 해제 (물리 효과를 위해)
        Rigidbody rb = currentCropVisual.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = currentCropVisual.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;
        
        // 약간의 힘을 가해서 드롭 효과 연출
        Vector3 dropForce = Vector3.up * 2f + Random.insideUnitSphere * 1f;
        rb.AddForce(dropForce, ForceMode.Impulse);
        
        // 흙더미 비활성화
        if (soilMoundObject != null)
            soilMoundObject.SetActive(false);
        
        // 작물에는 더 이상 특별한 컴포넌트가 없음 (순수 시각적 오브젝트)
        
        if (enableDebugLogs)
            Debug.Log($"[FarmTile] {plantedSeed.cropItem.itemName} 작물이 수집 가능한 아이템으로 드롭되었습니다.");
        
        currentCropVisual = null;
    }
    
    
    /// <summary>
    /// 작물을 제거하고 타일을 초기화
    /// </summary>
    public void ClearCrop()
    {
        plantedSeed = null;
        currentState = FarmTileState.Tilled; // 갈아진 상태로 유지
        plantedTime = 0f;
        
        // 흙더미 오브젝트 비활성화
        if (soilMoundObject != null)
            soilMoundObject.SetActive(false);
        
        // CropItem 시각적 오브젝트 제거
        if (currentCropVisual != null)
        {
            if (Application.isPlaying)
                Destroy(currentCropVisual);
            else
                DestroyImmediate(currentCropVisual);
            currentCropVisual = null;
        }
        
        if (enableDebugLogs)
            Debug.Log($"[FarmTile] {gameObject.name}: 작물이 제거되었습니다.");
    }
    
    /// <summary>
    /// 괭이로 땅을 갈아엎는 처리 (오버라이드)
    /// </summary>
    protected override void ProcessWithHoe()
    {
        base.ProcessWithHoe(); // 기본 LandObject 동작 수행
        
        // 농장 타일 상태 설정
        if (currentState == FarmTileState.Empty)
        {
            currentState = FarmTileState.Tilled;
            
            if (enableDebugLogs)
                Debug.Log($"[FarmTile] {gameObject.name}: 땅이 갈아졌습니다. 씨앗을 심을 수 있습니다.");
        }
    }
    
    
    /// <summary>
    /// 성장 가속 배율을 설정 (비료, 마법 등)
    /// </summary>
    /// <param name="multiplier">가속 배율</param>
    public void SetGrowthMultiplier(float multiplier)
    {
        growthMultiplier = Mathf.Clamp(multiplier, 1.0f, 5.0f);
        
        if (enableDebugLogs)
            Debug.Log($"[FarmTile] {gameObject.name}: 성장 가속 배율이 {growthMultiplier}x로 설정되었습니다.");
    }
    
    /// <summary>
    /// 현재 작물의 성장 진행률 반환 (0~1)
    /// </summary>
    /// <returns>성장 진행률</returns>
    public float GetGrowthProgress()
    {
        if (plantedSeed == null || currentState != FarmTileState.Growing)
            return 0f;
        
        float growthTime = Time.time - plantedTime;
        float totalGrowthTimeInSeconds = plantedSeed.minutesToGrow * 60f;
        float effectiveGrowthTime = growthTime * growthMultiplier;
        
        return Mathf.Clamp01(effectiveGrowthTime / totalGrowthTimeInSeconds);
    }
    
    /// <summary>
    /// 완전 성장까지 남은 시간 반환
    /// </summary>
    /// <returns>남은 시간 (초)</returns>
    public float GetTimeToGrowth()
    {
        if (plantedSeed == null || currentState != FarmTileState.Growing)
            return 0f;
        
        float growthTime = Time.time - plantedTime;
        float totalGrowthTimeInSeconds = plantedSeed.minutesToGrow * 60f;
        float effectiveGrowthTime = growthTime * growthMultiplier;
        
        return Mathf.Max(0f, (totalGrowthTimeInSeconds - effectiveGrowthTime) / growthMultiplier);
    }
    
    /// <summary>
    /// 씨앗 심기용 하이라이트 표시 (GizmosSelected 오버라이드)
    /// </summary>
    /// <param name="select">하이라이트 표시 여부</param>
    /// <param name="canPlant">씨앗을 심을 수 있는지 여부 (색상 결정용)</param>
    public void ShowPlantingHighlight(bool select, bool canPlant = true)
    {
        if (selectObject != null)
        {
            selectObject.SetActive(select);
            
            if (select)
            {
                var renderer = selectObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // 심기 가능 여부에 따른 색상 설정
                    if (canPlant && currentState == FarmTileState.Tilled)
                    {
                        renderer.material.color = Color.green; // 심기 가능
                    }
                    else
                    {
                        renderer.material.color = Color.red;   // 심기 불가능
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 기본 상호작용용 하이라이트 (기존 동작 유지)
    /// </summary>
    /// <param name="select">하이라이트 표시 여부</param>
    public override void GizmosSelected(bool select)
    {
        if (selectObject != null)
        {
            selectObject.SetActive(select);
            
            if (select)
            {
                var renderer = selectObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // 기본 하이라이트 색상 (파란색 등)
                    renderer.material.color = Color.blue;
                }
            }
        }
    }
    
    /// <summary>
    /// 디버그 정보 표시
    /// </summary>
    public string GetDebugInfo()
    {
        string info = $"FarmTile: {gameObject.name}\n";
        info += $"State: {currentState}\n";
        info += $"Land Type: {landType}\n";
        
        if (plantedSeed != null)
        {
            info += $"Crop: {plantedSeed.itemName}\n";
            info += $"Growth Progress: {GetGrowthProgress():P1}\n";
            info += $"Time to Growth: {GetTimeToGrowth():F1}s\n";
            info += $"Growth Multiplier: {growthMultiplier}x\n";
        }
        
        return info;
    }
    
    #region IInteractable Implementation Override
    
    /// <summary>
    /// FarmTile 전용 상호작용 가능 여부 확인
    /// 수확 가능한 상태이거나 기본 LandObject 상호작용이 가능한 경우
    /// </summary>
    /// <returns>
    /// true: 수확 가능한 작물이 있거나 기본 도구 상호작용 가능
    /// false: 상호작용 불가능
    /// </returns>
    public override bool CanInteract()
    {
        // 우선순위 1: 수확 가능한 작물이 있는 경우
        if (currentState == FarmTileState.ReadyToHarvest && plantedSeed != null)
            return true;
            
        // 우선순위 2: 기본 LandObject 상호작용 (괭이 사용 등)
        return base.CanInteract();
    }
    
    /// <summary>
    /// FarmTile 전용 호버 시작 처리
    /// 수확 가능한 상태에 따라 다른 하이라이트 표시
    /// </summary>
    public override void OnHoverEnter()
    {
        if (currentState == FarmTileState.ReadyToHarvest && plantedSeed != null)
        {
            // 수확 가능한 경우: 수확용 하이라이트 (녹색)
            ShowPlantingHighlight(true, true);
            
            if (enableDebugLogs)
                Debug.Log($"[FarmTile] {plantedSeed.itemName} 작물 수확 가능 - 도구 사용 버튼을 눌러 수확하세요!");
        }
        else
        {
            // 일반 상호작용 가능한 경우: 기본 하이라이트
            base.OnHoverEnter();
        }
    }
    
    /// <summary>
    /// FarmTile 전용 호버 종료 처리
    /// 모든 하이라이트 제거
    /// </summary>
    public override void OnHoverExit()
    {
        ShowPlantingHighlight(false);
        base.OnHoverExit();
    }
    
    /// <summary>
    /// FarmTile 전용 상호작용 실행
    /// 상태에 따라 수확 또는 기본 도구 상호작용 수행
    /// </summary>
    public override void OnInteract()
    {
        if (currentState == FarmTileState.ReadyToHarvest && plantedSeed != null)
        {
            // 수확 가능한 경우: 작물 수확 실행
            HarvestCrop();
            
            if (enableDebugLogs)
                Debug.Log($"[FarmTile] {plantedSeed.itemName} 작물을 수확했습니다!");
        }
        else
        {
            // 일반 상호작용: 기본 LandObject 동작 (괭이 사용 등)
            base.OnInteract();
        }
    }
    
    #endregion
}

/// <summary>
/// 농장 타일의 상태를 나타내는 열거형
/// </summary>
public enum FarmTileState
{
    Empty,          // 빈 땅 (풀밭)
    Tilled,         // 갈아진 땅 (씨앗 심기 가능)
    Growing,        // 작물 성장 중
    ReadyToHarvest  // 수확 가능
}