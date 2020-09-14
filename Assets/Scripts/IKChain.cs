/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Raycasting;

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

    // Assign these if corresponding mode is selected
    public Transform debugTarget;

    private TargetInfo currentTarget;
    private float error = 0.0f;
    private bool pause = false;

    private RayCast debugModeRay;

    private Vector3 endEffectorVelocity;
    private Vector3 lastEndeffectorPos;

    private void Awake() {
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

        if (ikSolveMethod==IKSolveMethod.CCD) {
            IKSolver.solveChainCCD(ref joints, endEffector, currentTarget, getTolerance(), getMinimumChangePerIterationOfSolving(), getSingularityRadius(), adjustLastJointToNormal, printDebugLogs);
        }
        else if (ikSolveMethod == IKSolveMethod.CCDFrameByFrame) {
            StartCoroutine(IKSolver.solveChainCCDFrameByFrame(joints, endEffector, currentTarget, getTolerance(), getMinimumChangePerIterationOfSolving(), getSingularityRadius(), adjustLastJointToNormal, printDebugLogs));
            deactivateSolving = true;
            //Important here is that the coroutine has to update the error after it is done. Not implemented here yet!
        }
        error = Vector3.Distance(endEffector.position, currentTarget.position);
    }
  
    /* Calculates the length of the IK chain. */
    // Gets called from IKStepper and not from this class .
    // Should capsule this in this class though. However IKStepper needs the chain length at Awake...
    public float calculateChainLength() {
        float chainLength = 0;
        for (int i = 0; i < joints.Length; i++) {
            Vector3 p = joints[i].getRotationPoint();
            Vector3 q = (i != joints.Length - 1) ? joints[i + 1].getRotationPoint() : endEffector.position;
            chainLength += Vector3.Distance(p, q);
        }
        return chainLength;
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

    public Transform getEndEffector() {
        return endEffector;
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


#if UNITY_EDITOR
    void OnDrawGizmosSelected() {

        if (UnityEditor.EditorApplication.isPlaying) return;
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;

        Awake();

        if (debugTarget == null && (targetMode == TargetMode.DebugTarget || targetMode == TargetMode.DebugTargetRay)) return;

        //Draw the Chain
        for (int k = 0; k < joints.Length - 1; k++) {
            Debug.DrawLine(joints[k].getRotationPoint(), joints[k + 1].getRotationPoint(), Color.green);
        }
        Debug.DrawLine(joints[joints.Length - 1].getRotationPoint(), endEffector.position, Color.green);

        //Draw the tolerance as a sphere
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(endEffector.position, getTolerance());

        //Draw the minChange as a sphere slighly next to endeffector
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(endEffector.position + (getTolerance() + getMinimumChangePerIterationOfSolving()) * transform.up, getMinimumChangePerIterationOfSolving());

        //Draw the singularity radius
        Gizmos.color = Color.red;
        for (int k = 0; k < joints.Length; k++) {
            Gizmos.DrawWireSphere(joints[k].getRotationPoint(), getSingularityRadius());
        }
    }
#endif
}