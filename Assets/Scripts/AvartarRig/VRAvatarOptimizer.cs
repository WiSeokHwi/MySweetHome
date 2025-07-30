using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// VR 아바타 최적화 및 문제 해결을 위한 유틸리티
/// </summary>
public class VRAvatarOptimizer : MonoBehaviour
{
    [Header("성능 최적화")]
    [SerializeField] private bool useFixedUpdate = false; // 물리 업데이트와 동기화
    [SerializeField] private int updateFrequency = 2; // N프레임마다 업데이트
    [SerializeField] private bool enableLOD = true; // 거리 기반 LOD
    [SerializeField] private float lodDistance = 10f;

    [Header("IK 스무딩")]
    [SerializeField] private bool enableSmoothing = true;
    [SerializeField] private float positionSmoothSpeed = 15f;
    [SerializeField] private float rotationSmoothSpeed = 10f;

    [Header("충돌 방지")]
    [SerializeField] private bool preventArmIntersection = true;
    [SerializeField] private float minArmDistance = 0.3f;

    [Header("IK 참조 (선택사항)")]
    [SerializeField] private Transform leftHandIKTarget;
    [SerializeField] private Transform rightHandIKTarget;
    [SerializeField] private TwoBoneIKConstraint leftArmIK;
    [SerializeField] private TwoBoneIKConstraint rightArmIK;

    private AdvancedVRCalibration advancedCalibration;
    private int frameCounter = 0;
    private Camera playerCamera;

    void Start()
    {
        // 캘리브레이션 스크립트 찾기
        advancedCalibration = GetComponent<AdvancedVRCalibration>();
        playerCamera = Camera.main;

        // AdvancedVRCalibration을 사용하는 경우 IK 참조들을 자동으로 가져오기
        if (advancedCalibration != null)
        {
            // AdvancedVRCalibration에서 IK 제약조건들을 직접 참조
            // Inspector에서 설정하지 않은 경우 자동으로 찾기
            if (leftArmIK == null || rightArmIK == null)
            {
                FindIKConstraintsFromAdvanced();
            }
        }

        // IK 타겟들 자동 찾기 (설정되지 않은 경우)
        if (leftHandIKTarget == null)
            leftHandIKTarget = FindIKTarget("LeftHandTarget");
        if (rightHandIKTarget == null)
            rightHandIKTarget = FindIKTarget("RightHandTarget");
    }

    private void FindIKConstraintsFromAdvanced()
    {
        // Scene에서 TwoBoneIKConstraint 컴포넌트들을 찾아서 할당
        TwoBoneIKConstraint[] ikConstraints = FindObjectsOfType<TwoBoneIKConstraint>();

        foreach (var constraint in ikConstraints)
        {
            // 제약조건 이름이나 연결된 본을 기준으로 좌/우 구분
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
        // 이름으로 구분
        if (constraint.name.ToLower().Contains("left"))
            return true;

        // 연결된 본으로 구분
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
        // 이름으로 구분
        if (constraint.name.ToLower().Contains("right"))
            return true;

        // 연결된 본으로 구분
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
            // 하위 오브젝트에서 재귀 검색
            found = FindInChildren(transform, targetName);
        }

        // 전체 Scene에서 검색 (마지막 수단)
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
        // 프레임 스킵 최적화
        frameCounter++;
        if (frameCounter % updateFrequency != 0) return;

        // LOD 체크
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
        // 팔이 몸통을 관통하지 않도록 보정
        if (leftHandIKTarget == null || rightHandIKTarget == null) return;

        float handDistance = Vector3.Distance(leftHandIKTarget.position, rightHandIKTarget.position);
        if (handDistance < minArmDistance)
        {
            // 최소 거리 유지하도록 보정
            Vector3 midPoint = (leftHandIKTarget.position + rightHandIKTarget.position) * 0.5f;
            Vector3 separation = (rightHandIKTarget.position - leftHandIKTarget.position).normalized * minArmDistance * 0.5f;

            leftHandIKTarget.position = midPoint - separation;
            rightHandIKTarget.position = midPoint + separation;
        }
    }

    /// <summary>
    /// IK 타겟 위치를 부드럽게 보간
    /// </summary>
    public Vector3 SmoothIKPosition(Vector3 currentPos, Vector3 targetPos)
    {
        if (!enableSmoothing) return targetPos;

        return Vector3.Lerp(currentPos, targetPos, positionSmoothSpeed * Time.deltaTime);
    }

    /// <summary>
    /// IK 타겟 회전을 부드럽게 보간
    /// </summary>
    public Quaternion SmoothIKRotation(Quaternion currentRot, Quaternion targetRot)
    {
        if (!enableSmoothing) return targetRot;

        return Quaternion.Slerp(currentRot, targetRot, rotationSmoothSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 외부에서 호출하여 스무딩된 위치 적용
    /// </summary>
    public void ApplySmoothingToTarget(Transform target, Vector3 desiredPosition, Quaternion desiredRotation)
    {
        if (target == null) return;

        target.position = SmoothIKPosition(target.position, desiredPosition);
        target.rotation = SmoothIKRotation(target.rotation, desiredRotation);
    }

    /// <summary>
    /// 디버그 정보 출력
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
/// VR 아바타 디버그 도구 (AdvancedVRCalibration 전용)
/// </summary>
public class VRAvatarDebugger : MonoBehaviour
{
    [Header("디버그 표시")]
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

        // 주요 본들 연결선 그리기
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

        // IK 타겟들을 찾아서 표시
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
        // 먼저 자식에서 찾기
        Transform found = FindInChildren(transform, targetName);

        // 전체 Scene에서 찾기
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

        // 아바타 치수 표시
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