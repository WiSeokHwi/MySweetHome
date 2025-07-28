using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// CraftingManager - 제작 시스템의 중앙 관리자
/// 
/// == 주요 기능 ==
/// 1. 모든 제작 레시피 등록 및 관리
/// 2. 제작 그리드 상태 검사 및 레시피 매칭
/// 3. 제작 실행 및 결과 처리
/// 4. 레시피 검색 및 필터링
/// 5. 디버깅 및 로깅 지원
/// 
/// == 싱글톤 패턴 ==
/// - 전역적으로 접근 가능한 제작 시스템 관리자
/// - 씬 전환 시에도 유지되는 레시피 데이터베이스
/// </summary>
public class CraftingManager : MonoBehaviour
{
    [Header("Recipe Database")]
    [Tooltip("게임에서 사용할 모든 제작 레시피들")]
    [SerializeField] private CraftingRecipe[] allRecipes = new CraftingRecipe[0];
    
    [Header("Debug Settings")]
    [Tooltip("제작 시도 시 디버그 로그 출력 여부")]
    public bool enableDebugLogging = true;
    
    [Tooltip("레시피 매칭 과정 상세 로그 출력")]
    public bool enableVerboseLogging = false;
    
    // 싱글톤 인스턴스
    private static CraftingManager _instance;
    public static CraftingManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CraftingManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("CraftingManager");
                    _instance = go.AddComponent<CraftingManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    
    // 레시피 검색 최적화를 위한 캐시
    private Dictionary<string, CraftingRecipe> recipeCache;
    private Dictionary<CraftingMaterial, List<CraftingRecipe>> recipesByResult;
    private Dictionary<CraftingMaterial, List<CraftingRecipe>> recipesByIngredient;
    
    /// <summary>
    /// 등록된 모든 레시피 (읽기 전용)
    /// </summary>
    public IReadOnlyList<CraftingRecipe> AllRecipes => allRecipes;
    
