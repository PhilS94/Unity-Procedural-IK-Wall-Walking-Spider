/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Raycasting;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum TargetMode {
    HandledByIKStepper,
    DebugTarget,
    DebugTargetRay
}

/*
 * This class represents a chain of hinge joints together with an end effector.
 * It is supplied with a target, for which the chain will give a call to the IKSolvers CCD algorithm every LateUpdate to be solved.
 * The target can be set externally by the setTarget() function only if the ExternallyHandled target mode is selected.
 * In the other two debug modes this class handles the target on its own by the set Transform target.
 */
public class IKChain : MonoBehaviour {

    [Header("Debug")]
    public bool printDebugLogs;
    public bool deactivateSolving = false;

    public enum IKSolveMethod { CCD, CCDFrameByFrame };

    [Header("Solving")]
    public IKSolveMethod ikSolveMethod;
    [Range(0.1f, 10f)]
    public float tolerance;
    [Range(0.01f, 1f)]
    public float minChangePerIteration;
    [Range(1f, 100f)]
    public float singularityRadius;

    [Header("Chain")]
    public JointHinge[] joints;
    public Transform endEffector;
    public bool adjustLastJointToNormal;
    [Range(0, 90)]
    public float footAngle;

    [Header("Target Mode")]
    public TargetMode targetMode;
    public LayerMask debugTargetRayLayer;

    [Tooltip("Assign debug target if a debug target mode is selected.")]
    public Transform debugTarget;

    private float chainLength;

    private TargetInfo currentTarget;
    private float error = 0.0f;
    private bool pause = false;

    private RayCast debugModeRay;

    private Vector3 endEffectorVelocity;
    private Vector3 lastEndeffectorPos;

    public void Awake() {
        Debug.Log("Called Awake " + name + " on IKChain");
        if (chainLength == 0) initializeChainLength();

        if (targetMode == TargetMode.DebugTarget || targetMode == TargetMode.DebugTargetRay) {
            if (debugTarget == null) {
                Debug.LogError("Assign a target when using this mode.");
            }
        }

        if (targetMode == TargetMode.DebugTargetRay && debugTarget != null) debugModeRay = new RayCast(debugTarget.position + 1.0f * Vector3.up, debugTarget.position - 1.0f * Vector3.up, debugTarget, debugTarget);
        lastEndeffectorPos = endEffector.position;
    }

    private void Start() {
        if (targetMode == TargetMode.DebugTarget) currentTarget = getDebugTarget();
        else if (targetMode == TargetMode.DebugTargetRay) currentTarget = getDebugTargetRay();
    }

    /* Update calls*/

    void Update() {
        if (deactivateSolving) return;

        if (targetMode == TargetMode.DebugTarget) currentTarget = getDebugTarget();
    }

    private void FixedUpdate() {

        if (deactivateSolving) return;

        if (targetMode == TargetMode.DebugTargetRay) currentTarget = getDebugTargetRay();
    }

    /* Late Update calls the solve function which will solve this IK chain */
    private void LateUpdate() {
        if (deactivateSolving) return;

        endEffectorVelocity = (endEffector.position - lastEndeffectorPos) / Time.deltaTime;

        // We only want to solve if we moved away too much since our last solve.
        if (!hasMovementOccuredSinceLastSolve()) return;
        // In theory everything below will only be called if a fixedupdate took place prior to this update since that is the only way the spider moves.
        // However, the spider changes bodytorso every frame through breathing, thus solving takes places every frame.
        if (!pause) solve();

        lastEndeffectorPos = endEffector.position;
    }

    /* This function performs a call to the IKSolvers CCD algorithm, which then solves this chain to the current target. */
    private void solve() {

        if (ikSolveMethod == IKSolveMethod.CCD) {
            IKSolver.solveChainCCD(ref joints, endEffector, currentTarget, getTolerance(), getMinimumChangePerIterationOfSolving(), getSingularityRadius(), adjustLastJointToNormal, footAngle, printDebugLogs);
        }
        else if (ikSolveMethod == IKSolveMethod.CCDFrameByFrame) {
            StartCoroutine(IKSolver.solveChainCCDFrameByFrame(joints, endEffector, currentTarget, getTolerance(), getMinimumChangePerIterationOfSolving(), getSingularityRadius(), adjustLastJointToNormal, footAngle, printDebugLogs));
            deactivateSolving = true;
            //Important here is that the coroutine has to update the error after it is done. Not implemented here yet!
        }
        error = Vector3.Distance(endEffector.position, currentTarget.position);
    }

