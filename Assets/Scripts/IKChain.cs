using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

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

    private void Awake() {
        ikStepper = GetComponent<IKStepper>();
        chainLength = calculateChainLength();
        validChain = isValidChain();
    }

    private void Start() {
        setTarget(new TargetInfo(getEndEffector().position, Vector3.up));
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

        TargetInfo newTarget;

        switch (targetMode) {
            case TargetMode.DebugTarget:

                newTarget = new TargetInfo(debugTarget.position, debugTarget.up);
                //ikStepper.checkInvalidTarget(newTarget); //Just for Debug Prints
                setTarget(newTarget);
                break;

            case TargetMode.DebugTargetRay:
                float height = 1.0f;
                float distance = 1.1f;
                Ray debugRay = new Ray(debugTarget.position + height * Vector3.up, Vector3.down);
                Debug.DrawLine(debugRay.origin, debugRay.origin + distance * debugRay.direction, Color.yellow);

                if (Physics.Raycast(debugRay, out RaycastHit rayHit, distance, spider.walkableLayer, QueryTriggerInteraction.Ignore)) {
                    newTarget = new TargetInfo(rayHit.point, rayHit.normal);
                }
                else {
                    newTarget = new TargetInfo(debugTarget.position, debugTarget.up);
                }
                setTarget(newTarget);
                break;
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