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
    ExternallyHandled,
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
        if (targetMode == TargetMode.DebugTarget) debugModeRay = new RayCast(debugTarget.position + 1.0f * Vector3.up, debugTarget.position - 1.0f * Vector3.up, debugTarget, debugTarget);
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
            IKSolver.solveChainCCD(ref joints, endEffector, currentTarget, getTolerance(), getMinimumChangePerIterationOfSolving(), getSingularityRadius(), adjustLastJointToNormal, printDebugLogs);
        }
        else if (ikSolveMethod == IKSolveMethod.CCDFrameByFrame) {
            StartCoroutine(IKSolver.solveChainCCDFrameByFrame(joints, endEffector, currentTarget, getTolerance(), getMinimumChangePerIterationOfSolving(), getSingularityRadius(), adjustLastJointToNormal, printDebugLogs));
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
        if (targetMode != TargetMode.ExternallyHandled) {
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
    public bool isTargetExternallyHandled() {
        return targetMode == TargetMode.ExternallyHandled;
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
        if (showDebug) ikchain.Awake();
    }

    public override void OnInspectorGUI() {
        if (ikchain == null) return;

        Undo.RecordObject(ikchain, "Changes to IKChain");

        EditorDrawing.DrawHorizontalLine(Color.gray);
        EditorGUILayout.LabelField("Debug Drawing", EditorStyles.boldLabel);
        showDebug = EditorGUILayout.Toggle("Show Debug Drawings", showDebug);
        if (showDebug) {
            EditorGUI.indentLevel++;
            showChain = EditorGUILayout.Toggle("Draw IK Chain", showChain);
            showSolveTolerance = EditorGUILayout.Toggle("Draw IK Solve Tolerance", showSolveTolerance);
            showMinimumSolveChange = EditorGUILayout.Toggle("Draw Minimum Solve Change Breakcondition", showMinimumSolveChange);
            showSingularityRadius = EditorGUILayout.Toggle("Draw Singularity Radius", showSingularityRadius);
            EditorGUI.indentLevel--;
        }
        EditorDrawing.DrawHorizontalLine(Color.gray);

        base.OnInspectorGUI();
        if (showDebug) ikchain.Awake();
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
            Handles.DrawLine(ikchain.joints[k].getRotationPoint(), ikchain.joints[k + 1].getRotationPoint());
        }
        Handles.DrawLine(ikchain.joints[ikchain.joints.Length - 1].getRotationPoint(), ikchain.endEffector.position);
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
            Handles.RadiusHandle(joint.transform.rotation, joint.getRotationPoint(), ikchain.getSingularityRadius(), false);
            EditorDrawing.DrawText(joint.transform.position, "Singularity\n" + joint.name, col);
        }
    }
}
#endif