using UnityEngine;

/// <summary>
/// 농장 타일 - 씨앗을 심고 작물을 재배할 수 있는 타일
/// 
/// == 주요 기능 ==
/// 1. LandObject 기반 농장 시스템
/// 2. 씨앗 심기 및 작물 성장 관리
/// 3. 실시간 성장 시간 추적
/// 4. 작물 수확 시스템
/// 5. 그리드 기반 배치 지원
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
    [Tooltip("작물 오브젝트가 생성될 위치 (로컬 좌표)")]
    public Vector3 cropSpawnOffset = Vector3.up * 0.1f;
    
    // 현재 성장 단계 (캐시)
    private int currentGrowthStage = 0;
    
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
        currentGrowthStage = 0;
        
        // 초기 작물 시각적 오브젝트 생성
        UpdateCropVisual();
        
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
        
        // 반복 수확 가능한 작물이면 재수확 상태로 변경
        if (plantedSeed.isRepeatedHarvest)
        {
            currentState = FarmTileState.Growing;
            plantedTime = Time.time; // 재수확 타이머 리셋
            currentGrowthStage = 0;  // 성장 단계 리셋
            UpdateCropVisual();
        }
        else
        {
            // 일반 작물은 수확 후 빈 타일로 변경
            ClearCrop();
        }
        
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
        int newGrowthStage = CalculateGrowthStage(growthTime);
        
        // 성장 단계가 변경되었으면 시각적 업데이트
        if (newGrowthStage != currentGrowthStage)
        {
            currentGrowthStage = newGrowthStage;
            UpdateCropVisual();
            
            if (enableDebugLogs)
                Debug.Log($"[FarmTile] {plantedSeed.itemName} 성장 단계: {currentGrowthStage}/{plantedSeed.maxGrowthStages}");
        }
        
        // 완전 성장 체크
        if (plantedSeed.IsFullyGrown(currentGrowthStage))
        {
            currentState = FarmTileState.ReadyToHarvest;
            
            // 수확 가능한 상호작용 컴포넌트 추가
            AddHarvestableComponent();
            
            if (enableDebugLogs)
                Debug.Log($"[FarmTile] {plantedSeed.itemName} 작물이 완전히 자랐습니다! 수확 가능.");
        }
    }
    
    /// <summary>
    /// 작물의 시각적 표현을 업데이트
    /// </summary>
    private void UpdateCropVisual()
    {
        // 기존 시각적 오브젝트 제거
        if (currentCropVisual != null)
        {
            if (Application.isPlaying)
                Destroy(currentCropVisual);
            else
                DestroyImmediate(currentCropVisual);
        }
        
        if (plantedSeed == null)
            return;
        
        // 새로운 성장 단계 프리팹 생성
        GameObject stagePrefab = plantedSeed.GetGrowthStagePrefab(currentGrowthStage);
        if (stagePrefab != null)
        {
            currentCropVisual = Instantiate(stagePrefab, transform);
            currentCropVisual.transform.localPosition = cropSpawnOffset;
            currentCropVisual.name = $"{plantedSeed.itemName}_Stage{currentGrowthStage}";
        }
    }
    
    /// <summary>
    /// 완전히 자란 작물에 수확 가능한 상호작용 컴포넌트 추가
    /// </summary>
    private void AddHarvestableComponent()
    {
        if (currentCropVisual == null) return;
        
        // 이미 HarvestableItem 컴포넌트가 있는지 확인
        HarvestableItem existingHarvest = currentCropVisual.GetComponent<HarvestableItem>();
        if (existingHarvest != null) return;
        
        // HarvestableItem 컴포넌트 추가
        HarvestableItem harvestable = currentCropVisual.AddComponent<HarvestableItem>();
        harvestable.parentFarmTile = this;
        
        // 콜라이더가 없다면 추가 (상호작용을 위해 필요)
        Collider collider = currentCropVisual.GetComponent<Collider>();
        if (collider == null)
        {
            // 기본적으로 BoxCollider 추가
            BoxCollider boxCollider = currentCropVisual.AddComponent<BoxCollider>();
            
            // 렌더러가 있다면 bounds에 맞춰 크기 조정
            Renderer renderer = currentCropVisual.GetComponent<Renderer>();
            if (renderer != null)
            {
                boxCollider.size = renderer.bounds.size;
                boxCollider.center = renderer.bounds.center - currentCropVisual.transform.position;
            }
        }
        
        if (enableDebugLogs)
            Debug.Log($"[FarmTile] {plantedSeed.itemName} 작물에 수확 상호작용 컴포넌트를 추가했습니다.");
    }
    
    /// <summary>
    /// 작물을 제거하고 타일을 초기화
    /// </summary>
    public void ClearCrop()
    {
        plantedSeed = null;
        currentState = FarmTileState.Tilled; // 갈아진 상태로 유지
        plantedTime = 0f;
        currentGrowthStage = 0;
        
        // 시각적 오브젝트 제거
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
    /// 현재 성장 시간으로 성장 단계를 계산
    /// </summary>
    /// <param name="currentGrowthTime">현재까지 자란 시간 (초)</param>
    /// <returns>현재 성장 단계</returns>
    private int CalculateGrowthStage(float currentGrowthTime)
    {
        if (plantedSeed == null) return 0;
        
        // 일 단위를 초 단위로 변환 (1일 = 60초로 설정, 게임 플레이를 위해 빠르게)
        float totalGrowthTimeInSeconds = plantedSeed.daysToGrow * 60f; // 1일 = 1분
        
        float effectiveGrowthTime = currentGrowthTime * growthMultiplier;
        float progressRatio = effectiveGrowthTime / totalGrowthTimeInSeconds;
        
        // 단계별 균등 분배
        int stage = Mathf.FloorToInt(progressRatio * (plantedSeed.maxGrowthStages + 1));
        return Mathf.Clamp(stage, 0, plantedSeed.maxGrowthStages);
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
        float totalGrowthTimeInSeconds = plantedSeed.daysToGrow * 60f; // 1일 = 1분
        float effectiveGrowthTime = growthTime * growthMultiplier;
        
        return Mathf.Clamp01(effectiveGrowthTime / totalGrowthTimeInSeconds);
    }
    
    /// <summary>
    /// 다음 성장 단계까지 남은 시간 반환
    /// </summary>
    /// <returns>남은 시간 (초)</returns>
    public float GetTimeToNextStage()
    {
        if (plantedSeed == null || currentState != FarmTileState.Growing)
            return 0f;
        
        float growthTime = Time.time - plantedTime;
        float totalGrowthTimeInSeconds = plantedSeed.daysToGrow * 60f; // 1일 = 1분
        
        int currentStage = CalculateGrowthStage(growthTime);
        if (currentStage >= plantedSeed.maxGrowthStages)
            return 0f; // 이미 완전 성장
        
        float timePerStage = totalGrowthTimeInSeconds / (plantedSeed.maxGrowthStages + 1);
        float nextStageTime = (currentStage + 1) * timePerStage;
        float effectiveCurrentTime = growthTime * growthMultiplier;
        
        return Mathf.Max(0f, (nextStageTime - effectiveCurrentTime) / growthMultiplier);
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
            info += $"Growth Stage: {currentGrowthStage}/{plantedSeed.maxGrowthStages}\n";
            info += $"Growth Progress: {GetGrowthProgress():P1}\n";
            info += $"Time to Next Stage: {GetTimeToNextStage():F1}s\n";
            info += $"Growth Multiplier: {growthMultiplier}x\n";
        }
        
        return info;
    }
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