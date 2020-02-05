using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum targetValidity {
    valid = 0,
    tooFar,
    tooClose,
    tooHigh,
    tooLow,
    tooMin,
    tooMax
}

public struct Line {
    public Vector3 origin;
    public Vector3 end;

    public Line(Vector3 p, Vector3 q) {
        origin = p;
        end = q;
    }
    public Line(Vector3 p, Vector3 direction, float length) {
        origin = p;
        end = p + direction * length; ;
    }
}

[RequireComponent(typeof(IKChain))]
public class IKStepper : MonoBehaviour {
    public SpiderController spidercontroller;
    public IKStepper asyncChain;

    public bool showDebug;

    [Range(0.01f, 1.0f)]
    public float debugIconScale = 0.1f;

    [Range(1.0f, 2.0f)]
    public float velocityPrediction = 1.5f;

    [Range(0, 10.0f)]
    public float stepTime;

    [Range(0.0f, 10.0f)]
    public float stepHeight;

    [Range(0.0f, 5.0f)]
    public float stepCooldown = 0.5f;
    private float timeSinceLastStep;

    public Vector3 originPointOutwardsRay;
    public Vector3 endPointInwardsRay;

    public AnimationCurve stepAnimation;

    private IKChain ikChain;

    private bool isStepping = false;

    private float minDistance;
    private float maxDistance;
    private float height = 2.5f;

    private Line lineDown;
    private Line lineOutward;
    private Line lineInwards;
    private Line lineDefaultDown;
    private Line lineDefaultOutward;
    private Line lineDefaultInward;

    private AHingeJoint rootJoint;
    private Vector3 rootPos;
    private Vector3 defaultPositionLocal;
    private Vector3 prediction;
    private Vector3 lastEndEffectorPos;


    // Start is called before the first frame update
    void Start() {
        ikChain = GetComponent<IKChain>();
        spidercontroller = ikChain.spiderController;
        rootJoint = ikChain.getRootJoint();
        rootPos = rootJoint.getRotationPoint();

        float chainLength = ikChain.getChainLength();
        maxDistance = chainLength;
        minDistance = 0.2f * chainLength;
        defaultPositionLocal = spidercontroller.transform.InverseTransformPoint(rootPos + (minDistance + 0.5f * (maxDistance - minDistance)) * rootJoint.getMidOrientation());
        defaultPositionLocal.y = -1.0f / spidercontroller.scale; // Because spider has a 20.0f reference scale
        timeSinceLastStep = 2 * stepCooldown;
    }

    void Update() {
        rootPos = rootJoint.getRotationPoint();

        timeSinceLastStep += Time.deltaTime;
    }

    private void LateUpdate() {
        if (showDebug) {
            drawDebug();
        }
    }

    /*
     * Checks if the target is invalid or not.
     * Returns 0 if the target is valid.
     * If the target is invalid returns
     * 
     */
    public targetValidity checkInvalidTarget(TargetInfo target) {
        Vector3 toTarget = target.position - rootPos;

        // Calculate current distances
        Vector3 normal = rootJoint.getRotationAxis();
        Vector3 horiz = Vector3.ProjectOnPlane(toTarget, normal);
        Vector3 vert = Vector3.Project(toTarget, normal);
        float horizontalDistance = Vector3.Magnitude(horiz);
        float verticalDistance = Vector3.Magnitude(vert);

        if (showDebug) {
            Debug.DrawLine(rootPos, rootPos + horiz, Color.green);
            Debug.DrawLine(rootPos + horiz, rootPos + horiz + vert, Color.green);
        }

        // Check if target is within cylinder section

        // First check Scope
        int scope = rootJoint.isVectorWithinScope(toTarget);
        if (scope == 1) {
            Debug.Log("Target above my max angle.");
            return targetValidity.tooMax;
        }
        else if (scope == -1) {
            Debug.Log("Target below my min angle.");
            return targetValidity.tooMin;
        }

        //Then check horizontal distance
        if (horizontalDistance > maxDistance) {
            Debug.Log("Target too far away too reach.");
            return targetValidity.tooFar;
        }
        if (horizontalDistance < minDistance) {
            Debug.Log("Target too close for comfort.");
            return targetValidity.tooClose;
        }

        //Then check vertical distance (height)
        if (verticalDistance > height) {
            if (Vector3.Dot(normal, vert) >= 0) {
                Debug.Log("Target too high.");
                return targetValidity.tooHigh;
            }
            else {
                Debug.Log("Target too low.");
                return targetValidity.tooLow;
            }
        }
        return targetValidity.valid;
    }