    public float getChainLength() {
        if (chainLength == 0) initializeChainLength(); // chainLength can be unintialized if called from Awake of e.g. the IKStepper
        return chainLength;
    }

    /* Calculates the length of the IK chain. */
    private void initializeChainLength() {
        chainLength = 0;
        for (int i = 0; i < joints.Length; i++) {
            Vector3 p = joints[i].getRotationPoint();
            Vector3 q = (i != joints.Length - 1) ? joints[i + 1].getRotationPoint() : endEffector.position;
            chainLength += Vector3.Distance(p, q);
        }
    }

    /* Target functions */
    private TargetInfo getDebugTarget() {
        return new TargetInfo(debugTarget.position, debugTarget.up);
    }

    private TargetInfo getDebugTargetRay() {
        debugModeRay.draw(Color.yellow);
        if (debugModeRay.castRay(out RaycastHit hitInfo, debugTargetRayLayer)) {
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }
        else {
            return new TargetInfo(debugTarget.position, debugTarget.up);
        }
    }

    /* Use this setter to externally set the target for the CCD algorithm.
     * The CCD runs with every late frame update and uses this target.
     * Dont allow external target manipulation if the debug modes are used.
     */
    public void setTarget(TargetInfo target) {
        if (targetMode != TargetMode.HandledByIKStepper) {
            Debug.LogWarning("Not allowed to change target of IKChain " + gameObject.name + " since a debug mode is selected.");
            return;
        }
        currentTarget = target;
    }

    // Getters for important references
    public JointHinge getRootJoint() {
        return joints[0];
    }

    public TargetInfo getTarget() {
        return currentTarget;
    }

    // Getters and Setters for important states
    public bool isTargetHandledByIKStepper() {
        return targetMode == TargetMode.HandledByIKStepper;
    }
    public void pauseSolving() {
        pause = true;
    }
    public void unpauseSolving() {
        pause = false;
    }

    // Compare the current distance and the last registered error.
    // If the distance changed, either the target or the endeffector moved (e.g. the spider moved), thus we need to solve again.
    private bool hasMovementOccuredSinceLastSolve() {
        return (Mathf.Abs(Vector3.Distance(endEffector.position, currentTarget.position) - error) > float.Epsilon);
    }

    // Getters for important values
    public float getError() {
        return error;
    }
    public float getTolerance() {
        return transform.lossyScale.y * 0.00001f * tolerance;
    }

    public float getMinimumChangePerIterationOfSolving() {
        return transform.lossyScale.y * 0.00001f * minChangePerIteration;
    }

    public float getSingularityRadius() {
        return transform.lossyScale.y * 0.00001f * singularityRadius;
    }

    public Vector3 getEndeffectorVelocityPerSecond() {
        return endEffectorVelocity;
    }
}


#if UNITY_EDITOR
[CustomEditor(typeof(IKChain))]
public class IKChainEditor : Editor {
 
    private IKChain ikchain;

    private static bool showDebug = true;

    private static bool showChain = true;
    private static bool showSolveTolerance = true;
    private static bool showMinimumSolveChange = true;
    private static bool showSingularityRadius = true;

    public void OnEnable() {
        ikchain = (IKChain)target;
        if (showDebug && !EditorApplication.isPlaying) ikchain.Awake();
    }

    public void findJointsInChildren() {
        List<JointHinge> temp = new List<JointHinge>();
        foreach (var joint in ikchain.GetComponentsInChildren<JointHinge>()) { temp.Add(joint); }
        ikchain.joints = temp.ToArray();

        Transform[] transforms = ikchain.GetComponentsInChildren<Transform>();
        ikchain.endEffector = transforms[transforms.Length - 1];
    }

    public void addJoint() {
        int n = ikchain.joints.Length;
        JointHinge[] temp = new JointHinge[n + 1];
        for (int i = 0; i < n; i++) { temp[i] = ikchain.joints[i]; }
        ikchain.joints = temp;
    }

