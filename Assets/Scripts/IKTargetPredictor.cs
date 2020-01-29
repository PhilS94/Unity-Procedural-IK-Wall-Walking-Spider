using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CCDIKSolver))]
public class IKTargetPredictor : MonoBehaviour
{
    public SpiderController spidercontroller;
    public bool showDebug;

    [Range(0.01f, 1.0f)]
    public float debugIconScale = 0.1f;

    private CCDIKSolver ccdsolver;

    [Range(1.0f, 2.0f)]
    public float velocityPrediction = 1.5f;

    [Range(0.0f, 10.0f)]
    public float stepHeight;

    public AnimationCurve stepAnimation;

    private bool isStepping = false;

    private float minDistance;
    private float maxDistance;
    private float height;

    private Vector3 rootPos;
    private Vector3 lastRootPos;
    private Vector3 rootMoveDir;

    private Vector3 defaultPositionLocal;

    private AHingeJoint rootJoint;

    private Vector3 prediction;
    private Vector3 lastEndEffectorPos;


    // Start is called before the first frame update
    void Start()
    {
        ccdsolver = GetComponent<CCDIKSolver>();
        rootJoint = ccdsolver.getRootJoint();
        rootPos = rootJoint.getRotationPoint();
        lastRootPos = rootJoint.getRotationPoint();

        float chainLength = ccdsolver.getChainLength();
        maxDistance = chainLength;
        minDistance = 0.3f * chainLength;
        height = 1.5f;
        defaultPositionLocal = spidercontroller.transform.InverseTransformPoint(rootPos + (minDistance + 0.5f * (maxDistance - minDistance)) * rootJoint.getMidOrientation());
        defaultPositionLocal.y = -1.0f * 1.0f / spidercontroller.scale; // Because spider has a 20.0f reference scale
    }


    void Update()
    {
        rootPos = rootJoint.getRotationPoint();
        rootMoveDir = rootPos - lastRootPos;
        lastRootPos = rootPos; // Order is important here, as we use the lastrootPos above

        if (showDebug)
        {
            DebugShapes.DrawPoint(ccdsolver.getTarget().position, Color.cyan, debugIconScale);
            DebugShapes.DrawPoint(spidercontroller.transform.TransformPoint(defaultPositionLocal), Color.magenta, debugIconScale);
            DebugShapes.DrawPoint(prediction, Color.black, debugIconScale);
            DebugShapes.DrawPoint(lastEndEffectorPos, Color.gray, debugIconScale);
            Debug.DrawLine(lastEndEffectorPos, prediction, Color.black);
        }
    }

