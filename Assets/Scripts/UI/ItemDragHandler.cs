using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.UI;

/// <summary>
/// VR 환경에서 아이템 드래그 앤 드롭 처리
/// XR Interaction Toolkit과 연동하여 VR 컨트롤러로 아이템을 이동할 수 있게 함
/// </summary>
public class ItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Drag Settings")]
    [SerializeField] private float dragDistance = 0.1f;
    [SerializeField] private LayerMask validDropLayers = -1;
    
    [Header("Visual Feedback")]
    [SerializeField] private Color dragColor = new Color(1, 1, 1, 0.7f);
    [SerializeField] private float dragScale = 1.2f;
    
    private InventorySlotUI sourceSlot;
    private ItemStack draggedItem;
    private GameObject dragPreview;
    private Canvas dragCanvas;
    private CanvasGroup canvasGroup;
    private Vector3 originalScale;
    private Color originalColor;
    private bool isDragging = false;
    
    // VR 관련
    private XRRayInteractor currentRayInteractor;
    private Camera vrCamera;
    
    public System.Action<ItemStack, InventorySlotUI, InventorySlotUI> OnItemMoved;
    public bool IsDragging => isDragging;
    
    void Awake()
    {
        // VR 카메라 찾기
        vrCamera = Camera.main;
        if (vrCamera == null)
        {
            vrCamera = FindAnyObjectByType<Camera>();
        }
        
        // 드래그용 캔버스 설정
        SetupDragCanvas();
    }
    
    /// <summary>
    /// 드래그 캔버스 설정
    /// </summary>
    private void SetupDragCanvas()
    {
        // 월드 스페이스 캔버스 생성 (VR 환경용)
        GameObject canvasObj = new GameObject("DragCanvas");
        canvasObj.transform.SetParent(transform.root);
        
        dragCanvas = canvasObj.AddComponent<Canvas>();
        dragCanvas.renderMode = RenderMode.WorldSpace;
        dragCanvas.worldCamera = vrCamera;
        dragCanvas.sortingOrder = 1000; // 최상위 렌더링
        
        // CanvasScaler 추가
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
        scaler.physicalUnit = CanvasScaler.Unit.Millimeters;
        scaler.referencePixelsPerUnit = 100;
        
        // GraphicRaycaster 추가
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // 초기에는 비활성화
        canvasObj.SetActive(false);
    }
    
    /// <summary>
    /// 드래그할 아이템 설정
    /// </summary>
    public void SetDragItem(InventorySlotUI slot, ItemStack item)
    {
        sourceSlot = slot;
        draggedItem = item;
    }
    
    /// <summary>
    /// VR Ray Interactor 설정
    /// </summary>
    public void SetRayInteractor(XRRayInteractor rayInteractor)
    {
        currentRayInteractor = rayInteractor;
    }
    
    /// <summary>
    /// 드래그 시작
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (draggedItem == null || sourceSlot == null) return;
        
        isDragging = true;
        
        // 드래그 프리뷰 생성
        CreateDragPreview();
        
        // 원본 슬롯 시각 효과
        ApplyDragVisualEffects();
        
        Debug.Log($"ItemDragHandler: {draggedItem.material.materialName} 드래그 시작");
    }
    
    /// <summary>
    /// 드래그 중
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || dragPreview == null) return;
        
        UpdateDragPreviewPosition(eventData);
    }
    
    /// <summary>
    /// 드래그 종료
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        
        // 드롭 대상 찾기
        var dropTarget = FindDropTarget(eventData);
        
        // 아이템 이동 시도
        bool moveSuccess = false;
        if (dropTarget != null)
        {
            moveSuccess = TryMoveItem(dropTarget);
        }
        
        // 정리
        CleanupDrag(moveSuccess);
        
        isDragging = false;
        
        Debug.Log($"ItemDragHandler: 드래그 종료 - {(moveSuccess ? "성공" : "실패")}");
    }
    
    /// <summary>
    /// 드래그 프리뷰 생성
    /// </summary>
    private void CreateDragPreview()
    {
        if (draggedItem?.material?.icon == null) return;
        
        // 드래그 캔버스 활성화
        dragCanvas.gameObject.SetActive(true);
        
        // 프리뷰 오브젝트 생성
        dragPreview = new GameObject("DragPreview");
        dragPreview.transform.SetParent(dragCanvas.transform);
        
        // Image 컴포넌트 추가
        var image = dragPreview.AddComponent<UnityEngine.UI.Image>();
        image.sprite = draggedItem.material.icon;
        image.color = dragColor;
        
        // RectTransform 설정
        var rectTransform = dragPreview.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(100, 100); // VR 환경에 맞는 크기
        
        // CanvasGroup 추가 (투명도 제어용)
        canvasGroup = dragPreview.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.8f;
        canvasGroup.blocksRaycasts = false; // 레이캐스트 차단하지 않음
        
        // 초기 위치 설정
        UpdateDragPreviewPositionVR();
    }
    
    /// <summary>
    /// VR 환경에서 드래그 프리뷰 위치 업데이트
    /// </summary>
    private void UpdateDragPreviewPositionVR()
    {
        if (dragPreview == null || currentRayInteractor == null) return;
        
        // VR 컨트롤러 위치 기반으로 프리뷰 위치 설정
        Transform controllerTransform = currentRayInteractor.transform;
        Vector3 previewPosition = controllerTransform.position + controllerTransform.forward * dragDistance;
        
        dragPreview.transform.position = previewPosition;
        dragPreview.transform.LookAt(vrCamera.transform);
        dragPreview.transform.Rotate(0, 180, 0); // UI가 카메라를 향하도록
    }
    
    /// <summary>
    /// 일반적인 드래그 프리뷰 위치 업데이트 (마우스/터치용)
    /// </summary>
    private void UpdateDragPreviewPosition(PointerEventData eventData)
    {
        if (dragPreview == null) return;
        
        // VR 환경에서는 VR 전용 위치 업데이트 사용
        if (currentRayInteractor != null)
        {
            UpdateDragPreviewPositionVR();
        }
        else
        {
            // 일반 UI 환경에서의 위치 업데이트
            Vector3 screenPosition = eventData.position;
            Vector3 worldPosition = vrCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, dragDistance));
            dragPreview.transform.position = worldPosition;
        }
    }
    
    /// <summary>
    /// 드래그 시각 효과 적용
    /// </summary>
    private void ApplyDragVisualEffects()
    {
        if (sourceSlot == null) return;
        
        // 원본 슬롯의 시각적 변화
        var image = sourceSlot.GetComponent<UnityEngine.UI.Image>();
        if (image != null)
        {
            originalColor = image.color;
            originalScale = sourceSlot.transform.localScale;
            
            image.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);
            sourceSlot.transform.localScale = originalScale * dragScale;
        }
    }
    
    /// <summary>
    /// 드롭 대상 찾기
    /// </summary>
    private InventorySlotUI FindDropTarget(PointerEventData eventData)
    {
        // VR 환경에서는 Ray Interactor의 히트 정보 사용
        if (currentRayInteractor != null && currentRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            var slotUI = hit.collider.GetComponentInParent<InventorySlotUI>();
            if (slotUI != null && slotUI != sourceSlot)
            {
                return slotUI;
            }
        }
        
        // 일반 UI 환경에서는 EventSystem 사용
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        foreach (var result in results)
        {
            var slotUI = result.gameObject.GetComponentInParent<InventorySlotUI>();
            if (slotUI != null && slotUI != sourceSlot)
            {
                return slotUI;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 아이템 이동 시도
    /// </summary>
    private bool TryMoveItem(InventorySlotUI targetSlot)
    {
        if (sourceSlot == null || targetSlot == null || draggedItem == null)
            return false;
        
        bool success = sourceSlot.TryMoveItemTo(targetSlot);
        
        if (success)
        {
            OnItemMoved?.Invoke(draggedItem, sourceSlot, targetSlot);
            Debug.Log($"ItemDragHandler: {draggedItem.material.materialName}을 슬롯 {sourceSlot.SlotIndex}에서 {targetSlot.SlotIndex}로 이동");
        }
        
        return success;
    }
    
    /// <summary>
    /// 드래그 정리
    /// </summary>
    private void CleanupDrag(bool moveSuccess)
    {
        // 드래그 프리뷰 제거
        if (dragPreview != null)
        {
            Destroy(dragPreview);
            dragPreview = null;
        }
        
        // 드래그 캔버스 비활성화
        if (dragCanvas != null)
        {
            dragCanvas.gameObject.SetActive(false);
        }
        
        // 원본 슬롯 시각 효과 복원
        RestoreOriginalVisualEffects();
        
        // 데이터 정리
        if (moveSuccess)
        {
            sourceSlot = null;
            draggedItem = null;
        }
    }
    
    /// <summary>
    /// 원본 시각 효과 복원
    /// </summary>
    private void RestoreOriginalVisualEffects()
    {
        if (sourceSlot == null) return;
        
        var image = sourceSlot.GetComponent<UnityEngine.UI.Image>();
        if (image != null)
        {
            image.color = originalColor;
            sourceSlot.transform.localScale = originalScale;
        }
    }
    
    /// <summary>
    /// 현재 드래그 중인 아이템 정보
    /// </summary>
    public string GetDragInfo()
    {
        if (!isDragging || draggedItem == null)
            return "드래그 중인 아이템 없음";
            
        return $"드래그 중: {draggedItem.material.materialName} x{draggedItem.Quantity}";
    }
    
    void OnDestroy()
    {
        // 정리
        if (dragCanvas != null)
        {
            Destroy(dragCanvas.gameObject);
        }
    }
}