using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;

public class PlacableItem : MonoBehaviour
{
    // === 외부에서 할당해야 할 참조들 ===
    [Tooltip("그리드 관리자 참조. Hierarchy의 GridManager 오브젝트를 드래그 앤 드롭하세요.")]
    public GridManager gridManager;

    // === 아이템 배치 관련 설정 ===
    [Tooltip("이 아이템이 차지할 그리드 셀의 크기 (x, z). Y축은 높이 관련이므로 무시.")]
    public Vector3Int itemGridSize = new Vector3Int(1, 1, 1);
    [Tooltip("이 아이템의 실제 월드 높이 (피벗이 바닥에 있는 경우 전체 높이, 중앙에 있으면 전체 높이의 절반).")]
    [SerializeField]
    private float itemWorldHeight = 1.0f;

    public float ItemWorldHeight => itemWorldHeight;

    [Tooltip("아이템의 미리보기(Preview) 머티리얼. 배치 가능 시 사용.")]
    public Material canPlaceMaterial;
    [Tooltip("아이템의 미리보기(Preview) 머티리얼. 배치 불가능 시 사용.")]
    public Material cannotPlaceMaterial;

    private XRGrabInteractable grabInteractable;
    private MeshRenderer itemMeshRenderer;
    private Rigidbody itemRigidbody; // <--- Rigidbody 참조 추가

    // === 상태 관리 Enum 변수 ===
    private PlacementState currentPlacementState = PlacementState.None;

    private GameObject previewObject;
    private MeshRenderer previewMeshRenderer;

    // === Input System 관련 변수 ===
    [Header("Input Action for Grid Snap")]
    [Tooltip("그리드 스냅을 활성화할 버튼의 Input Action (Secondary Button).")]
    public InputActionProperty gridSnapButton;

    private IXRSelectInteractor currentInteractor;
    private LineRenderer createdLineRenderer;

    private Vector3 originalGrabPosition;
    private Quaternion originalGrabRotation;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        itemMeshRenderer = GetComponent<MeshRenderer>();
        itemRigidbody = GetComponent<Rigidbody>(); // <--- Rigidbody 참조 가져오기

        if (grabInteractable == null)
        {
            Debug.LogError("PlacableItem: XRGrabInteractable 컴포넌트가 필요합니다.", this);
            enabled = false;
            return;
        }

        // Rigidbody가 없으면 추가 (물리 시뮬레이션을 위해 필요)
        if (itemRigidbody == null)
        {
            itemRigidbody = gameObject.AddComponent<Rigidbody>();
            Debug.LogWarning("PlacableItem: Rigidbody 컴포넌트가 없어 추가했습니다. 물리 시뮬레이션을 위해 필요합니다.", this);
        }

        // 초기 상태: Rigidbody는 물리 시뮬레이션에 참여 (중력 영향 받음), GrabInteractable 활성화
        if (itemRigidbody != null)
        {
            itemRigidbody.isKinematic = false;
            itemRigidbody.useGravity = true;
        }
        if (grabInteractable != null)
        {
            grabInteractable.enabled = true;
        }


