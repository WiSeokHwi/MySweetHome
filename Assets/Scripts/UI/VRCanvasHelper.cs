using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VR Canvas 크기 및 위치 자동 조정 헬퍼
/// </summary>
public class VRCanvasHelper : MonoBehaviour
{
    [Header("Canvas Settings")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private float desiredWidth = 800f;
    [SerializeField] private float desiredHeight = 600f;
    [SerializeField] private float worldScale = 0.001f;
    
    [Header("Position Settings")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Vector3 offsetFromPlayer = new Vector3(-1.5f, 1.5f, 2.0f);
    [SerializeField] private bool facePlayer = true;
    
    void Start()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponent<Canvas>();
            
        if (playerCamera == null)
            playerCamera = Camera.main?.transform;
            
        SetupCanvas();
    }
    
    void Update()
    {
        if (facePlayer && playerCamera != null)
        {
            // 캔버스가 항상 플레이어를 향하도록
            transform.LookAt(playerCamera);
            transform.Rotate(0, 180, 0); // UI가 올바른 방향을 향하도록
        }
    }
    
    /// <summary>
    /// 캔버스 초기 설정
    /// </summary>
    private void SetupCanvas()
    {
        if (targetCanvas == null) return;
        
        // World Space 설정
        targetCanvas.renderMode = RenderMode.WorldSpace;
        targetCanvas.worldCamera = Camera.main;
        
        // RectTransform 크기 설정
        RectTransform rectTransform = targetCanvas.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(desiredWidth, desiredHeight);
            rectTransform.localScale = Vector3.one * worldScale;
        }
        
        // 위치 설정
        if (playerCamera != null)
        {
            transform.position = playerCamera.position + playerCamera.TransformDirection(offsetFromPlayer);
        }
        
        // CanvasScaler 설정
        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();
        }
        
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        
        // GraphicRaycaster 설정
        GraphicRaycaster raycaster = targetCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = targetCanvas.gameObject.AddComponent<GraphicRaycaster>();
        }
        
        Debug.Log($"VRCanvasHelper: {targetCanvas.name} 설정 완료 - 크기: {desiredWidth}x{desiredHeight}, 스케일: {worldScale}");
    }
    
    /// <summary>
    /// 캔버스 크기 동적 조정
    /// </summary>
    public void SetCanvasSize(float width, float height)
    {
        desiredWidth = width;
        desiredHeight = height;
        
        RectTransform rectTransform = targetCanvas.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(desiredWidth, desiredHeight);
        }
    }
    
    /// <summary>
    /// 월드 스케일 조정
    /// </summary>
    public void SetWorldScale(float scale)
    {
        worldScale = scale;
        transform.localScale = Vector3.one * worldScale;
    }
    
    /// <summary>
    /// 플레이어로부터의 오프셋 설정
    /// </summary>
    public void SetOffset(Vector3 offset)
    {
        offsetFromPlayer = offset;
        if (playerCamera != null)
        {
            transform.position = playerCamera.position + playerCamera.TransformDirection(offsetFromPlayer);
        }
    }
}