    public void removeJoint() {
        int n = ikchain.joints.Length;
        if (n == 0) return;
        JointHinge[] temp = new JointHinge[n - 1];
        for (int i = 0; i < n - 1; i++) { temp[i] = ikchain.joints[i]; }
        ikchain.joints = temp;
    }

    public override void OnInspectorGUI() {
        if (ikchain == null) return;

        serializedObject.Update();

        EditorDrawing.DrawMonoScript(ikchain, typeof(IKChain));

        EditorDrawing.DrawHorizontalLine();

        //Debug
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        showDebug = EditorGUILayout.Toggle("Show Debug Drawings", showDebug);
        if (showDebug) {
            EditorGUI.indentLevel++;
            {
                showChain = EditorGUILayout.Toggle("Draw IK Chain", showChain);
                showSolveTolerance = EditorGUILayout.Toggle("Draw IK Solve Tolerance", showSolveTolerance);
                showMinimumSolveChange = EditorGUILayout.Toggle("Draw Minimum Solve Change Breakcondition", showMinimumSolveChange);
                showSingularityRadius = EditorGUILayout.Toggle("Draw Singularity Radius", showSingularityRadius);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space();
        serializedObject.FindProperty("printDebugLogs").boolValue = EditorGUILayout.Toggle("Print Debug Logs", ikchain.printDebugLogs);

        EditorDrawing.DrawHorizontalLine();

        // The IK Chain
        EditorGUILayout.LabelField("The IK Chain", EditorStyles.boldLabel);

        // The Array Joints
        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.LabelField("Joints");
            EditorGUILayout.LabelField("Weights");
        }
        EditorGUILayout.EndHorizontal();
        EditorGUI.indentLevel++;
        {
            SerializedProperty jointsProperty = serializedObject.FindProperty("joints");
            for (int i = 0; i < jointsProperty.arraySize; i++) {
                SerializedProperty singleJointProperty = jointsProperty.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginHorizontal();
                {
                    singleJointProperty.objectReferenceValue = (JointHinge)EditorGUILayout.ObjectField(ikchain.joints[i], typeof(JointHinge), true);
                    GUI.enabled = false;
                    if (ikchain.joints[i] != null) EditorGUILayout.FloatField(ikchain.joints[i].weight);
                    else EditorGUILayout.TextField("-");
                    GUI.enabled = true;
                }
                EditorGUILayout.EndHorizontal();
            }

            // Endeffector
            serializedObject.FindProperty("endEffector").objectReferenceValue = (Transform)EditorGUILayout.ObjectField("End Effector", ikchain.endEffector, typeof(Transform), true);

            // Buttons for Joint array
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("");
                if (EditorDrawing.DrawButton("Add Joint")) addJoint();
                if (EditorDrawing.DrawButton("Remove Joint")) removeJoint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("");
                if (EditorDrawing.DrawButton("Find Automatically")) findJointsInChildren();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        // The Solving Algorithm
        EditorGUILayout.LabelField("IK Solving Algorithm", EditorStyles.boldLabel);

        serializedObject.FindProperty("deactivateSolving").boolValue = EditorGUILayout.Toggle("Deactivate Solving?", ikchain.deactivateSolving);

        if (!ikchain.deactivateSolving) {
            EditorGUI.indentLevel++;
            {
                serializedObject.FindProperty("ikSolveMethod").enumValueIndex = (int)(IKChain.IKSolveMethod)EditorGUILayout.EnumPopup("Solving Algorithm", ikchain.ikSolveMethod);
                EditorGUI.indentLevel++;
                {
                    serializedObject.FindProperty("tolerance").floatValue = EditorGUILayout.Slider("Solve Tolerance", ikchain.tolerance, 0.1f, 10f);
                    serializedObject.FindProperty("minChangePerIteration").floatValue = EditorGUILayout.Slider("Minimum Change Per Iteration Break Condition", ikchain.minChangePerIteration, 0.01f, 1f);
                    serializedObject.FindProperty("singularityRadius").floatValue = EditorGUILayout.Slider("Singularity Radius of Joints", ikchain.singularityRadius, 1f, 100f);
                    serializedObject.FindProperty("adjustLastJointToNormal").boolValue = EditorGUILayout.Toggle("Adjust Last Segment to Ground (Foot) ?", ikchain.adjustLastJointToNormal);
                    if (ikchain.adjustLastJointToNormal) {
                        EditorGUI.indentLevel++;
                        {
                            serializedObject.FindProperty("footAngle").floatValue = EditorGUILayout.Slider("Angle of Foot", ikchain.footAngle, 0, 90);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space();

        // The Target to Solve for
        EditorGUILayout.LabelField("Target To Solve For", EditorStyles.boldLabel);
        serializedObject.FindProperty("targetMode").enumValueIndex = (int)(TargetMode)EditorGUILayout.EnumPopup("Select the Target Mode", ikchain.targetMode);
        EditorGUI.indentLevel++;
        {
            if (ikchain.targetMode == TargetMode.DebugTarget || ikchain.targetMode == TargetMode.DebugTargetRay) {
                serializedObject.FindProperty("debugTarget").objectReferenceValue = (Transform)EditorGUILayout.ObjectField("Debug Target", ikchain.debugTarget, typeof(Transform), true);
            }

            if (ikchain.targetMode == TargetMode.DebugTargetRay) {
                serializedObject.FindProperty("debugTargetRayLayer").intValue = EditorGUILayout.LayerField("Layer of Ray", ikchain.debugTargetRayLayer);
            }
        }
        EditorGUI.indentLevel--;

        // Apply Changes
        serializedObject.ApplyModifiedProperties();

        if (showDebug && !EditorApplication.isPlaying) ikchain.Awake();
    }

    void OnSceneGUI() {
        if (!showDebug || ikchain == null) return;

        //Draw the Chain
        if (showChain) DrawChain(ref ikchain, Color.green);

        //Draw the solve tolerance as a sphere
        if (showSolveTolerance) DrawSolveTolerance(ref ikchain, Color.yellow);

        //Draw the minimum change break condition of endeffector as a sphere
        if (showMinimumSolveChange) DrawMinimumSolveChange(ref ikchain, Color.blue);

        //Draw the singularity radius for each joint
        if (showSingularityRadius) DrawSingularityRadius(ref ikchain, Color.red);
    }

    public void DrawChain(ref IKChain ikchain, Color col) {
        Handles.color = col;
        for (int k = 0; k < ikchain.joints.Length - 1; k++) {
            JointHinge a = ikchain.joints[k];
            JointHinge b = ikchain.joints[k + 1];

            if (a == null || b == null) continue;
            Handles.DrawLine(a.getRotationPoint(), b.getRotationPoint());
        }
        JointHinge c = ikchain.joints[ikchain.joints.Length - 1];
        if (c != null) Handles.DrawLine(c.getRotationPoint(), ikchain.endEffector.position);
    }

    public void DrawSolveTolerance(ref IKChain ikchain, Color col) {
        Handles.color = col;
        Handles.RadiusHandle(ikchain.endEffector.rotation, ikchain.endEffector.position, ikchain.getTolerance(), false);
        EditorDrawing.DrawText(ikchain.endEffector.position, "Tolerance", col);
    }

    public void DrawMinimumSolveChange(ref IKChain ikchain, Color col) {
        Handles.color = col;
        Vector3 position = ikchain.endEffector.position + (ikchain.getTolerance() + ikchain.getMinimumChangePerIterationOfSolving()) * ikchain.transform.up;
        Handles.RadiusHandle(ikchain.endEffector.rotation, position, ikchain.getMinimumChangePerIterationOfSolving(), false);
        EditorDrawing.DrawText(position, "Minimum Change", col);
    }

    public void DrawSingularityRadius(ref IKChain ikchain, Color col) {
        Handles.color = col;
        for (int k = 0; k < ikchain.joints.Length; k++) {
            JointHinge joint = ikchain.joints[k];
            if (joint == null) continue;

            Handles.RadiusHandle(joint.transform.rotation, joint.getRotationPoint(), ikchain.getSingularityRadius(), false);
            EditorDrawing.DrawText(joint.transform.position, "Singularity\n" + joint.name, col);
        }
    }
}
#endif