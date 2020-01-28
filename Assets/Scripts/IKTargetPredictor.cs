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

    private float heigth = 2.0f;
    private float distance = 4.0f;

    private Vector3 rootPos;
    private Vector3 lastRootPos;
    private Vector3 rootMoveDir;

    private Vector3 defaultPositionLocal;
    private GameObject debugDefaultPosition;

    private AHingeJoint rootJoint;
    private GameObject debugTargetPoint;




    // Start is called before the first frame update
    void Start()
    {
        ccdsolver = GetComponent<CCDIKSolver>();
        rootJoint = ccdsolver.getRootJoint();
        rootPos = rootJoint.getRotationPoint();
        lastRootPos = rootJoint.getRotationPoint();

        defaultPositionLocal = spidercontroller.transform.InverseTransformPoint(rootPos + (ccdsolver.getEndEffector().position - rootPos).normalized * 0.75f * ccdsolver.getChainLength());
        defaultPositionLocal.y = -1.0f * 1.0f / spidercontroller.scale; // Because spider has a 20.0f reference scale

        if (showDebug)
        {
            inititalizeDebug();
        }
    }

    void Update()
    {
        rootPos = rootJoint.getRotationPoint();
        rootMoveDir = rootPos - lastRootPos;
        lastRootPos = rootPos; // Order is important here, as we use the lastrootPos above

        if (showDebug)
        {
            debugTargetPoint.transform.position = ccdsolver.getCurrentTargetPosition();
            debugDefaultPosition.transform.position = spidercontroller.transform.TransformPoint(defaultPositionLocal);
        }
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

        debugDefaultPosition = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        debugDefaultPosition.name = "Default Position for" + gameObject.name;
        Destroy(debugDefaultPosition.GetComponent<SphereCollider>());
        debugDefaultPosition.GetComponent<MeshRenderer>().material.color = Color.magenta;
        debugDefaultPosition.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        debugDefaultPosition.transform.parent = group.transform;
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
            //Debug for the distance of the target
            Debug.DrawLine(rootPos, rootPos + chainLength * toTarget.normalized, Color.grey);
            Debug.DrawLine(rootPos, rootPos + toTarget, Color.red);

            //Debug for the scope of the target
            float heigth = 1.0f;
            float length = 2.0f;
            Vector3 p = rootPos + spidercontroller.transform.up * heigth;
            Vector3 x1 = rootPos + rootJoint.getMinOrientation() * length;
            Vector3 x2 = p + rootJoint.getMinOrientation() * length;
            Vector3 y1 = rootPos + rootJoint.getMaxOrientation() * length;
            Vector3 y2 = p + rootJoint.getMaxOrientation() * length;
            Debug.DrawLine(rootPos, p, Color.red);

            Debug.DrawLine(rootPos, x1, Color.red);
            Debug.DrawLine(p, x2, Color.red);
            Debug.DrawLine(x1, x2, Color.red);
            Debug.DrawLine(p, x1, Color.red);
            Debug.DrawLine(rootPos, x2, Color.red);

            Debug.DrawLine(rootPos, y1, Color.red);
            Debug.DrawLine(p, y2, Color.red);
            Debug.DrawLine(y1, y2, Color.red);
            Debug.DrawLine(p, y1, Color.red);
            Debug.DrawLine(rootPos, y2, Color.red);
        }

        //      if targetposition is farther away from the root joint than the whole chainlength
        //OR    if targetposition is not within the valid angle scope of the root joint
        //      This can cause trouble if we want the leg to be able to move e.g. beneath the spider since the scope does not allow the target to be behind the rootjoint
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

        //Maybe add if the target is too close for comfort

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

        //If rootJointOrientation and moveDirection orthogonal then use law of cosines to solve    
        alpha = 0.4f * Mathf.Lerp(Mathf.Sqrt(2) * chainLength * Mathf.Sqrt(1 - Mathf.Cos(Mathf.Deg2Rad * angleRange)),
                                    // If rootJointOrientation and moveDirection parallel then
                                    chainLength,
                                    t);

        Vector3 p = currentTarget + alpha * rootMoveDir.normalized;

        float heigth = 2.0f;
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


        if (showDebug)
        {
            debugTargetPoint.transform.position = finalPoint;
            Debug.Break();
        }
        return finalPoint;
    }

    public targetInfo calculateNewTargetPositionUsingDefaultPos()
    {
        Debug.Log("I'm calculating a new target position.");

        Vector3 endeffectorPosition = ccdsolver.getEndEffector().position;
        Vector3 defaultPosition = spidercontroller.transform.TransformPoint(defaultPositionLocal); //This gives us the defaultPos in World Space

        //Now predict step target
        float velocityPrediction = 1.5f; //Value of one means the new target will be the defaultpositon, but we want to overshoot it smartly
        Vector3 prediction = endeffectorPosition + (defaultPosition - endeffectorPosition) * velocityPrediction;

        //Now shoot a ray using the prediction to find an actual point on a surface. Maybe add angle, that is a focus to the ray
        Vector3 normal = spidercontroller.transform.up;
        targetPredictorRay = new Ray(prediction + heigth * normal, -normal);
        targetInfo finalTarget = new targetInfo(defaultPosition, spidercontroller.transform.up); // Initialize the finalTarget as defaultposition such that if no following ray hits, this will be returned as default

        if (showDebug)
        {
            Debug.DrawLine(targetPredictorRay.origin, targetPredictorRay.origin + distance * targetPredictorRay.direction, Color.cyan, 1.0f);
            Debug.DrawLine(endeffectorPosition, endeffectorPosition + (defaultPosition - endeffectorPosition) * velocityPrediction, Color.magenta, 1.0f);

        }

        if (Physics.Raycast(targetPredictorRay, out RaycastHit hitInfo, distance, spidercontroller.groundedLayer, QueryTriggerInteraction.Ignore))
        {
            // could check if the normal is acceptable
            finalTarget.position = hitInfo.point;
            finalTarget.normal = hitInfo.normal;
        }
        //Maybe add more Raycasts

        if (showDebug)
        {
            debugTargetPoint.transform.position = prediction;
            //Debug.Break();
        }
        return finalTarget;
    }
}
