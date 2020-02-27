using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Raycasting;

public enum TargetMode {
    IKStepper,
    DebugTarget,
    DebugTargetRay
}

public class IKChain : MonoBehaviour {

    [Header("Debug")]
    public bool printDebugLogs;
    public bool solveFrameByFrame;
    public bool deactivateSolving = false;

    [Header("Chain")]
    public AHingeJoint[] joints;
    public Transform endEffector;
    public bool adjustLastJointToNormal;

    [Header("Target Mode")]
    public TargetMode targetMode;
    public LayerMask debugTargetRayLayer;

    // Assign these if corresponding mode is selected
    public Transform debugTarget;
    private IKStepper ikStepper;

    private float chainLength;
    private TargetInfo currentTarget;
    private float error = 0.0f;
    private bool validChain;
    private bool pause = false;

    private RayCast debugModeRay;

    private void Awake() {
        ikStepper = GetComponent<IKStepper>();
        chainLength = calculateChainLength();
        validChain = isValidChain();
    }

    private void Start() {
        setTarget(new TargetInfo(getEndEffector().position, Vector3.up));
        debugModeRay = new RayCast(debugTarget.position + 1.0f * Vector3.up, debugTarget.position - 1.0f * Vector3.up, debugTarget, debugTarget);
    }

    float calculateChainLength() {
        chainLength = 0;

        for (int i = 0; i < joints.Length - 1; i++) {
            chainLength += Vector3.Distance(joints[i].getRotationPoint(), joints[i + 1].getRotationPoint());
        }
        //Debug.Log("Chain length for the chain " + gameObject.name + ": " + chainLength);
        return chainLength;
    }

    bool isValidChain() {
        if ((debugTarget == null) && ((targetMode == TargetMode.DebugTarget) || (targetMode == TargetMode.DebugTargetRay))) {
            Debug.LogError("Please assign a Target Transform when using this mode.");
            return false;
        }

        if ((ikStepper == null) && (targetMode == TargetMode.IKStepper)) {
            Debug.LogError("Please assign a IKTargetPredictor Component when using this mode.");
            return false;
        }
        return true;
    }

    void Update() {
        if (deactivateSolving || !validChain) return;

        if (targetMode == TargetMode.DebugTarget) {
            setTarget(new TargetInfo(debugTarget.position, debugTarget.up));
        }
    }

    private void FixedUpdate() {
        if (deactivateSolving || !validChain) return;

        if (targetMode == TargetMode.DebugTargetRay) {
            debugModeRay.draw(Color.yellow);
            if (debugModeRay.castRay(out RaycastHit hitInfo, debugTargetRayLayer)) {
                setTarget(new TargetInfo(hitInfo.point, hitInfo.normal));
            }
            else {
                setTarget(new TargetInfo(debugTarget.position, debugTarget.up));
            }
        }
    }

    private void LateUpdate() {
        if (deactivateSolving || !validChain) return;

        // We only want to solve if we moved away too much since our last solve.
        if (!hasMovementOccuredSinceLastSolve()) return;
        // In theory everything below will only be called if a fixedupdate took place prior to this update since that is the only way the spider moves.
        if (!pause) solve();

        //We only check if stepping is needed (and step if necessary) after a solve took place.
        if (IKStepperActivated()) ikStepper.stepCheck();
    }

    public void solve() {

        if (solveFrameByFrame) {
            StartCoroutine(IKSolver.solveChainCCDFrameByFrame(joints, endEffector, currentTarget, getTolerance(), getMinimumChangePerIterationOfSolving(), adjustLastJointToNormal, printDebugLogs));
            deactivateSolving = true;
            //Important here is that the coroutine has to update the error after it is done. Not implemented yet here
        }
        else {
            IKSolver.solveChainCCD(ref joints, endEffector, currentTarget, getTolerance(), getMinimumChangePerIterationOfSolving(), adjustLastJointToNormal, printDebugLogs);
            error = Vector3.Distance(endEffector.position, currentTarget.position);
        }
    }

    // Compare the current distance and the last registered error.
    // If the distance changed, either the target or the endeffector moved (e.g. the spider moved), thus we need to solve again.
    public bool hasMovementOccuredSinceLastSolve() {
        return (Mathf.Abs(Vector3.Distance(endEffector.position, currentTarget.position) - error) > float.Epsilon);
    }
    public float getTolerance() {
        return transform.lossyScale.y * 0.0005f;
    }

    public float getMinimumChangePerIterationOfSolving() {
        return transform.lossyScale.y * 0.00001f;
    }

    public float getChainLength() {
        return chainLength;
    }

    public AHingeJoint getRootJoint() {
        return joints[0];
    }

    public Transform getEndEffector() {
        return endEffector;
    }

    public TargetInfo getTarget() {
        return currentTarget;
    }

    // Use this setter to set the target for the CCD algorithm. The CCD runs with every frame update and uses this target.
    public void setTarget(TargetInfo target) {
        currentTarget = target;
    }

    public bool IKStepperActivated() {
        return (targetMode == TargetMode.IKStepper && !deactivateSolving && validChain);
    }

    public float getError() {
        return error;
    }

    public void pauseSolving() {
        pause = true;
    }

    public void unpauseSolving() {
        pause = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected() {

        if (UnityEditor.EditorApplication.isPlaying) return;
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;

        // Set ChainLength and ValidChain here
        Awake();

        if (!validChain) return;

        //Draw the Chain
        for (int k = 0; k < joints.Length - 1; k++) {
            Debug.DrawLine(joints[k].getRotationPoint(), joints[k + 1].getRotationPoint(), Color.green);
        }
        Debug.DrawLine(joints[joints.Length - 1].getRotationPoint(), endEffector.position, Color.green);
    }

#endif
}