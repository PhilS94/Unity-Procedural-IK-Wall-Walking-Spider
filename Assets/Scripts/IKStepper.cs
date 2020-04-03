using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Raycasting;

[RequireComponent(typeof(IKChain))]
public class IKStepper : MonoBehaviour {

    public Spider spider;

    [Header("Debug")]

    public bool showDebug;
    public bool printDebugLogs;
    public bool pauseOnStep = false;
    [Range(1, 10.0f)]
    public float debugIconScale;

    [Header("Step Layer")]
    public LayerMask stepLayer;

    [Header("Leg Synchronicity")]
    public IKStepper[] asyncChain;

    [Header("Step Timing")]
    [Range(0.0f, 5.0f)]
    public float stepCooldown = 0.0f;
    private float timeSinceLastStep;

    [Range(0.0f, 2.0f)]
    public float stopSteppingAfterSecondsStill;

    [Header("Step Transition")]
    [Range(0.0f, 10.0f)]
    public float stepHeight;
    public AnimationCurve stepAnimation;

    [Header("Default Position")]
    [Range(-1.0f, 1.0f)]
    public float defaultOffsetLength;
    [Range(-1.0f, 1.0f)]
    public float defaultOffsetHeight;
    [Range(-1.0f, 1.0f)]
    public float defaultOffsetStride;

    [Header("Default Overshoot Multiplier")]
    [Range(1.0f, 2.0f)]
    public float defaultOvershootMultiplier = 1.5f;

    [Header("Last Resort IK Target Position")]
    [Range(0f, 1.0f)]
    public float lastResortHeight;

    [Header("Ray Casting")]
    public CastMode castMode;
    public float radius;

    [Header("Frontal Ray")]
    [Range(0f, 1f)]
    public float rayFrontalHeight;
    [Range(0f, 1f)]
    public float rayFrontalLength;
    [Range(0f, 1f)]
    public float rayFrontalOriginOffset;

    [Header("Outwards Ray")]
    public Vector3 rayTopFocalPoint;
    [Range(0f, 1f)]
    public float rayOutwardsOriginOffset;
    [Range(0f, 1f)]
    public float rayOutwardsEndOffset;

    [Header("Downwards Ray")]
    [Range(0f, 6f)]
    public float downRayHeight;
    [Range(0f, 6f)]
    public float downRayDepth;

    [Header("Inwards Ray")]
    public Vector3 rayBottomFocalPoint;
    [Range(0f, 1f)]
    public float rayInwardsEndOffset;


    private IKChain ikChain;

    private bool isStepping = false;
    private float timeStandingStill;

    private float minDistance;

    private Dictionary<string, Cast> casts;
    RaycastHit hitInfo;

    private AHingeJoint rootJoint;
    private float chainLength;
    private Vector3 defaultPositionLocal;
    private Vector3 lastResortPositionLocal;
    private Vector3 frontalStartPositionLocal;
    private Vector3 prediction;

    //Debug Variables
    private Vector3 lastEndEffectorPos;
    private Vector3 projPrediction;
    private Vector3 overshootPrediction;
    private Vector3 minOrient;
    private Vector3 maxOrient;
    private string lastHitRay;


    private void Awake() {
        ikChain = GetComponent<IKChain>();
        rootJoint = ikChain.getRootJoint();
        timeSinceLastStep = 2 * stepCooldown; // Make sure im allowed to step at start
        timeStandingStill = 0f;

        //Let the chainlength be calculated and set it for future access
        chainLength = ikChain.calculateChainLength();

        //Set the distance which the root joint and the endeffector are allowed to have. If below this distance, stepping is forced.
        minDistance = 0.2f * chainLength;

        //Set Default Position
        defaultPositionLocal = calculateDefault();
        lastResortPositionLocal = defaultPositionLocal + new Vector3(0, lastResortHeight * 4f * spider.getNonScaledColliderRadius(), 0);
        frontalStartPositionLocal = new Vector3(0, rayFrontalHeight * 4f * spider.getNonScaledColliderRadius(), 0);

        // Initialize prediction
        prediction = getDefault();

        //Set debug variables so they dont draw as lines to the zero point
        lastEndEffectorPos = prediction;
        projPrediction = prediction;
        overshootPrediction = prediction;

        // Initialize Casts as either RayCast or SphereCast 
        casts = new Dictionary<string, Cast>();
        updateCasts();
    }

