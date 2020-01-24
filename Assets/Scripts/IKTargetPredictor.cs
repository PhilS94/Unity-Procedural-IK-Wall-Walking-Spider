using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CCDIKSolver))]
public class IKTargetPredictor : MonoBehaviour
{
    private CCDIKSolver ccdsolver;
    private Ray targetPredictorRay;

    private Vector3 rootMoveDir;
    private Vector3 lastRootPos;

    private void Awake()
    {
        ccdsolver = GetComponent<CCDIKSolver>();
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        AHingeJoint rootJoint = ccdsolver.getRootJoint();
        Vector3 rootPos = rootJoint.getRotationPoint();
        Vector3 currentTargetPos = ccdsolver.getCurrentTargetPosition();
        float chainLength = ccdsolver.getChainLength();

        Vector3 toTarget = currentTargetPos - rootPos;
        rootMoveDir = rootPos - lastRootPos;

        //if targetposition is farther away from the root joint than the whole chainlength
        if (Vector3.Magnitude(toTarget) > chainLength)
        {
            Vector3 newTarget = calculateNewTargetPosition();
            ccdsolver.setNewTargetPosition(newTarget);
        }

        //If targetposition is not within the valid angle scope of the root joint
        //This can cause trouble if we want the leg to be able to move e.g. under the spider
        if (!rootJoint.isVectorWithinScope(toTarget))
        {
            Vector3 newTarget = calculateNewTargetPosition();
            ccdsolver.setNewTargetPosition(newTarget);
        }

        //Update this last, as to be able to use this prior position in the lines above
        lastRootPos = rootPos;
    }

    private Vector3 calculateNewTargetPosition()
    {
        float alpha = 0.1f; //calc this smartly
        Vector3 newTarget = ccdsolver.getCurrentTargetPosition() + alpha * rootMoveDir;
        return Vector3.zero;
    }
}
