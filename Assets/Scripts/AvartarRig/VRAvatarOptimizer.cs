using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// VR �ƹ�Ÿ ����ȭ �� ���� �ذ��� ���� ��ƿ��Ƽ
/// </summary>
public class VRAvatarOptimizer : MonoBehaviour
{
    [Header("���� ����ȭ")]
    [SerializeField] private bool useFixedUpdate = false; // ���� ������Ʈ�� ����ȭ
    [SerializeField] private int updateFrequency = 2; // N�����Ӹ��� ������Ʈ
    [SerializeField] private bool enableLOD = true; // �Ÿ� ��� LOD
    [SerializeField] private float lodDistance = 10f;

    [Header("IK ������")]
    [SerializeField] private bool enableSmoothing = true;
    [SerializeField] private float positionSmoothSpeed = 15f;
    [SerializeField] private float rotationSmoothSpeed = 10f;

    [Header("�浹 ����")]
    [SerializeField] private bool preventArmIntersection = true;
    [SerializeField] private float minArmDistance = 0.3f;

    [Header("IK ���� (���û���)")]
    [SerializeField] private Transform leftHandIKTarget;
    [SerializeField] private Transform rightHandIKTarget;
    [SerializeField] private TwoBoneIKConstraint leftArmIK;
    [SerializeField] private TwoBoneIKConstraint rightArmIK;

    private AdvancedVRCalibration advancedCalibration;
    private int frameCounter = 0;
    private Camera playerCamera;

    void Start()
    {
        // Ķ���극�̼� ��ũ��Ʈ ã��
        advancedCalibration = GetComponent<AdvancedVRCalibration>();
        playerCamera = Camera.main;

        // AdvancedVRCalibration�� ����ϴ� ��� IK �������� �ڵ����� ��������
        if (advancedCalibration != null)
        {
            // AdvancedVRCalibration���� IK �������ǵ��� ���� ����
            // Inspector���� �������� ���� ��� �ڵ����� ã��
            if (leftArmIK == null || rightArmIK == null)
            {
                FindIKConstraintsFromAdvanced();
            }
        }

        // IK Ÿ�ٵ� �ڵ� ã�� (�������� ���� ���)
        if (leftHandIKTarget == null)
            leftHandIKTarget = FindIKTarget("LeftHandTarget");
        if (rightHandIKTarget == null)
            rightHandIKTarget = FindIKTarget("RightHandTarget");
    }

    private void FindIKConstraintsFromAdvanced()
    {
        // Scene���� TwoBoneIKConstraint ������Ʈ���� ã�Ƽ� �Ҵ�
        TwoBoneIKConstraint[] ikConstraints = FindObjectsOfType<TwoBoneIKConstraint>();

        foreach (var constraint in ikConstraints)
        {
            // �������� �̸��̳� ����� ���� �������� ��/�� ����
            if (IsLeftArmConstraint(constraint))
            {
                leftArmIK = constraint;
            }
            else if (IsRightArmConstraint(constraint))
            {
                rightArmIK = constraint;
            }
        }

        if (leftArmIK != null) Debug.Log($"Left Arm IK found: {leftArmIK.name}");
        if (rightArmIK != null) Debug.Log($"Right Arm IK found: {rightArmIK.name}");
    }

    private bool IsLeftArmConstraint(TwoBoneIKConstraint constraint)
    {
        // �̸����� ����
        if (constraint.name.ToLower().Contains("left"))
            return true;

        // ����� ������ ����
        var data = constraint.data;
        if (data.root != null)
        {
            string rootName = data.root.name.ToLower();
            return rootName.Contains("left") || rootName.Contains("l_");
        }

        return false;
    }

    private bool IsRightArmConstraint(TwoBoneIKConstraint constraint)
    {
        // �̸����� ����
        if (constraint.name.ToLower().Contains("right"))
            return true;

        // ����� ������ ����
        var data = constraint.data;
        if (data.root != null)
        {
            string rootName = data.root.name.ToLower();
            return rootName.Contains("right") || rootName.Contains("r_");
        }

        return false;
    }

    private Transform FindIKTarget(string targetName)
    {
        Transform found = transform.Find(targetName);
        if (found == null)
        {
            // ���� ������Ʈ���� ��� �˻�
            found = FindInChildren(transform, targetName);
        }

        // ��ü Scene���� �˻� (������ ����)
        if (found == null)
        {
            GameObject foundObj = GameObject.Find(targetName);
            if (foundObj != null)
                found = foundObj.transform;
        }

        return found;
    }

