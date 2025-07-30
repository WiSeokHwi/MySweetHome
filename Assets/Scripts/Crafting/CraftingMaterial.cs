using UnityEngine;

/// <summary>
/// CraftingMaterial - 제작 시스템의 기본 재료 정의
///
/// == 주요 기능 ==
/// 1. 제작 재료의 기본 정보 저장 (이름, 아이콘, 프리팹)
/// 2. 스택 가능 여부 및 최대 스택 크기 정의
/// 3. ScriptableObject로 에셋 형태로 관리
/// 4. 제작 레시피에서 참조되는 기본 단위
///
/// == 사용 예시 ==
/// - 나무: 스택 가능, 최대 64개
/// - 도구: 스택 불가능, 최대 1개
/// - 광물: 스택 가능, 최대 64개
/// </summary>
[CreateAssetMenu(fileName = "New CraftingMaterial", menuName = "Crafting System/Material")]
public class CraftingMaterial : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("재료의 표시 이름")]
    public string materialName = "New Material";

    [Tooltip("인벤토리/UI에 표시될 아이콘")]
    public Sprite icon;

    [Tooltip("월드에 생성될 3D 프리팹")]
    public GameObject worldPrefab;

    [Header("Stack Properties")]
    [Tooltip("스택 가능 여부 (여러 개를 하나로 묶을 수 있는지)")]
    public bool isStackable = true;

    [Tooltip("최대 스택 크기 (한 번에 들 수 있는 최대 개수)")]
    [Range(1, 999)]
    public int maxStackSize = 64;

    [Header("Crafting Properties")]
    [Tooltip("제작 재료로 사용 가능 여부")]
    public bool canBeUsedInCrafting = true;

    [Tooltip("제작 결과물로 생성 가능 여부")]
    public bool canBeCrafted = true;

    [Tooltip("인벤토리에서 사용 시 소모되는 재료인지 여부")] // Tooltip 추가
    public bool isConsumable = true; // internal에서 public으로 변경

    [Header("Visual Properties")]
    [Tooltip("재료의 기본 색상 (UI 테마용)")]
    public Color materialColor = Color.white;

    [TextArea(3, 5)]
    [Tooltip("재료에 대한 설명")]
    public string description = "재료 설명을 입력하세요.";

    /// <summary>
    /// 재료의 고유 식별자 반환
    /// 파일명을 기반으로 한 고유 ID
    /// </summary>
    public string MaterialID => name;

    /// <summary>
    /// 재료 정보를 디버그 문자열로 반환
    /// </summary>
    /// <returns>재료의 상세 정보 문자열</returns>
    public string GetDebugInfo()
    {
        return $"Material: {materialName} (ID: {MaterialID})\n" +
               $"Stackable: {isStackable} (Max: {maxStackSize})\n" +
               $"Consumable: {isConsumable}\n" + // isConsumable 정보 추가
               $"Can Craft: {canBeCrafted}, Can Use: {canBeUsedInCrafting}";
    }

    /// <summary>
    /// 유효성 검사 - 필수 필드들이 올바르게 설정되었는지 확인
    /// </summary>
    /// <returns>유효하면 true, 아니면 false</returns>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(materialName))
        {
            Debug.LogError($"CraftingMaterial '{name}': materialName이 비어있습니다.", this);
            return false;
        }

        if (worldPrefab == null)
        {
            Debug.LogWarning($"CraftingMaterial '{materialName}': worldPrefab이 할당되지 않았습니다.", this);
        }

        if (maxStackSize <= 0)
        {
            Debug.LogError($"CraftingMaterial '{materialName}': maxStackSize는 1 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 값이 변경될 때 호출 (유효성 검사)
    /// </summary>
    void OnValidate()
    {
        // 이름이 비어있으면 파일명으로 설정
        if (string.IsNullOrEmpty(materialName))
        {
            materialName = name.Replace("_", " ");
        }

        // 스택 크기 유효성 검사
        if (maxStackSize <= 0)
        {
            maxStackSize = 1;
        }

        // 스택 불가능하면 최대 크기를 1로 제한
        if (!isStackable && maxStackSize > 1)
        {
            maxStackSize = 1;
        }
    }
#endif
}