    private Vector3 calculateDefault() {
        float diameter = chainLength - minDistance;
        Vector3 rootRotAxis = rootJoint.getRotationAxis();

        //Be careful with the use of transform.up and rootjoint.getRotationAxis(). In my case they are equivalent with the exception of the right side being inverted.
        //However they can be different and this has to be noticed here. The code below is probably wrong for the general case.
        Vector3 normal = spider.transform.up;

        Vector3 toEnd = ikChain.getEndEffector().position - rootJoint.getRotationPoint();
        toEnd = Vector3.ProjectOnPlane(toEnd, normal).normalized;

        Vector3 pivot = spider.getColliderBottomPoint() + Vector3.ProjectOnPlane(rootJoint.getRotationPoint() - spider.transform.position, normal);

        Vector3 midOrient = Quaternion.AngleAxis(0.5f * (rootJoint.maxAngle + rootJoint.minAngle), rootRotAxis) * toEnd;

        //Set the following debug variables for the DOF Arc
        minOrient = spider.transform.InverseTransformDirection(Quaternion.AngleAxis(rootJoint.minAngle, rootRotAxis) * toEnd);
        maxOrient = spider.transform.InverseTransformDirection(Quaternion.AngleAxis(rootJoint.maxAngle, rootRotAxis) * toEnd);

        // Now set the default position using the given stride, length and height
        Vector3 defOrientation = Quaternion.AngleAxis(defaultOffsetStride * 0.5f * rootJoint.getAngleRange(), rootRotAxis) * midOrient;
        Vector3 def = pivot;
        def += (minDistance + 0.5f * (1f + defaultOffsetLength) * diameter) * defOrientation;
        def += defaultOffsetHeight * 2f * spider.getColliderRadius() * rootRotAxis;
        return spider.transform.InverseTransformPoint(def);
    }

    /*
     * This method defines the RayCasts/SphereCasts and stores them in a dictionary with a corresponding key
     * The order in which they appear in the dictionary is the order in which they will be casted.
     * This order is of very high importance, so choose smartly.
     */
    private void updateCasts() {

        Vector3 defaultPos = getDefault();
        Vector3 normal = spider.transform.up;

        //Frontal Parameters
        Vector3 frontal = getFrontalStartPosition();
        Vector3 frontalPredictionEnd = frontal + Vector3.ProjectOnPlane(prediction - frontal, spider.transform.up).normalized * rayFrontalLength * chainLength;
        Vector3 frontalDefaultEnd = frontal + Vector3.ProjectOnPlane(defaultPos - frontal, spider.transform.up).normalized * rayFrontalLength * chainLength;
        Vector3 frontalPredictionOrigin = Vector3.Lerp(frontal, frontalPredictionEnd, rayFrontalOriginOffset);
        Vector3 frontalDefaultOrigin = Vector3.Lerp(frontal, frontalDefaultEnd, rayFrontalOriginOffset);

        //Outwards Parameters
        Vector3 top = getTopFocalPoint();
        Vector3 topPredictionEnd = top + 2 * (prediction - top);
        Vector3 topDefaultEnd = top + 2 * (defaultPos - top);

        Vector3 outwardsPredictionOrigin = Vector3.Lerp(top, prediction, rayOutwardsOriginOffset);
        Vector3 outwardsPredictionEnd = Vector3.Lerp(prediction, topPredictionEnd, rayOutwardsEndOffset);
        Vector3 outwardsDefaultOrigin = Vector3.Lerp(top, defaultPos, rayOutwardsOriginOffset);
        Vector3 outwardsDefaultEnd = Vector3.Lerp(prediction, topDefaultEnd, rayOutwardsEndOffset);


        //Downwards Parameters
        float height = downRayHeight * spider.getColliderRadius();
        float depth = downRayDepth * spider.getColliderRadius();
        Vector3 downwardsPredictionOrigin = prediction + normal * height;
        Vector3 downwardsPredictionEnd = prediction - normal * depth;
        Vector3 downwardsDefaultOrigin = defaultPos + normal * height;
        Vector3 downwardsDefaultEnd = defaultPos - normal * depth;

        //Inwards Parameters
        Vector3 bottom = getBottomFocalPoint();
        Vector3 bottomBorder = spider.transform.position - 1.5f * spider.getColliderRadius() * normal;
        Vector3 bottomMid = spider.transform.position - 4f * spider.getColliderRadius() * normal;

        float inwardsPredictionLength = rayInwardsEndOffset * Vector3.Distance(bottom, prediction);
        float inwardsDefaultLength = rayInwardsEndOffset * Vector3.Distance(bottom, defaultPos);

        Vector3 inwardsPredictionEndClose = bottomBorder;
        Vector3 inwardsPredictionEndMid = bottomMid;
        Vector3 inwardsPredictionEndFar = Vector3.Lerp(prediction, bottom, inwardsPredictionLength / Vector3.Distance(prediction, bottom));

        Vector3 inwardsDefaultEndClose = bottomBorder;
        Vector3 inwardsDefaultEndMid = bottomMid;
        Vector3 inwardsDefaultEndFar = Vector3.Lerp(defaultPos, bottom, inwardsDefaultLength / Vector3.Distance(prediction, bottom));

        //The scaled radius
        float r = spider.getScale() * radius;

        // Note that Prediction Out will never hit a targetpoint on a flat surface or hole since it stop at the prediction point which is on
        // default height, that is the height where the collider stops.
        casts.Clear();
        casts = new Dictionary<string, Cast> {
            { "Prediction Frontal", getCast(frontalPredictionOrigin, frontalPredictionEnd, r) },
            { "Prediction Out", getCast(outwardsPredictionOrigin,outwardsPredictionEnd, r) },
            { "Prediction Down", getCast(downwardsPredictionOrigin,downwardsPredictionEnd, r) },
            { "Prediction In Far", getCast(prediction,inwardsPredictionEndFar,r) },
            { "Prediction In Mid", getCast(prediction, inwardsPredictionEndMid, r) },
            { "Prediction In Close", getCast(prediction, inwardsPredictionEndClose, r) },

            { "Default Frontal", getCast(frontalDefaultOrigin, frontalDefaultEnd, r) },
            { "Default Out", getCast(outwardsDefaultOrigin,outwardsDefaultEnd, r) },
            { "Default Down", getCast(downwardsDefaultOrigin,downwardsDefaultEnd, r) },
            { "Default In Far", getCast(defaultPos,inwardsDefaultEndFar, r) },
            { "Default In Mid", getCast(defaultPos, inwardsDefaultEndMid, r) },
            { "Default In Close", getCast(defaultPos, inwardsDefaultEndClose, r) },
        };
    }

