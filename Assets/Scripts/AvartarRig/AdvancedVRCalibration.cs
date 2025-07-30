using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections;

/// <summary>
/// Dynamic avatar calibration similar to VRChat.
/// Handles T-pose calibration and real-time IK application.
/// </summary>
public class AdvancedVRCalibration : MonoBehaviour
{
    [Header("VR Setup")]
    [SerializeField] private Transform headset;
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform rightController;

    [Header("Avatar Setup")]
    [SerializeField] private Animator avatarAnimator;
    [SerializeField] private RigBuilder rigBuilder;

    [Header("IK Constraints")]
    [SerializeField] private TwoBoneIKConstraint leftArmIK;
    [SerializeField] private TwoBoneIKConstraint rightArmIK;
    [SerializeField] private MultiAimConstraint headAimIK;

    [Header("Calibration Poses")]
    [SerializeField] private bool requireTPose = true;
    [SerializeField] private float tPoseHoldTime = 3f;
    [SerializeField] private float tPoseToleranceAngle = 15f;

    [Header("IK Settings")]
    [SerializeField] private float ikWeight = 1f;
    [SerializeField] private bool useElbowHints = true;
    [SerializeField] private float elbowBendAmount = 0.3f;

    // Calibration state
    private bool isCalibrating = false;
    private bool isCalibrated = false;
    private float tPoseTimer = 0f;

    // User body data
    private float userArmSpan;
    private float userShoulderWidth;
    private float userHeight;
    private Vector3 userShoulderOffset;

    // Avatar body data
    private float avatarArmSpan;
    private float avatarShoulderWidth;
    private float avatarHeight;

    // Ratios
    private float armLengthRatio = 1f;
    private float shoulderWidthRatio = 1f;
    private float heightRatio = 1f;

    // IK Targets
    private Transform headTarget;
    private Transform leftHandTarget;
    private Transform rightHandTarget;
    private Transform leftElbowHint;
    private Transform rightElbowHint;

    private VRAvatarOptimizer optimizer;

    void Start()
    {
        optimizer = GetComponent<VRAvatarOptimizer>();
        
        SetupIKTargets();
        MeasureAvatarDimensions();

        if (requireTPose)
        {
            StartCoroutine(WaitForTPoseCalibration());
        }
        else
        {
            PerformQuickCalibration();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            StartCoroutine(WaitForTPoseCalibration());
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetCalibration();
        }

        if (isCalibrated)
        {
            UpdateVRTracking();
        }
    }

    private void SetupIKTargets()
    {
        // Create parent object for IK targets
        GameObject ikTargetsParent = new GameObject("IK_Targets");
        ikTargetsParent.transform.SetParent(transform);

        headTarget = new GameObject("HeadTarget").transform;
        headTarget.SetParent(ikTargetsParent.transform);

        leftHandTarget = new GameObject("LeftHandTarget").transform;
        leftHandTarget.SetParent(ikTargetsParent.transform);

        rightHandTarget = new GameObject("RightHandTarget").transform;
        rightHandTarget.SetParent(ikTargetsParent.transform);

        if (useElbowHints)
        {
            leftElbowHint = new GameObject("LeftElbowHint").transform;
            leftElbowHint.SetParent(ikTargetsParent.transform);

            rightElbowHint = new GameObject("RightElbowHint").transform;
            rightElbowHint.SetParent(ikTargetsParent.transform);
        }

        // Assign targets to IK components
        if (leftArmIK != null)
        {
            var leftIKData = leftArmIK.data;
            leftIKData.target = leftHandTarget;
            if (useElbowHints) leftIKData.hint = leftElbowHint;
            leftArmIK.data = leftIKData;
        }

        if (rightArmIK != null)
        {
            var rightIKData = rightArmIK.data;
            rightIKData.target = rightHandTarget;
            if (useElbowHints) rightIKData.hint = rightElbowHint;
            rightArmIK.data = rightIKData;
        }
        
        if (headAimIK != null)
        {
            var headIKData = headAimIK.data;
            var sourceObjects = new WeightedTransformArray(1);
            sourceObjects[0] = new WeightedTransform(headTarget, 1f);
            headIKData.sourceObjects = sourceObjects;
            headAimIK.data = headIKData;
        }
    }

    private void MeasureAvatarDimensions()
    {
        if (avatarAnimator == null) return;

        // Measure avatar height
        Transform leftFoot = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform head = avatarAnimator.GetBoneTransform(HumanBodyBones.Head);
        if (leftFoot && head)
        {
            avatarHeight = Vector3.Distance(leftFoot.position, head.position);
        }

        // Measure shoulder width
        Transform leftShoulder = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder);
        Transform rightShoulder = avatarAnimator.GetBoneTransform(HumanBodyBones.RightShoulder);
        if (leftShoulder && rightShoulder)
        {
            avatarShoulderWidth = Vector3.Distance(leftShoulder.position, rightShoulder.position);
        }

