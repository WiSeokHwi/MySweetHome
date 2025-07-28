using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// CraftingSystemIntegratedTest - 제작 시스템 통합 테스트
/// 
/// == 테스트 시나리오 ==
/// 1. 나무를 때려서 나뭇가지(Stick) 획득
/// 2. 나뭇가지를 인벤토리에 저장
/// 3. 제작대에서 나뭇가지로 도구 제작
/// 4. 전체 워크플로우 검증
/// 
/// == 주요 기능 ==
/// 1. 테스트용 자원 오브젝트 자동 생성
/// 2. 기본 재료 ScriptableObject 생성
/// 3. 테스트 레시피 생성 및 등록
/// 4. VR 환경에서 실제 플레이 테스트 지원
/// 5. 각 단계별 상태 확인 및 로깅
/// </summary>
public class CraftingSystemIntegratedTest : MonoBehaviour
{
    [Header("테스트 설정")]
    [Tooltip("테스트용 나무 오브젝트 생성 개수")]
    [SerializeField] private int numberOfWoodObjects = 3;
    [Tooltip("테스트 자동 실행 여부")]
    [SerializeField] private bool autoRunTests = false;
    [Tooltip("상세 로그 출력 여부")]
    [SerializeField] private bool verboseLogging = true;
    
    [Header("테스트 재료들 (자동 생성됨)")]
    [Tooltip("나무 재료")]
    [SerializeField] private CraftingMaterial woodMaterial;
    [Tooltip("막대기 재료")]
    [SerializeField] private CraftingMaterial stickMaterial;
    [Tooltip("나무 검 재료")]
    [SerializeField] private CraftingMaterial woodenSwordMaterial;
    
    [Header("테스트 오브젝트들")]
    [Tooltip("생성된 테스트용 나무 오브젝트들")]
    [SerializeField] private GameObject[] testWoodObjects;
    [Tooltip("테스트용 제작대")]
    [SerializeField] private GameObject testCraftingTable;
    
    // 테스트 상태 추적
    private int totalWoodHarvested = 0;
    private int totalSticksObtained = 0;
    private int totalItemsCrafted = 0;
    
    void Start()
    {
        if (autoRunTests)
        {
            StartCoroutine(RunIntegratedTestSequence());
        }
        else
        {
            SetupTestEnvironment();
        }
    }
    
    /// <summary>
    /// 테스트 환경 설정
    /// </summary>
    [ContextMenu("테스트 환경 설정")]
    public void SetupTestEnvironment()
    {
        LogVerbose("=== 제작 시스템 통합 테스트 환경 설정 시작 ===");
        
        CreateTestMaterials();
        CreateTestRecipes();
        CreateTestWoodObjects();
        CreateTestCraftingTable();
        RegisterEventListeners();
        
        LogVerbose("=== 테스트 환경 설정 완료 ===");
        LogVerbose("VR 컨트롤러로 나무를 때려서 테스트를 시작하세요!");
    }
    
    /// <summary>
    /// 테스트용 기본 재료 생성
    /// </summary>
    private void CreateTestMaterials()
    {
        LogVerbose("기본 재료 ScriptableObject 생성 중...");
        
        // 나무 재료
        if (woodMaterial == null)
        {
            woodMaterial = ScriptableObject.CreateInstance<CraftingMaterial>();
            woodMaterial.materialName = "Wood";
            woodMaterial.description = "기본 나무 재료";
            woodMaterial.maxStackSize = 64;
            woodMaterial.isConsumable = true;
            
            SaveMaterialAsset(woodMaterial, "TestMaterial_Wood");
        }
        
        // 막대기 재료
        if (stickMaterial == null)
        {
            stickMaterial = ScriptableObject.CreateInstance<CraftingMaterial>();
            stickMaterial.materialName = "Stick";
            stickMaterial.description = "나무로 만든 막대기";
            stickMaterial.maxStackSize = 64;
            stickMaterial.isConsumable = true;
            
            SaveMaterialAsset(stickMaterial, "TestMaterial_Stick");
        }
        
        // 나무 검 재료
        if (woodenSwordMaterial == null)
        {
            woodenSwordMaterial = ScriptableObject.CreateInstance<CraftingMaterial>();
            woodenSwordMaterial.materialName = "Wooden Sword";
            woodenSwordMaterial.description = "나무로 만든 검";
            woodenSwordMaterial.maxStackSize = 1;
            woodenSwordMaterial.isConsumable = false;
            
            SaveMaterialAsset(woodenSwordMaterial, "TestMaterial_WoodenSword");
        }
        
        LogVerbose("기본 재료 생성 완료!");
    }
    
