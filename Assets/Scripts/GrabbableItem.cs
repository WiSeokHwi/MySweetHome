using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// GrabbableItem - VR 환경에서 XRGrabInteractable을 통해 잡을 수 있는 모든 아이템의 기본 클래스
///
/// == 주요 기능 ==
/// 1. XRGrabInteractable 및 Rigidbody 컴포넌트 자동 관리 및 초기화
/// 2. 잡힌 상태와 놓인 상태에 따른 Rigidbody 물리 속성 제어
/// 3. 잡기 상호작용 관련 이벤트 처리 (잡기 시작/끝)
///
/// == 사용 방법 ==
/// - 플레이어가 잡을 수 있는 모든 월드 아이템의 부모 클래스로 사용합니다.
/// - 이 클래스를 상속받아 각 아이템의 고유한 로직을 구현합니다.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))] // VR에서 잡을 수 있도록 XRGrabInteractable 필요
[RequireComponent(typeof(Rigidbody))] // 물리적 상호작용을 위해 Rigidbody 필요
public abstract class GrabbableItem : MonoBehaviour
{
    protected XRGrabInteractable grabInteractable; // VR 잡기 인터랙션 컴포넌트
    protected Rigidbody itemRigidbody; // 물리 시뮬레이션 컴포넌트

    protected bool isGrabbed = false; // 현재 플레이어에게 잡혀있는지 여부

    /// <summary>
    /// Unity Awake: 컴포넌트 초기화 및 종속성 검증
    /// </summary>
    protected virtual void Awake()
    {
        InitializeComponents();
    }

    /// <summary>
    /// Unity OnEnable: XRGrabInteractable 이벤트 리스너 등록
    /// </summary>
    protected virtual void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabStarted);
            grabInteractable.selectExited.AddListener(OnGrabEnded);
        }
    }

    /// <summary>
    /// Unity OnDisable: XRGrabInteractable 이벤트 리스너 해제
    /// </summary>
    protected virtual void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabStarted);
            grabInteractable.selectExited.RemoveListener(OnGrabEnded);
        }
    }

    /// <summary>
    /// 필수 컴포넌트 초기화 및 설정
    /// </summary>
    private void InitializeComponents()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
        {
            Debug.LogError($"GrabbableItem: '{gameObject.name}'에 XRGrabInteractable 컴포넌트가 필요합니다.", this);
            enabled = false; // 컴포넌트가 없으면 이 스크립트 비활성화
            return;
        }

        itemRigidbody = GetComponent<Rigidbody>();
        if (itemRigidbody == null)
        {
            itemRigidbody = gameObject.AddComponent<Rigidbody>();
            Debug.LogWarning($"GrabbableItem: '{gameObject.name}'에 Rigidbody가 없어 자동으로 추가했습니다.", this);
        }

        // 초기 상태 설정 (잡기 가능하고 물리 시뮬레이션 활성화)
        SetPhysicsForUnGrabbed();
    }

    /// <summary>
    /// 아이템이 잡혔을 때 호출됩니다.
    /// </summary>
    /// <param name="args">상호작용 이벤트 인자</param>
    protected virtual void OnGrabStarted(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        SetPhysicsForGrabbed();
        Debug.Log($"GrabbableItem: '{gameObject.name}'이 잡혔습니다.");
    }

    /// <summary>
    /// 아이템이 놓였을 때 호출됩니다.
    /// </summary>
    /// <param name="args">상호작용 이벤트 인자</param>
    protected virtual void OnGrabEnded(SelectExitEventArgs args)
    {
        isGrabbed = false;
        SetPhysicsForUnGrabbed();
        Debug.Log($"GrabbableItem: '{gameObject.name}'이 놓였습니다.");
    }

    /// <summary>
    /// 아이템이 잡혔을 때의 물리 속성 설정
    /// (일반적으로 Kinematic으로 설정하여 컨트롤러에 따라 움직이도록 함)
    /// </summary>
    protected virtual void SetPhysicsForGrabbed()
    {
        if (itemRigidbody == null) return;

        itemRigidbody.isKinematic = true;  // 잡혔을 때는 물리 영향 받지 않음
        itemRigidbody.useGravity = false;  // 중력 무시
        itemRigidbody.linearVelocity = Vector3.zero; // 속도 초기화
        itemRigidbody.angularVelocity = Vector3.zero; // 각속도 초기화
    }

    /// <summary>
    /// 아이템이 놓였을 때의 물리 속성 설정
    /// (일반적으로 물리 시뮬레이션 활성화)
    /// </summary>
    protected virtual void SetPhysicsForUnGrabbed()
    {
        if (itemRigidbody == null) return;

        itemRigidbody.isKinematic = false; // 놓였을 때는 물리 영향 받음
        itemRigidbody.useGravity = true;   // 중력 활성화
    }

    /// <summary>
    /// Rigidbody 컴포넌트 유효성 검사
    /// </summary>
    public bool HasValidRigidbody()
    {
        return itemRigidbody != null;
    }

    /// <summary>
    /// XRGrabInteractable 컴포넌트 유효성 검사
    /// </summary>
    public bool HasValidGrabInteractable()
    {
        return grabInteractable != null && grabInteractable.enabled;
    }
}
