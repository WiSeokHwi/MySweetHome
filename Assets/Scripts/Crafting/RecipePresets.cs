using UnityEngine;

/// <summary>
/// RecipePresets - 기본 제작 레시피들을 생성하는 헬퍼 클래스
/// 
/// == 주요 기능 ==
/// 1. 마인크래프트 스타일의 기본 레시피들 제공
/// 2. 에디터에서 쉽게 테스트용 레시피 생성
/// 3. 일반적인 제작 패턴 예시 제공
/// 4. 개발 시 빠른 프로토타이핑 지원
/// 
/// == 사용 방법 ==
/// 1. CraftingMaterial들을 먼저 생성
/// 2. 이 스크립트를 GameObject에 붙이기
/// 3. Inspector에서 재료들 할당
/// 4. Context Menu에서 레시피 생성 버튼 클릭
/// </summary>
public class RecipePresets : MonoBehaviour
{
    [Header("Required Materials")]
    [Tooltip("기본 재료들 (레시피 생성에 필요)")]
    public CraftingMaterial wood;
    public CraftingMaterial stone;
    public CraftingMaterial ironOre;
    public CraftingMaterial stick;
    
    [Header("Result Materials")]
    [Tooltip("제작 결과물들")]
    public CraftingMaterial woodenSword;
    public CraftingMaterial stoneSword;
    public CraftingMaterial ironSword;
    public CraftingMaterial craftingTable;
    
    [Header("Generated Recipes")]
    [Tooltip("생성된 레시피들이 저장될 위치")]
    public CraftingRecipe[] generatedRecipes = new CraftingRecipe[0];
    
#if UNITY_EDITOR
    /// <summary>
    /// 막대기 제작 레시피 생성 (나무 2개 → 막대기 4개)
    /// 패턴: 세로로 나무 2개
    /// </summary>
    [ContextMenu("Create Stick Recipe")]
    public void CreateStickRecipe()
    {
        if (wood == null || stick == null)
        {
            Debug.LogError("RecipePresets: 나무와 막대기 재료가 필요합니다.");
            return;
        }
        
        var recipe = CreateRecipeAsset("Recipe_Stick", "막대기");
        recipe.recipeName = "막대기";
        recipe.resultMaterial = stick;
        recipe.resultQuantity = 4;
        recipe.requiresExactPattern = true;
        recipe.consumeQuantity = 1;
        
        // 패턴 설정: 세로로 나무 2개
        // [ ][ ][ ]
        // [W][ ][ ]
        // [W][ ][ ]
        recipe.SetPatternAt(0, 1, wood); // 중간-왼쪽
        recipe.SetPatternAt(0, 2, wood); // 아래-왼쪽
        
        Debug.Log("막대기 레시피가 생성되었습니다.");
    }
    
    /// <summary>
    /// 나무 검 제작 레시피 생성 (나무 2개 + 막대기 1개 → 나무 검 1개)
    /// 패턴: T자 모양
    /// </summary>
    [ContextMenu("Create Wooden Sword Recipe")]
    public void CreateWoodenSwordRecipe()
    {
        if (wood == null || stick == null || woodenSword == null)
        {
            Debug.LogError("RecipePresets: 나무, 막대기, 나무 검 재료가 필요합니다.");
            return;
        }
        
        var recipe = CreateRecipeAsset("Recipe_WoodenSword", "나무 검");
        recipe.recipeName = "나무 검";
        recipe.resultMaterial = woodenSword;
        recipe.resultQuantity = 1;
        recipe.requiresExactPattern = true;
        recipe.consumeQuantity = 1;
        
        // 패턴 설정: T자 모양
        // [W][ ][ ]
        // [W][ ][ ]
        // [S][ ][ ]
        recipe.SetPatternAt(0, 0, wood);  // 위-왼쪽
        recipe.SetPatternAt(0, 1, wood);  // 중간-왼쪽
        recipe.SetPatternAt(0, 2, stick); // 아래-왼쪽 (손잡이)
        
        Debug.Log("나무 검 레시피가 생성되었습니다.");
    }
    
    /// <summary>
    /// 돌 검 제작 레시피 생성
    /// </summary>
    [ContextMenu("Create Stone Sword Recipe")]
    public void CreateStoneSwordRecipe()
    {
        if (stone == null || stick == null || stoneSword == null)
        {
            Debug.LogError("RecipePresets: 돌, 막대기, 돌 검 재료가 필요합니다.");
            return;
        }
        
        var recipe = CreateRecipeAsset("Recipe_StoneSword", "돌 검");
        recipe.recipeName = "돌 검";
        recipe.resultMaterial = stoneSword;
        recipe.resultQuantity = 1;
        recipe.requiresExactPattern = true;
        recipe.consumeQuantity = 1;
        
        // 패턴: 돌 2개 + 막대기 1개
        recipe.SetPatternAt(0, 0, stone);  // 위-왼쪽
        recipe.SetPatternAt(0, 1, stone);  // 중간-왼쪽
        recipe.SetPatternAt(0, 2, stick);  // 아래-왼쪽
        
        Debug.Log("돌 검 레시피가 생성되었습니다.");
    }
    
