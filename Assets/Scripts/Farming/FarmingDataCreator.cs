using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 농업 시스템 테스트용 데이터 생성 유틸리티
/// 
/// == 주요 기능 ==
/// 1. 샘플 씨앗 데이터 자동 생성
/// 2. 기본 작물 아이템 데이터 생성
/// 3. 에디터 컨텍스트 메뉴를 통한 쉬운 생성
/// 4. 농업 시스템 테스트 환경 구축
/// </summary>
public class FarmingDataCreator : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Data Generation Settings")]
    [Tooltip("생성된 데이터를 저장할 폴더 경로")]
    public string dataFolderPath = "Assets/Data/Farming/";
    
    [Tooltip("생성할 씨앗 및 작물 데이터")]
    public FarmingDataPreset[] dataPresets = new FarmingDataPreset[]
    {
        new FarmingDataPreset
        {
            seedName = "당근 씨앗",
            cropName = "당근",
            growthTime = 60f, // 1분
            maxStages = 3,
            harvestAmount = 2,
            seedDropChance = 0.4f,
            seedDropAmount = 1
        },
        new FarmingDataPreset
        {
            seedName = "감자 씨앗", 
            cropName = "감자",
            growthTime = 90f, // 1.5분
            maxStages = 4,
            harvestAmount = 3,
            seedDropChance = 0.3f,
            seedDropAmount = 2
        },
        new FarmingDataPreset
        {
            seedName = "밀 씨앗",
            cropName = "밀",
            growthTime = 120f, // 2분
            maxStages = 5,
            harvestAmount = 4,
            seedDropChance = 0.5f,
            seedDropAmount = 3
        },
        new FarmingDataPreset
        {
            seedName = "토마토 씨앗",
            cropName = "토마토",
            growthTime = 150f, // 2.5분
            maxStages = 4,
            harvestAmount = 2,
            seedDropChance = 0.2f,
            seedDropAmount = 1,
        }
    };
    
    [ContextMenu("모든 농업 데이터 생성")]
    public void CreateAllFarmingData()
    {
        // 폴더 생성
        CreateFoldersIfNeeded();
        
        foreach (var preset in dataPresets)
        {
            CreateFarmingDataFromPreset(preset);
        }
        
        AssetDatabase.Refresh();
        Debug.Log($"[FarmingDataCreator] 총 {dataPresets.Length}개의 농업 데이터를 생성했습니다!");
    }
    
    [ContextMenu("당근 데이터만 생성")]
    public void CreateCarrotData()
    {
        CreateFoldersIfNeeded();
        if (dataPresets.Length > 0)
        {
            CreateFarmingDataFromPreset(dataPresets[0]);
            AssetDatabase.Refresh();
            Debug.Log("[FarmingDataCreator] 당근 데이터를 생성했습니다!");
        }
    }
    
    [ContextMenu("폴더 구조 생성")]
    public void CreateFoldersIfNeeded()
    {
        string[] folders = {
            dataFolderPath,
            dataFolderPath + "Seeds/",
            dataFolderPath + "Crops/",
            dataFolderPath + "Icons/",
            dataFolderPath + "Prefabs/"
        };
        
        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parentFolder = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
                string folderName = System.IO.Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }
        }
    }
    
    private void CreateFarmingDataFromPreset(FarmingDataPreset preset)
    {
        // 1. 작물 아이템 데이터 생성
        ItemData cropItem = CreateCropItemData(preset);
        
        // 2. 씨앗 데이터 생성
        SeedData seedData = CreateSeedData(preset, cropItem);
        
        Debug.Log($"[FarmingDataCreator] {preset.seedName} 및 {preset.cropName} 데이터를 생성했습니다.");
    }
    
    private ItemData CreateCropItemData(FarmingDataPreset preset)
    {
        ItemData cropItem = ScriptableObject.CreateInstance<ItemData>();
        
        cropItem.itemName = preset.cropName;
        cropItem.description = $"{preset.cropName} - 농장에서 재배한 신선한 작물";
        cropItem.maxStackSize = 64;
        cropItem.isStackable = true;
        
        // 기본 큐브 프리팹을 작물 모델로 사용 (나중에 실제 모델로 교체 가능)
        GameObject tempPrefab = CreateTempCropPrefab(preset.cropName);
        cropItem.gameModel = tempPrefab;
        
        string cropPath = dataFolderPath + "Crops/" + preset.cropName + ".asset";
        AssetDatabase.CreateAsset(cropItem, cropPath);
        
        return cropItem;
    }
    
    private SeedData CreateSeedData(FarmingDataPreset preset, ItemData cropItem)
    {
        SeedData seedData = ScriptableObject.CreateInstance<SeedData>();
        
        seedData.itemName = preset.seedName;
        seedData.description = $"{preset.seedName} - 심으면 {preset.cropName}이 자랍니다";
        seedData.maxStackSize = 64;
        seedData.isStackable = true;
        
        // 성장 관련 설정
        seedData.minutesToGrow = preset.growthTime / 60f; // 초를 분으로 변환
        seedData.maxGrowthStages = preset.maxStages;
        
        // 수확 관련 설정
        seedData.cropItem = cropItem;
        seedData.baseHarvestAmount = preset.harvestAmount;
        seedData.seedDropChance = preset.seedDropChance;
        seedData.seedDropAmount = preset.seedDropAmount;
        
        
        // 성장 단계 프리팹 생성
        seedData.growthStagePrefabs = CreateGrowthStagePrefabs(preset);
        
        // 씨앗 모델 생성 (작은 구)
        GameObject seedPrefab = CreateTempSeedPrefab(preset.seedName);
        seedData.gameModel = seedPrefab;
        
        string seedPath = dataFolderPath + "Seeds/" + preset.seedName + ".asset";
        AssetDatabase.CreateAsset(seedData, seedPath);
        
        return seedData;
    }
    
    /// <summary>
    /// 기존 SeedData 에셋들을 새로운 minutesToGrow 필드로 업데이트
    /// </summary>
    [ContextMenu("Update Existing SeedData Assets")]
    public void UpdateExistingSeedDataAssets()
    {
        #if UNITY_EDITOR
        // Assets 폴더에서 모든 SeedData 에셋 찾기
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:SeedData");
        
        int updatedCount = 0;
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            SeedData seedData = UnityEditor.AssetDatabase.LoadAssetAtPath<SeedData>(path);
            
            if (seedData != null)
            {
                // 기존 daysToGrow 값이 0이 아니면 minutesToGrow로 변환
                // (이미 minutesToGrow가 설정된 경우는 건드리지 않음)
                if (seedData.minutesToGrow == 0f || seedData.minutesToGrow == 3f) // 기본값인 경우
                {
                    // 기존 에셋들은 보통 3일이었으므로 3분으로 변환
                    seedData.minutesToGrow = 3.0f;
                    
                    UnityEditor.EditorUtility.SetDirty(seedData);
                    updatedCount++;
                    
                    Debug.Log($"Updated SeedData: {seedData.itemName} - minutesToGrow set to {seedData.minutesToGrow}");
                }
            }
        }
        
        if (updatedCount > 0)
        {
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"Successfully updated {updatedCount} SeedData assets to use minutesToGrow field.");
        }
        else
        {
            Debug.Log("No SeedData assets needed updating.");
        }
        #endif
    }
    
    private GameObject[] CreateGrowthStagePrefabs(FarmingDataPreset preset)
    {
        GameObject[] stagePrefabs = new GameObject[preset.maxStages + 1];
        
        for (int i = 0; i <= preset.maxStages; i++)
        {
            GameObject stagePrefab = CreateTempGrowthStagePrefab(preset.cropName, i, preset.maxStages);
            string prefabPath = dataFolderPath + "Prefabs/" + preset.cropName + "_Stage" + i + ".prefab";
            
            PrefabUtility.SaveAsPrefabAsset(stagePrefab, prefabPath);
            stagePrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            DestroyImmediate(stagePrefab);
        }
        
        return stagePrefabs;
    }
    
    private GameObject CreateTempSeedPrefab(string seedName)
    {
        GameObject seedPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        seedPrefab.name = seedName + "_Prefab";
        seedPrefab.transform.localScale = Vector3.one * 0.1f;
        
        // 씨앗 색상 (갈색)
        Renderer renderer = seedPrefab.GetComponent<Renderer>();
        Material seedMaterial = new Material(Shader.Find("Standard"));
        seedMaterial.color = new Color(0.4f, 0.2f, 0.1f);
        renderer.material = seedMaterial;
        
        // SeedItem 컴포넌트 추가 (나중에 Inspector에서 seedData 할당 필요)
        seedPrefab.AddComponent<SeedItem>();
        
        // Rigidbody 추가
        Rigidbody rb = seedPrefab.GetComponent<Rigidbody>();
        if (rb == null) rb = seedPrefab.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        
        string prefabPath = dataFolderPath + "Prefabs/" + seedName + "_Prefab.prefab";
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(seedPrefab, prefabPath);
        
        DestroyImmediate(seedPrefab);
        return savedPrefab;
    }
    
    private GameObject CreateTempCropPrefab(string cropName)
    {
        GameObject cropPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cropPrefab.name = cropName + "_Prefab";
        cropPrefab.transform.localScale = Vector3.one * 0.2f;
        
        // 작물 색상 (오렌지/노란색)
        Renderer renderer = cropPrefab.GetComponent<Renderer>();
        Material cropMaterial = new Material(Shader.Find("Standard"));
        cropMaterial.color = new Color(1f, 0.6f, 0.2f);
        renderer.material = cropMaterial;
        
        // PickableItem 컴포넌트 추가
        PickableItem pickable = cropPrefab.AddComponent<PickableItem>();
        
        // Rigidbody 추가
        Rigidbody rb = cropPrefab.GetComponent<Rigidbody>();
        if (rb == null) rb = cropPrefab.AddComponent<Rigidbody>();
        rb.mass = 0.2f;
        
        string prefabPath = dataFolderPath + "Prefabs/" + cropName + "_Prefab.prefab";
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(cropPrefab, prefabPath);
        
        DestroyImmediate(cropPrefab);
        return savedPrefab;
    }
    
    private GameObject CreateTempGrowthStagePrefab(string cropName, int stage, int maxStages)
    {
        GameObject stagePrefab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stagePrefab.name = cropName + "_Stage" + stage;
        
        // 성장 단계에 따른 크기 증가
        float sizeRatio = (float)(stage + 1) / (maxStages + 1);
        float height = 0.1f + (sizeRatio * 0.4f); // 0.1 ~ 0.5 범위
        stagePrefab.transform.localScale = new Vector3(0.2f, height, 0.2f);
        
        // 성장 단계에 따른 색상 변화 (갈색 → 녹색)
        Renderer renderer = stagePrefab.GetComponent<Renderer>();
        Material stageMaterial = new Material(Shader.Find("Standard"));
        
        Color startColor = new Color(0.4f, 0.2f, 0.1f); // 갈색
        Color endColor = new Color(0.2f, 0.8f, 0.3f);   // 녹색
        stageMaterial.color = Color.Lerp(startColor, endColor, sizeRatio);
        renderer.material = stageMaterial;
        
        // 마지막 단계에는 과일/채소 추가 표현
        if (stage == maxStages)
        {
            GameObject fruit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fruit.name = "Fruit";
            fruit.transform.SetParent(stagePrefab.transform);
            fruit.transform.localPosition = Vector3.up * 0.6f;
            fruit.transform.localScale = Vector3.one * 0.3f;
            
            Renderer fruitRenderer = fruit.GetComponent<Renderer>();
            Material fruitMaterial = new Material(Shader.Find("Standard"));
            fruitMaterial.color = new Color(1f, 0.6f, 0.2f); // 오렌지색
            fruitRenderer.material = fruitMaterial;
        }
        
        return stagePrefab;
    }
#endif
}

/// <summary>
/// 농업 데이터 생성을 위한 프리셋 구조체
/// </summary>
[System.Serializable]
public struct FarmingDataPreset
{
    public string seedName;
    public string cropName;
    public float growthTime;
    public int maxStages;
    public int harvestAmount;
    public float seedDropChance;
    public int seedDropAmount;
}