    /*
     * Depending on the cast mode selected this method returns either a RayCast or a SphereCast with the given start, end and parents.
     * The parameter radius is redundant if cast mode Raycast is selected but needed for the SphereCast.
     */
    private Cast getCast(Vector3 start, Vector3 end, float radius, Transform parentStart = null, Transform parentEnd = null) {
        if (castMode == CastMode.RayCast) return new RayCast(start, end, parentStart, parentEnd);
        else return new SphereCast(start, end, radius, parentStart, parentEnd);
    }

    public bool stepCheck() {
        // If im currently in the stepping process i have no business doing anything besides that.
        if (isStepping) return false;

        // If ive been standing still for a certain time, i dont allow any more stepping. This fixes the indefinite stepping going on.
        if (timeStandingStill > stopSteppingAfterSecondsStill) {
            return false;
        }

        //If current target not grounded step
        if (!ikChain.getTarget().grounded) return true;

        //If the error of the IK solver gets too big, that is if it cant solve for the current target appropriately anymore, step.
        // This is the main way this class determines if it needs to step.
        else if (ikChain.getError() > ikChain.getTolerance()) return true;

        // Alternativaly step if too close to root joint
        else if (Vector3.Distance(rootJoint.getRotationPoint(), ikChain.getTarget().position) < minDistance) return true;

        return false;
    }

    private void Update() {
        timeSinceLastStep += Time.deltaTime;
        if (!spider.getIsMoving()) timeStandingStill += Time.deltaTime;
        else timeStandingStill = 0f;

#if UNITY_EDITOR
        if (showDebug && UnityEditor.Selection.Contains(transform.gameObject)) drawDebug();
#endif
    }

    private Vector3 calculateDesiredPosition() {
        Vector3 endeffectorPosition = ikChain.getEndEffector().position;
        Vector3 defaultPosition = getDefault();
        Vector3 normal = spider.transform.up;

        // Option 1: Include spider movement in the prediction process: prediction += SpiderMoveVector * stepTime
        //      Problem:    Spider might stop moving while stepping, if this happens i will over predict
        //                  Spider might change direction while stepping, if this happens i could predict out of range
        //      Solution:   Keep the stepTime short such that not much will happen

        // Option 2: Dynamically update the prediction in a the stepping coroutine where i keep up with the spider with its local coordinates
        //      Problem:    I will only know if the foot lands on a surface point after the stepping is already done
        //                  This means the foot could land in a bump on the ground or in the air, and i will have to look what i will do from there
        //                  Update the position within the last frame (unrealistic) or start another different stepping coroutine?
        //                  Or shoot more rays in the stepping process to somewhat adjust to the terrain changes?


        // For now I choose Option 1

        // Level end effector position with the default position in regards to the normal
        Vector3 start = Vector3.ProjectOnPlane(endeffectorPosition, normal);
        start = spider.transform.InverseTransformPoint(start);
        start.y = defaultPositionLocal.y;
        start = spider.transform.TransformPoint(start);

        //Debug value
        projPrediction = start;
        lastEndEffectorPos = endeffectorPosition;

        // Overshoot by velocity prediction
        return start + (defaultPosition - start) * defaultOvershootMultiplier;
    }

