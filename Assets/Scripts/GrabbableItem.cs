using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// GrabbableItem - VR ȯ�濡�� XRGrabInteractable�� ���� ���� �� �ִ� ��� �������� �⺻ Ŭ����
///
/// == �ֿ� ��� ==
/// 1. XRGrabInteractable �� Rigidbody ������Ʈ �ڵ� ���� �� �ʱ�ȭ
/// 2. ���� ���¿� ���� ���¿� ���� Rigidbody ���� �Ӽ� ����
/// 3. ��� ��ȣ�ۿ� ���� �̺�Ʈ ó�� (��� ����/��)
///
/// == ��� ��� ==
/// - �÷��̾ ���� �� �ִ� ��� ���� �������� �θ� Ŭ������ ����մϴ�.
/// - �� Ŭ������ ��ӹ޾� �� �������� ������ ������ �����մϴ�.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))] // VR���� ���� �� �ֵ��� XRGrabInteractable �ʿ�
[RequireComponent(typeof(Rigidbody))] // ������ ��ȣ�ۿ��� ���� Rigidbody �ʿ�
public abstract class GrabbableItem : MonoBehaviour
{
    protected XRGrabInteractable grabInteractable; // VR ��� ���ͷ��� ������Ʈ
    protected Rigidbody itemRigidbody; // ���� �ùķ��̼� ������Ʈ

    protected bool isGrabbed = false; // ���� �÷��̾�� �����ִ��� ����

    /// <summary>
    /// Unity Awake: ������Ʈ �ʱ�ȭ �� ���Ӽ� ����
    /// </summary>
    protected virtual void Awake()
    {
        InitializeComponents();
    }

    /// <summary>
    /// Unity OnEnable: XRGrabInteractable �̺�Ʈ ������ ���
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
    /// Unity OnDisable: XRGrabInteractable �̺�Ʈ ������ ����
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
    /// �ʼ� ������Ʈ �ʱ�ȭ �� ����
    /// </summary>
    private void InitializeComponents()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
        {
            Debug.LogError($"GrabbableItem: '{gameObject.name}'�� XRGrabInteractable ������Ʈ�� �ʿ��մϴ�.", this);
            enabled = false; // ������Ʈ�� ������ �� ��ũ��Ʈ ��Ȱ��ȭ
            return;
        }

        itemRigidbody = GetComponent<Rigidbody>();
        if (itemRigidbody == null)
        {
            itemRigidbody = gameObject.AddComponent<Rigidbody>();
            Debug.LogWarning($"GrabbableItem: '{gameObject.name}'�� Rigidbody�� ���� �ڵ����� �߰��߽��ϴ�.", this);
        }

        // �ʱ� ���� ���� (��� �����ϰ� ���� �ùķ��̼� Ȱ��ȭ)
        SetPhysicsForUnGrabbed();
    }

    /// <summary>
    /// �������� ������ �� ȣ��˴ϴ�.
    /// </summary>
    /// <param name="args">��ȣ�ۿ� �̺�Ʈ ����</param>
    protected virtual void OnGrabStarted(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        SetPhysicsForGrabbed();
        Debug.Log($"GrabbableItem: '{gameObject.name}'�� �������ϴ�.");
    }

    /// <summary>
    /// �������� ������ �� ȣ��˴ϴ�.
    /// </summary>
    /// <param name="args">��ȣ�ۿ� �̺�Ʈ ����</param>
    protected virtual void OnGrabEnded(SelectExitEventArgs args)
    {
        isGrabbed = false;
        SetPhysicsForUnGrabbed();
        Debug.Log($"GrabbableItem: '{gameObject.name}'�� �������ϴ�.");
    }

    /// <summary>
    /// �������� ������ ���� ���� �Ӽ� ����
    /// (�Ϲ������� Kinematic���� �����Ͽ� ��Ʈ�ѷ��� ���� �����̵��� ��)
    /// </summary>
    protected virtual void SetPhysicsForGrabbed()
    {
        if (itemRigidbody == null) return;

        itemRigidbody.isKinematic = true;  // ������ ���� ���� ���� ���� ����
        itemRigidbody.useGravity = false;  // �߷� ����
        itemRigidbody.linearVelocity = Vector3.zero; // �ӵ� �ʱ�ȭ
        itemRigidbody.angularVelocity = Vector3.zero; // ���ӵ� �ʱ�ȭ
    }

    /// <summary>
    /// �������� ������ ���� ���� �Ӽ� ����
    /// (�Ϲ������� ���� �ùķ��̼� Ȱ��ȭ)
    /// </summary>
    protected virtual void SetPhysicsForUnGrabbed()
    {
        if (itemRigidbody == null) return;

        itemRigidbody.isKinematic = false; // ������ ���� ���� ���� ����
        itemRigidbody.useGravity = true;   // �߷� Ȱ��ȭ
    }

    /// <summary>
    /// Rigidbody ������Ʈ ��ȿ�� �˻�
    /// </summary>
    public bool HasValidRigidbody()
    {
        return itemRigidbody != null;
    }

    /// <summary>
    /// XRGrabInteractable ������Ʈ ��ȿ�� �˻�
    /// </summary>
    public bool HasValidGrabInteractable()
    {
        return grabInteractable != null && grabInteractable.enabled;
    }
}
