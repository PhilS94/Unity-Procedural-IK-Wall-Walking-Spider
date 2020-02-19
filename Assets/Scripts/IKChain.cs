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

    public bool deactivateSolving = false;

    public Spider spider;

    [Header("Chain")]
    public AHingeJoint[] joints;
    public Transform endEffector;

    [Header("Target Mode")]
    public TargetMode targetMode;

    // Assign these if corresponding mode is selected
    public Transform debugTarget;
    private IKStepper ikStepper;

    private float chainLength;
    private TargetInfo currentTarget;
    private float error = 0.0f;
    private bool validChain;

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


    // Update is called once per frame
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
            if (debugModeRay.castRay(out RaycastHit hitInfo, spider.walkableLayer)) {
                setTarget(new TargetInfo(hitInfo.point, hitInfo.normal));
            }
            else {
                setTarget(new TargetInfo(debugTarget.position, debugTarget.up));
            }
        }
    }

    private void LateUpdate() {
        if (deactivateSolving || !validChain) return;
        solve();
    }

    public void solve() {
        IKSolver.solveCCD(ref joints, endEffector, currentTarget, true);
        error = Vector3.Distance(endEffector.position, currentTarget.position);
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
        Debug.DrawLine(joints[joints.Length-1].getRotationPoint(), endEffector.position, Color.green);
    }

#endif
}