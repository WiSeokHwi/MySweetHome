using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.InputSystem;

/// <summary>
/// VR 레이캐스트 통합 관리자
/// - UI 상호작용용 레이캐스트와 오브젝트 상호작용용 레이캐스트를 분리 관리
/// - 상황에 따라 적절한 레이캐스트만 활성화하여 성능 최적화 및 충돌 방지
/// </summary>
public class VRRaycastController : MonoBehaviour
{
    [Header("Raycast Components")]
    [Tooltip("UI 상호작용용 레이 인터랙터")]
    [SerializeField] private XRRayInteractor uiRayInteractor;

    [Header("Input Action")]
    [Tooltip("UI 상호작용을 위한 입력 액션")]
    [SerializeField] public InputActionProperty uiInteractionAction; // Inspector에서 할당할 수 있도록 public 또는 SerializeField 유지

    private void Awake()
    {
        // XRRayInteractor 컴포넌트가 자신에게 없으면 자식 오브젝트에서 찾거나 에러를 로그합니다.
        // 현재 코드에서는 GetComponent<XRRayInteractor>()를 사용하고 있는데,
        // uiRayInteractor는 주로 별도의 UI 레이 컨트롤러에 붙어있으므로 GetComponentInChildren이 더 적절할 수 있습니다.
        // 여기서는 명확성을 위해 GetComponent<XRRayInteractor>()가 이 스크립트와 동일한 GameObject에 있다고 가정합니다.
        if (uiRayInteractor == null)
        {
            uiRayInteractor = GetComponent<XRRayInteractor>();
            if (uiRayInteractor == null)
            {
                Debug.LogError("VRRaycastController: 이 GameObject에서 XRRayInteractor 컴포넌트를 찾을 수 없습니다! 수동 할당하거나 확인해주세요.", this);
                enabled = false; // 컴포넌트가 없으면 스크립트 비활성화
                return;
            }
            Debug.Log("VRRaycastController: XRRayInteractor 컴포넌트가 성공적으로 할당되었습니다 (Awake).", this);
        }

        // 초기 상태에서는 UI 레이를 비활성화합니다.
        uiRayInteractor.enabled = false;
    }

    private void OnEnable()
    {
        if (uiInteractionAction.action != null)
        {
            // UI 상호작용 입력 액션 활성화
            uiInteractionAction.action.Enable();
            Debug.Log("VRRaycastController: UI 상호작용 입력 액션이 활성화되었습니다.", this);

            // 입력 액션이 시작될 때 (버튼을 누를 때) 레이 활성화
            uiInteractionAction.action.started += OnInteractionStarted;
            // 입력 액션이 취소될 때 (버튼을 뗄 때) 레이 비활성화
            uiInteractionAction.action.canceled += OnInteractionCanceled;
        }
        else
        {
            Debug.LogError("VRRaycastController: UI Interaction Action이 할당되지 않았습니다! Input Action Asset을 확인해주세요.", this);
        }
    }

    private void OnDisable()
    {
        if (uiInteractionAction.action != null)
        {
            // 입력 액션 이벤트 구독 해제
            uiInteractionAction.action.started -= OnInteractionStarted;
            uiInteractionAction.action.canceled -= OnInteractionCanceled;

            // UI 상호작용 입력 액션 비활성화
            uiInteractionAction.action.Disable();
            Debug.Log("VRRaycastController: UI 상호작용 입력 액션이 비활성화되었습니다.", this);
        }

        // 스크립트 비활성화 시 레이도 비활성화
        if (uiRayInteractor != null)
        {
            uiRayInteractor.enabled = false;
        }
    }

    /// <summary>
    /// UI 상호작용 입력 액션이 시작될 때 호출됩니다.
    /// </summary>
    private void OnInteractionStarted(InputAction.CallbackContext context)
    {
        if (uiRayInteractor != null)
        {
            uiRayInteractor.enabled = true; // 레이 활성화
            Debug.Log("VRRaycastController: UI 레이캐스트 활성화됨 (입력 시작).", this);
        }
    }

    /// <summary>
    /// UI 상호작용 입력 액션이 취소될 때 호출됩니다.
    /// </summary>
    private void OnInteractionCanceled(InputAction.CallbackContext context)
    {
        if (uiRayInteractor != null)
        {
            uiRayInteractor.enabled = false; // 레이 비활성화
            Debug.Log("VRRaycastController: UI 레이캐스트 비활성화됨 (입력 종료).", this);
        }
    }
}