    /*
     * Calculates a new target using the endeffector Position and a default position defined in this class.
     * The new target position is a movement towards the default position but overshoots the default position using the velocity prediction
     * Moreover the movement per second of the player multiplied with the steptime is added to the prediction to account for movement while stepping.
     */
    private TargetInfo findTargetOnSurface() {

        LayerMask layer = stepLayer;

        //If there is no collider in reach there is no need to try to find a surface point, just return default here.
        //This should cut down runtime cost if the spider is not grounded (e.g. in the air).
        //However this does add an extra calculation if grounded, increasing it slighly.
        if (Physics.OverlapSphere(rootJoint.getRotationPoint(), chainLength, layer, QueryTriggerInteraction.Ignore) == null) {
            return getLastResortTarget();
        }

        //Update Casts for new prediction point. Do this more smartly?
        updateCasts();

        //Now shoot rays using the casts to find an actual point on a surface.

        //Iterate through all casts to until i find a target position.
        foreach (var cast in casts) {

            //If the spider cant see the ray origin there is no need to cast: Consider using a different layer here, since there can be objects in the way that are not walkable surfaces.
            if (new RayCast(spider.transform.position, cast.Value.getOrigin()).castRay(out hitInfo, layer)) continue;

            if (cast.Value.castRay(out hitInfo, layer)) {

                //For the frontal ray we only allow not too steep slopes, that is +-65°
                if (cast.Key == "Frontal" && Vector3.Angle(cast.Value.getDirection(), hitInfo.normal) < 180f - 65f) continue;

                if (printDebugLogs) Debug.Log("Got a target point from the cast '" + cast.Key + "'lastHitRay = cast.Key.");
                lastHitRay = cast.Key;
                return new TargetInfo(hitInfo.point, hitInfo.normal);

            }
        }

        // Return default position
        if (printDebugLogs) Debug.Log("No ray was able to find a target position. Therefore i will return a default position.");
        return getLastResortTarget();
    }

    /*
    * If im walking so fast that  one legs keeps wanna step after step complete, one leg might not step at all since its never able to
    * Could implement a some sort of a queue where i enqueue chains that want to step next?
    */
    public void step(float stepTime) {
        IEnumerator coroutineStepping = Step(stepTime);
        StartCoroutine(coroutineStepping);
    }

    /*
    * Coroutine for stepping.
    * If im not allowed to step yet (this happens if one of the async legs is currently stepping or my step cooldown hasnt finished yet,
    * then ill wait until i can.
    */
    private IEnumerator Step(float stepTime) {
        if (pauseOnStep) Debug.Break();

        if (printDebugLogs) Debug.Log(gameObject.name + " starts stepping now.");

        //Calculate desired position
        Vector3 desiredPosition = calculateDesiredPosition();

        //Get the current velocity of the end effector
        Vector3 endEffectorVelocity = ikChain.getEndeffectorVelocityPerSecond();



        // Correct the desired position with the current velocity since the spider will probably move away while stepping and set it to prediction
        prediction = desiredPosition + endEffectorVelocity * stepTime;

        // Finally find an actual target which lies on a surface point using the calculated prediction with raycasting
        TargetInfo newTarget = findTargetOnSurface();

        // We only step if either the old target or new one is grounded. Otherwise we would be in the case of leg in air where we dont want to step.
        // Try to think of a different way to implement this without using the grounded parameter?
        if (ikChain.getTarget().grounded || newTarget.grounded) {
            isStepping = true;
            TargetInfo lastTarget = ikChain.getTarget();
            TargetInfo lerpTarget;
            float time = Time.deltaTime;

            while (time < stepTime) {
                lerpTarget.position = Vector3.Lerp(lastTarget.position, newTarget.position, time / stepTime) + stepHeight * 0.01f * spider.getScale() * stepAnimation.Evaluate(time / stepTime) * spider.transform.up;
                lerpTarget.normal = Vector3.Lerp(lastTarget.normal, newTarget.normal, time / stepTime);
                lerpTarget.grounded = false;

                time += Time.deltaTime;
                ikChain.setTarget(lerpTarget);
                yield return null;
            }
            isStepping = false;
            timeSinceLastStep = 0.0f;
        }

        ikChain.setTarget(newTarget);
        if (printDebugLogs) Debug.Log(gameObject.name + " completed stepping.");

        //Debug variable
        overshootPrediction = desiredPosition;
    }

