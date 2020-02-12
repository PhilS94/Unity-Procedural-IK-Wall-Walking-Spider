using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RayType {
    singleRay,
    sphereRay
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

    [Header("Debug")]

    public bool showDebug;
    [Range(0.01f, 1.0f)]
    public float debugIconScale = 0.1f;

    [Header("Stepping")]

    public IKStepper asyncChain;

    [Range(1.0f, 2.0f)]
    public float velocityPrediction = 1.5f;

    [Range(0, 10.0f)]
    public float stepTime;

    [Range(0.0f, 10.0f)]
    public float stepHeight;

    [Range(0.0f, 5.0f)]
    public float stepCooldown = 0.5f;
    private float timeSinceLastStep;

    public AnimationCurve stepAnimation;

    public RayType rayType;
    private float radius; // If SphereRay is selected


    [Header("Points")]
    public Vector3 originPointOutwardsRay;
    public Vector3 endPointInwardsRay;

    private IKChain ikChain;

    private bool isStepping = false;
    private bool uncomfortable = false;

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

    //Debug Variables
    private Vector3 prediction1;
    private Vector3 prediction2;
    private Vector3 prediction3;
    private Vector3 lastEndEffectorPos;
    private Vector3 lastDefaultPositionOfStep;


    private void Awake() {
        ikChain = GetComponent<IKChain>();
        spidercontroller = ikChain.spiderController;
        rootJoint = ikChain.getRootJoint();
    }

    void Start() {
        rootPos = rootJoint.getRotationPoint();

        // Set important parameters
        timeSinceLastStep = 2 * stepCooldown;
        radius = 0.005f * spidercontroller.scale;
        float chainLength = ikChain.getChainLength();
        maxDistance = chainLength;
        minDistance = 0.2f * chainLength;

        //Calc default position
        Vector3 v = Quaternion.AngleAxis((rootJoint.minAngle + rootJoint.maxAngle) / 2, rootJoint.getRotationAxis()) * Vector3.ProjectOnPlane(ikChain.getEndEffector().position - rootPos, rootJoint.getRotationAxis()).normalized;
        defaultPositionLocal = spidercontroller.transform.InverseTransformPoint(rootPos + (minDistance + 0.5f * (maxDistance - minDistance)) * v);
        defaultPositionLocal.y = -spidercontroller.col.radius;
    }

    void Update() {
        rootPos = rootJoint.getRotationPoint();
        timeSinceLastStep += Time.deltaTime;

        if (!ikChain.IKStepperActivated() || isStepping || !allowedToStep()) return;


        //If im uncomfortable and there is a new comfortable target, step, otherwise just refresh the uncomfortable target.
        if (uncomfortable) {
            TargetInfo newTarget = calcNewTarget(out uncomfortable);
            if (!uncomfortable) step(newTarget);
            else ikChain.setTarget(newTarget);
        }
        //If comfortable but target not close enough anymore even after the last CCD iteration, step
        else if (ikChain.getError() > IKSolver.tolerance) {
            //Debug.Log("Have to step now since im too far away at an error of " + ikChain.getError());
            step(calcNewTarget(out uncomfortable));
            //Debug.Break();
        }
        // Or if too close to RootJoint
        else if (Vector3.Distance(rootJoint.getRotationPoint(), ikChain.getTarget().position) < minDistance) {
            step(calcNewTarget(out uncomfortable));
        }

        if (Input.GetKeyDown(KeyCode.Space)) step(calcNewTarget(out uncomfortable));
    }

    private void LateUpdate() {
        if (showDebug) {
            drawDebug();
        }
    }

    /*
     * Calculates a new target using the endeffector Position and a default position defined in this class.
     * The new target position is a movement towards the default position but overshoots the default position using the velocity prediction
     */
    public TargetInfo calcNewTarget(out bool uncomfortable) {

        /*
         * Think about using the last target position instead of the endeffector position?
         */
        uncomfortable = false;
        Vector3 endeffectorPosition = ikChain.getEndEffector().position;
        Vector3 defaultPosition = spidercontroller.transform.TransformPoint(defaultPositionLocal);
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

        //Calculate prediction
        prediction = endeffectorPosition + (defaultPosition - endeffectorPosition) * velocityPrediction
                    + spidercontroller.getMovement() * stepTime;
        prediction = Vector3.ProjectOnPlane(prediction, normal);

        // The following just adjusts the prediction such that is lies on the same plane as the defaultposition
        prediction = spidercontroller.transform.InverseTransformPoint(prediction);
        prediction.y = defaultPositionLocal.y;
        prediction = spidercontroller.transform.TransformPoint(prediction);

        //Debug variables
        lastEndEffectorPos = endeffectorPosition;
        lastDefaultPositionOfStep = defaultPosition;
        prediction1 = endeffectorPosition + (defaultPosition - endeffectorPosition) * velocityPrediction;
        prediction2 = prediction1 + spidercontroller.getMovement() * stepTime;
        prediction3 = prediction;

        //Now shoot a rays using the prediction to find an actual point on a surface.
        RaycastHit hitInfo;
        updateRays(prediction, defaultPosition, normal, height);

        // Outwards to prediction
        if (shootRay(lineOutward, out hitInfo)) {
            Debug.Log("Got Targetpoint shooting outwards to prediction.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        //Straight down through prediction point
        if (shootRay(lineDown, out hitInfo)) {
            Debug.Log("Got Targetpoint shooting down to prediction.");
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
        Debug.Log("No ray was able to find a target position. Therefore i will return a default position.");
        uncomfortable = true;
        return new TargetInfo(defaultPosition + 0.25f * height * normal, normal);
    }

    private bool shootRay(Line l, out RaycastHit hitInfo) {
        Vector3 direction = (l.end - l.origin);
        float magnitude = Vector3.Magnitude(direction);
        direction = direction / magnitude;

        if (rayType == RayType.singleRay) {
            if (Physics.Raycast(l.origin, direction, out hitInfo, magnitude, spidercontroller.walkableLayer, QueryTriggerInteraction.Ignore)) {
                // could check if the normal is acceptable
                if (Mathf.Cos(Mathf.Deg2Rad*Vector3.Angle(direction, hitInfo.normal)) < 0) {
                    return true;
                }
            }
        }
        else {
            if (Physics.SphereCast(l.origin, radius, direction, out hitInfo, magnitude, spidercontroller.walkableLayer, QueryTriggerInteraction.Ignore)) {
                // could check if the normal is acceptable
                if (Mathf.Cos(Mathf.Deg2Rad * Vector3.Angle(direction, hitInfo.normal)) < 0) {
                    return true;
                }
            }
        }
        return false;
    }

    private void updateRays(Vector3 prediction, Vector3 defaultPosition, Vector3 normal, float height) {
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
    }

    // If im walking so fast that  one legs keeps wanna step after step complete, one leg might not step at all since its never able to
    // Could implement a some sort of a queue where i enqueue chains that want to step next?
    public void step(TargetInfo target) {
        if (isStepping) {
            return;
        }
        if (!allowedToStep()) {
            //I cant step but my target is not reachable. So i simply add the spiders movevector here
            ikChain.setTarget(new TargetInfo(ikChain.getTarget().position + spidercontroller.getMovement(),ikChain.getTarget().normal));
            return;
        }
        IEnumerator coroutineStepping = Step(target);
        StartCoroutine(coroutineStepping);
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

    public bool allowedToStep() {
        if (timeSinceLastStep < stepCooldown || (asyncChain != null && asyncChain.getIsStepping())) {
            return false;
        }
        return true;
    }

    public bool getIsStepping() {
        return isStepping;
    }

    public TargetInfo getDefault() {
        return new TargetInfo(spidercontroller.transform.TransformPoint(defaultPositionLocal), spidercontroller.transform.up);
    }


    void drawDebug() {
        //Target Point
        if (isStepping) DebugShapes.DrawPoint(ikChain.getTarget().position, Color.cyan, debugIconScale, stepTime);
        // Target Point while Stepping
        else DebugShapes.DrawPoint(ikChain.getTarget().position, Color.cyan, debugIconScale);

        // Default Position
        DebugShapes.DrawPoint(spidercontroller.transform.TransformPoint(defaultPositionLocal), Color.magenta, debugIconScale);

        //Draw the prediction process
        DebugShapes.DrawPoint(lastEndEffectorPos, Color.white, debugIconScale);
        DebugShapes.DrawPoint(lastDefaultPositionOfStep, new Color(1, 0, 1, 0.5f), debugIconScale);
        DebugShapes.DrawPoint(prediction1, Color.grey, debugIconScale);
        DebugShapes.DrawPoint(prediction2, Color.grey, debugIconScale);
        DebugShapes.DrawPoint(prediction3, Color.black, debugIconScale);
        Debug.DrawLine(lastEndEffectorPos, prediction1, Color.grey);
        Debug.DrawLine(prediction1, prediction2, Color.green);
        Debug.DrawLine(prediction2, prediction3, Color.grey);

        // MinDistance
        DebugShapes.DrawSphere(rootPos, minDistance, Color.red);
        //All the Raycasts:

        Debug.DrawLine(lineOutward.origin, lineOutward.end, Color.yellow);
        Debug.DrawLine(lineDown.origin, lineDown.end, 0.9f * Color.yellow);
        Debug.DrawLine(lineInwards.origin, lineInwards.end, 0.8f * Color.yellow);
        //Debug.DrawLine(lineDefaultOutward.origin, lineDefaultOutward.end, 0.7f * Color.yellow);
        //Debug.DrawLine(lineDefaultDown.origin, lineDefaultDown.end, 0.6f * Color.yellow);
        //Debug.DrawLine(lineDefaultInward.origin, lineDefaultInward.end, 0.5f * Color.yellow);

        if (rayType == RayType.sphereRay) {
            //DebugShapes.DrawSphere(lineOutward.origin, radius, Color.yellow);
            //DebugShapes.DrawSphere(lineDown.origin, radius, 0.9f * Color.yellow);
            //DebugShapes.DrawSphere(lineInwards.origin, radius, 0.8f * Color.yellow);
            //DebugShapes.DrawSphere(lineDefaultOutward.origin, radius, 0.7f * Color.yellow);
            //DebugShapes.DrawSphere(lineDefaultDown.origin, radius, 0.6f * Color.yellow);
            //DebugShapes.DrawSphere(lineDefaultInward.origin, radius, 0.5f * Color.yellow);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected() {
        if (!UnityEditor.Selection.Contains(transform.gameObject)) {
            return;
        }

        //Draw the two focus points
        DebugShapes.DrawPoint(spidercontroller.transform.TransformPoint(originPointOutwardsRay), Color.green, debugIconScale);
        DebugShapes.DrawPoint(spidercontroller.transform.TransformPoint(endPointInwardsRay), Color.green, debugIconScale);

        // Run Awake to set the pointers
        Awake();

        // Run Start to set all parameters (Since chainlength is used in this call we make sure it is set or else we return)
        if (ikChain.getChainLength() == 0) return;
        Start();

        //Default position
        Vector3 defaultPosition = spidercontroller.transform.TransformPoint(defaultPositionLocal);
        DebugShapes.DrawPoint(defaultPosition, Color.magenta, debugIconScale);

        // MinDistance
        DebugShapes.DrawSphere(rootPos, minDistance, Color.red);
    }
#endif
}
