using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CCDIKSolver))]
public class IKTargetPredictor : MonoBehaviour
{
    public SpiderController spidercontroller;
    public bool showDebug;
    private CCDIKSolver ccdsolver;
    private Ray targetPredictorRay;

    private Vector3 rootPos;
    private Vector3 lastRootPos;
    private Vector3 rootMoveDir;

    private AHingeJoint rootJoint;
    private GameObject debugTargetPoint;


    // Start is called before the first frame update
    void Start()
    {
        ccdsolver = GetComponent<CCDIKSolver>();
        rootJoint = ccdsolver.getRootJoint();
        lastRootPos = rootJoint.getRotationPoint();
        if (showDebug)
        {
            inititalizeDebug();
        }
    }

    void Update()
    {
        if (showDebug)
        {
            debugTargetPoint.transform.position = ccdsolver.getCurrentTargetPosition();
        }

        rootPos = rootJoint.getRotationPoint();
        rootMoveDir = rootPos - lastRootPos;
        lastRootPos = rootPos; // Order is important here, as we use the lastrootPos above
    }

    void inititalizeDebug()
    {
        GameObject group = new GameObject("IKTargetPredictor Debug Group");

        debugTargetPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        debugTargetPoint.name = "Target Point for " + gameObject.name;
        Destroy(debugTargetPoint.GetComponent<SphereCollider>());
        debugTargetPoint.GetComponent<MeshRenderer>().material.color = Color.black;
        debugTargetPoint.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        debugTargetPoint.transform.parent = group.transform;
    }

    public bool checkValidTarget()
    {
        // Rootjoint variables
        Vector3 rootOrient = rootJoint.getOrientation();
        float angleRange = rootJoint.getAngleRange();

        //CCD Solver variables
        Vector3 currentTargetPos = ccdsolver.getCurrentTargetPosition();
        float chainLength = ccdsolver.getChainLength();

        Vector3 toTarget = currentTargetPos - rootPos;

        if (showDebug)
        {
            Debug.DrawLine(rootPos, rootPos + chainLength*toTarget.normalized, Color.grey);
            Debug.DrawLine(rootPos, rootPos + toTarget, Color.red);
            //TODO: Draw debug visualization here for the second or case
        }

        //      if targetposition is farther away from the root joint than the whole chainlength
        //OR    if targetposition is not within the valid angle scope of the root joint
        //      This can cause trouble if we want the leg to be able to move e.g. beneath the spider
        if (Vector3.Magnitude(toTarget) > chainLength)
        {
            Debug.Log("Target too far away too reach.");
            return false;
        }
        if (!rootJoint.isVectorWithinScope(toTarget))
        {
            Debug.Log("Target not in scope");
            return false;
        }

        return true;
    }

    /*
     * Takes a current position and the direction the joint is currently moving and calculates a new position on a surface using Raycasts.
     * For vertical joint movement, that is in the same dir as the joint is facing, i want to move maximally chainlength
     * For horizontal joint movement, that is the joint and moveDir are orthogonal to eachother, i want to move maximally by law of cosines
     */
    public Vector3 calculateNewTargetPosition()
    {
        Debug.Log("I'm calculating a new target position.");

        // Rootjoint variables
        Vector3 rootOrient = rootJoint.getOrientation();
        float angleRange = rootJoint.getAngleRange();

        //CCD Solver variables
        float chainLength = ccdsolver.getChainLength();
        Vector3 currentTarget = ccdsolver.getCurrentTargetPosition();

        float alpha; //calc this smartly

        // Value between 0 and 1, where 0 if they are orthogonal and 1 if they are parallel
        float t = Mathf.Abs(Vector3.Dot(rootMoveDir.normalized, rootOrient.normalized));

        //Use linear combination of the two cases using the float value t:

        // If rootJointOrientation and moveDirection parallel then
        alpha = t * ccdsolver.getChainLength()
                // If rootJointOrientation and moveDirection orthogonal then use law of cosines to solve 
                + (1 - t) * Mathf.Sqrt(2 * Mathf.Pow(chainLength, 2) * (1 - Mathf.Cos(Mathf.Deg2Rad * angleRange)));
        alpha *= 0.9f;

        Vector3 p = currentTarget + alpha * rootMoveDir.normalized;
        float heigth = 5.0f;
        float distance = 2 * heigth;
        Vector3 normal = spidercontroller.getCurrentNormal().normalized;
        //maybe add angle, that is a focus to the ray

        Vector3 finalPoint;
        Ray findTargetRay = new Ray(p + heigth * normal, -normal);

        if (showDebug)
        {
            Debug.DrawLine(findTargetRay.origin, findTargetRay.origin + distance * findTargetRay.direction, Color.cyan, 5.0f);
        }

        if (Physics.Raycast(findTargetRay, out RaycastHit hitInfo, distance, spidercontroller.groundedLayer, QueryTriggerInteraction.Ignore))
        {
            // could check if the normal is acceptable
            finalPoint = hitInfo.point;
        }
        else
        {
            // might want to use a standard relaxed point in the air, or try a differnt ray here, but for now ill just do nothing
            finalPoint = p;
        }

        return finalPoint;
    }
}