    void Awake()
    {
        // 싱글톤 설정
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeRecipeDatabase();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// 레시피 데이터베이스 초기화 및 캐시 구축
    /// </summary>
    private void InitializeRecipeDatabase()
    {
        recipeCache = new Dictionary<string, CraftingRecipe>();
        recipesByResult = new Dictionary<CraftingMaterial, List<CraftingRecipe>>();
        recipesByIngredient = new Dictionary<CraftingMaterial, List<CraftingRecipe>>();
        
        // 모든 레시피 검증 및 캐시 구축
        foreach (var recipe in allRecipes)
        {
            if (recipe == null)
            {
                Debug.LogWarning("CraftingManager: null 레시피가 발견되었습니다.");
                continue;
            }
            
            if (!recipe.IsValid())
            {
                Debug.LogError($"CraftingManager: 유효하지 않은 레시피 '{recipe.recipeName}'가 발견되었습니다.");
                continue;
            }
            
            // 이름 기반 캐시
            string key = recipe.recipeName.ToLower();
            if (!recipeCache.ContainsKey(key))
            {
                recipeCache[key] = recipe;
            }
            else
            {
                Debug.LogWarning($"CraftingManager: 중복된 레시피 이름 '{recipe.recipeName}'가 발견되었습니다.");
            }
            
            // 결과물 기반 캐시
            if (recipe.resultMaterial != null)
            {
                if (!recipesByResult.ContainsKey(recipe.resultMaterial))
                {
                    recipesByResult[recipe.resultMaterial] = new List<CraftingRecipe>();
                }
                recipesByResult[recipe.resultMaterial].Add(recipe);
            }
            
            // 재료 기반 캐시
            var pattern = recipe.GetFullPattern();
            foreach (var material in pattern)
            {
                if (material == null) continue;
                
                if (!recipesByIngredient.ContainsKey(material))
                {
                    recipesByIngredient[material] = new List<CraftingRecipe>();
                }
                
                if (!recipesByIngredient[material].Contains(recipe))
                {
                    recipesByIngredient[material].Add(recipe);
                }
            }
        }
        
        if (enableDebugLogging)
        {
            Debug.Log($"CraftingManager: {allRecipes.Length}개의 레시피가 로드되었습니다.");
        }
    }
    
    /// <summary>
    /// 제작 그리드에서 매칭되는 레시피 찾기
    /// </summary>
    /// <param name="craftingGrid">9개 슬롯의 제작 그리드</param>
    /// <returns>매칭되는 레시피 (없으면 null)</returns>
    public CraftingRecipe FindMatchingRecipe(ItemStack[] craftingGrid)
    {
        if (craftingGrid == null || craftingGrid.Length != 9)
        {
            if (enableDebugLogging)
                Debug.LogError("CraftingManager: 잘못된 제작 그리드입니다.");
            return null;
        }
        
        // 그리드에 있는 재료들 확인
        var gridMaterials = new HashSet<CraftingMaterial>();
        foreach (var stack in craftingGrid)
        {
            if (stack?.material != null)
                gridMaterials.Add(stack.material);
        }
        
        if (gridMaterials.Count == 0)
            return null;
        
        // 후보 레시피들 수집 (성능 최적화)
        var candidateRecipes = new HashSet<CraftingRecipe>();
        foreach (var material in gridMaterials)
        {
            if (recipesByIngredient.ContainsKey(material))
            {
                foreach (var recipe in recipesByIngredient[material])
                {
                    candidateRecipes.Add(recipe);
                }
            }
        }
        
        // 후보 레시피들 중에서 매칭 검사
        foreach (var recipe in candidateRecipes)
        {
            if (recipe.MatchesPattern(craftingGrid))
            {
                if (enableVerboseLogging)
                {
                    Debug.Log($"CraftingManager: 레시피 '{recipe.recipeName}' 매칭 성공!");
                }
                return recipe;
            }
        }
        
        if (enableVerboseLogging)
        {
            Debug.Log("CraftingManager: 매칭되는 레시피를 찾지 못했습니다.");
            LogGridContents(craftingGrid);
        }
        
        return null;
    }
    
    /// <summary>
    /// 제작 실행
    /// </summary>
    /// <param name="craftingGrid">제작 그리드</param>
    /// <returns>제작 결과 아이템 스택 (실패 시 null)</returns>
    public ItemStack ExecuteCrafting(ItemStack[] craftingGrid)
    {
        var recipe = FindMatchingRecipe(craftingGrid);
        if (recipe == null)
        {
            if (enableDebugLogging)
                Debug.Log("CraftingManager: 매칭되는 레시피가 없어 제작할 수 없습니다.");
            return null;
        }
        
        var result = recipe.ExecuteRecipe(craftingGrid);
        
        if (result != null && enableDebugLogging)
        {
            Debug.Log($"CraftingManager: '{recipe.recipeName}' 제작 완료! 결과: {result.GetDebugInfo()}");
        }
        
        return result;
    }
    
    /// <summary>
    /// 특정 재료로 만들 수 있는 모든 레시피 검색
    /// </summary>
    /// <param name="material">결과물 재료</param>
    /// <returns>해당 재료를 만드는 레시피들</returns>
    public List<CraftingRecipe> GetRecipesForResult(CraftingMaterial material)
    {
        if (material == null || !recipesByResult.ContainsKey(material))
            return new List<CraftingRecipe>();
        
        return new List<CraftingRecipe>(recipesByResult[material]);
    }
    
    /// <summary>
    /// 특정 재료를 사용하는 모든 레시피 검색
    /// </summary>
    /// <param name="material">입력 재료</param>
    /// <returns>해당 재료를 사용하는 레시피들</returns>
    public List<CraftingRecipe> GetRecipesUsingIngredient(CraftingMaterial material)
    {
        if (material == null || !recipesByIngredient.ContainsKey(material))
            return new List<CraftingRecipe>();
        
        return new List<CraftingRecipe>(recipesByIngredient[material]);
    }
    
    /// <summary>
    /// 이름으로 레시피 검색
    /// </summary>
    /// <param name="recipeName">레시피 이름</param>
    /// <returns>찾은 레시피 (없으면 null)</returns>
    public CraftingRecipe GetRecipeByName(string recipeName)
    {
        if (string.IsNullOrEmpty(recipeName))
            return null;
        
        string key = recipeName.ToLower();
        return recipeCache.ContainsKey(key) ? recipeCache[key] : null;
    }
    
    /// <summary>
    /// 제작 가능한 아이템들 검색 (현재 인벤토리 기준)
    /// </summary>
    /// <param name="availableMaterials">사용 가능한 재료들</param>
    /// <returns>제작 가능한 레시피들</returns>
    public List<CraftingRecipe> GetCraftableRecipes(Dictionary<CraftingMaterial, int> availableMaterials)
    {
        var craftableRecipes = new List<CraftingRecipe>();
        
        foreach (var recipe in allRecipes)
        {
            if (recipe == null) continue;
            
            if (CanCraftRecipe(recipe, availableMaterials))
            {
                craftableRecipes.Add(recipe);
            }
        }
        
        return craftableRecipes;
    }
    
    /// <summary>
    /// 특정 레시피를 제작할 수 있는지 확인
    /// </summary>
    /// <param name="recipe">확인할 레시피</param>
    /// <param name="availableMaterials">사용 가능한 재료들</param>
    /// <returns>제작 가능하면 true</returns>
    private bool CanCraftRecipe(CraftingRecipe recipe, Dictionary<CraftingMaterial, int> availableMaterials)
    {
        var pattern = recipe.GetFullPattern();
        var requiredMaterials = new Dictionary<CraftingMaterial, int>();
        
        // 필요한 재료들 계산
        foreach (var material in pattern)
        {
            if (material == null) continue;
            
            if (requiredMaterials.ContainsKey(material))
                requiredMaterials[material] += recipe.consumeQuantity;
            else
                requiredMaterials[material] = recipe.consumeQuantity;
        }
        
        // 충분한 재료가 있는지 확인
        foreach (var required in requiredMaterials)
        {
            if (!availableMaterials.ContainsKey(required.Key) || 
                availableMaterials[required.Key] < required.Value)
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// 런타임에 새 레시피 추가
    /// </summary>
    /// <param name="recipe">추가할 레시피</param>
    public void AddRecipe(CraftingRecipe recipe)
    {
        if (recipe == null || !recipe.IsValid())
        {
            Debug.LogError("CraftingManager: 유효하지 않은 레시피를 추가하려고 했습니다.");
            return;
        }
        
        // 배열에 추가
        var newArray = new CraftingRecipe[allRecipes.Length + 1];
        System.Array.Copy(allRecipes, newArray, allRecipes.Length);
        newArray[allRecipes.Length] = recipe;
        allRecipes = newArray;
        
        // 캐시 재구축 (간단한 방법)
        InitializeRecipeDatabase();
        
        if (enableDebugLogging)
        {
            Debug.Log($"CraftingManager: 레시피 '{recipe.recipeName}'가 추가되었습니다.");
        }
    }
    
    /// <summary>
    /// 디버깅용 그리드 내용 로그 출력
    /// </summary>
    /// <param name="craftingGrid">제작 그리드</param>
    private void LogGridContents(ItemStack[] craftingGrid)
    {
        Debug.Log("=== 제작 그리드 내용 ===");
        for (int y = 0; y < 3; y++)
        {
            string row = "";
            for (int x = 0; x < 3; x++)
            {
                int index = y * 3 + x;
                var stack = craftingGrid[index];
                if (stack?.material != null)
                {
                    row += $"[{stack.material.materialName}:{stack.Quantity}] ";
                }
                else
                {
                    row += "[Empty] ";
                }
            }
            Debug.Log($"Row {y}: {row}");
        }
        Debug.Log("=======================");
    }
    
    /// <summary>
    /// 모든 레시피 정보를 디버그 로그로 출력
    /// </summary>
    [ContextMenu("Log All Recipes")]
    public void LogAllRecipes()
    {
        Debug.Log($"=== 등록된 레시피 목록 ({allRecipes.Length}개) ===");
        foreach (var recipe in allRecipes)
        {
            if (recipe != null)
            {
                Debug.Log(recipe.GetDebugInfo());
            }
        }
        Debug.Log("=====================================");
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 레시피 배열 검증
    /// </summary>
    void OnValidate()
    {
        if (allRecipes != null)
        {
            for (int i = 0; i < allRecipes.Length; i++)
            {
                if (allRecipes[i] != null && !allRecipes[i].IsValid())
                {
                    Debug.LogWarning($"CraftingManager: 인덱스 {i}의 레시피가 유효하지 않습니다.", this);
                }
            }
        }
    }
#endif
}