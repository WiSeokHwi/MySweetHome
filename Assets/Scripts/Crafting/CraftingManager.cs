using UnityEngine;
using System.Collections.Generic;
using System.Linq; // LINQ 사용을 위해 추가 (예: Count)

/// <summary>
/// CraftingManager - 제작 시스템의 중앙 관리자
///
/// == 주요 기능 ==
/// 1. 모든 제작 레시피 등록 및 관리
/// 2. 제작 그리드 상태 검사 및 레시피 매칭
/// 3. 레시피 검색 및 필터링
/// 4. 디버깅 및 로깅 지원
///
/// == 싱글톤 패턴 ==
/// - 전역적으로 접근 가능한 제작 시스템 관리자
/// - 씬 전환 시에도 유지되는 레시피 데이터베이스
/// </summary>
public class CraftingManager : MonoBehaviour
{
    [Header("Recipe Database")]
    [Tooltip("게임에서 사용할 모든 제작 레시피들")]
    [SerializeField] private List<CraftingRecipe> allRecipes = new List<CraftingRecipe>(); // 배열 대신 List<CraftingRecipe> 사용

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
                _instance = FindAnyObjectByType<CraftingManager>();
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
            // 레시피의 inputPattern을 직접 가져와 사용합니다.
            foreach (var material in recipe.GetPattern()) // GetFullPattern 대신 GetPattern 사용
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
            Debug.Log($"CraftingManager: {allRecipes.Count}개의 레시피가 로드되었습니다."); // .Length 대신 .Count
        }
    }

    /// <summary>
    /// 제작 그리드에서 매칭되는 레시피 찾기 (2D 배열 버전)
    /// </summary>
    /// <param name="craftingGrid">3x3 제작 그리드 (ItemStack 2D 배열)</param>
    /// <returns>매칭되는 레시피 (없으면 null)</returns>
    public CraftingRecipe FindMatchingRecipe(ItemStack[,] craftingGrid)
    {
        if (craftingGrid == null || craftingGrid.GetLength(0) != 3 || craftingGrid.GetLength(1) != 3)
        {
            if (enableDebugLogging)
                Debug.LogError("CraftingManager: 잘못된 제작 그리드입니다. 3x3 배열이어야 합니다.");
            return null;
        }

        // 2D 배열을 1D 배열로 변환
        ItemStack[] flatGrid = new ItemStack[9];
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                flatGrid[x * 3 + y] = craftingGrid[x, y];
            }
        }

        return FindMatchingRecipe(flatGrid);
    }

    /// <summary>
    /// 제작 그리드에서 매칭되는 레시피 찾기 (1D 배열 버전)
    /// </summary>
    /// <param name="craftingGrid">9개 슬롯의 제작 그리드 (ItemStack 배열)</param>
    /// <returns>매칭되는 레시피 (없으면 null)</returns>
    public CraftingRecipe FindMatchingRecipe(ItemStack[] craftingGrid)
    {
        if (craftingGrid == null || craftingGrid.Length != 9)
        {
            if (enableDebugLogging)
                Debug.LogError("CraftingManager: 잘못된 제작 그리드입니다.");
            return null;
        }

        // 그리드에 있는 재료들 확인 (재료 타입만 추출)
        var gridMaterials = new HashSet<CraftingMaterial>();
        foreach (var stack in craftingGrid)
        {
            if (stack?.material != null && !stack.IsEmpty) // IsEmpty 체크 추가
                gridMaterials.Add(stack.material);
        }

        if (gridMaterials.Count == 0) // 그리드가 완전히 비어있으면 레시피 없음
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
    /// 제작 실행 (이 메서드는 더 이상 사용되지 않습니다. PlayerInventory.Instance.ExecuteRecipe(CraftingRecipe recipe)를 사용하세요.)
    /// CraftingManager는 레시피 매칭 역할만 수행하고, 실제 재료 소모 및 결과물 추가는 PlayerInventory가 담당합니다.
    /// </summary>
    /// <param name="craftingGrid">제작 그리드</param>
    /// <returns>제작 결과 아이템 스택 (실패 시 null)</returns>
    [System.Obsolete("ExecuteCrafting(ItemStack[] craftingGrid)는 더 이상 사용되지 않습니다. PlayerInventory.Instance.ExecuteRecipe(CraftingRecipe recipe)를 사용하세요.")]
    public ItemStack ExecuteCrafting(ItemStack[] craftingGrid)
    {
        Debug.LogWarning("ExecuteCrafting(ItemStack[] craftingGrid)는 더 이상 사용되지 않습니다. PlayerInventory.Instance.ExecuteRecipe(CraftingRecipe recipe)를 사용하세요.");
        return null; // 더 이상 이 메서드에서 직접 제작을 실행하지 않음
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
    /// 이 메서드는 PlayerInventory의 CanCraftRecipe를 사용하는 것이 더 적절합니다.
    /// </summary>
    /// <param name="availableMaterials">사용 가능한 재료들 (재료 타입과 수량 딕셔너리)</param>
    /// <returns>제작 가능한 레시피들</returns>
    public List<CraftingRecipe> GetCraftableRecipes(Dictionary<CraftingMaterial, int> availableMaterials)
    {
        var craftableRecipes = new List<CraftingRecipe>();

        foreach (var recipe in allRecipes)
        {
            if (recipe == null) continue;

            // PlayerInventory의 CanCraftRecipe와 유사한 로직을 여기서 재구현
            if (CanCraftRecipe(recipe, availableMaterials))
            {
                craftableRecipes.Add(recipe);
            }
        }

        return craftableRecipes;
    }

    /// <summary>
    /// 특정 레시피를 제작할 수 있는지 확인 (주어진 재료 딕셔너리 기준)
    /// </summary>
    /// <param name="recipe">확인할 레시피</param>
    /// <param name="availableMaterials">사용 가능한 재료들 (재료 타입과 수량 딕셔너리)</param>
    /// <returns>제작 가능하면 true</returns>
    private bool CanCraftRecipe(CraftingRecipe recipe, Dictionary<CraftingMaterial, int> availableMaterials)
    {
        // CraftingRecipe의 GetRequiredMaterials()를 사용하여 필요한 재료를 가져옵니다.
        var requiredMaterials = recipe.GetRequiredMaterials();

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

        // List에 추가
        if (!allRecipes.Contains(recipe))
        {
            allRecipes.Add(recipe);
        }

        // 캐시 재구축 (새 레시피 추가 시 캐시도 업데이트)
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
                if (stack != null && !stack.IsEmpty) // IsEmpty 체크 추가
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
        Debug.Log($"=== 등록된 레시피 목록 ({allRecipes.Count}개) ==="); // .Length 대신 .Count
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
            for (int i = 0; i < allRecipes.Count; i++) // .Length 대신 .Count
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
