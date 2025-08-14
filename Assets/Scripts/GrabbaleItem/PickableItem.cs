using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class PickableItem : GrabbableItem
{
    [Header("아이템 데이터")]
    public ItemData itemData;
    public int quantity = 1;

    [Header("시각적 요소 (선택 사항)")]
    [SerializeField] private MeshRenderer itemMeshRenderer;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLogs = true;

    protected override void Awake()
    {
        base.Awake();

        if (itemMeshRenderer == null)
            itemMeshRenderer = GetComponent<MeshRenderer>();

        // 데이터가 없으면 나중에 할당될 수 있으므로 경고만 출력
        if (itemData == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[PickableItem] {gameObject.name}: 'ItemData'가 아직 할당되지 않았습니다. 나중에 할당 예정.", this);
            return; // 초기화는 건너뛰지만 비활성화는 하지 않음
        }

        UpdateVisuals();
    }

    protected override void OnGrabEnded(SelectExitEventArgs args)
    {
        base.OnGrabEnded(args);

        if (itemData == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[PickableItem] {gameObject.name}: 유효한 아이템 데이터가 없어 인벤토리에 추가하지 않습니다.");
            Destroy(gameObject);
            return;
        }

    }


    private void UpdateVisuals()
    {
        // 필요한 시각적 업데이트 구현
        // 예: 아이템 텍스처나 색상 변경 등
    }

    /// <summary>
    /// 데이터 할당 후 수동으로 초기화를 완료하는 메소드
    /// </summary>
    public void CompleteInitialization()
    {
        if (itemData != null)
        {
            UpdateVisuals();
            if (enableDebugLogs)
                Debug.Log($"[PickableItem] {gameObject.name}: 초기화 완료", this);
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogError($"[PickableItem] {gameObject.name}: 데이터가 여전히 할당되지 않았습니다!", this);
        }
    }

    public override ItemData GetItemData()
    {
        return itemData;
    }

    public override int GetQuantity()
    {
        return quantity;
    }

    // OnInventoryAddInput: 부모 클래스의 기본 구현 사용 (드롭존에서 인벤토리 추가 가능)
    
    public override void OnToolUseInput()
    {
        if (enableDebugLogs)
            Debug.Log($"[PickableItem] {gameObject.name}: 일반 아이템은 도구로 사용할 수 없습니다.");
    }

    /// <summary>
    /// PickableItem은 이펙트와 함께 제거됩니다.
    /// </summary>
    protected override void TryAddToInventory()
    {
        ItemData itemData = GetItemData();
        int quantity = GetQuantity();
        
        if (itemData == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[PickableItem] {gameObject.name}에 유효한 ItemData가 없습니다.");
            return;
        }

        // 인벤토리에 아이템 추가 시도
        bool success = InventoryManager.Instance.AddItemToInventory(itemData, quantity);

        if (success)
        {
            if (enableDebugLogs)
                Debug.Log($"[PickableItem] {itemData.itemName} x{quantity}을(를) 인벤토리에 추가했습니다.");

            // 이펙트와 함께 오브젝트 제거
            StartCoroutine(DestroyWithEffect());
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[PickableItem] 인벤토리가 가득 차서 {itemData.itemName}을(를) 추가할 수 없습니다.");
        }
    }

    /// <summary>
    /// 아이템이 인벤토리에 성공적으로 추가되면 호출되는 파괴 이펙트 코루틴
    /// (간단히 색상 깜빡임 후 삭제)
    /// </summary>
    private IEnumerator DestroyWithEffect()
    {
        if (itemMeshRenderer == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Color originalColor = itemMeshRenderer.material.color;
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.PingPong(elapsed * 10f, 1f);
            itemMeshRenderer.material.color = Color.Lerp(originalColor, Color.yellow, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