    /*
     * Calculates a new target using the endeffector Position and a default position defined in this class.
     * The new target position is a movement towards the default position but overshoots the default position using the velocity prediction
     */
    public TargetInfo calcNewTarget() {

        Vector3 endeffectorPosition = ikChain.getEndEffector().position;
        Vector3 defaultPosition = spidercontroller.transform.TransformPoint(defaultPositionLocal);
        lastEndEffectorPos = endeffectorPosition; //Update this so it can be debug drawn in update function
        Vector3 normal = spidercontroller.transform.up;

        //Now predict step target

        // Option 1: Include spider movement in the prediction process: prediction += SpiderMoveVector * stepTime
        //      Problem:    Spider might stop moving while stepping, if this happens i will over predict
        //                  Spider might change direction while stepping, if this happens i could predict out of range
        //      Solution:   Keep the stepTime short such that not much will happen

        // Option 2: Dynamically update the prediction in a the stepping coroutine where i keep up with the spider with its local coordinates
        //      Problem:    I will only know if the foot lands on a surface point after the stepping is already done
        //                  This means the foot could land in a bump on the ground or in the air, and i will have to look what i will do from there
        //                  Update the position within the last frame (unrealistic) or start another different stepping coroutine?
        //                  Or shoot more rays in the stepping process to somewhat adjust to the terrain changes?

        // For now I choose Option 1:

        prediction = Vector3.ProjectOnPlane(endeffectorPosition + (defaultPosition - endeffectorPosition) * velocityPrediction, normal)
                        + spidercontroller.getMovement() * stepTime;

        // The following just adjusts the prediction such that is lies on the same plane as the defaultposition
        prediction = spidercontroller.transform.InverseTransformPoint(prediction);
        prediction.y = defaultPositionLocal.y;
        prediction = spidercontroller.transform.TransformPoint(prediction);

        //Now shoot a rays using the prediction to find an actual point on a surface.
        RaycastHit hitInfo;
        Vector3 originPointOutward = spidercontroller.transform.TransformPoint(originPointOutwardsRay);
        Vector3 endPointInward = spidercontroller.transform.TransformPoint(endPointInwardsRay);

        lineOutward.origin = originPointOutward;
        lineOutward.end = prediction;
        lineDown.origin = prediction + height * normal;
        lineDown.end = lineDown.origin + 2 * height * -normal;
        lineInwards.origin = prediction;
        lineInwards.end = endPointInward;
        lineDefaultOutward.origin = originPointOutward;
        lineDefaultOutward.end = defaultPosition;
        lineDefaultDown.origin = defaultPosition + height * normal;
        lineDefaultDown.end = lineDefaultDown.origin + 2 * height * -normal;
        lineDefaultInward.origin = defaultPosition;
        lineDefaultInward.end = endPointInward;

        //Straight down through prediction point
        if (shootRay(lineDown, out hitInfo)) {
            Debug.Log("Got Targetpoint shooting down to prediction.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        // Outwards to prediction
        if (shootRay(lineOutward, out hitInfo)) {
            Debug.Log("Got Targetpoint shooting outwards to prediction.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        // Inwards from prediction

        if (shootRay(lineInwards, out hitInfo)) {
            Debug.Log("Got Targetpoint shooting inwards from prediction.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        // Outwards to default position

        if (shootRay(lineDefaultOutward, out hitInfo)) {
            Debug.Log("Got Targetpoint shooting outwards to default point.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        //Straight down to default point
        if (shootRay(lineDefaultDown, out hitInfo)) {
            Debug.Log("Got Targetpoint shooting down to default point.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        // Inwards from default point

        if (shootRay(lineDefaultInward, out hitInfo)) {
            Debug.Log("Got Targetpoint shooting inwards from default point.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        // Return default position
        Debug.Log("No ray was able to find a target position. Therefore i will return the default position.");
        return new TargetInfo(defaultPosition, normal);
    }

    // Immediately sets down target position and stops any stepping process going on.
    // Not used yet since if i do it gets called whenever i actually want to start a step
    public void setTargetDown() {
        Debug.Log("Had to stet Target Down, since i overpredicted.");
        StopAllCoroutines();
        isStepping = false;
        timeSinceLastStep = 0.0f;
        Vector3 normal = spidercontroller.transform.up;

        Vector3 origin = Vector3.Project(transform.position, normal) + Vector3.ProjectOnPlane(ikChain.getTarget().position, normal) + height * normal;
        Line l = new Line(origin, -normal, 2 * height);

        if (shootRay(l, out RaycastHit hitInfo)) {
            ikChain.setTarget(new TargetInfo(hitInfo.point, hitInfo.normal));
        }
        else {
            Vector3 defaultPosition = spidercontroller.transform.TransformPoint(defaultPositionLocal);
            ikChain.setTarget(new TargetInfo(defaultPosition, normal));
        }
    }

    private bool shootRay(Line l, out RaycastHit hitInfo) {
        Vector3 direction = (l.end - l.origin);
        float magnitude = Vector3.Magnitude(direction);
        direction = direction / magnitude;

        if (showDebug) {
            Debug.DrawLine(l.origin, l.end, Color.yellow);
        }

        if (Physics.Raycast(l.origin, direction, out hitInfo, magnitude, spidercontroller.groundedLayer, QueryTriggerInteraction.Ignore)) {
            // could check if the normal is acceptable
            if (Mathf.Cos(Vector3.Angle(direction, hitInfo.normal)) < 0) {
                return true;
            }
        }
        return false;
    }

    private bool shootSphere(Vector3 origin, Vector3 direction, float distance, float radius, out RaycastHit hitInfo) {
        direction = direction.normalized;

        if (showDebug) {
            DebugShapes.DrawSphere(origin, debugIconScale, 24, 12, Color.cyan);
            DebugShapes.DrawSphere(origin + distance / 2 * direction, debugIconScale, 24, 12, Color.cyan);
            DebugShapes.DrawSphere(origin + distance * direction, debugIconScale, 24, 12, Color.cyan);
        }

        if (Physics.SphereCast(origin, radius, direction, out hitInfo, distance, spidercontroller.groundedLayer, QueryTriggerInteraction.Ignore)) {
            // could check if the normal is acceptable
            return true;
        }
        return false;
    }

    // If im walking so fast that  one legs keeps wanna step after step complete, one leg might not step at all since its never able to
    // Could implement a some sort of a queue where i enqueue chains that want to step next?
    public void step(TargetInfo target) {

        if (isStepping) {
            return;
        }

        if (!allowedToStep()) {
            return;
        }

        IEnumerator coroutineStepping = Step(target);
        StartCoroutine(coroutineStepping);
    }

    public bool allowedToStep() {
        if (asyncChain != null && asyncChain.getIsStepping()) {
            return false;
        }
        return true;
    }

    public bool getIsStepping() {
        return ((isStepping) || (timeSinceLastStep < stepCooldown));
    }

    public TargetInfo getDefault() {
        return new TargetInfo(spidercontroller.transform.TransformPoint(defaultPositionLocal), spidercontroller.transform.up);
    }

    /*
     * Coroutine for stepping since i want to actually see the stepping process instead of it happening all within one frame
     * */
    private IEnumerator Step(TargetInfo newTarget) {
        Debug.Log("Stepping");
        // should probably move the targets while im moving so that i dont outrun them, however this is stupid because then im no longer at the surface point i want
        // otherwise i could increase the velocity prediction in the first place, However in this case an invalid prediction may not be invalid after stepping
        isStepping = true;
        TargetInfo lastTarget = ikChain.getTarget();
        TargetInfo lerpTarget;
        float time = 0;
        while (time < stepTime) {
            lerpTarget.position = Vector3.Lerp(lastTarget.position, newTarget.position, time / stepTime) + stepHeight * stepAnimation.Evaluate(time / stepTime) * spidercontroller.transform.up;
            lerpTarget.normal = Vector3.Lerp(lastTarget.normal, newTarget.normal, time / stepTime);

            time += Time.deltaTime;
            ikChain.setTarget(lerpTarget);
            yield return null;
        }
        ikChain.setTarget(newTarget);
        isStepping = false;
        timeSinceLastStep = 0.0f;
    }

    // Testing Option 2 of predicting
    private IEnumerator stepDynamically(Vector3 prediction) {

        TargetInfo lastTarget = ikChain.getTarget();

        Vector3 predictionLocal = spidercontroller.transform.InverseTransformPoint(prediction);
        Vector3 lastTargetPositionLocal = spidercontroller.transform.InverseTransformPoint(lastTarget.position);

        TargetInfo lerpTarget = lastTarget; //Initialize since its possibly unassigned later
        lerpTarget.normal = lastTarget.normal;
        float time = 0;
        while (time < stepTime) {
            Vector3 target = spidercontroller.transform.TransformPoint(lastTargetPositionLocal);
            Vector3 naivPred = spidercontroller.transform.TransformPoint(predictionLocal);

            // Lerp locally + Height 
            lerpTarget.position = Vector3.Lerp(target, naivPred, time / stepTime)
                                    + stepHeight * stepAnimation.Evaluate(time / stepTime) * spidercontroller.transform.up;
            time += Time.deltaTime;
            ikChain.setTarget(lerpTarget);
            yield return null;
        }
        // Now im at the provisorical prediction and moved with the spider movement.
        // I dont have a surface point though so now i need to raycast
        RaycastHit hitInfo;

        //Straight down through prediction point
        Vector3 normal = spidercontroller.transform.up;
        if (shootSphere(lerpTarget.position + height * normal, -normal, 2 * height, 0.01f, out hitInfo)) {
            ikChain.setTarget(new TargetInfo(hitInfo.point, hitInfo.normal));
        }
        else {
            //Use default if the above didnt work
            ikChain.setTarget(new TargetInfo(spidercontroller.transform.TransformPoint(defaultPositionLocal), normal));
        }
    }

    void drawDebug() {
        //Target Point
        if (isStepping) DebugShapes.DrawPoint(ikChain.getTarget().position, Color.cyan, debugIconScale, stepTime);
        // Target Point while Stepping
        else DebugShapes.DrawPoint(ikChain.getTarget().position, Color.cyan, debugIconScale);

        // Default Position
        DebugShapes.DrawPoint(spidercontroller.transform.TransformPoint(defaultPositionLocal), Color.magenta, debugIconScale);

        //Prediction Point
        DebugShapes.DrawPoint(prediction, Color.black, debugIconScale);

        // Last Position the End effector had
        DebugShapes.DrawPoint(lastEndEffectorPos, Color.gray, debugIconScale);

        // Line From the last end effector position to the prediction
        Debug.DrawLine(lastEndEffectorPos, prediction, Color.black);

        // The cylinder section of viable target positions
        DebugShapes.DrawCylinderSection(rootPos, rootJoint.getMinOrientation(), rootJoint.getMaxOrientation(), rootJoint.getRotationAxis(), minDistance, maxDistance, height, height, 3, Color.red);

        //All the Raycasts:
        Debug.DrawLine(lineOutward.origin, lineOutward.end, Color.yellow);
        Debug.DrawLine(lineDown.origin, lineDown.end, 0.9f * Color.yellow);
        Debug.DrawLine(lineInwards.origin, lineInwards.end, 0.8f * Color.yellow);
        Debug.DrawLine(lineDefaultOutward.origin, lineDefaultOutward.end, 0.7f * Color.yellow);
        Debug.DrawLine(lineDefaultDown.origin, lineDefaultDown.end, 0.6f * Color.yellow);
        Debug.DrawLine(lineDefaultInward.origin, lineDefaultInward.end, 0.5f * Color.yellow);

    }

    void OnDrawGizmosSelected() {
#if UNITY_EDITOR
        if (!UnityEditor.Selection.Contains(transform.gameObject)) {
            return;
        }
        DebugShapes.DrawPoint(spidercontroller.transform.TransformPoint(originPointOutwardsRay), Color.green, debugIconScale);
        DebugShapes.DrawPoint(spidercontroller.transform.TransformPoint(endPointInwardsRay), Color.green, debugIconScale);
#endif
    }
}