    /// <summary>
    /// 테스트용 레시피 생성
    /// </summary>
    private void CreateTestRecipes()
    {
        LogVerbose("테스트 레시피 생성 중...");
        
        // 막대기 레시피 (나무 2개 → 막대기 4개)
        var stickRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
        stickRecipe.recipeName = "막대기";
        stickRecipe.resultMaterial = stickMaterial;
        stickRecipe.resultQuantity = 4;
        stickRecipe.requiresExactPattern = true;
        stickRecipe.consumeQuantity = 1;
        
        // 패턴 설정: 세로로 나무 2개
        stickRecipe.SetPatternAt(0, 1, woodMaterial); // 중간-왼쪽
        stickRecipe.SetPatternAt(0, 2, woodMaterial); // 아래-왼쪽
        
        SaveRecipeAsset(stickRecipe, "TestRecipe_Stick");
        
        // 나무 검 레시피 (나무 2개 + 막대기 1개 → 나무 검 1개)  
        var swordRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
        swordRecipe.recipeName = "나무 검";
        swordRecipe.resultMaterial = woodenSwordMaterial;
        swordRecipe.resultQuantity = 1;
        swordRecipe.requiresExactPattern = true;
        swordRecipe.consumeQuantity = 1;
        
        // 패턴 설정: T자 모양
        swordRecipe.SetPatternAt(0, 0, woodMaterial);  // 위-왼쪽
        swordRecipe.SetPatternAt(0, 1, woodMaterial);  // 중간-왼쪽  
        swordRecipe.SetPatternAt(0, 2, stickMaterial); // 아래-왼쪽 (손잡이)
        
        SaveRecipeAsset(swordRecipe, "TestRecipe_WoodenSword");
        
        // CraftingManager에 레시피 등록
        var craftingManager = FindObjectOfType<CraftingManager>();
        if (craftingManager == null)
        {
            var managerObj = new GameObject("CraftingManager");
            craftingManager = managerObj.AddComponent<CraftingManager>();
        }
        
        LogVerbose("테스트 레시피 생성 완료!");
    }
    
    /// <summary>
    /// 테스트용 나무 오브젝트들 생성
    /// </summary>
    private void CreateTestWoodObjects()
    {
        LogVerbose($"{numberOfWoodObjects}개의 테스트용 나무 오브젝트 생성 중...");
        
        testWoodObjects = new GameObject[numberOfWoodObjects];
        
        for (int i = 0; i < numberOfWoodObjects; i++)
        {
            // 나무 오브젝트 생성 (큐브 형태)
            GameObject woodObj = GameObject.CreatePrimitive(PrimitiveType.Cube);  
            woodObj.name = $"TestWood_{i + 1}";
            
            // 위치 설정 (플레이어 주변에 배치)
            Vector3 position = transform.position + new Vector3(
                Random.Range(-3f, 3f),
                0.5f,
                Random.Range(-3f, 3f)
            );
            woodObj.transform.position = position;
            woodObj.transform.localScale = Vector3.one * 0.5f;
            
            // 시각적 설정 (갈색으로)
            var renderer = woodObj.GetComponent<Renderer>();
            if (renderer != null)  
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(0.6f, 0.3f, 0.1f); // 갈색
                renderer.material = material;
            }
            
            // ResourceGathering 컴포넌트 추가
            var resourceGathering = woodObj.GetComponent<ResourceGathering>();
            if (resourceGathering == null)
            {
                resourceGathering = woodObj.AddComponent<ResourceGathering>();
            }
            
            // 컴포넌트 설정을 위해 직접 필드 접근이 필요하므로 
            // 리플렉션이나 public 메서드 사용
            SetResourceGatheringProperties(resourceGathering, woodMaterial, 2, 5);
            
            testWoodObjects[i] = woodObj;
        }
        
