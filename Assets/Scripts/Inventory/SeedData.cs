using UnityEngine;

[CreateAssetMenu(menuName = "Item/Seed")]
public class SeedData : ItemData
{
    [Header("Growth Settings")]
    public int daysToGrow = 3; // 작물이 자라는데 걸리는 시간 (일 단위)
    public ItemData cropItem; // 수확 시 생성되는 작물 아이템
    
    [Header("Growth Stages")]
    [Tooltip("성장 단계 수 (0: 씨앗, 최대값: 완전 성장)")]
    public int maxGrowthStages = 3;
    
    [Tooltip("성장 단계별 프리팹들")]
    public GameObject[] growthStagePrefabs;
    
    [Header("Harvest Settings")]
    [Tooltip("수확 시 기본 획득 수량")]
    public int baseHarvestAmount = 1;
    
    [Tooltip("수확 시 씨앗 드롭 확률 (0~1)")]
    [Range(0f, 1f)]
    public float seedDropChance = 0.3f;
    
    [Tooltip("수확 시 씨앗 드롭 수량")]
    public int seedDropAmount = 1;
    
    [Header("Special Properties")]
    [Tooltip("여러 번 수확 가능한 작물인가? (예: 베리류)")]
    public bool isRepeatedHarvest = false;
    
    /// <summary>
    /// 특정 성장 단계의 프리팹을 반환
    /// </summary>
    public GameObject GetGrowthStagePrefab(int stage)
    {
        if (growthStagePrefabs == null || growthStagePrefabs.Length == 0)
        {
            return gameModel; // 기본 모델 반환
        }
        
        int clampedStage = Mathf.Clamp(stage, 0, growthStagePrefabs.Length - 1);
        return growthStagePrefabs[clampedStage] ?? gameModel;
    }
    
    /// <summary>
    /// 작물이 완전히 성장했는지 확인
    /// </summary>
    public bool IsFullyGrown(int currentStage)
    {
        return currentStage >= maxGrowthStages;
    }
    
    /// <summary>
    /// 수확 보상 계산
    /// </summary>
    public HarvestReward CalculateHarvestReward()
    {
        HarvestReward reward = new HarvestReward();
        
        if (cropItem != null)
        {
            reward.mainItem = cropItem;
            reward.mainItemAmount = baseHarvestAmount;
        }
        
        if (Random.value <= seedDropChance)
        {
            reward.bonusSeeds = seedDropAmount;
        }
        
        return reward;
    }
}

/// <summary>
/// 수확 보상 정보
/// </summary>
[System.Serializable]
public struct HarvestReward
{
    public ItemData mainItem;
    public int mainItemAmount;
    public int bonusSeeds;
}
