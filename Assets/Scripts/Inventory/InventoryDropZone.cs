using UnityEngine;

/// <summary>
/// 인벤토리 캔버스 주변에 설치하여 월드 오브젝트를 인벤토리로 변환하는 드롭 존
/// </summary>
public class InventoryDropZone : MonoBehaviour
{
    [Header("드롭 존 설정")]
    [Tooltip("드롭 존의 크기")]
    public Vector3 dropZoneSize = new Vector3(2f, 2f, 1f);
    
    [Header("시각적 피드백")]
    [Tooltip("드롭 존 영역을 시각화할 머티리얼")]
    public Material dropZoneMaterial;
    
    [Tooltip("아이템이 드롭 존에 있을 때 표시할 머티리얼")]
    public Material highlightMaterial;
    
    [Header("디버그")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool enableDebugLogs = true;
    
    private BoxCollider dropZoneCollider;
    private MeshRenderer dropZoneRenderer;
    
    void Awake()
    {
        SetupDropZone();
    }
    
    private void SetupDropZone()
    {
        // BoxCollider 설정
        dropZoneCollider = GetComponent<BoxCollider>();
        if (dropZoneCollider == null)
        {
            dropZoneCollider = gameObject.AddComponent<BoxCollider>();
        }
        
        dropZoneCollider.isTrigger = true;
        dropZoneCollider.size = dropZoneSize;
        
        // 시각적 표현을 위한 큐브 생성 (선택사항)
        if (dropZoneMaterial != null)
        {
            // 자식 오브젝트로 시각적 표현 생성
            GameObject visualChild = new GameObject("DropZoneVisual");
            visualChild.transform.SetParent(transform);
            visualChild.transform.localPosition = Vector3.zero;
            visualChild.transform.localRotation = Quaternion.identity;
            visualChild.transform.localScale = dropZoneSize;
            
            // 메시와 렌더러 추가
            MeshFilter meshFilter = visualChild.AddComponent<MeshFilter>();
            meshFilter.mesh = CreateCubeMesh();
            
            dropZoneRenderer = visualChild.AddComponent<MeshRenderer>();
            dropZoneRenderer.material = dropZoneMaterial;
            
            // 반투명하게 설정
            if (dropZoneMaterial.HasProperty("_Color"))
            {
                Color color = dropZoneMaterial.color;
                color.a = 0.3f;
                dropZoneRenderer.material.color = color;
            }
        }
        
        if (enableDebugLogs)
            Debug.Log($"[InventoryDropZone] 드롭 존 설정 완료: {dropZoneSize}");
    }
    
    /// <summary>
    /// GrabbableItem에서 호출되는 인벤토리 추가 시도 메소드
    /// </summary>
    public void TryAddItemToInventory(GrabbableItem item)
    {
        if (item == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[InventoryDropZone] GrabbableItem이 null입니다.");
            return;
        }

        ItemData itemData = item.GetItemData();
        int quantity = item.GetQuantity();
        
        if (itemData == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[InventoryDropZone] {item.gameObject.name}에 유효한 ItemData가 없습니다.");
            return;
        }
        
        // 인벤토리에 아이템 추가 시도
        bool success = InventoryManager.Instance.AddItemToInventory(itemData, quantity);
        
        if (success)
        {
            if (enableDebugLogs)
                Debug.Log($"[InventoryDropZone] {itemData.itemName} x{quantity}을(를) 인벤토리에 추가했습니다.");
            
            // 월드 오브젝트 제거
            Destroy(item.gameObject);
            
            // 성공 피드백
            ShowSuccessFeedback();
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[InventoryDropZone] 인벤토리가 가득 차서 {itemData.itemName}을(를) 추가할 수 없습니다.");
            
            // 실패 피드백
            ShowFailureFeedback();
        }
    }
    
    
    private void ShowSuccessFeedback()
    {
        // 성공 시 시각적/오디오 피드백 (구현 예정)
        if (enableDebugLogs)
            Debug.Log("[InventoryDropZone] 아이템 추가 성공!");
    }
    
    private void ShowFailureFeedback()
    {
        // 실패 시 시각적/오디오 피드백 (구현 예정)
        if (enableDebugLogs)
            Debug.Log("[InventoryDropZone] 아이템 추가 실패!");
    }
    
    private Mesh CreateCubeMesh()
    {
        // 간단한 큐브 메시 생성
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = cube.GetComponent<MeshFilter>().mesh;
        DestroyImmediate(cube);
        return mesh;
    }
    
    private void OnDrawGizmos()
    {
        if (showGizmos)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, dropZoneSize);
        }
    }
}