        // Measure arm length (shoulder to wrist)
        Transform leftHand = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (leftShoulder && leftHand)
        {
            avatarArmSpan = Vector3.Distance(leftShoulder.position, leftHand.position);
        }

        Debug.Log($"Avatar - Height: {avatarHeight:F2}, Shoulder: {avatarShoulderWidth:F2}, Arm: {avatarArmSpan:F2}");
    }

    private IEnumerator WaitForTPoseCalibration()
    {
        isCalibrating = true;
        tPoseTimer = 0f;

        Debug.Log("Please assume a T-pose and hold for " + tPoseHoldTime + " seconds!");

        while (tPoseTimer < tPoseHoldTime)
        {
            if (IsInTPose())
            {
                tPoseTimer += Time.deltaTime;
                Debug.Log($"Holding T-pose... {tPoseTimer:F1}/{tPoseHoldTime:F1}");
            }
            else
            {
                tPoseTimer = 0f;
            }

            yield return null;
        }

        PerformTPoseCalibration();
        isCalibrating = false;
    }

    private bool IsInTPose()
    {
        if (headset == null || leftController == null || rightController == null) return false;

        Vector3 headPos = headset.position;
        Vector3 leftPos = leftController.position;
        Vector3 rightPos = rightController.position;

        // Check if controllers are horizontal
        float yDiff = Mathf.Abs(leftPos.y - rightPos.y);
        if (yDiff > 0.15f) return false; // Hands are at different heights, not a T-pose

        // Check if arms are roughly 90 degrees from the body's forward direction
        Vector3 headForward = Vector3.ProjectOnPlane(headset.forward, Vector3.up);
        Vector3 armDir = (rightPos - leftPos).normalized;
        
        float angle = Vector3.Angle(headForward, armDir);

        return Mathf.Abs(angle - 90) < tPoseToleranceAngle;
    }

    private void PerformTPoseCalibration()
    {
        Debug.Log("Performing T-Pose Calibration...");

        // Measure user's dimensions
        userHeight = headset.position.y;

        Vector3 leftControllerPos = leftController.position;
        Vector3 rightControllerPos = rightController.position;
        
        // Estimate shoulder width (approx. 80% of controller distance)
        userShoulderWidth = Vector3.Distance(leftControllerPos, rightControllerPos) * 0.8f;

        // Measure arm length (shoulder to wrist)
        Vector3 shoulderEst = headset.position + (Vector3.down * userHeight * 0.13f);
        Vector3 shoulderLeft = shoulderEst + (leftControllerPos - shoulderEst).normalized * (userShoulderWidth / 2f);
        userArmSpan = Vector3.Distance(shoulderLeft, leftControllerPos);

        // Shoulder offset from head center
        userShoulderOffset = Vector3.down * (userHeight * 0.13f); // Approx. 13% of height down from head

        CalculateRatios();
        ApplyCalibration();

        isCalibrated = true;
        Debug.Log("Calibration Complete!");
    }

    private void PerformQuickCalibration()
    {
        // Quick calibration without T-pose
        userHeight = headset.position.y;

        Vector3 headToLeft = leftController.position - headset.position;
        Vector3 headToRight = rightController.position - headset.position;

        userArmSpan = (headToLeft.magnitude + headToRight.magnitude) / 2f * 0.85f;
        userShoulderWidth = Vector3.Distance(leftController.position, rightController.position) * 0.7f;
        userShoulderOffset = Vector3.down * (userHeight * 0.13f);

        CalculateRatios();
        ApplyCalibration();

        isCalibrated = true;
        Debug.Log("Quick Calibration Complete!");
    }

    private void CalculateRatios()
    {
        if (avatarHeight > 0) heightRatio = userHeight / avatarHeight;
        if (avatarArmSpan > 0) armLengthRatio = userArmSpan / avatarArmSpan;
        if (avatarShoulderWidth > 0) shoulderWidthRatio = userShoulderWidth / avatarShoulderWidth;

        // Clamp ratios to prevent extreme values
        heightRatio = Mathf.Clamp(heightRatio, 0.5f, 2.0f);
        armLengthRatio = Mathf.Clamp(armLengthRatio, 0.5f, 2.0f);
        shoulderWidthRatio = Mathf.Clamp(shoulderWidthRatio, 0.5f, 2.0f);

        Debug.Log($"Ratios - Height: {heightRatio:F2}, Arm: {armLengthRatio:F2}, Shoulder: {shoulderWidthRatio:F2}");
    }

    private void ApplyCalibration()
    {
        // Set IK weights
        if (leftArmIK != null) leftArmIK.weight = ikWeight;
        if (rightArmIK != null) rightArmIK.weight = ikWeight;
        if (headAimIK != null) headAimIK.weight = ikWeight;

        // Rebuild the rig
        if (rigBuilder != null)
        {
            rigBuilder.Build();
        }
    }

    private void UpdateVRTracking()
    {
        // Head tracking
        UpdateHeadTracking();

        // Hand tracking
        UpdateHandTracking(leftController, leftHandTarget, true);
        UpdateHandTracking(rightController, rightHandTarget, false);

        // Update elbow hints
        if (useElbowHints)
        {
            UpdateElbowHint(leftController, leftElbowHint, true);
            UpdateElbowHint(rightController, rightElbowHint, false);
        }
    }

    private void UpdateHeadTracking()
    {
        if (headAimIK != null && headTarget != null)
        {
            headTarget.position = headset.position;
            headTarget.rotation = headset.rotation;
        }
    }

    private void UpdateHandTracking(Transform controller, Transform ikTarget, bool isLeft)
    {
        if (controller == null || ikTarget == null) return;

        Vector3 headPos = headset.position;

        // Calculate a stable body direction, independent of head tilt
        Vector3 bodyForward = Vector3.ProjectOnPlane(headset.forward, Vector3.up).normalized;
        if (bodyForward.sqrMagnitude < 0.001f)
        {
            bodyForward = Vector3.forward; // Fallback for when user looks straight up or down
        }
        Vector3 bodyRight = Vector3.Cross(Vector3.up, bodyForward);

        // Calculate shoulder position
        float shoulderOffsetX = isLeft ? -userShoulderWidth / 2f : userShoulderWidth / 2f;
        Vector3 shoulderPos = headPos + userShoulderOffset + bodyRight * shoulderOffsetX;

        // Apply arm length limit
        Vector3 shoulderToController = controller.position - shoulderPos;
        float maxReach = userArmSpan * armLengthRatio;

        if (shoulderToController.magnitude > maxReach)
        {
            shoulderToController = shoulderToController.normalized * maxReach;
        }

        // Calculate final IK target position and rotation
        Vector3 finalPosition = shoulderPos + shoulderToController;
        Quaternion finalRotation = controller.rotation;

        // Apply smoothing if an optimizer is present
        if (optimizer != null)
        {
            optimizer.ApplySmoothingToTarget(ikTarget, finalPosition, finalRotation);
        }
        else
        {
            // Set position/rotation directly if no optimizer
            ikTarget.position = finalPosition;
            ikTarget.rotation = finalRotation;
        }
    }

    private void UpdateElbowHint(Transform controller, Transform elbowHint, bool isLeft)
    {
        if (controller == null || elbowHint == null) return;

        // Position the elbow hint behind and slightly below the controller
        Vector3 elbowDirection = (isLeft ? Vector3.left : Vector3.right) * 0.1f;
        elbowDirection += Vector3.down * 0.2f; 
        elbowDirection += Vector3.back * elbowBendAmount;

        elbowHint.position = controller.position + controller.TransformDirection(elbowDirection);
    }

    private void ResetCalibration()
    {
        isCalibrated = false;
        isCalibrating = false;
        tPoseTimer = 0f;

        armLengthRatio = 1f;
        shoulderWidthRatio = 1f;
        heightRatio = 1f;

        if (leftArmIK != null) leftArmIK.weight = 0f;
        if (rightArmIK != null) rightArmIK.weight = 0f;
        if (headAimIK != null) headAimIK.weight = 0f;

        Debug.Log("Calibration has been reset.");
    }

    void OnGUI()
    {
        if (!isCalibrated && !isCalibrating)
        {
            GUI.Label(new Rect(10, 10, 300, 30), "Press 'T' for T-Pose Calibration");
        }

        if (isCalibrating)
        {
            GUI.Label(new Rect(10, 10, 300, 30), $"Hold T-Pose: {tPoseTimer:F1}/{tPoseHoldTime:F1}");

            if (IsInTPose())
            {
                GUI.color = Color.green;
                GUI.Label(new Rect(10, 40, 200, 30), "Good T-Pose!");
            }
            else
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(10, 40, 200, 30), "T-Pose Required");
            }
            GUI.color = Color.white;
        }

        if (isCalibrated)
        {
            GUI.Label(new Rect(10, 10, 200, 30), "Calibrated! Press 'R' to reset");
        }
    }
}
