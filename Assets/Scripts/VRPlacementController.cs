using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;
using UnityEngine.XR;

public class VRPlacementController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("씬에 있는 GridManager 참조.")]
    public GridManager gridManager;
    [Tooltip("이 스크립트가 붙은 GameObject에 있는 NearFarInteractor 컴포넌트.")]
    public NearFarInteractor nearFarInteractor;
    [Tooltip("아이템 배치 모드에서 Raycast를 수행할 XRRayInteractor.")]
    public XRRayInteractor placementRayInteractor;

    [Header("Input Actions")]
    public InputActionProperty placementModeToggleAction;

    [Header("Placement Visuals")]
    public Material previewCanPlaceMaterial;
    public Material previewCannotPlaceMaterial;

    [Header("Raycast Settings")]
    public LayerMask placementLayerMask = -1;
    public float raycastDistance = 10.0f;

    [Header("Placement Rotation")]
    [Tooltip("Smart Rotation 사용 여부")]
    public bool useSmartRotation = true;
    [Tooltip("Smart Rotation을 사용하지 않을 때의 고정 Y축 회전값 (도 단위)")]
    public float fixedPlacementRotationY = 0f;

    [Header("Haptic Feedback")]
    [Range(0f, 1f)]
    public float placementSuccessHapticIntensity = 0.5f;
    [Range(0f, 1f)]
    public float placementFailureHapticIntensity = 0.8f;

    private PlacableItem currentGrabbedItem;
    private GameObject previewObject;
    private MeshRenderer previewMeshRenderer;
    private Collider previewCollider;
    private bool inPlacementMode = false;
    private bool previewObjectActive = false;
    private bool hasCollision = false; // 미리보기 오브젝트 충돌 상태

    private Vector3 originalGrabbedItemPosition;
    private Quaternion originalGrabbedItemRotation;

    void Awake()
    {
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (gridManager == null)
        {
            gridManager = FindAnyObjectByType<GridManager>();
            if (gridManager == null)
            {
                Debug.LogError("VRPlacementController: GridManager를 찾을 수 없습니다!", this);
                enabled = false;
                return;
            }
        }

        if (nearFarInteractor == null)
        {
            nearFarInteractor = GetComponent<NearFarInteractor>() ?? GetComponentInChildren<NearFarInteractor>();
            if (nearFarInteractor == null)
            {
                Debug.LogError("VRPlacementController: NearFarInteractor를 찾을 수 없습니다.", this);
                enabled = false;
                return;
            }
        }

        if (placementRayInteractor == null)
        {
            Debug.LogWarning("VRPlacementController: placementRayInteractor가 할당되지 않았습니다.", this);
        }
        else
        {
            // 초기 상태에서는 배치 레이캐스터 비활성화
            placementRayInteractor.enabled = false;
        }
    }

    void OnEnable()
    {
        if (nearFarInteractor != null)
        {
            nearFarInteractor.selectEntered.AddListener(OnSelectEntered);
            nearFarInteractor.selectExited.AddListener(OnSelectExited);
        }

        if (placementModeToggleAction.action != null)
        {
            placementModeToggleAction.action.Enable();
            placementModeToggleAction.action.started += OnPlacementModeTogglePressed;
        }
    }

    void OnDisable()
    {
        if (nearFarInteractor != null)
        {
            nearFarInteractor.selectEntered.RemoveListener(OnSelectEntered);
            nearFarInteractor.selectExited.RemoveListener(OnSelectExited);
        }

        if (placementModeToggleAction.action != null)
        {
            placementModeToggleAction.action.started -= OnPlacementModeTogglePressed;
            placementModeToggleAction.action.Disable();
        }

        ExitPlacementMode();

        // 배치 레이캐스터 명시적 비활성화 (안전장치)
        if (placementRayInteractor != null)
            placementRayInteractor.enabled = false;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        GameObject grabbedGameObject = (args.interactableObject as MonoBehaviour)?.gameObject;
        if (grabbedGameObject == null)
        {
            Debug.LogWarning("VRPlacementController: 잡힌 Interactable이 유효하지 않습니다.");
            return;
        }

        currentGrabbedItem = grabbedGameObject.GetComponent<PlacableItem>();
        if (currentGrabbedItem == null)
        {
            Debug.LogWarning($"VRPlacementController: '{grabbedGameObject.name}'에 PlacableItem 컴포넌트가 없습니다.", grabbedGameObject);
            return;
        }

        // 원래 위치와 회전 저장
        originalGrabbedItemPosition = currentGrabbedItem.transform.position;
        originalGrabbedItemRotation = currentGrabbedItem.transform.rotation;

        // 이미 배치된 아이템이면 그리드에서 해제
        if (currentGrabbedItem.IsPlaced)
        {
            gridManager.ReleaseCells(currentGrabbedItem);
            currentGrabbedItem.SetPlaced(false);
        }

        // Rigidbody 상태 설정
        SetGrabbedRigidbodyState(currentGrabbedItem);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        GameObject releasedGameObject = (args.interactableObject as MonoBehaviour)?.gameObject;
        if (releasedGameObject == null) return;

        PlacableItem releasedItem = releasedGameObject.GetComponent<PlacableItem>();
        if (releasedItem == null || releasedItem != currentGrabbedItem) return;

        if (inPlacementMode)
        {
            AttemptPlaceItem();
        }
        else
        {
            // 배치 모드가 아닐 때는 물리 상태 복원하고 그리드 점유 해제
            RestoreRigidbodyState(currentGrabbedItem);

            // GridManager에게 아이템이 떨어뜨려졌음을 알림
            if (gridManager != null)
                gridManager.HandleItemDropped(currentGrabbedItem);

            // 아이템을 놓았으므로 currentGrabbedItem 초기화
            currentGrabbedItem = null;
        }
    }

    private void OnPlacementModeTogglePressed(InputAction.CallbackContext context)
    {
        if (currentGrabbedItem == null)
        {
            Debug.Log("VRPlacementController: 아이템을 잡고 있지 않아 배치 모드로 전환할 수 없습니다.");
            return;
        }

        // 아이템이 실제로 잡혀있는지 확인
        if (!IsItemCurrentlyGrabbed(currentGrabbedItem))
        {
            Debug.LogWarning("VRPlacementController: 아이템이 실제로 잡혀있지 않아 배치 모드로 전환할 수 없습니다.");
            currentGrabbedItem = null; // 상태 정리
            return;
        }

        TogglePlacementMode();
    }

    private void TogglePlacementMode()
    {
        inPlacementMode = !inPlacementMode;
        if (inPlacementMode)
            EnterPlacementMode();
        else
            ExitPlacementMode();
    }

    void Update()
    {
        // 배치 모드인데 아이템을 잡고 있지 않으면 배치 모드 해제
        if (inPlacementMode && currentGrabbedItem == null)
        {
            Debug.LogWarning("VRPlacementController: 배치 모드에서 아이템이 없어 배치 모드를 해제합니다.");
            ExitPlacementMode();
            return;
        }

        if (inPlacementMode && currentGrabbedItem != null)
        {
            UpdatePlacementPreview();

            // 로컬 그리드 업데이트 (플레이어 위치 기준)
            if (gridManager != null && nearFarInteractor != null)
            {
                Vector3 playerPosition = nearFarInteractor.transform.position;
                gridManager.UpdateLocalGrid(playerPosition);
            }
        }
        else if (previewObjectActive)
        {
            HidePreview();
        }
    }

    private void EnterPlacementMode()
    {
        if (currentGrabbedItem == null) return;

        CreatePreviewObject(currentGrabbedItem.gameObject);
        HideOriginalItem(true);

        // 회전 추적 비활성화
        var grabInteractable = currentGrabbedItem.GetComponent<XRGrabInteractable>();
        if (grabInteractable != null)
            grabInteractable.trackRotation = false;

        if (placementRayInteractor != null)
            placementRayInteractor.enabled = true;

        if (gridManager != null)
            gridManager.SetCurrentPlacingItem(currentGrabbedItem);
    }

    private void ExitPlacementMode()
    {
        inPlacementMode = false;
        DestroyPreviewObject();
        HideOriginalItem(false);

        if (currentGrabbedItem != null)
        {
            // 회전 추적 복원
            var grabInteractable = currentGrabbedItem.GetComponent<XRGrabInteractable>();
            if (grabInteractable != null)
                grabInteractable.trackRotation = true;
        }

        if (placementRayInteractor != null)
            placementRayInteractor.enabled = false;

        if (gridManager != null)
            gridManager.ClearCurrentPlacingItem();
    }

    private void UpdatePlacementPreview()
    {
        if (currentGrabbedItem == null || gridManager == null)
        {
            HidePreview();
            return;
        }

        if (!TryGetPlacementRaycast(out RaycastHit hit, out bool hasHit))
        {
            HidePreview();
            return;
        }

        Vector3 targetPreviewPosition = gridManager.SnapToGridForPlacement(
            hasHit ? hit.point : currentGrabbedItem.transform.position,
            currentGrabbedItem.itemGridSize,
            currentGrabbedItem.ItemWorldHeight
        );

        Vector3Int projectedGridOrigin = gridManager.WorldToGridCoordinates(targetPreviewPosition);
        bool canPlace = hasHit &&
                       gridManager.IsInGridBounds(projectedGridOrigin) &&
                       gridManager.CanPlaceItem(projectedGridOrigin, currentGrabbedItem.itemGridSize) &&
                       !hasCollision; // 충돌 상태도 검사

        ShowPreview(targetPreviewPosition, GetPlacementRotation(hasHit ? hit : default), canPlace);
    }

    private bool TryGetPlacementRaycast(out RaycastHit hit, out bool hasHit)
    {
        hit = default;
        hasHit = false;

        if (placementRayInteractor == null) return false;

        Vector3 rayOrigin = placementRayInteractor.transform.position;
        Vector3 rayDirection = placementRayInteractor.transform.forward;

        hasHit = Physics.Raycast(rayOrigin, rayDirection, out hit, raycastDistance, placementLayerMask);
        return true;
    }

    private Quaternion GetPlacementRotation(RaycastHit hit)
    {
        if (!useSmartRotation)
            return Quaternion.Euler(0, fixedPlacementRotationY, 0);

        // Smart rotation logic (90도 단위 스냅)
        Vector3 rayDirection = hit.normal != Vector3.zero ? -hit.normal : Vector3.forward;
        Vector3 projectedDirection = new Vector3(rayDirection.x, 0, rayDirection.z).normalized;

        float angle = Mathf.Atan2(projectedDirection.x, projectedDirection.z) * Mathf.Rad2Deg;
        float snappedAngle = Mathf.Round(angle / 90f) * 90f;

        return Quaternion.Euler(0, snappedAngle, 0);
    }

    private void ShowPreview(Vector3 position, Quaternion rotation, bool canPlace)
    {
        if (previewObject == null) return;

        previewObject.transform.position = position;
        previewObject.transform.rotation = rotation;

        if (previewMeshRenderer != null)
        {
            previewMeshRenderer.material = canPlace ? previewCanPlaceMaterial : previewCannotPlaceMaterial;
        }

        if (!previewObjectActive)
        {
            previewObject.SetActive(true);
            previewObjectActive = true;
        }

        if (gridManager != null)
            gridManager.UpdateGridMaterialForPlacement(canPlace);
    }

    private void HidePreview()
    {
        if (previewObject != null && previewObjectActive)
        {
            previewObject.SetActive(false);
            previewObjectActive = false;
        }

        if (gridManager != null)
            gridManager.SetGridMaterial(gridManager.RuntimeDefaultGridMaterial);
    }

    private void AttemptPlaceItem()
    {
        if (currentGrabbedItem == null || gridManager == null)
        {
            Debug.LogWarning("AttemptPlaceItem: 필요한 컴포넌트가 없어 배치를 시도할 수 없습니다.");
            ExitPlacementMode();
            return;
        }

        if (!TryGetPlacementRaycast(out RaycastHit hit, out bool hasHit))
        {
            HandlePlacementFailure("Raycast 실패");
            return;
        }

        Vector3 finalPlacementPosition = gridManager.SnapToGridForPlacement(
            hasHit ? hit.point : originalGrabbedItemPosition,
            currentGrabbedItem.itemGridSize,
            currentGrabbedItem.ItemWorldHeight
        );

        Vector3Int projectedGridOrigin = gridManager.WorldToGridCoordinates(finalPlacementPosition);
        bool canPlace = hasHit &&
                       gridManager.IsInGridBounds(projectedGridOrigin) &&
                       gridManager.CanPlaceItem(projectedGridOrigin, currentGrabbedItem.itemGridSize) &&
                       !hasCollision; // 충돌 상태도 검사

        if (canPlace)
        {
            HandlePlacementSuccess(finalPlacementPosition, GetPlacementRotation(hit));
        }
        else
        {
            // 실패 원인을 구체적으로 파악
            string failureReason = "배치 불가능한 위치";
            if (!hasHit)
                failureReason = "레이캐스트 실패";
            else if (!gridManager.IsInGridBounds(projectedGridOrigin))
                failureReason = "그리드 범위 초과";
            else if (!gridManager.CanPlaceItem(projectedGridOrigin, currentGrabbedItem.itemGridSize))
                failureReason = "그리드 셀이 이미 점유됨";
            else if (hasCollision)
                failureReason = "다른 오브젝트와 충돌";

            HandlePlacementFailure(failureReason);
        }

        ExitPlacementMode();
    }

    private void HandlePlacementSuccess(Vector3 position, Quaternion rotation)
    {
        // 위치와 회전 설정
        currentGrabbedItem.transform.position = position;
        currentGrabbedItem.transform.rotation = rotation;

        // 원본 아이템 메시 렌더러 다시 활성화
        HideOriginalItem(false);

        // 그리드에 등록 후 배치 상태로 변경  
        gridManager.OccupyCells(currentGrabbedItem);
        currentGrabbedItem.SetPlaced(true);

        // 물리 상태 설정
        SetPlacedRigidbodyState(currentGrabbedItem);

        // 햅틱 피드백
        SendHapticFeedback(true);

        Debug.Log($"아이템 '{currentGrabbedItem.gameObject.name}'이 성공적으로 배치되었습니다.");

        currentGrabbedItem = null;
    }

    private void HandlePlacementFailure(string reason)
    {
        Debug.LogWarning($"아이템 배치 실패: {reason}. 현재 위치에서 떨어뜨립니다.");

        // 현재 위치에서 떨어뜨리기
        if (currentGrabbedItem != null)
        {
            // 원본 아이템 메시 렌더러 다시 활성화
            HideOriginalItem(false);

            // 배치 실패 시 그리드 점유 해제 (GridManager에게 알림)
            if (gridManager != null)
            {
                gridManager.HandleItemDropped(currentGrabbedItem);
            }

            // 현재 위치 유지 (원래 위치로 돌아가지 않음)
            // 물리 상태만 복원하여 자연스럽게 떨어지도록 함
            RestoreRigidbodyState(currentGrabbedItem);
        }

        // 햅틱 피드백
        SendHapticFeedback(false);

        // 배치 실패했으므로 currentGrabbedItem 초기화
        currentGrabbedItem = null;
    }

    private void SetGrabbedRigidbodyState(PlacableItem item)
    {
        var rb = item.GetComponent<Rigidbody>();
        if (rb == null) return;

        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void SetPlacedRigidbodyState(PlacableItem item)
    {
        var rb = item.GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void RestoreRigidbodyState(PlacableItem item)
    {
        var rb = item.GetComponent<Rigidbody>();
        if (rb == null || item.IsPlaced) return;

        rb.isKinematic = false;
        rb.useGravity = true;
    }

    private void HideOriginalItem(bool hide)
    {
        if (currentGrabbedItem == null) return;

        var meshRenderer = currentGrabbedItem.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = !hide;
    }

    private void CreatePreviewObject(GameObject originalObject)
    {
        DestroyPreviewObject();

        var originalMeshFilter = originalObject.GetComponent<MeshFilter>();
        if (originalMeshFilter == null)
        {
            Debug.LogWarning($"VRPlacementController: '{originalObject.name}'에 MeshFilter가 없습니다.");
            return;
        }

        previewObject = new GameObject($"Preview_{originalObject.name}");

        var previewMeshFilter = previewObject.AddComponent<MeshFilter>();
        previewMeshFilter.mesh = originalMeshFilter.mesh;

        previewMeshRenderer = previewObject.AddComponent<MeshRenderer>();
        previewMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        previewMeshRenderer.receiveShadows = false;

        // 원본 오브젝트의 콜라이더를 복사하여 충돌 감지용으로 추가
        AddPreviewCollider(originalObject);

        // 충돌 감지 컴포넌트 추가
        var collisionDetector = previewObject.AddComponent<PreviewCollisionDetector>();
        collisionDetector.Initialize(this);

        previewObject.SetActive(false);
        previewObjectActive = false;
        hasCollision = false;

        if (previewCanPlaceMaterial == null || previewCannotPlaceMaterial == null)
        {
            Debug.LogWarning("VRPlacementController: Preview Materials가 할당되지 않았습니다.");
        }
    }

    private void AddPreviewCollider(GameObject originalObject)
    {
        // 원본 오브젝트의 콜라이더를 찾아서 복사
        var originalCollider = originalObject.GetComponent<Collider>();
        if (originalCollider != null)
        {
            // 원본 콜라이더 타입에 따라 동일한 타입 생성
            if (originalCollider is BoxCollider originalBox)
            {
                var previewBox = previewObject.AddComponent<BoxCollider>();
                previewBox.center = originalBox.center;
                previewBox.size = originalBox.size;
                previewBox.isTrigger = true; // 트리거로 설정하여 물리 충돌 방지
                previewCollider = previewBox;
            }
            else if (originalCollider is SphereCollider originalSphere)
            {
                var previewSphere = previewObject.AddComponent<SphereCollider>();
                previewSphere.center = originalSphere.center;
                previewSphere.radius = originalSphere.radius;
                previewSphere.isTrigger = true;
                previewCollider = previewSphere;
            }
            else if (originalCollider is CapsuleCollider originalCapsule)
            {
                var previewCapsule = previewObject.AddComponent<CapsuleCollider>();
                previewCapsule.center = originalCapsule.center;
                previewCapsule.radius = originalCapsule.radius;
                previewCapsule.height = originalCapsule.height;
                previewCapsule.direction = originalCapsule.direction;
                previewCapsule.isTrigger = true;
                previewCollider = previewCapsule;
            }
            else if (originalCollider is MeshCollider originalMesh)
            {
                var previewMesh = previewObject.AddComponent<MeshCollider>();
                previewMesh.sharedMesh = originalMesh.sharedMesh;
                previewMesh.convex = true; // 트리거 사용을 위해 convex 필요
                previewMesh.isTrigger = true;
                previewCollider = previewMesh;
            }
        }
        else
        {
            // 콜라이더가 없으면 메시를 기반으로 박스 콜라이더 생성
            var meshFilter = originalObject.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                var bounds = meshFilter.mesh.bounds;
                var previewBox = previewObject.AddComponent<BoxCollider>();
                previewBox.center = bounds.center;
                previewBox.size = bounds.size;
                previewBox.isTrigger = true;
                previewCollider = previewBox;
            }
        }
    }

    private void DestroyPreviewObject()
    {
        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
            previewMeshRenderer = null;
            previewCollider = null;
            previewObjectActive = false;
            hasCollision = false;
        }
    }

    // 충돌 상태 업데이트 (PreviewCollisionDetector에서 호출)
    public void SetPreviewCollisionState(bool collision)
    {
        hasCollision = collision;
    }

    // PreviewCollisionDetector에서 사용할 현재 잡힌 아이템 참조 제공
    public PlacableItem GetCurrentGrabbedItem()
    {
        return currentGrabbedItem;
    }

    private void SendHapticFeedback(bool success)
    {
        if (nearFarInteractor == null) return;

        // nearFarInteractor가 붙은 게임오브젝트에서 XR Controller의 InputDevice를 얻음
        var xrInteractor = nearFarInteractor;
        var inputDevice = GetInputDeviceFromInteractor(xrInteractor);
        if (!inputDevice.isValid)
        {
            Debug.LogWarning("SendHapticFeedback: 유효한 InputDevice를 찾을 수 없습니다.");
            return;
        }

        float amplitude = success ? placementSuccessHapticIntensity : placementFailureHapticIntensity;
        float duration = success ? 0.1f : 0.2f;

        // 채널 0에서 햅틱 실행
        inputDevice.SendHapticImpulse(0, amplitude, duration);
    }

    private UnityEngine.XR.InputDevice GetInputDeviceFromInteractor(NearFarInteractor interactor)
    {
        // XRDirectInteractor, XRRayInteractor 등 인터랙터에서 InputDevice 접근 방법
        // 여기서는 간단히 interactor.transform을 통해 DeviceRole을 찾는 예시입니다.

        UnityEngine.XR.InputDevice device = default;

        // 예시: 왼손 혹은 오른손 역할 찾기 (필요시 수정)
        var characteristics = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller;

        var devices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(characteristics, devices);

        foreach (var dev in devices)
        {
            // 위치가 비슷한 디바이스 찾기 (더 정교하게 매칭 필요)
            if (dev.isValid)
            {
                device = dev;
                break;
            }
        }

        return device;
    }

    private bool IsItemCurrentlyGrabbed(PlacableItem item)
    {
        if (item == null || nearFarInteractor == null) return false;

        // NearFarInteractor가 현재 이 아이템을 선택하고 있는지 확인
        var currentInteractable = nearFarInteractor.firstInteractableSelected;
        if (currentInteractable == null) return false;

        // 현재 선택된 interactable이 우리가 추적 중인 아이템과 같은지 확인
        var interactableGameObject = (currentInteractable as MonoBehaviour)?.gameObject;
        return interactableGameObject == item.gameObject;
    }

    void OnDestroy()
    {
        DestroyPreviewObject();
    }
}