    /// <summary>
    /// 철 검 제작 레시피 생성
    /// </summary>
    [ContextMenu("Create Iron Sword Recipe")]
    public void CreateIronSwordRecipe()
    {
        if (ironOre == null || stick == null || ironSword == null)
        {
            Debug.LogError("RecipePresets: 철광석, 막대기, 철 검 재료가 필요합니다.");
            return;
        }
        
        var recipe = CreateRecipeAsset("Recipe_IronSword", "철 검");
        recipe.recipeName = "철 검";
        recipe.resultMaterial = ironSword;
        recipe.resultQuantity = 1;
        recipe.requiresExactPattern = true;
        recipe.consumeQuantity = 1;
        
        // 패턴: 철광석 2개 + 막대기 1개
        recipe.SetPatternAt(0, 0, ironOre); // 위-왼쪽
        recipe.SetPatternAt(0, 1, ironOre); // 중간-왼쪽
        recipe.SetPatternAt(0, 2, stick);   // 아래-왼쪽
        
        Debug.Log("철 검 레시피가 생성되었습니다.");
    }
    
    /// <summary>
    /// 제작대 레시피 생성 (나무 4개 → 제작대 1개)
    /// 패턴: 2x2 사각형
    /// </summary>
    [ContextMenu("Create Crafting Table Recipe")]
    public void CreateCraftingTableRecipe()
    {
        if (wood == null || craftingTable == null)
        {
            Debug.LogError("RecipePresets: 나무와 제작대 재료가 필요합니다.");
            return;
        }
        
        var recipe = CreateRecipeAsset("Recipe_CraftingTable", "제작대");
        recipe.recipeName = "제작대";
        recipe.resultMaterial = craftingTable;
        recipe.resultQuantity = 1;
        recipe.requiresExactPattern = true;
        recipe.consumeQuantity = 1;
        
        // 패턴: 2x2 나무 블록
        // [W][W][ ]
        // [W][W][ ]
        // [ ][ ][ ]
        recipe.SetPatternAt(0, 0, wood); // 위-왼쪽
        recipe.SetPatternAt(1, 0, wood); // 위-중간
        recipe.SetPatternAt(0, 1, wood); // 중간-왼쪽
        recipe.SetPatternAt(1, 1, wood); // 중간-중간
        
        Debug.Log("제작대 레시피가 생성되었습니다.");
    }
    
    /// <summary>
    /// 형태 무관 레시피 예시 생성 (나무 + 돌 → 도구, 위치 상관없음)
    /// </summary>
    [ContextMenu("Create Shapeless Recipe Example")]
    public void CreateShapelessRecipeExample()
    {
        if (wood == null || stone == null)
        {
            Debug.LogError("RecipePresets: 나무와 돌 재료가 필요합니다.");
            return;
        }
        
        var recipe = CreateRecipeAsset("Recipe_Shapeless_Example", "형태무관 예시");
        recipe.recipeName = "형태무관 레시피 예시";
        recipe.resultMaterial = stick; // 예시로 막대기 생성
        recipe.resultQuantity = 2;
        recipe.requiresExactPattern = false; // 형태 무관!
        recipe.ignoreEmptySlots = true;
        recipe.consumeQuantity = 1;
        
        // 패턴: 나무 1개 + 돌 1개 (위치는 상관없음)
        recipe.SetPatternAt(0, 0, wood);  // 첫 번째 슬롯에 나무
        recipe.SetPatternAt(1, 0, stone); // 두 번째 슬롯에 돌
        
        Debug.Log("형태무관 레시피 예시가 생성되었습니다.");
    }
    
    /// <summary>
    /// 모든 기본 레시피 한 번에 생성
    /// </summary>
    [ContextMenu("Create All Basic Recipes")]
    public void CreateAllBasicRecipes()
    {
        CreateStickRecipe();
        CreateWoodenSwordRecipe();
        CreateStoneSwordRecipe();
        CreateIronSwordRecipe();
        CreateCraftingTableRecipe();
        CreateShapelessRecipeExample();
        
        Debug.Log("모든 기본 레시피가 생성되었습니다!");
    }
    
    /// <summary>
    /// ScriptableObject 레시피 에셋 생성 헬퍼
    /// </summary>
    /// <param name="fileName">파일명</param>
    /// <param name="displayName">표시 이름</param>
    /// <returns>생성된 레시피</returns>
    private CraftingRecipe CreateRecipeAsset(string fileName, string displayName)
    {
        var recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
        recipe.recipeName = displayName;
        
        // 에디터에서만 에셋 생성
        #if UNITY_EDITOR
        string path = $"Assets/ScriptableObjects/Recipes/{fileName}.asset";
        
        // 폴더가 없으면 생성
        string folderPath = "Assets/ScriptableObjects";
        if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        }
        
        folderPath = "Assets/ScriptableObjects/Recipes";
        if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Recipes");
        }
        
        UnityEditor.AssetDatabase.CreateAsset(recipe, path);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
        
        Debug.Log($"레시피 에셋이 생성되었습니다: {path}");
        #endif
        
        return recipe;
    }
    
    /// <summary>
    /// 생성된 레시피들을 배열에 자동 할당
    /// </summary>
    [ContextMenu("Collect Generated Recipes")]
    public void CollectGeneratedRecipes()
    {
        #if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CraftingRecipe", new[] { "Assets/ScriptableObjects/Recipes" });
        generatedRecipes = new CraftingRecipe[guids.Length];
        
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            generatedRecipes[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<CraftingRecipe>(path);
        }
        
        Debug.Log($"{generatedRecipes.Length}개의 레시피를 수집했습니다.");
        #endif
    }
#endif
}