        if (itemMeshRenderer != null)
        {
            if (itemWorldHeight <= 0.01f)
            {
                itemWorldHeight = itemMeshRenderer.bounds.size.y;
            }
        }
        else
        {
            Debug.LogWarning("PlacableItem: MeshRenderer 컴포넌트가 없습니다. 미리보기 시각화가 제한될 수 있으며, itemWorldHeight가 정확하지 않을 수 있습니다.", this);
        }

        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);

        if (gridManager == null)
        {
            gridManager = FindObjectOfType<GridManager>();
            if (gridManager == null)
            {
                Debug.LogError("PlacableItem: GridManager를 씬에서 찾을 수 없습니다! 수동으로 할당해주세요.", this);
                enabled = false;
                return;
            }
        }
    }

    void OnEnable()
    {
        if (gridSnapButton != null && gridSnapButton.action != null)
        {
            gridSnapButton.action.Enable();
            gridSnapButton.action.started += OnGridSnapButtonPressed;
            gridSnapButton.action.canceled += OnGridSnapButtonReleased;
        }
    }

    void OnDisable()
    {
        if (gridSnapButton != null && gridSnapButton.action != null)
        {
            gridSnapButton.action.started -= OnGridSnapButtonPressed;
            gridSnapButton.action.canceled -= OnGridSnapButtonReleased;
            gridSnapButton.action.Disable();
        }

        SetPlacementState(PlacementState.None);
        if (gridManager != null)
        {
            gridManager.ReleaseCells(this);
        }
    }

    private void SetPlacementState(PlacementState newState)
    {
        if (currentPlacementState == newState) return;

        currentPlacementState = newState;

        switch (currentPlacementState)
        {
            case PlacementState.None:
                DestroyPreviewObject();
                if (itemMeshRenderer != null) itemMeshRenderer.enabled = true;
                if (createdLineRenderer != null) createdLineRenderer.enabled = false;
                gridManager.ClearCurrentPlacingItem();
                currentInteractor = null;
                // 오브젝트가 배치되지 않고 놓였을 때, Rigidbody는 활성화 상태 유지
                if (itemRigidbody != null)
                {
                    itemRigidbody.isKinematic = false;
                    itemRigidbody.useGravity = true;
                }
                if (grabInteractable != null)
                {
                    grabInteractable.enabled = true; // 다시 잡힐 수 있도록 활성화
                }
                break;
            case PlacementState.Grabbing:
                CreatePreviewObject();
                if (itemMeshRenderer != null) itemMeshRenderer.enabled = true;
                if (previewObject != null && previewMeshRenderer != null) previewMeshRenderer.enabled = false;
                if (createdLineRenderer != null) createdLineRenderer.enabled = false;
                gridManager.SetCurrentPlacingItem(this);
                // 잡는 중에는 물리력 영향 받지 않도록 isKinematic 설정
                if (itemRigidbody != null)
                {
                    itemRigidbody.isKinematic = true;
                    itemRigidbody.linearVelocity = Vector3.zero;
                    itemRigidbody.angularVelocity = Vector3.zero;
                }
                break;
            case PlacementState.Placing:
                if (itemMeshRenderer != null) itemMeshRenderer.enabled = false;
                if (previewObject != null && previewMeshRenderer != null) previewMeshRenderer.enabled = true;
                if (createdLineRenderer != null) createdLineRenderer.enabled = true;
                // 배치 중에는 이미 isKinematic이 true (Grabbing에서 넘어왔으므로)
                break;
            case PlacementState.Placed: // 이 상태는 OnReleased 내부에서 처리되므로 실제로는 사용되지 않음
                SetPlacementState(PlacementState.None);
                break;
        }
    }

    private void OnGridSnapButtonPressed(InputAction.CallbackContext context)
    {
        if (currentPlacementState == PlacementState.Grabbing)
        {
            SetPlacementState(PlacementState.Placing);
        }
    }

    private void OnGridSnapButtonReleased(InputAction.CallbackContext context)
    {
        if (currentPlacementState == PlacementState.Placing)
        {
            SetPlacementState(PlacementState.Grabbing);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        originalGrabPosition = transform.position;
        originalGrabRotation = transform.rotation;
        currentInteractor = args.interactorObject;

        if (gridManager != null)
        {
            gridManager.ReleaseCells(this);
        }

        if (currentInteractor != null)
        {
            createdLineRenderer = currentInteractor.transform.gameObject.GetComponent<LineRenderer>();
            if (createdLineRenderer == null)
            {
                createdLineRenderer = currentInteractor.transform.gameObject.AddComponent<LineRenderer>();
                createdLineRenderer.startWidth = 0.01f;
                createdLineRenderer.endWidth = 0.01f;
                createdLineRenderer.positionCount = 2;
                createdLineRenderer.useWorldSpace = true;
                createdLineRenderer.startColor = Color.yellow;
                createdLineRenderer.endColor = Color.yellow;
                if (lineMaterial != null)
                {
                    createdLineRenderer.material = lineMaterial;
                }
                else
                {
                    createdLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                    Debug.LogWarning("Line Material is not assigned. Using default Sprite/Default shader for Line Renderer.");
                }
            }
            createdLineRenderer.enabled = false;
        }
        SetPlacementState(PlacementState.Grabbing);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        Vector3 finalPlacementPosition = transform.position;
        Quaternion finalPlacementRotation = transform.rotation;
        bool successfulPlacement = false; // <--- 배치 성공 여부 플래그

        if (currentPlacementState == PlacementState.Placing && currentInteractor != null)
        {
            RaycastHit hit;
            Vector3 rayOrigin = currentInteractor.transform.position;
            Vector3 rayDirection = currentInteractor.transform.forward;

            if (Physics.Raycast(rayOrigin, rayDirection, out hit, raycastDistance, placementLayerMask))
            {
                Vector3 calculatedSnappedPosition = gridManager.SnapToGridForPlacement(hit.point, itemGridSize, itemWorldHeight);
                finalPlacementPosition = calculatedSnappedPosition;
                finalPlacementRotation = Quaternion.Euler(0, 0, 0); // 그리드 배치는 Y회전만 가능하도록 고정

                Vector3Int projectedGridOrigin = gridManager.WorldToGridCoordinates(finalPlacementPosition);
                bool canPlace = gridManager.CanPlaceItem(projectedGridOrigin, itemGridSize);

                if (canPlace)
                {
                    gridManager.OccupyCells(this);
                    transform.position = finalPlacementPosition;
                    transform.rotation = finalPlacementRotation;
                    // Debug.Log($"아이템 '{gameObject.name}'이 그리드에 성공적으로 배치되었습니다. 위치: {finalPlacementPosition}, 회전: {finalPlacementRotation.eulerAngles}");
                    successfulPlacement = true; // 성공적으로 배치
                }
                else
                {
                    // Debug.LogWarning($"아이템 '{gameObject.name}'을 이 위치에 배치할 수 없습니다 (그리드 충돌 또는 범위 밖). 원래 위치로 돌아갑니다.");
                    transform.position = originalGrabPosition;
                    transform.rotation = originalGrabRotation;
                }
            }
            else
            {
                // Debug.LogWarning($"아이템 '{gameObject.name}' 배치 시 Raycast가 유효한 배치 영역을 찾지 못했습니다. 원래 위치로 돌아갑니다.");
                transform.position = originalGrabPosition;
                transform.rotation = originalGrabRotation;
            }
        }
        else // 배치 모드가 아니거나 Raycast 실패 시 (자유롭게 놓는 경우)
        {
            // Debug.Log($"아이템 '{gameObject.name}'이 자유롭게 놓였습니다. 위치: {finalPlacementPosition}, 회전: {finalPlacementRotation.eulerAngles}");
            // 자유롭게 놓인 경우, 현재 위치에 그대로 둠.
            successfulPlacement = true; // 자유롭게 놓는 것도 "성공"으로 간주 (고정하지 않음)
        }

        // --- 여기에 고정 로직 추가 ---
        if (successfulPlacement)
        {
            // 배치 성공 시 Rigidbody를 Kinematic으로 설정하여 물리 시뮬레이션에서 제외
            if (itemRigidbody != null)
            {
                itemRigidbody.isKinematic = true;
                itemRigidbody.useGravity = false; // 중력 영향 받지 않도록
                itemRigidbody.linearVelocity = Vector3.zero; // 현재 속도 초기화
                itemRigidbody.angularVelocity = Vector3.zero; // 현재 각속도 초기화
            }
            // XRGrabInteractable을 비활성화하여 다시 잡히지 않도록
            if (grabInteractable != null)
            {
                grabInteractable.enabled = false;
            }
            Debug.Log($"아이템 '{gameObject.name}'이 그리드에 고정되었습니다.");
        }
        else
        {
            // 배치 실패 시에는 Rigidbody와 GrabInteractable 상태를 원래대로 복구 (잡힐 수 있도록)
            if (itemRigidbody != null)
            {
                itemRigidbody.isKinematic = false;
                itemRigidbody.useGravity = true;
            }
            if (grabInteractable != null)
            {
                grabInteractable.enabled = true;
            }
            Debug.Log($"아이템 '{gameObject.name}' 배치 실패 (또는 자유롭게 놓음). 고정되지 않았습니다.");
        }
        // --- 고정 로직 끝 ---

        SetPlacementState(PlacementState.None); // 놓는 순간 항상 None 상태로 전환하여 정리
    }

    void Update()
    {
        if (currentPlacementState == PlacementState.Placing && currentInteractor != null)
        {
            if (createdLineRenderer != null)
            {
                createdLineRenderer.enabled = true;
            }

            RaycastHit hit;
            Vector3 rayOrigin = currentInteractor.transform.position;
            Vector3 rayDirection = currentInteractor.transform.forward;

            if (Physics.Raycast(rayOrigin, rayDirection, out hit, raycastDistance, placementLayerMask))
            {
                if (createdLineRenderer != null)
                {
                    createdLineRenderer.SetPosition(0, rayOrigin);
                    createdLineRenderer.SetPosition(1, hit.point);
                }

                Vector3 targetPreviewPosition = gridManager.SnapToGridForPlacement(hit.point, itemGridSize, itemWorldHeight);
                Quaternion targetPreviewRotation = Quaternion.Euler(0, 0, 0);

                if (previewObject != null && previewMeshRenderer != null)
                {
                    previewObject.transform.position = targetPreviewPosition;
                    previewObject.transform.rotation = targetPreviewRotation;

                    Vector3Int projectedGridOrigin = gridManager.WorldToGridCoordinates(targetPreviewPosition);
                    bool canPlace = gridManager.CanPlaceItem(projectedGridOrigin, itemGridSize);

                    previewMeshRenderer.material = canPlace ? canPlaceMaterial : cannotPlaceMaterial;
                    previewMeshRenderer.enabled = true;

                    gridManager.UpdateGridMaterialForPlacement(canPlace);
                }
            }
            else
            {
                if (createdLineRenderer != null)
                {
                    createdLineRenderer.SetPosition(0, rayOrigin);
                    createdLineRenderer.SetPosition(1, rayOrigin + rayDirection * raycastDistance);
                }
                if (previewObject != null && previewMeshRenderer != null)
                {
                    previewMeshRenderer.enabled = false;
                }
                gridManager.UpdateGridMaterialForPlacement(false);
            }
        }
        else
        {
            if (previewObject != null && previewMeshRenderer != null)
            {
                previewMeshRenderer.enabled = false;
            }
            if (createdLineRenderer != null)
            {
                createdLineRenderer.enabled = false;
            }
            gridManager.SetGridMaterial(gridManager.RuntimeDefaultGridMaterial);
        }
    }

    void CreatePreviewObject()
    {
        if (previewObject != null) Destroy(previewObject);

        previewObject = new GameObject("Preview_" + gameObject.name);
        previewObject.transform.position = transform.position;
        previewObject.transform.rotation = transform.rotation;
        previewObject.transform.localScale = transform.localScale;

        MeshFilter originalMeshFilter = GetComponent<MeshFilter>();
        if (originalMeshFilter != null)
        {
            MeshFilter previewMeshFilter = previewObject.AddComponent<MeshFilter>();
            previewMeshFilter.mesh = originalMeshFilter.mesh;
        }
        else
        {
            Debug.LogWarning($"PlacableItem: '{gameObject.name}'에 MeshFilter가 없습니다. 미리보기 메시를 생성할 수 없습니다.");
            Destroy(previewObject);
            previewObject = null;
            return;
        }

        previewMeshRenderer = previewObject.AddComponent<MeshRenderer>();
        previewMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        previewMeshRenderer.receiveShadows = false;

        previewMeshRenderer.enabled = false;

        if (canPlaceMaterial != null)
        {
            previewMeshRenderer.material = canPlaceMaterial;
        }
        else
        {
            Debug.LogWarning("PlacableItem: canPlaceMaterial이 할당되지 않았습니다. 미리보기 색상 전환이 작동하지 않습니다.");
        }
    }

    void DestroyPreviewObject()
    {
        if (previewObject != null)
            Destroy(previewObject);
        previewObject = null;
        previewMeshRenderer = null;
    }

    [Header("Raycast Settings for Direct Interactor")]
    public float raycastDistance = 5.0f;
    public LayerMask placementLayerMask;
    public Material lineMaterial;
}