    public bool checkValidTarget(TargetInfo target)
    {
        Vector3 toTarget = target.position - rootPos;

        // Calculate current distances
        Vector3 horiz = Vector3.ProjectOnPlane(toTarget, spidercontroller.transform.up);
        Vector3 vert = Vector3.Project(toTarget, spidercontroller.transform.up);
        float horizontalDistance = Vector3.Magnitude(horiz);
        float verticalDistance = Vector3.Magnitude(vert);

        if (showDebug)
        {
            DebugShapes.DrawScope(rootPos, Vector3.ProjectOnPlane(rootJoint.getMinOrientation(), spidercontroller.transform.up), Vector3.ProjectOnPlane(rootJoint.getMaxOrientation(), spidercontroller.transform.up), spidercontroller.transform.up, minDistance, maxDistance, height, 3, Color.red);
            Debug.DrawLine(rootPos, rootPos + horiz, Color.green);
            Debug.DrawLine(rootPos + horiz, rootPos + horiz + vert, Color.green);
        }

        //      if targetposition projected onto the plane the spider is standing on, is farther away from the root joint than the whole chainlength
        //OR    if targetposition is not within the valid angle scope of the root joint
        //      This can cause trouble if we want the leg to be able to move e.g. beneath the spider since the scope does not allow the target to be behind the rootjoint
        if (horizontalDistance > maxDistance)
        {
            Debug.Log("Target too far away too reach.");
            return false;
        }
        else if (horizontalDistance < minDistance)
        {
            Debug.Log("Target too close for comfort.");
            return false;
        }
        else if (verticalDistance > height)
        {
            Debug.Log("Target too high or low too reach.");
            return false;
        }
        else if (!rootJoint.isVectorWithinScope(toTarget))
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
        Vector3 currentTarget = ccdsolver.getTarget().position;

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
            //DebugShapes.DrawPoint(finalPoint, Color.black, 0.1f);
            //Debug.Break();
        }
        return finalPoint;
    }

    /*
     * Calculates a new target using the endeffector Position and a default position defined in this class.
     * The new target position is a movement towards the default position but overshoots the default position using the velocity prediction
     */
    public TargetInfo calcNewTarget()
    {
        Debug.Log("I'm calculating a new target position.");

        Vector3 endeffectorPosition = ccdsolver.getEndEffector().position;
        Vector3 defaultPosition = spidercontroller.transform.TransformPoint(defaultPositionLocal); //This gives us the defaultPos in World Space

        //Now predict step target and for debug purposes set the debug target point to the prediction(this will be overridden with the actual target in the update, so use the break if needed)
        prediction = endeffectorPosition + (defaultPosition - endeffectorPosition) * velocityPrediction;
        lastEndEffectorPos = endeffectorPosition;

        //Now shoot a ray using the prediction to find an actual point on a surface. Maybe add angle, that is a focus to the ray
        Vector3 normal = spidercontroller.transform.up;
        Ray targetPredictorRay = new Ray(prediction + height * normal, -normal);
        RaycastHit hitInfo;
        float rayDist = 2 * height;
        TargetInfo finalTarget = new TargetInfo(defaultPosition, normal); // Initialize the finalTarget as defaultposition such that if the following ray hit is not valid, this will be returned as default

        if (showDebug)
        {
            Debug.DrawLine(targetPredictorRay.origin, targetPredictorRay.origin + rayDist * targetPredictorRay.direction, Color.cyan, 1.0f);
            Debug.DrawLine(endeffectorPosition, endeffectorPosition + (defaultPosition - endeffectorPosition) * velocityPrediction, Color.magenta, 1.0f);
        }

        if (Physics.Raycast(targetPredictorRay, out hitInfo, rayDist, spidercontroller.groundedLayer, QueryTriggerInteraction.Ignore))
        {
            // could check if the normal is acceptable
            finalTarget.position = hitInfo.point;
            finalTarget.normal = hitInfo.normal;
        }

        if (checkValidTarget(finalTarget))
        {
            return finalTarget;
        }

        //Might want to test more rays now

        //Since predictor Ray didnt work, we try a ray from default position
        Ray defaultRay = new Ray(defaultPosition + height * normal, -normal);

        if (Physics.Raycast(defaultRay, out hitInfo, rayDist, spidercontroller.groundedLayer, QueryTriggerInteraction.Ignore))
        {
            // could check if the normal is acceptable
            finalTarget.position = hitInfo.point;
            finalTarget.normal = hitInfo.normal;
        }

        if (checkValidTarget(finalTarget))
        {
            return finalTarget;
        }

        // Now even the default ray didnt find a valid target, therefore we simply return the default position
        finalTarget = new TargetInfo(defaultPosition, normal);

        return finalTarget;
    }

    /*
     * Coroutine for stepping since i want to actually see the stepping process instead of it happening all within one frame
     * */
    IEnumerator Step(TargetInfo newTarget, float timeForStep)
    {
        // should probably move the targets while im moving so that i dont outrun them, however this is stupid because then im no longer at the surface point i want
        isStepping = true;
        TargetInfo lastTarget = ccdsolver.getTarget();
        TargetInfo lerpTarget;
        float time = 0;
        while (time < timeForStep)
        {
            lerpTarget.position = Vector3.Lerp(lastTarget.position, newTarget.position,time/timeForStep) + stepHeight * stepAnimation.Evaluate(time / timeForStep)*spidercontroller.transform.up;
            lerpTarget.normal = Vector3.Lerp(lastTarget.normal, newTarget.normal,time/timeForStep);

            ccdsolver.setTarget(lerpTarget);
            time += Time.deltaTime;
        }
        ccdsolver.setTarget(newTarget);
        isStepping = false;
        yield return null;
    }
}