    private Transform FindInChildren(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;

            Transform found = FindInChildren(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    void Update()
    {
        if (!useFixedUpdate)
            UpdateIKOptimized();
    }

    void FixedUpdate()
    {
        if (useFixedUpdate)
            UpdateIKOptimized();
    }

    private void UpdateIKOptimized()
    {
        // ������ ��ŵ ����ȭ
        frameCounter++;
        if (frameCounter % updateFrequency != 0) return;

        // LOD üũ
        if (enableLOD && IsOutOfLODRange())
        {
            DisableIK();
            return;
        }
        else
        {
            EnableIK();
        }

        if (preventArmIntersection)
            PreventArmBodyIntersection();
    }

    private bool IsOutOfLODRange()
    {
        if (playerCamera == null) return false;

        float distance = Vector3.Distance(transform.position, playerCamera.transform.position);
        return distance > lodDistance;
    }

    private void EnableIK()
    {
        if (leftArmIK != null && leftArmIK.weight < 1f)
            leftArmIK.weight = Mathf.Lerp(leftArmIK.weight, 1f, Time.deltaTime * 5f);

        if (rightArmIK != null && rightArmIK.weight < 1f)
            rightArmIK.weight = Mathf.Lerp(rightArmIK.weight, 1f, Time.deltaTime * 5f);
    }

    private void DisableIK()
    {
        if (leftArmIK != null && leftArmIK.weight > 0f)
            leftArmIK.weight = Mathf.Lerp(leftArmIK.weight, 0f, Time.deltaTime * 5f);

        if (rightArmIK != null && rightArmIK.weight > 0f)
            rightArmIK.weight = Mathf.Lerp(rightArmIK.weight, 0f, Time.deltaTime * 5f);
    }

    private void PreventArmBodyIntersection()
    {
        // ���� ������ �������� �ʵ��� ����
        if (leftHandIKTarget == null || rightHandIKTarget == null) return;

        float handDistance = Vector3.Distance(leftHandIKTarget.position, rightHandIKTarget.position);
        if (handDistance < minArmDistance)
        {
            // �ּ� �Ÿ� �����ϵ��� ����
            Vector3 midPoint = (leftHandIKTarget.position + rightHandIKTarget.position) * 0.5f;
            Vector3 separation = (rightHandIKTarget.position - leftHandIKTarget.position).normalized * minArmDistance * 0.5f;

            leftHandIKTarget.position = midPoint - separation;
            rightHandIKTarget.position = midPoint + separation;
        }
    }

    /// <summary>
    /// IK Ÿ�� ��ġ�� �ε巴�� ����
    /// </summary>
    public Vector3 SmoothIKPosition(Vector3 currentPos, Vector3 targetPos)
    {
        if (!enableSmoothing) return targetPos;

        return Vector3.Lerp(currentPos, targetPos, positionSmoothSpeed * Time.deltaTime);
    }

    /// <summary>
    /// IK Ÿ�� ȸ���� �ε巴�� ����
    /// </summary>
    public Quaternion SmoothIKRotation(Quaternion currentRot, Quaternion targetRot)
    {
        if (!enableSmoothing) return targetRot;

        return Quaternion.Slerp(currentRot, targetRot, rotationSmoothSpeed * Time.deltaTime);
    }

    /// <summary>
    /// �ܺο��� ȣ���Ͽ� �������� ��ġ ����
    /// </summary>
    public void ApplySmoothingToTarget(Transform target, Vector3 desiredPosition, Quaternion desiredRotation)
    {
        if (target == null) return;

        target.position = SmoothIKPosition(target.position, desiredPosition);
        target.rotation = SmoothIKRotation(target.rotation, desiredRotation);
    }

    /// <summary>
    /// ����� ���� ���
    /// </summary>
    [ContextMenu("Print IK Status")]
    public void PrintIKStatus()
    {
        Debug.Log("=== VRAvatarOptimizer Status ===");
        Debug.Log($"Using Advanced Calibration: {advancedCalibration != null}");
        Debug.Log($"Left Hand IK Target: {(leftHandIKTarget != null ? leftHandIKTarget.name : "Not Found")}");
        Debug.Log($"Right Hand IK Target: {(rightHandIKTarget != null ? rightHandIKTarget.name : "Not Found")}");
        Debug.Log($"Left Arm IK Constraint: {(leftArmIK != null ? leftArmIK.name : "Not Found")}");
        Debug.Log($"Right Arm IK Constraint: {(rightArmIK != null ? rightArmIK.name : "Not Found")}");
        Debug.Log($"Smoothing Enabled: {enableSmoothing}");
        Debug.Log($"LOD Enabled: {enableLOD}");
    }
}

/// <summary>
/// VR �ƹ�Ÿ ����� ���� (AdvancedVRCalibration ����)
/// </summary>
public class VRAvatarDebugger : MonoBehaviour
{
    [Header("����� ǥ��")]
    [SerializeField] private bool showBoneStructure = false;
    [SerializeField] private bool showIKTargets = true;
    [SerializeField] private bool showMeasurements = false;
    [SerializeField] private Color boneColor = Color.cyan;
    [SerializeField] private Color ikTargetColor = Color.red;

    private AdvancedVRCalibration advancedCalibration;
    private Animator avatarAnimator;

    void Start()
    {
        advancedCalibration = GetComponent<AdvancedVRCalibration>();
        avatarAnimator = GetComponent<Animator>();
    }

    void OnDrawGizmos()
    {
        if (showBoneStructure)
            DrawBoneStructure();

        if (showIKTargets)
            DrawIKTargets();

        if (showMeasurements)
            DrawMeasurements();
    }

    private void DrawBoneStructure()
    {
        if (avatarAnimator == null) return;

        Gizmos.color = boneColor;

        // �ֿ� ���� ���ἱ �׸���
        DrawBoneConnection(HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm);
        DrawBoneConnection(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
        DrawBoneConnection(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);

        DrawBoneConnection(HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm);
        DrawBoneConnection(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
        DrawBoneConnection(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);

        DrawBoneConnection(HumanBodyBones.Neck, HumanBodyBones.Head);
    }

    private void DrawBoneConnection(HumanBodyBones from, HumanBodyBones to)
    {
        Transform fromBone = avatarAnimator.GetBoneTransform(from);
        Transform toBone = avatarAnimator.GetBoneTransform(to);

        if (fromBone != null && toBone != null)
        {
            Gizmos.DrawLine(fromBone.position, toBone.position);
            Gizmos.DrawSphere(fromBone.position, 0.02f);
        }
    }

    private void DrawIKTargets()
    {
        Gizmos.color = ikTargetColor;

        // IK Ÿ�ٵ��� ã�Ƽ� ǥ��
        Transform leftTarget = FindIKTarget("LeftHandTarget");
        Transform rightTarget = FindIKTarget("RightHandTarget");

        if (leftTarget != null)
        {
            Gizmos.DrawSphere(leftTarget.position, 0.05f);
            Gizmos.DrawWireCube(leftTarget.position, Vector3.one * 0.1f);
        }

        if (rightTarget != null)
        {
            Gizmos.DrawSphere(rightTarget.position, 0.05f);
            Gizmos.DrawWireCube(rightTarget.position, Vector3.one * 0.1f);
        }
    }

    private Transform FindIKTarget(string targetName)
    {
        // ���� �ڽĿ��� ã��
        Transform found = FindInChildren(transform, targetName);

        // ��ü Scene���� ã��
        if (found == null)
        {
            GameObject foundObj = GameObject.Find(targetName);
            if (foundObj != null)
                found = foundObj.transform;
        }

        return found;
    }

    private Transform FindInChildren(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;

            Transform found = FindInChildren(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private void DrawMeasurements()
    {
        if (avatarAnimator == null) return;

        Gizmos.color = Color.yellow;

        // �ƹ�Ÿ ġ�� ǥ��
        Transform leftFoot = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform head = avatarAnimator.GetBoneTransform(HumanBodyBones.Head);

        if (leftFoot != null && head != null)
        {
            Gizmos.DrawLine(leftFoot.position, head.position);
        }

        Transform leftShoulder = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder);
        Transform rightShoulder = avatarAnimator.GetBoneTransform(HumanBodyBones.RightShoulder);

        if (leftShoulder != null && rightShoulder != null)
        {
            Gizmos.DrawLine(leftShoulder.position, rightShoulder.position);
        }
    }
}