using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class PickableItem : GrabbableItem
{
    [Header("아이템 데이터")]
    public CraftingMaterial itemData;
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

        SetupColliders();

        if (itemData == null)
        {
            if (enableDebugLogs)
                Debug.LogError($"[PickableItem] {gameObject.name}: 'Item Data'가 할당되지 않았습니다! 스크립트를 비활성화합니다.", this);
            enabled = false;
            return;
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

        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            if (enableDebugLogs)
                Debug.LogError("[PickableItem] PlayerInventory.Instance가 null입니다! 아이템 추가 불가.");
            return;
        }

        bool success = inventory.AddItem(itemData, quantity);
        if (success)
        {
            if (enableDebugLogs)
                Debug.Log($"[PickableItem] {itemData.materialName} x{quantity} 인벤토리에 추가됨.");

            if (itemMeshRenderer != null)
            {
                StartCoroutine(DestroyWithEffect());
            }
            else
            {
                Destroy(gameObject);
            }
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[PickableItem] 인벤토리가 가득 차 {itemData.materialName} 추가 실패, 아이템 다시 드롭 처리 필요.");
        }
    }

    private void SetupColliders()
    {
        Collider physicsCollider = GetComponent<Collider>();
        if (physicsCollider == null)
        {
            physicsCollider = gameObject.AddComponent<BoxCollider>();
            if (enableDebugLogs)
                Debug.Log($"[PickableItem] {gameObject.name}: 물리용 BoxCollider 추가");
        }

        physicsCollider.isTrigger = false;

        GameObject triggerChild = new GameObject("InteractionTrigger");
        triggerChild.transform.SetParent(transform);
        triggerChild.transform.localPosition = Vector3.zero;
        triggerChild.transform.localRotation = Quaternion.identity;
        triggerChild.transform.localScale = Vector3.one;

        SphereCollider triggerCollider = triggerChild.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;

        if (physicsCollider is BoxCollider boxCol)
        {
            float maxSize = Mathf.Max(boxCol.size.x, boxCol.size.y, boxCol.size.z);
            triggerCollider.radius = maxSize * 0.7f;
        }
        else if (physicsCollider is SphereCollider sphereCol)
        {
            triggerCollider.radius = sphereCol.radius * 1.2f;
        }
        else
        {
            triggerCollider.radius = 0.5f;
        }

        var grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable != null)
        {
            grabInteractable.colliders.Clear();
            grabInteractable.colliders.Add(triggerCollider);
        }

        if (enableDebugLogs)
            Debug.Log($"[PickableItem] {gameObject.name}: 듀얼 콜라이더 시스템 설정 완료");
    }

    private void UpdateVisuals()
    {
        // 필요한 시각적 업데이트 구현
        // 예: 아이템 텍스처나 색상 변경 등
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
