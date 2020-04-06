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

    [Header("Solving")]
    [Range(0.1f, 10f)]
    public float tolerance;
    [Range(0.01f, 1f)]
    public float minChangePerIteration;
    [Range(1f, 100f)]
    public float singularityRadius;

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

    private TargetInfo currentTarget;
    private float error = 0.0f;
    private bool validChain;
    private bool pause = false;

    private RayCast debugModeRay;

    private Vector3 endEffectorVelocity;
    private Vector3 lastEndeffectorPos;

    private void Awake() {
        ikStepper = GetComponent<IKStepper>();
        validChain = isValidChain();
        if (targetMode == TargetMode.DebugTarget) debugModeRay = new RayCast(debugTarget.position + 1.0f * Vector3.up, debugTarget.position - 1.0f * Vector3.up, debugTarget, debugTarget);
        lastEndeffectorPos = endEffector.position;
    }

    private void Start() {
        if (!validChain) return;

        if (targetMode == TargetMode.DebugTarget) currentTarget = getDebugTarget();
        else if (targetMode == TargetMode.DebugTargetRay) currentTarget = getDebugTargetRay();
        else if (targetMode == TargetMode.IKStepper) currentTarget = ikStepper.getDefaultTarget();
    }

    bool isValidChain() {
        if ((debugTarget == null) && ((targetMode == TargetMode.DebugTarget) || (targetMode == TargetMode.DebugTargetRay))) {
            Debug.LogError("Please assign a Target Transform when using this mode.");
            return false;
        }

        if ((ikStepper == null) && (targetMode == TargetMode.IKStepper)) {
            Debug.LogError("Please assign a IKStepper Component when using this mode.");
            return false;
        }
        return true;
    }

    void Update() {
        if (deactivateSolving || !validChain) return;

        if (targetMode == TargetMode.DebugTarget) currentTarget = getDebugTarget();
    }

    private void FixedUpdate() {

        if (deactivateSolving || !validChain) return;

        if (targetMode == TargetMode.DebugTargetRay) currentTarget = getDebugTargetRay();
    }

    private void LateUpdate() {
        if (deactivateSolving || !validChain) return;

        endEffectorVelocity = (endEffector.position - lastEndeffectorPos) / Time.deltaTime;

        // We only want to solve if we moved away too much since our last solve.
        if (!hasMovementOccuredSinceLastSolve()) return;
        // In theory everything below will only be called if a fixedupdate took place prior to this update since that is the only way the spider moves.
        // However, the spider changes bodytorso every frame through breathing, thus solving takes places every frame.
        if (!pause) solve();

        lastEndeffectorPos = endEffector.position;
    }

    private void solve() {

        if (solveFrameByFrame) {
            StartCoroutine(IKSolver.solveChainCCDFrameByFrame(joints, endEffector, currentTarget, getTolerance(), getMinimumChangePerIterationOfSolving(), getSingularityRadius(), adjustLastJointToNormal, printDebugLogs));
            deactivateSolving = true;
            //Important here is that the coroutine has to update the error after it is done. Not implemented yet here
        }
        else {
            IKSolver.solveChainCCD(ref joints, endEffector, currentTarget, getTolerance(), getMinimumChangePerIterationOfSolving(), getSingularityRadius(), adjustLastJointToNormal, printDebugLogs);
            error = Vector3.Distance(endEffector.position, currentTarget.position);
        }
    }

    public float calculateChainLength() {
        float chainLength = 0;
        for (int i = 0; i < joints.Length - 1; i++) {
            chainLength += Vector3.Distance(joints[i].getRotationPoint(), joints[i + 1].getRotationPoint());
        }
        return chainLength;
    }

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

    public bool IKStepperActive() {
        return targetMode == TargetMode.IKStepper;
    }

    // Compare the current distance and the last registered error.
    // If the distance changed, either the target or the endeffector moved (e.g. the spider moved), thus we need to solve again.
    private bool hasMovementOccuredSinceLastSolve() {
        return (Mathf.Abs(Vector3.Distance(endEffector.position, currentTarget.position) - error) > float.Epsilon);
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
    // Dont allow external target manipulation if the debug modes are used.
    public void setTarget(TargetInfo target) {
        if (targetMode != TargetMode.IKStepper) {
            Debug.LogWarning("Not allowed to change target of IKChain " + gameObject.name + " since a debug mode is selected.");
            return;
        }
        currentTarget = target;
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

    public Vector3 getEndeffectorVelocityPerSecond() {
        return endEffectorVelocity;
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