/// <summary>
/// ==================== 인벤토리 드롭 존 시스템 ====================
/// 
/// 【 시스템 개요 】
/// VR 환경에서 월드의 GrabbableItem을 인벤토리로 변환할 수 있는 3D 트리거 영역입니다.
/// 플레이어가 아이템을 특정 영역에서 놓으면 인벤토리에 자동으로 추가됩니다.
/// 
/// 【 주요 기능 】
/// 1. 3D 트리거 영역 관리 (BoxCollider 기반)
/// 2. 진입/탈출 이벤트 처리 및 GrabbableItem 추적
/// 3. 인벤토리 추가 로직 실행
/// 4. 시각적 피드백 (드롭존 표시, 성공/실패 알림)
/// 
/// 【 연동 시스템 】
/// - GrabbableItem: OnInventoryAddInput() 호출 시 이 클래스의 TryAddItemToInventory() 실행
/// - InventoryManager: 실제 인벤토리 데이터 추가 처리
/// - VR 상호작용: EquipState에서 인벤토리 추가 입력 전달
/// 
/// 【 데이터 저장 위치 】
/// - itemsInZone: 현재 드롭존 내부에 있는 GrabbableItem들 (HashSet, 이 클래스에서 관리)
/// - dropZoneCollider: 트리거 영역 (BoxCollider, 이 클래스에서 관리)
/// - 드롭존 설정: dropZoneSize, dropZoneMaterial 등 (Inspector 설정)
/// 
/// 【 상호작용 흐름 】
/// 1. 아이템이 드롭존 진입 → OnTriggerEnter → itemsInZone에 추가 → GrabbableItem.OnEnteredDropZone
/// 2. 플레이어가 인벤토리 추가 입력 → GrabbableItem.OnInventoryAddInput → TryAddItemToInventory
/// 3. 드롭존 내부 아이템만 처리 → InventoryManager.AddItemToInventory → 성공시 GameObject.Destroy
/// </summary>
using UnityEngine;
using System.Collections.Generic;

public class InventoryDropZone : MonoBehaviour
{
    // ========== 드롭존 설정 ==========
    [Header("드롭 존 설정")]
    [Tooltip("드롭 존의 3D 크기 (BoxCollider 크기와 동일)")]
    public Vector3 dropZoneSize = new Vector3(2f, 2f, 1f);
    
    [Header("시각적 피드백")]
    [Tooltip("드롭 존 영역을 시각화할 머티리얼 (반투명 박스 표시용)")]
    public Material dropZoneMaterial;
    
    [Tooltip("아이템이 드롭 존에 있을 때 표시할 머티리얼 (미구현)")]
    public Material highlightMaterial;
    
    [Header("디버그")]
    [SerializeField] private bool showGizmos = true;     // Scene View에서 기즈모 표시 여부
    [SerializeField] private bool enableDebugLogs = true; // 콘솔 로그 출력 여부
    
    // ========== 내부 컴포넌트 ==========
    private BoxCollider dropZoneCollider;    // 트리거 영역 (자동 생성)
    private MeshRenderer dropZoneRenderer;   // 시각적 표현 렌더러 (자동 생성)
    
    // ========== 상태 추적 ==========
    /// <summary>
    /// 【데이터 저장】현재 드롭존 내부에 있는 GrabbableItem들의 집합
    /// 【업데이트 위치】OnTriggerEnter(추가), OnTriggerExit(제거), TryAddItemToInventory(제거)
    /// 【참조 위치】TryAddItemToInventory()에서 아이템이 드롭존 내부에 있는지 확인
    /// </summary>
    private HashSet<GrabbableItem> itemsInZone = new HashSet<GrabbableItem>();
    
    /// <summary>
    /// 【Unity 생명주기】컴포넌트 초기화
    /// 【처리 내용】드롭존 콜라이더 및 시각적 표현 설정
    /// </summary>
    void Awake()
    {
        SetupDropZone();
    }
    
    /// <summary>
    /// 드롭존의 트리거 콜라이더와 시각적 표현을 설정합니다.
    /// 【호출 위치】Awake()에서 컴포넌트 초기화 시
    /// 【처리 내용】BoxCollider 트리거 설정, 시각적 큐브 메시 생성
    /// </summary>
    private void SetupDropZone()
    {
        // BoxCollider 설정 (트리거로 사용)
        dropZoneCollider = GetComponent<BoxCollider>();
        if (dropZoneCollider == null)
        {
            dropZoneCollider = gameObject.AddComponent<BoxCollider>();
        }
        
        dropZoneCollider.isTrigger = true;              // 물리 충돌이 아닌 트리거로 설정
        dropZoneCollider.size = dropZoneSize;           // Inspector에서 설정한 크기 적용
        
        // 시각적 표현을 위한 반투명 큐브 생성 (dropZoneMaterial이 있을 때만)
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
            
            // 반투명 효과 적용 (알파값 0.3)
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
    /// 트리거 진입 이벤트 - GrabbableItem이 드롭존에 들어올 때
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        GrabbableItem item = other.GetComponent<GrabbableItem>();
        if (item != null)
        {
            itemsInZone.Add(item);
            item.OnEnteredDropZone(this); // GrabbableItem에 알림
            if (enableDebugLogs)
                Debug.Log($"[InventoryDropZone] {item.gameObject.name}이(가) 드롭존에 진입했습니다. (그랩을 놓으면 인벤토리에 추가됩니다)");
        }
    }
    
    /// <summary>
    /// 트리거 탈출 이벤트 - GrabbableItem이 드롭존에서 나갈 때
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        GrabbableItem item = other.GetComponent<GrabbableItem>();
        if (item != null)
        {
            itemsInZone.Remove(item);
            item.OnExitedDropZone(this); // GrabbableItem에 알림
            if (enableDebugLogs)
                Debug.Log($"[InventoryDropZone] {item.gameObject.name}이(가) 드롭존에서 나갔습니다.");
        }
    }
    
    /// <summary>
    /// 아이템이 현재 드롭존 내부에 있는지 확인
    /// </summary>
    public bool IsItemInZone(GrabbableItem item)
    {
        return itemsInZone.Contains(item);
    }
    
    /// <summary>
    /// GrabbableItem에서 호출되는 인벤토리 추가 시도 메소드
    /// 이제 드롭존 내부에 있는 아이템만 처리합니다.
    /// </summary>
    public void TryAddItemToInventory(GrabbableItem item)
    {
        if (item == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[InventoryDropZone] GrabbableItem이 null입니다.");
            return;
        }
        
        // 아이템이 현재 드롭존 내부에 있는지 확인
        if (!IsItemInZone(item))
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[InventoryDropZone] {item.gameObject.name}이(가) 드롭존 내부에 있지 않습니다.");
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
            
            // 아이템을 추적 리스트에서 제거
            itemsInZone.Remove(item);
            
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