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
    [Range(0.01f, 1.0f)]
    public float debugIconScale = 0.1f;

    [Header("Stepping")]

    public IKStepper asyncChain;
    public IKStepper syncChain;

    [Range(1.0f, 2.0f)]
    public float velocityPrediction = 1.5f;

    [Range(0, 10.0f)]
    public float stepTime;

    [Range(0.0f, 10.0f)]
    public float stepHeight;

    [Range(0.0f, 5.0f)]
    public float stepCooldown = 0.5f;
    private float timeSinceLastStep;

    [Range(0.0f, 2.0f)]
    public float stopSteppingAfterSecondsStill;

    public AnimationCurve stepAnimation;

    public CastMode castMode;
    public float radius;


    [Header("Default Position")]
    [Range(-1.0f, 1.0f)]
    public float defaultOffsetLength;
    [Range(-1.0f, 1.0f)]
    public float defaultOffsetHeight;
    [Range(-1.0f, 1.0f)]
    public float defaultOffsetStride;

    [Header("Ray Adjustments")]
    public Vector3 rayTopPosition;
    public Vector3 rayBottomPosition;
    [Range(0, 4.0f)]
    public float rayHeight;

    private IKChain ikChain;

    private bool isStepping = false;
    private bool waitingForStep = false;
    private float timeStandingStill;

    private float minDistance;

    private Dictionary<string, Cast> casts;

    private AHingeJoint rootJoint;
    private Vector3 defaultPositionLocal;
    private Vector3 frontalVectorLocal;
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
    }

    void Start() {
        // Set important parameters
        timeSinceLastStep = 2 * stepCooldown; // Make sure im allowed to step at start
        minDistance = 0.2f * ikChain.getChainLength();
        timeStandingStill = 0f;

        //Set Default Position
        defaultPositionLocal = calculateDefault();

        // Calc frontal vector
        frontalVectorLocal = Vector3.ProjectOnPlane(defaultPositionLocal - rayTopPosition, spider.transform.up).normalized * ikChain.getChainLength(); ;

        // Initialize prediction
        prediction = getDefault();

        // Initialize Casts as either RayCast or SphereCast 
        initializeCasts();
    }

    private Vector3 calculateDefault() {
        float chainLength = ikChain.getChainLength();
        float diameter = chainLength - minDistance;

        //Be careful with the use of transform.up and rootjoint.getRotationAxis(). In my case they are equivalent with the exception of the right side being inverted.
        //However they can be different and this has to be noticed here. The code below is probably wrong for the general case.
        Vector3 normal = spider.transform.up;

        Vector3 toEnd = ikChain.getEndEffector().position - rootJoint.getRotationPoint();
        toEnd = Vector3.ProjectOnPlane(toEnd, normal).normalized;
        Vector3 p = rootJoint.getRotationPoint() - spider.getCapsuleCollider().radius * normal;

        Vector3 midOrient = Quaternion.AngleAxis(0.5f * (rootJoint.maxAngle + rootJoint.minAngle), rootJoint.getRotationAxis()) * toEnd;

        //Set the following debug variables for the DOF Arc
        minOrient = spider.transform.InverseTransformDirection(Quaternion.AngleAxis(rootJoint.minAngle, rootJoint.getRotationAxis()) * toEnd);
        maxOrient = spider.transform.InverseTransformDirection(Quaternion.AngleAxis(rootJoint.maxAngle, rootJoint.getRotationAxis()) * toEnd);

        Vector3 def = p + (minDistance + 0.5f * diameter) * midOrient;

        def += defaultOffsetLength * 0.5f * diameter * midOrient;
        def += defaultOffsetHeight * rayHeight * normal; //Would want to use spider.transform.up instead?
        def += defaultOffsetStride * Vector3.Cross(midOrient, rootJoint.getRotationAxis()) * ((minDistance + (0.5f * (1f + defaultOffsetLength) * diameter)) / chainLength) * Mathf.Sin(0.5f * rootJoint.getAngleRange());

        return spider.transform.InverseTransformPoint(def);
    }

    /*
     * This method defines the RayCasts/SphereCasts in a dictionary with a corresponding key
     * The order in which they appear in the dictionary is the order in which they will be casted.
     * This order is of very high importance, so choose smartly.
     */
    private void initializeCasts() {

        Transform parent = spider.transform;
        Vector3 normal = parent.up;
        Vector3 defaultPos = getDefault();
        Vector3 top = getTop();
        Vector3 bottom = getBottom();
        Vector3 bottomClose = parent.position - 2f * spider.col.radius * spider.scale * normal;
        Vector3 frontal = getFrontalVector();
        prediction = defaultPos;
        float r = spider.scale * radius;

        // Note that Prediction Out will never hit a targetpoint on a flat surface or hole since it stop at the prediction point which is on
        // default height, that is the height where the collider stops.
        casts = new Dictionary<string, Cast> {
            { "Frontal", getCast(top, top + frontal, r, parent, parent) },
            { "Prediction Out", getCast(top, prediction, r, parent, null) },
            { "Prediction Down", getCast(prediction + normal * rayHeight, prediction - normal * rayHeight, r, null, null) },
            { "Prediction In", getCast(prediction, bottom, r, null, parent) },
            { "Prediction In Close", getCast(prediction, bottomClose, r, null, parent) },
            { "Default Down", getCast(defaultPos + normal * rayHeight, defaultPos - normal * rayHeight, r, parent, parent) },
            { "Default Out", getCast(top, defaultPos, r, parent, parent) },
            { "Default In", getCast(defaultPos, bottom, r, parent, parent) }
        };
    }

    /*
     * Depending on the cast mode selected this method returns either a RayCast or a SphereCast with the given start, end and parents.
     * The parameter radius is redundant if cast mode Raycast is selected but needed for the SphereCast.
     */
    private Cast getCast(Vector3 start, Vector3 end, float radius, Transform parentStart, Transform parentEnd) {
        if (castMode == CastMode.RayCast) return new RayCast(start, end, parentStart, parentEnd);
        else return new SphereCast(start, end, radius, parentStart, parentEnd);
    }

    private void FixedUpdate() {
        if (!ikChain.IKStepperActivated()) return;

        // Increase timers
        timeSinceLastStep += Time.fixedDeltaTime;
        if (!spider.getIsMoving()) timeStandingStill += Time.fixedDeltaTime;
        else timeStandingStill = 0f;

        // If im currently in the stepping process i have no business doing anything besides that.
        if (isStepping || waitingForStep) return;

        // If ive been standing still for a certain time, i dont allow any more stepping. This fixes the indefinite stepping going on.
        if (timeStandingStill > stopSteppingAfterSecondsStill) {
            return;
        }

        //If current target uncomfortable and there is a new comfortable target, step
        if (!ikChain.getTarget().comfortable) step();


        //If the error of the IK solver gets to big, that is if it cant solve for the current target appropriately anymore, step.
        // This is the main way this class determines if it needs to step.
        else if (ikChain.getError() > IKSolver.tolerance) step();


        // Alternativaly step if too close to root joint
        else if (Vector3.Distance(rootJoint.getRotationPoint(), ikChain.getTarget().position) < minDistance) step();


        // Force Step by Pressing Space
        else if (Input.GetKeyDown(KeyCode.Space)) step();
    }

    private void Update() {
        if (showDebug) drawDebug();
    }

    /*
     * Calculates a new target using the endeffector Position and a default position defined in this class.
     * The new target position is a movement towards the default position but overshoots the default position using the velocity prediction
     * Moreover the movement per second of the player multiplied with the steptime is added to the prediction to account for movement while stepping.
     */
    private TargetInfo calcNewTarget() {

        Vector3 endeffectorPosition = ikChain.getEndEffector().position;
        Vector3 defaultPosition = getDefault();
        Vector3 normal = spider.transform.up;

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
        Vector3 start = Vector3.ProjectOnPlane(endeffectorPosition, normal);
        start = spider.transform.InverseTransformPoint(start);
        start.y = defaultPositionLocal.y;
        start = spider.transform.TransformPoint(start);

        Vector3 overshoot = start + (defaultPosition - start) * velocityPrediction;
        prediction = overshoot + spider.getCurrentVelocityPerSecond() * stepTime;

        //Debug variables
        lastEndEffectorPos = endeffectorPosition;
        projPrediction = start;
        overshootPrediction = overshoot;

        //Now shoot rays using the prediction to find an actual point on a surface.
        RaycastHit hitInfo;
        int layer = spider.walkableLayer;

        //Update Casts for new prediction point. Do this more smartly?
        foreach (var cast in casts) {
            if (cast.Key == "Prediction Out") { cast.Value.setEnd(prediction); }
            if (cast.Key == "Prediction Down") { cast.Value.setOrigin(prediction + normal * rayHeight); cast.Value.setEnd(prediction - normal * rayHeight); }
            if (cast.Key == "Prediction In") { cast.Value.setOrigin(prediction); }
            if (cast.Key == "Prediction In Close") { cast.Value.setOrigin(prediction); }
        }

        //Iterate through all casts to until i find a target position.
        foreach (var cast in casts) {
            if (cast.Value.castRay(out hitInfo, layer)) {
                if (printDebugLogs) Debug.Log("Got a target point from the cast '" + cast.Key + "'.");
                lastHitRay = cast.Key;
                return new TargetInfo(hitInfo.point, hitInfo.normal);
            }
        }

        // Return default position
        if (printDebugLogs) Debug.Log("No ray was able to find a target position. Therefore i will return a default position.");
        return new TargetInfo(defaultPosition + 0.5f * rayHeight * normal, normal, false);
    }

    /*
    * If im walking so fast that  one legs keeps wanna step after step complete, one leg might not step at all since its never able to
    * Could implement a some sort of a queue where i enqueue chains that want to step next?
    */
    private void step() {
        IEnumerator coroutineStepping = Step();
        StartCoroutine(coroutineStepping);
        if (syncChain != null) syncChain.step();
    }

    /*
    * Coroutine for stepping since i want to actually see the stepping process.
    * If im not allowed to step yet (this happens if the async leg is currently stepping or my step cooldown hasnt finished yet,
    * then ill wait until i can.
    */
    private IEnumerator Step() {
        if (pauseOnStep) Debug.Break();

        // First wait until im allowed to step
        if (!allowedToStep()) {
            if (printDebugLogs) Debug.Log(gameObject.name + " is waiting for step now.");
            waitingForStep = true;
            ikChain.setTarget(new TargetInfo(ikChain.getTarget().position + 2f * spider.getCurrentVelocityPerFixedFrame() + 0.1f * rayHeight * spider.transform.up, ikChain.getTarget().normal));
            yield return null;
            ikChain.pauseSolving();

            while (!allowedToStep()) {
                yield return null;
            }
            ikChain.unpauseSolving();
            waitingForStep = false;
        }

        // Then start the stepping
        if (pauseOnStep) Debug.Break();
        if (printDebugLogs) Debug.Log(gameObject.name + " starts stepping now.");
        TargetInfo newTarget = calcNewTarget();

        // We only step between comfortable positions. Otherwise we would be in the case of leg in air where we dont want to indefinitely step.
        // Try to think of a different way to implement this without using the comfortable parameter?
        if (ikChain.getTarget().comfortable || newTarget.comfortable) {
            isStepping = true;
            TargetInfo lastTarget = ikChain.getTarget();
            TargetInfo lerpTarget;
            float time = 0;

            while (time < stepTime) {
                lerpTarget.position = Vector3.Lerp(lastTarget.position, newTarget.position, time / stepTime) + stepHeight * stepAnimation.Evaluate(time / stepTime) * spider.transform.up;
                lerpTarget.normal = Vector3.Lerp(lastTarget.normal, newTarget.normal, time / stepTime);
                lerpTarget.comfortable = true;

                time += Time.deltaTime;
                ikChain.setTarget(lerpTarget);
                yield return null;
            }
            isStepping = false;
            timeSinceLastStep = 0.0f;
        }

        ikChain.setTarget(newTarget);
        if (printDebugLogs) Debug.Log(gameObject.name + " completed stepping.");
    }

    private bool allowedToStep() {
        if (!ikChain.getTarget().comfortable) {
            return true;
        }
        if (timeSinceLastStep < stepCooldown) {
            return false;
        }
        if (asyncChain != null && asyncChain.getIsStepping()) {
            return false;
        }
        //Carefull here could end in infinite loop
        if (syncChain != null && syncChain.asyncChain != null && syncChain.asyncChain.getIsStepping()) {
            return false;
        }
        return true;
    }

    private bool getIsStepping() {
        return isStepping;
    }

    private Vector3 getDefault() {
        return spider.transform.TransformPoint(defaultPositionLocal);
    }

    private Vector3 getTop() {
        return spider.transform.TransformPoint(rayTopPosition);
    }

    private Vector3 getBottom() {
        return spider.transform.TransformPoint(rayBottomPosition);
    }

    private Vector3 getFrontalVector() {
        return spider.transform.TransformDirection(frontalVectorLocal);
    }

    private void drawDebug(bool points = true, bool steppingProcess = true, bool rayCasts = true, bool DOFArc = true) {

        if (points) {
            // Default Position
            DebugShapes.DrawPoint(getDefault(), Color.magenta, debugIconScale);

            //Draw the top and bottom ray points
            DebugShapes.DrawPoint(getTop(), Color.green, debugIconScale);
            DebugShapes.DrawPoint(getBottom(), Color.green, debugIconScale);

            //Target Point
            if (isStepping) DebugShapes.DrawPoint(ikChain.getTarget().position, Color.cyan, debugIconScale, 2 * stepTime);
            else DebugShapes.DrawPoint(ikChain.getTarget().position, Color.cyan, debugIconScale);
        }

        if (steppingProcess) {
            //Draw the prediction process
            DebugShapes.DrawPoint(lastEndEffectorPos, Color.white, debugIconScale);
            DebugShapes.DrawPoint(projPrediction, Color.grey, debugIconScale);
            DebugShapes.DrawPoint(overshootPrediction, Color.green, debugIconScale);
            DebugShapes.DrawPoint(prediction, Color.red, debugIconScale);
            Debug.DrawLine(lastEndEffectorPos, projPrediction, Color.white);
            Debug.DrawLine(projPrediction, overshootPrediction, Color.grey);
            Debug.DrawLine(overshootPrediction, prediction, Color.green);
        }

        if (rayCasts) {
            foreach (var cast in casts) {
                if (cast.Key == lastHitRay) cast.Value.draw(new Color(1.0f, 0.5f, 0f, 1f));
                else cast.Value.draw(Color.yellow);
            }
        }

        if (DOFArc) {
            Vector3 v = spider.transform.TransformDirection(minOrient);
            Vector3 w = spider.transform.TransformDirection(maxOrient);
            Vector3 p = spider.transform.InverseTransformPoint(rootJoint.getRotationPoint());
            p.y = defaultPositionLocal.y;
            p = spider.transform.TransformPoint(p);
            DebugShapes.DrawCircleSection(p, v, w, minDistance, ikChain.getChainLength(), Color.red);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected() {
        if (UnityEditor.EditorApplication.isPlaying) return;
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;
        if (!showDebug) return;

        // Run Awake to set the pointers
        Awake();

        // Run Start to set all parameters (Since chainlength is used in this call we make sure it is set or else we return)
        if (ikChain.getChainLength() == 0) return;
        Start();
        drawDebug(true, false, true, true);
    }
#endif
}
