using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public enum TargetMode {
    targetPredictor,
    debugTarget,
    debugTargetRay
}

public class IKChain : MonoBehaviour {
    public SpiderController spiderController;
    public AHingeJoint[] joints;
    public Transform endEffector;

    public bool deactivate = false;

    public TargetMode targetMode;

    // By assigning one of these the CCD IK Solver will use one of these transfoms as target, if unassigned  the IKTargetPredictor is used
    public Transform debugTarget;
    private IKStepper ikStepper;

    private float chainLength;
    private TargetInfo currentTarget;
    private bool validChain;

    private void Awake() {
        ikStepper = GetComponent<IKStepper>();
        initializeChain();
        validChain = isValidChain();
        // Assign a starting target
        setTarget(new TargetInfo(getEndEffector().position, Vector3.up));
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
        if ((debugTarget == null) && ((targetMode == TargetMode.debugTarget) || (targetMode == TargetMode.debugTargetRay))) {
            Debug.LogError("Please assign a Target Transform when using this mode.");
            return false;
        }

        if ((ikStepper == null) && (targetMode == TargetMode.targetPredictor)) {
            Debug.LogError("Please assign a IKTargetPredictor Component when using this mode.");
            return false;
        }
        return true;
    }


    // Update is called once per frame
    void Update() {
        if (deactivate) {
            return;
        }

        if (!validChain) {
            return;
        }

        if (ikStepper.getIsStepping()) {
            return;
        }

        TargetInfo newTarget = currentTarget;

        switch (targetMode) {
            case TargetMode.debugTarget:

                newTarget = new TargetInfo(debugTarget.position, debugTarget.up);
                break;

            case TargetMode.debugTargetRay:
                float height = 1.0f;
                float distance = 1.1f;
                Ray debugRay = new Ray(debugTarget.position + height * Vector3.up, Vector3.down);
                Debug.DrawLine(debugRay.origin, debugRay.origin + distance * debugRay.direction, Color.green);

                if (Physics.Raycast(debugRay, out RaycastHit rayHit, distance, spiderController.groundedLayer, QueryTriggerInteraction.Ignore)) {
                    newTarget = new TargetInfo(rayHit.point, rayHit.normal);
                }
                else {
                    newTarget = new TargetInfo(debugTarget.position, debugTarget.up);
                }
                break;

            case TargetMode.targetPredictor:

                if (!ikStepper.checkValidTarget(currentTarget)) {
                    ikStepper.step(ikStepper.calcNewTarget());
                }
                break;
        }
        setTarget(newTarget);
    }

    private void LateUpdate() {
        if (deactivate) {
            return;
        }

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
        if ((targetMode == TargetMode.debugTarget) || (targetMode == TargetMode.debugTargetRay)) {
            debugTarget.position = target.position;
            debugTarget.rotation = Quaternion.LookRotation(debugTarget.forward, target.normal);
        }

        currentTarget = target;
        //In Theory i want to call solveCCD here but it runs every frame anyway so i wont for now
    }
}