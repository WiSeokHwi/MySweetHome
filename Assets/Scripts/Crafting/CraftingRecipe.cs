using UnityEngine;
using System.Collections.Generic;
using System.Linq; // LINQ 사용을 위해 추가

/// <summary>
/// CraftingRecipe - 제작 레시피 정의 시스템
///
/// == 주요 기능 ==
/// 1. 3x3 제작 그리드 패턴 정의
/// 2. 입력 재료와 출력 결과 매핑
/// 3. 패턴 매칭 방식 설정 (정확한 패턴 vs 재료만 필요)
/// 4. 레시피 유효성 검사 및 디버깅
///
/// == 레시피 타입 ==
/// - Shaped Recipe: 정확한 위치에 재료가 있어야 함 (예: 검 제작)
/// - Shapeless Recipe: 재료만 있으면 위치 상관없음 (예: 염료 혼합)
/// </summary>
[CreateAssetMenu(fileName = "New CraftingRecipe", menuName = "Crafting System/Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Header("Recipe Information")]
    [Tooltip("레시피의 표시 이름")]
    public string recipeName = "New Recipe";

    [Tooltip("결과물의 아이콘 (UI 표시용)")]
    public Sprite recipeIcon;

    [TextArea(2, 4)]
    [Tooltip("레시피에 대한 설명")]
    public string description = "레시피 설명을 입력하세요.";

    [Header("Recipe Type")]
    [Tooltip("정확한 패턴이 필요한지 여부\n- True: 재료가 정확한 위치에 있어야 함\n- False: 재료만 있으면 위치는 상관없음")]
    public bool requiresExactPattern = true;

    [Tooltip("빈 슬롯을 무시할지 여부 (패턴이 작을 때 유용)")]
    public bool ignoreEmptySlots = true;

    [Header("Input Pattern (3x3 Grid)")]
    [Tooltip("제작 그리드 패턴 (왼쪽 위부터 오른쪽 아래 순서)")]
    [SerializeField] private CraftingMaterial[] inputPattern = new CraftingMaterial[9];

    [Header("Output Result")]
    [Tooltip("제작 결과로 나오는 재료")]
    public CraftingMaterial resultMaterial;

    [Tooltip("결과물의 수량")]
    [Range(1, 64)]
    public int resultQuantity = 1;

    [Header("Recipe Properties")]
    [Tooltip("패턴 내 각 재료 슬롯당 소모되는 재료 수량 (예: 나무 큐브 1개당 1개 소모)")] // 툴팁 명확화
    [Range(1, 64)]
    public int consumeQuantity = 1;

    [Tooltip("레시피를 반복 사용할 수 있는지 여부")]
    public bool canRepeatCraft = true;

    /// <summary>
    /// 3x3 그리드의 특정 위치에 있는 재료 반환
    /// (x: 열, y: 행. 예: (0,0)은 좌측 상단, (2,2)는 우측 하단)
    /// </summary>
    /// <param name="x">X 좌표 (0-2)</param>
    /// <param name="y">Y 좌표 (0-2)</param>
    /// <returns>해당 위치의 재료 (없으면 null)</returns>
    public CraftingMaterial GetPatternAt(int x, int y)
    {
        if (x < 0 || x > 2 || y < 0 || y > 2)
            return null;

        int index = y * 3 + x; // 행 우선 순서 (0,0), (0,1), (0,2), (1,0) ...
        return inputPattern[index];
    }

    /// <summary>
    /// 3x3 그리드의 특정 위치에 재료 설정
    /// (x: 열, y: 행. 예: (0,0)은 좌측 상단, (2,2)는 우측 하단)
    /// </summary>
    /// <param name="x">X 좌표 (0-2)</param>
    /// <param name="y">Y 좌표 (0-2)</param>
    /// <param name="material">설정할 재료</param>
    public void SetPatternAt(int x, int y, CraftingMaterial material)
    {
        if (x < 0 || x > 2 || y < 0 || y > 2)
            return;

        int index = y * 3 + x;
        inputPattern[index] = material;
    }

    /// <summary>
    /// 1차원 인덱스로 재료 접근
    /// </summary>
    /// <param name="index">인덱스 (0-8)</param>
    /// <returns>해당 인덱스의 재료</returns>
    public CraftingMaterial GetPatternAt(int index)
    {
        if (index < 0 || index >= 9)
            return null;

        return inputPattern[index];
    }

    /// <summary>
    /// 1차원 인덱스로 재료 설정
    /// </summary>
    /// <param name="index">인덱스 (0-8)</param>
    /// <param name="material">설정할 재료</param>
    public void SetPatternAt(int index, CraftingMaterial material)
    {
        if (index < 0 || index >= 9)
            return;

        inputPattern[index] = material;
    }

    /// <summary>
    /// 전체 패턴 배열 반환 (읽기 전용)
    /// </summary>
    /// <returns>패턴 배열의 복사본</returns>
    public CraftingMaterial[] GetFullPattern()
    {
        CraftingMaterial[] copy = new CraftingMaterial[9];
        System.Array.Copy(inputPattern, copy, 9);
        return copy;
    }

    /// <summary>
    /// 전체 패턴 배열 반환 (읽기 전용, 직접 참조)
    /// CraftingManager에서 레시피 패턴을 직접 참조할 때 사용될 수 있습니다.
    /// </summary>
    public CraftingMaterial[] GetPattern()
    {
        return inputPattern;
    }

    /// <summary>
    /// 주어진 제작 그리드가 이 레시피와 일치하는지 확인
    /// </summary>
    /// <param name="craftingGrid">확인할 제작 그리드 (9개 슬롯)</param>
    /// <returns>레시피와 일치하면 true</returns>
    public bool MatchesPattern(ItemStack[] craftingGrid)
    {
        if (craftingGrid == null || craftingGrid.Length != 9)
            return false;

        if (requiresExactPattern)
        {
            return CheckExactPattern(craftingGrid);
        }
        else
        {
            return CheckShapelessPattern(craftingGrid);
        }
    }

    /// <summary>
    /// 정확한 패턴 매칭 검사
    /// </summary>
    /// <param name="craftingGrid">제작 그리드</param>
    /// <returns>정확히 일치하면 true</returns>
    private bool CheckExactPattern(ItemStack[] craftingGrid)
    {
        for (int i = 0; i < 9; i++)
        {
            CraftingMaterial requiredMaterial = inputPattern[i];
            CraftingMaterial gridMaterial = craftingGrid[i]?.material;

            // 빈 슬롯 처리
            if (requiredMaterial == null)
            {
                if (gridMaterial != null && !ignoreEmptySlots)
                    return false;
                continue;
            }

            // 재료 일치 확인
            if (gridMaterial != requiredMaterial)
                return false;

            // 수량 확인 (각 슬롯의 아이템이 consumeQuantity 이상이어야 함)
            if (craftingGrid[i].Quantity < consumeQuantity)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 형태 무관 패턴 매칭 검사 (재료만 확인)
    /// </summary>
    /// <param name="craftingGrid">제작 그리드</param>
    /// <returns>필요한 재료가 모두 있으면 true</returns>
    private bool CheckShapelessPattern(ItemStack[] craftingGrid)
    {
        // 필요한 재료들을 카운트
        var requiredMaterials = new Dictionary<CraftingMaterial, int>();

        foreach (var material in inputPattern)
        {
            if (material == null) continue;

            if (requiredMaterials.ContainsKey(material))
                requiredMaterials[material] += consumeQuantity; // 각 패턴 슬롯에 있는 재료당 consumeQuantity만큼 필요
            else
                requiredMaterials[material] = consumeQuantity;
        }

        // 그리드의 재료들을 카운트
        var availableMaterials = new Dictionary<CraftingMaterial, int>();

        foreach (var stack in craftingGrid)
        {
            if (stack == null || stack.IsEmpty) continue;

            var material = stack.material;
            if (availableMaterials.ContainsKey(material))
                availableMaterials[material] += stack.Quantity;
            else
                availableMaterials[material] = stack.Quantity;
        }

        // 필요한 재료가 모두 충분한지 확인
        foreach (var required in requiredMaterials)
        {
            if (!availableMaterials.ContainsKey(required.Key) ||
                availableMaterials[required.Key] < required.Value)
            {
                return false;
            }
        }

        // 그리드에 필요 없는 재료가 더 들어있지 않은지 확인 (엄격하게 하려면)
        // 이 부분은 ignoreEmptySlots 및 requiresExactPattern 설정에 따라 달라질 수 있음
        // 현재 shapeless는 위치 무관이므로, 그리드에 레시피에 없는 재료가 있다면 불일치로 처리
        foreach (var gridMat in availableMaterials)
        {
            if (!requiredMaterials.ContainsKey(gridMat.Key))
            {
                return false; // 그리드에 레시피에 없는 재료가 있음
            }
        }

        return true;
    }

    /// <summary>
    /// 레시피 실행 (결과 아이템 스택 반환)
    /// 이 메서드는 재료 소모를 직접 처리하지 않고, 결과물만 생성합니다.
    /// 실제 재료 소모는 PlayerInventory에서 담당합니다.
    /// </summary>
    /// <param name="craftingGrid">제작 그리드 (매칭 검사용, 수정하지 않음)</param>
    /// <returns>제작 성공 시 결과 아이템 스택</returns>
    public ItemStack ExecuteRecipe(ItemStack[] craftingGrid) // craftingGrid는 매칭 확인용으로만 사용
    {
        // MatchesPattern을 이미 호출했다고 가정하거나, 여기서 다시 검사
        if (!MatchesPattern(craftingGrid))
            return null;

        // 결과 아이템 생성
        return new ItemStack(resultMaterial, resultQuantity);
    }

    /// <summary>
    /// 레시피 유효성 검사
    /// </summary>
    /// <returns>유효하면 true</returns>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(recipeName))
        {
            Debug.LogError($"CraftingRecipe '{name}': recipeName이 비어있습니다.", this);
            return false;
        }

        if (resultMaterial == null)
        {
            Debug.LogError($"CraftingRecipe '{recipeName}': resultMaterial이 null입니다.", this);
            return false;
        }

        if (resultQuantity <= 0)
        {
            Debug.LogError($"CraftingRecipe '{recipeName}': resultQuantity는 1 이상이어야 합니다.", this);
            return false;
        }

        // 패턴에 최소 하나의 재료는 있어야 함
        bool hasAnyMaterial = false;
        foreach (var material in inputPattern)
        {
            if (material != null)
            {
                hasAnyMaterial = true;
                break;
            }
        }

        if (!hasAnyMaterial)
        {
            Debug.LogWarning($"CraftingRecipe '{recipeName}': 입력 패턴에 재료가 없습니다.", this);
        }

        return true;
    }

    /// <summary>
    /// 디버깅용 레시피 정보 출력
    /// </summary>
    /// <returns>레시피 정보 문자열</returns>
    public string GetDebugInfo()
    {
        var info = $"Recipe: {recipeName}\n";
        info += $"Type: {(requiresExactPattern ? "Shaped" : "Shapeless")}\n";
        info += $"Result: {resultMaterial?.materialName} x{resultQuantity}\n";
        info += "Pattern:\n";

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                var material = GetPatternAt(x, y);
                info += material != null ? $"[{material.materialName[0]}]" : "[ ]";
            }
            info += "\n";
        }

        return info;
    }

    /// <summary>
    /// 레시피에서 필요한 재료들과 그 수량을 반환
    /// (consumeQuantity를 고려하여 각 패턴 슬롯의 재료 개수를 합산)
    /// </summary>
    /// <returns>재료와 필요 수량의 딕셔너리</returns>
    public Dictionary<CraftingMaterial, int> GetRequiredMaterials()
    {
        var requiredMaterials = new Dictionary<CraftingMaterial, int>();

        foreach (var material in inputPattern)
        {
            if (material != null)
            {
                if (requiredMaterials.ContainsKey(material))
                    requiredMaterials[material] += consumeQuantity;
                else
                    requiredMaterials[material] = consumeQuantity;
            }
        }

        return requiredMaterials;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 값 변경 시 유효성 검사
    /// </summary>
    void OnValidate()
    {
        if (string.IsNullOrEmpty(recipeName))
        {
            recipeName = name.Replace("_", " ");
        }

        if (resultQuantity <= 0)
        {
            resultQuantity = 1;
        }

        if (consumeQuantity <= 0)
        {
            consumeQuantity = 1;
        }

        // 패턴 배열 크기 확인
        if (inputPattern == null || inputPattern.Length != 9)
        {
            inputPattern = new CraftingMaterial[9];
        }
    }
#endif
}