    public bool allowedToStep() {
        if (!ikChain.getTarget().grounded) {
            return true;
        }
        if (timeSinceLastStep < stepCooldown) {
            return false;
        }

        foreach (var chain in asyncChain) {
            if (chain.getIsStepping()) {
                return false;
            }
        }
        return true;
    }

    private bool getIsStepping() {
        return isStepping;
    }

    private Vector3 getDefault() {
        return spider.transform.TransformPoint(defaultPositionLocal);
    }
    public TargetInfo getDefaultTarget() {
        return new TargetInfo(getDefault(), spider.transform.up);
    }
    private Vector3 getLastResort() {
        return spider.transform.TransformPoint(lastResortPositionLocal);
    }
    private TargetInfo getLastResortTarget() {
        return new TargetInfo(getLastResort(), spider.transform.up, false);
    }

    private Vector3 getTopFocalPoint() {
        return spider.transform.TransformPoint(rayTopFocalPoint);
    }

    private Vector3 getBottomFocalPoint() {
        return spider.transform.TransformPoint(rayBottomFocalPoint);
    }

    private Vector3 getFrontalStartPosition() {
        return spider.transform.TransformPoint(frontalStartPositionLocal);
    }

    public IKChain getIKChain() {
        return ikChain;
    }

    private void drawDebug(bool points = true, bool steppingProcess = true, bool rayCasts = true, bool DOFArc = true) {

        float scale = spider.getScale() * 0.0001f * debugIconScale;
        if (points) {
            // Default Position
            DebugShapes.DrawPoint(getDefault(), Color.magenta, scale);

            // Last Resort Position
            DebugShapes.DrawPoint(getLastResortTarget().position, Color.cyan, scale);


            //Draw the top and bottom ray points
            DebugShapes.DrawPoint(getTopFocalPoint(), Color.green, scale);
            DebugShapes.DrawPoint(getBottomFocalPoint(), Color.green, scale);

            //Target Point
            if (isStepping) DebugShapes.DrawPoint(ikChain.getTarget().position, Color.cyan, scale, 0.2f);
            else DebugShapes.DrawPoint(ikChain.getTarget().position, Color.cyan, scale);
        }

        if (steppingProcess) {
            //Draw the prediction process
            DebugShapes.DrawPoint(lastEndEffectorPos, Color.white, scale);
            DebugShapes.DrawPoint(projPrediction, Color.grey, scale);
            DebugShapes.DrawPoint(overshootPrediction, Color.green, scale);
            DebugShapes.DrawPoint(prediction, Color.yellow, scale);
            Debug.DrawLine(lastEndEffectorPos, projPrediction, Color.white);
            Debug.DrawLine(projPrediction, overshootPrediction, Color.grey);
            Debug.DrawLine(overshootPrediction, prediction, Color.green);
        }

        if (rayCasts) {
            Color col = Color.black;
            foreach (var cast in casts) {
                if (cast.Key.Contains("Default")) col = Color.magenta;
                else if (cast.Key.Contains("Prediction")) col = Color.yellow;

                if (cast.Key != lastHitRay) col = Color.Lerp(col, Color.white, 0.5f);
                cast.Value.draw(col);
            }
        }

        if (DOFArc) {
            Vector3 v = spider.transform.TransformDirection(minOrient);
            Vector3 w = spider.transform.TransformDirection(maxOrient);
            Vector3 p = spider.transform.InverseTransformPoint(rootJoint.getRotationPoint());
            p.y = defaultPositionLocal.y;
            p = spider.transform.TransformPoint(p);
            DebugShapes.DrawCircleSection(p, v, w, rootJoint.getRotationAxis(), minDistance, chainLength, Color.red);
        }
    }




#if UNITY_EDITOR
    private void OnDrawGizmosSelected() {
        if (UnityEditor.EditorApplication.isPlaying) return;
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;
        if (!showDebug) return;

        // Run Awake to set the pointers
        Awake();

        drawDebug(true, false, true, true);
    }
#endif
}
