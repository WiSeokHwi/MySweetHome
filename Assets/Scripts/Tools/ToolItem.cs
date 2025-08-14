using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

/// <summary>
/// 도구 아이템 - 잡힌 상태에서 입력으로 기능을 실행할 수 있는 도구
/// </summary>
public class ToolItem : GrabbableItem
{
    [Header("도구 데이터")]
    public EquipmentData toolData;
    
    [Header("입력 설정")]
    [Tooltip("도구 사용 입력 (예: 트리거 버튼)")]
    public InputActionReference useAction;
    
    [Header("디버그")]
    [SerializeField] private bool enableDebugLogs = true;

    protected override void Awake()
    {
        base.Awake();
        
        if (toolData == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[ToolItem] {gameObject.name}: ToolData가 할당되지 않았습니다.");
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        if (useAction != null)
        {
            useAction.action.Enable();
            useAction.action.performed += OnUsePerformed;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        
        if (useAction != null)
        {
            useAction.action.performed -= OnUsePerformed;
            useAction.action.Disable();
        }
    }

    private void OnUsePerformed(InputAction.CallbackContext context)
    {
        // 잡힌 상태에서만 사용 가능
        if (!isGrabbed) return;
        
        UseTool();
    }

    private void UseTool()
    {
        if (toolData == null) return;

        if (enableDebugLogs)
            Debug.Log($"[ToolItem] {toolData.toolType} 사용!");

        // 도구 타입에 따른 기능 실행
        switch (toolData.toolType)
        {
            case EquipmentData.ToolType.Hoe:
                UseHoe();
                break;
            case EquipmentData.ToolType.WateringCan:
                UseWateringCan();
                break;
            case EquipmentData.ToolType.Axe:
                UseAxe();
                break;
            case EquipmentData.ToolType.Pickaxe:
                UsePickaxe();
                break;
        }
    }

    private void UseHoe()
    {
        Debug.Log("괭이 사용: 땅을 갈고 있습니다!");
        // 괭이 기능 구현
    }

    private void UseWateringCan()
    {
        Debug.Log("물뿌리개 사용: 물을 뿌리고 있습니다!");
        // 물뿌리개 기능 구현
    }

    private void UseAxe()
    {
        Debug.Log("도끼 사용: 나무를 자르고 있습니다!");
        // 도끼 기능 구현
    }

    private void UsePickaxe()
    {
        Debug.Log("곡괭이 사용: 돌을 캐고 있습니다!");
        // 곡괭이 기능 구현
    }

    public override ItemData GetItemData()
    {
        return toolData;
    }

    public override int GetQuantity()
    {
        return 1; // 도구는 항상 1개
    }
}