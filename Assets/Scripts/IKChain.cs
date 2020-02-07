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

    public SpiderController spiderController;
    public AHingeJoint[] joints;
    public Transform endEffector;


    public TargetMode targetMode;

    // Assign these if corresponding mode is selected
    public Transform debugTarget;
    private IKStepper ikStepper;

    private float chainLength;
    private TargetInfo currentTarget;
    private bool validChain;

    private void Awake() {
        ikStepper = GetComponent<IKStepper>();
        initializeChain();
        validChain = isValidChain();
    }

    private void Start() {
        // Assign a starting target. Would assign default but the default position is calculated in the Start function of IKStepper and uses the IKChain length :         setTarget(ikStepper.getDefault());
        setTarget(new TargetInfo(getEndEffector().position, Vector3.up));
        solve();
    }
    void initializeChain() {
        if (endEffector.GetComponent<AHingeJoint>() != null) {
            Debug.Log("For the CCD chain " + this.name + " the end effector " + endEffector.gameObject.name + " has an attached AHingeJoint but is not a joint. The component should be removed.");
        }

        // Calc Chain Length
        chainLength = 0;

        for (int i = 0; i < joints.Length - 1; i++) {
            chainLength += Vector3.Distance(joints[i].getRotationPoint(), joints[i + 1].getRotationPoint());
        }
        Debug.Log("Chain length for the chain " + gameObject.name + ": " + chainLength);
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

                if (Physics.Raycast(debugRay, out RaycastHit rayHit, distance, spiderController.groundedLayer, QueryTriggerInteraction.Ignore)) {
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

    private void solve() {
        IKSolver.solveCCD(ref joints, endEffector, currentTarget, true);
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

    // Use these setters to set the target for the CCD algorithm. The CCD runs with every frame update and uses this target.
    public void setTarget(TargetInfo target) {
        currentTarget = target;
        //In Theory i want to call solveCCD here but it runs every frame anyway so i wont for now
    }

    public bool IKStepperActivated() {
        return (targetMode == TargetMode.IKStepper && !deactivateSolving && validChain);
    }
}