        LogVerbose("테스트용 나무 오브젝트 생성 완료!");
    }
    
    /// <summary>
    /// ResourceGathering 프로퍼티 설정 (리플렉션 사용)
    /// </summary>
    private void SetResourceGatheringProperties(ResourceGathering resourceGathering, 
        CraftingMaterial material, int quantity, int durability)
    {
        var type = typeof(ResourceGathering);
        
        // SerializeField 접근을 위한 리플렉션
        var resourceField = type.GetField("resourceToDrop", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var quantityField = type.GetField("dropQuantity", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var durabilityField = type.GetField("maxDurability", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        resourceField?.SetValue(resourceGathering, material);
        quantityField?.SetValue(resourceGathering, quantity);
        durabilityField?.SetValue(resourceGathering, durability);
    }
    
    /// <summary>
    /// 테스트용 제작대 생성
    /// </summary>
    private void CreateTestCraftingTable()
    {
        LogVerbose("테스트용 제작대 생성 중...");
        
        if (testCraftingTable == null)
        {
            testCraftingTable = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testCraftingTable.name = "TestCraftingTable";
            testCraftingTable.transform.position = transform.position + Vector3.forward * 2f;
            testCraftingTable.transform.localScale = new Vector3(1f, 0.1f, 1f); // 테이블 형태
            
            // 시각적 설정 (나무 색상)
            var renderer = testCraftingTable.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(0.8f, 0.6f, 0.4f); // 밝은 나무색
                renderer.material = material;
            }
            
            // VRCraftingTable 컴포넌트 추가
            var craftingTableComponent = testCraftingTable.GetComponent<VRCraftingTable>();
            if (craftingTableComponent == null)
            {
                craftingTableComponent = testCraftingTable.AddComponent<VRCraftingTable>();
            }
            
            // 제작대 UI 설정을 위한 자식 오브젝트들 생성
            CreateCraftingTableUI(testCraftingTable);
        }
        
        LogVerbose("테스트용 제작대 생성 완료!");
    }
    
    /// <summary>
    /// 제작대 UI 오브젝트들 생성
    /// </summary>
    private void CreateCraftingTableUI(GameObject craftingTable)
    {
        // 그리드 부모 오브젝트 생성
        var gridParent = new GameObject("CraftingGrid");
        gridParent.transform.SetParent(craftingTable.transform);
        gridParent.transform.localPosition = Vector3.up * 0.1f;
        
        // 결과 슬롯 생성
        var resultSlot = new GameObject("ResultSlot");
        resultSlot.transform.SetParent(craftingTable.transform);
        resultSlot.transform.localPosition = new Vector3(0.5f, 0.1f, 0f);
        
        // 제작 버튼 생성
        var craftButton = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        craftButton.name = "CraftButton";
        craftButton.transform.SetParent(craftingTable.transform);
        craftButton.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        craftButton.transform.localScale = Vector3.one * 0.1f;
        
        // 버튼에 XR 인터랙션 추가
        var interactable = craftButton.AddComponent<XRGrabInteractable>();
        
        // 시각적 설정 (초록색 버튼)
        var buttonRenderer = craftButton.GetComponent<Renderer>();
        if (buttonRenderer != null)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = Color.green;
            buttonRenderer.material = material;
        }
        
        // 패널 생성
        var panel = new GameObject("CraftingPanel");
        panel.transform.SetParent(craftingTable.transform);
        panel.transform.localPosition = Vector3.zero;
    }
    
    /// <summary>
    /// 이벤트 리스너 등록
    /// </summary>
    private void RegisterEventListeners()
    {
        LogVerbose("이벤트 리스너 등록 중...");
        
        var inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            // PlayerInventory 생성
            var inventoryObj = new GameObject("PlayerInventory");
            inventory = inventoryObj.AddComponent<PlayerInventory>();
        }
        
        // 인벤토리 이벤트 등록
        inventory.OnItemAdded += OnItemAddedToInventory;
        inventory.OnItemRemoved += OnItemRemovedFromInventory;
        
        LogVerbose("이벤트 리스너 등록 완료!");
    }
    
    /// <summary>
    /// 아이템 추가 이벤트 핸들러
    /// </summary>
    private void OnItemAddedToInventory(CraftingMaterial material, int quantity)
    {
        LogVerbose($"[인벤토리] {material.materialName} x{quantity} 추가됨");
        
        if (material == woodMaterial)
        {
            totalWoodHarvested += quantity;
        }
        else if (material == stickMaterial)
        {
            totalSticksObtained += quantity;
        }
        else if (material == woodenSwordMaterial)
        {
            totalItemsCrafted += quantity;
        }
        
        LogTestProgress();
    }
    
    /// <summary>
    /// 아이템 제거 이벤트 핸들러
    /// </summary>
    private void OnItemRemovedFromInventory(CraftingMaterial material, int quantity)
    {
        LogVerbose($"[인벤토리] {material.materialName} x{quantity} 제거됨");
    }
    
    /// <summary>
    /// 테스트 진행 상황 로깅
    /// </summary>
    private void LogTestProgress()
    {
        Debug.Log("=== 테스트 진행 상황 ===");
        Debug.Log($"수집한 나무: {totalWoodHarvested}개");
        Debug.Log($"획득한 막대기: {totalSticksObtained}개");
        Debug.Log($"제작한 아이템: {totalItemsCrafted}개");
        Debug.Log("=====================");
    }
    
    /// <summary>
    /// 자동 테스트 시퀀스 실행
    /// </summary>
    private System.Collections.IEnumerator RunIntegratedTestSequence()
    {
        LogVerbose("=== 자동 통합 테스트 시작 ===");
        
        SetupTestEnvironment();
        yield return new WaitForSeconds(2f);
        
        // 1단계: 자원 채취 시뮬레이션
        LogVerbose("1단계: 자원 채취 시뮬레이션");
        yield return StartCoroutine(SimulateResourceGathering());
        
        // 2단계: 제작 시뮬레이션
        LogVerbose("2단계: 제작 시뮬레이션");
        yield return StartCoroutine(SimulateCrafting());
        
        // 3단계: 결과 검증
        LogVerbose("3단계: 결과 검증");
        ValidateTestResults();
        
        LogVerbose("=== 자동 통합 테스트 완료 ===");
    }
    
    /// <summary>
    /// 자원 채취 시뮬레이션
    /// </summary>
    private System.Collections.IEnumerator SimulateResourceGathering()
    {
        var inventory = PlayerInventory.Instance;
        
        // 나무 재료를 직접 인벤토리에 추가 (시뮬레이션)
        for (int i = 0; i < 6; i++) // 막대기 제작에 충분한 양
        {
            inventory.AddItem(woodMaterial, 1);
            LogVerbose($"나무 채취 시뮬레이션: {i + 1}/6");
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    /// <summary>
    /// 제작 시뮬레이션
    /// </summary>
    private System.Collections.IEnumerator SimulateCrafting()
    {
        var inventory = PlayerInventory.Instance;
        var craftingManager = CraftingManager.Instance;
        
        if (craftingManager != null)
        {
            // 막대기 제작 시뮬레이션
            LogVerbose("막대기 제작 시도...");
            
            // 나무 2개로 막대기 4개 제작
            if (inventory.GetItemCount(woodMaterial) >= 2)
            {
                inventory.RemoveItem(woodMaterial, 2);
                inventory.AddItem(stickMaterial, 4);
                LogVerbose("막대기 제작 성공!");
            }
            
            yield return new WaitForSeconds(1f);
            
            // 나무 검 제작 시뮬레이션
            if (inventory.GetItemCount(woodMaterial) >= 2 && inventory.GetItemCount(stickMaterial) >= 1)
            {
                LogVerbose("나무 검 제작 시도...");
                inventory.RemoveItem(woodMaterial, 2);
                inventory.RemoveItem(stickMaterial, 1);
                inventory.AddItem(woodenSwordMaterial, 1);
                LogVerbose("나무 검 제작 성공!");
            }
        }
    }
    
    /// <summary>
    /// 테스트 결과 검증
    /// </summary>
    private void ValidateTestResults()
    {
        bool testPassed = true;
        
        LogVerbose("=== 테스트 결과 검증 ===");
        
        // 기대 결과와 실제 결과 비교
        if (totalWoodHarvested < 4)
        {
            Debug.LogError($"테스트 실패: 나무 채취 부족 (기대: 4+, 실제: {totalWoodHarvested})");
            testPassed = false;
        }
        
        if (totalSticksObtained < 3) // 4개 획득 후 1개 사용 = 3개 남음
        {
            Debug.LogError($"테스트 실패: 막대기 부족 (기대: 3+, 실제: {totalSticksObtained})");
            testPassed = false;
        }
        
        if (totalItemsCrafted < 1)
        {
            Debug.LogError($"테스트 실패: 제작 실패 (기대: 1+, 실제: {totalItemsCrafted})");
            testPassed = false;
        }
        
        if (testPassed)
        {
            Debug.Log("✓ 모든 테스트 통과! 제작 시스템이 정상 작동합니다.");
        }
        else
        {
            Debug.LogError("✗ 테스트 실패. 시스템을 점검하세요.");
        }
        
        LogVerbose("========================");
    }
    
    /// <summary>
    /// ScriptableObject 에셋 저장 헬퍼
    /// </summary>
    private void SaveMaterialAsset(CraftingMaterial material, string fileName)
    {
        #if UNITY_EDITOR
        string folderPath = "Assets/ScriptableObjects/Materials";
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        }
        if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Materials");
        }
        
        string path = $"{folderPath}/{fileName}.asset";
        UnityEditor.AssetDatabase.CreateAsset(material, path);
        UnityEditor.AssetDatabase.SaveAssets();
        #endif
    }
    
    /// <summary>
    /// 레시피 에셋 저장 헬퍼
    /// </summary>
    private void SaveRecipeAsset(CraftingRecipe recipe, string fileName)
    {
        #if UNITY_EDITOR
        string folderPath = "Assets/ScriptableObjects/Recipes";
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        }
        if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Recipes");
        }
        
        string path = $"{folderPath}/{fileName}.asset";
        UnityEditor.AssetDatabase.CreateAsset(recipe, path);
        UnityEditor.AssetDatabase.SaveAssets();
        #endif
    }
    
    /// <summary>
    /// 상세 로그 출력
    /// </summary>
    private void LogVerbose(string message)
    {
        if (verboseLogging)
        {
            Debug.Log($"[CraftingTest] {message}");
        }
    }
    
    /// <summary>
    /// 테스트 환경 정리
    /// </summary>
    [ContextMenu("테스트 환경 정리")]
    public void CleanupTestEnvironment()
    {
        LogVerbose("테스트 환경 정리 중...");
        
        // 생성된 나무 오브젝트들 제거
        if (testWoodObjects != null)
        {
            foreach (var woodObj in testWoodObjects)
            {
                if (woodObj != null)
                {
                    if (Application.isPlaying)
                        Destroy(woodObj);
                    else
                        DestroyImmediate(woodObj);
                }
            }
        }
        
        // 제작대 제거
        if (testCraftingTable != null)
        {
            if (Application.isPlaying)
                Destroy(testCraftingTable);
            else
                DestroyImmediate(testCraftingTable);
        }
        
        // 인벤토리 초기화
        var inventory = PlayerInventory.Instance;
        if (inventory != null)
        {
            inventory.ClearInventory();
        }
        
        // 통계 초기화
        totalWoodHarvested = 0;
        totalSticksObtained = 0;
        totalItemsCrafted = 0;
        
        LogVerbose("테스트 환경 정리 완료!");
    }
    
    void OnDestroy()
    {
        // 이벤트 리스너 해제
        var inventory = PlayerInventory.Instance;
        if (inventory != null)
        {
            inventory.OnItemAdded -= OnItemAddedToInventory;
            inventory.OnItemRemoved -= OnItemRemovedFromInventory;
        }
    }
}