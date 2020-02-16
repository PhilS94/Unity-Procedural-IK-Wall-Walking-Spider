using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Raycasting;

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

    public CastMode castMode;
    public float radius;


    [Header("Default Position")]
    [Range(-1.0f, 1.0f)]
    public float defaultOffsetLength;
    [Range(-1.0f, 1.0f)]
    public float defaultOffsetHeight;
    [Range(-1.0f, 1.0f)]
    public float defaultOffsetStride;

    [Header("Ray Points")]
    public Vector3 rayTopPosition;
    public Vector3 rayBottomPosition;

    private IKChain ikChain;

    private bool isStepping = false;

    private float minDistance;
    private float height = 2.0f;

    private Cast castFrontal;
    private Cast castDown;
    private Cast castOutward;
    private Cast castInwards;
    private Cast castDefaultDown;
    private Cast castDefaultOutward;
    private Cast castDefaultInward;

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


    private void Awake() {
        ikChain = GetComponent<IKChain>();
        spidercontroller = ikChain.spiderController;
        rootJoint = ikChain.getRootJoint();
    }

    void Start() {
        // Set important parameters
        timeSinceLastStep = 2 * stepCooldown; // Make sure im allowed to step at start
        minDistance = 0.2f * ikChain.getChainLength(); ;

        //Set Default Position
        defaultPositionLocal = calculateDefault();

        // Calc frontal vector
        frontalVectorLocal = Vector3.ProjectOnPlane(defaultPositionLocal - rayTopPosition, spidercontroller.transform.up).normalized * ikChain.getChainLength(); ;

        // Initialize prediction
        prediction = getDefault();

        // Initialize Casts as either RayCast or SphereCast 
        initializeCasts();
    }

    private Vector3 calculateDefault() {
        float chainLength = ikChain.getChainLength();
        Vector3 toEnd = (Vector3.ProjectOnPlane(ikChain.getEndEffector().position - rootJoint.getRotationPoint(), rootJoint.getRotationAxis())).normalized;
        Vector3 midOrient = Quaternion.AngleAxis(0.5f * (rootJoint.maxAngle + rootJoint.minAngle), rootJoint.getRotationAxis()) * toEnd;

        //Set the following debug variables for the DOF Arc
        minOrient = spidercontroller.transform.InverseTransformDirection(Quaternion.AngleAxis(rootJoint.minAngle, rootJoint.getRotationAxis()) * toEnd);
        maxOrient = spidercontroller.transform.InverseTransformDirection(Quaternion.AngleAxis(rootJoint.maxAngle, rootJoint.getRotationAxis()) * toEnd);

        Vector3 def = spidercontroller.transform.InverseTransformPoint(rootJoint.getRotationPoint() + (minDistance + 0.5f * (chainLength - minDistance)) * midOrient);
        def.y = -spidercontroller.getCapsuleCollider().radius;
        def += defaultOffsetLength * midOrient.normalized * 0.5f * (chainLength - minDistance) * 1 / spidercontroller.scale;
        def += defaultOffsetHeight * rootJoint.getRotationAxis() * height * 1 / spidercontroller.scale;
        def += defaultOffsetStride * Vector3.Cross(midOrient.normalized, rootJoint.getRotationAxis()) * ((minDistance + (0.5f + 0.5f * defaultOffsetLength) * (chainLength - minDistance)) / chainLength) * Mathf.Sin(0.5f * rootJoint.getAngleRange()) * 1 / spidercontroller.scale;
        return def;
    }

    private void initializeCasts() {

        Transform parent = spidercontroller.transform;
        Vector3 normal = parent.up;
        Vector3 defaultPos = getDefault();
        Vector3 top = getTop();
        Vector3 bottom = getBottom();
        Vector3 frontal = getFrontalVector();
        prediction = defaultPos;

        if (castMode == CastMode.RayCast) {
            castFrontal = new RayCast(top, top + frontal, parent, parent);
            castDown = new RayCast(prediction + normal * height, -normal, 2 * height, null, null);
            castOutward = new RayCast(top, prediction, parent, null);
            castInwards = new RayCast(prediction, bottom, null, parent);
            castDefaultDown = new RayCast(defaultPos + normal * height, -normal, 2 * height, parent, parent);
            castDefaultOutward = new RayCast(top, defaultPos, parent, parent);
            castDefaultInward = new RayCast(defaultPos, bottom, parent, parent);
        }
        else {
            float r = spidercontroller.scale * radius;
            castFrontal = new SphereCast(top, top + frontal, r, parent, parent);
            castDown = new SphereCast(prediction + normal * height, -normal, 2 * height, r, null, null);
            castOutward = new SphereCast(top, prediction, r, parent, null);
            castInwards = new SphereCast(prediction, bottom, r, null, parent);
            castDefaultDown = new SphereCast(defaultPos + normal * height, -normal, 2 * height, r, parent, parent);
            castDefaultOutward = new SphereCast(top, defaultPos, r, parent, parent);
            castDefaultInward = new SphereCast(defaultPos, bottom, r, parent, parent);
        }
    }

    void Update() {
        timeSinceLastStep += Time.deltaTime;

        if (!ikChain.IKStepperActivated() || isStepping || !allowedToStep()) return;

        //If current target uncomfortable and there is a new comfortable target, step, otherwise just refresh the uncomfortable target.
        if (!ikChain.getTarget().comfortable) {
            TargetInfo newTarget = calcNewTarget();
            if (newTarget.comfortable) step(newTarget);
            else ikChain.setTarget(newTarget);
        }

        //If current target comfortable but target not close enough anymore even after the last CCD iteration, step
        else if (ikChain.getError() > IKSolver.tolerance) {
            step(calcNewTarget());
        }

        // Alternativaly step if too close to root joint
        else if (Vector3.Distance(rootJoint.getRotationPoint(), ikChain.getTarget().position) < minDistance) {
            step(calcNewTarget());
        }

        // Force Step by Pressing Space
        if (Input.GetKeyDown(KeyCode.Space)) step(calcNewTarget());
    }

    private void LateUpdate() {
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
        Vector3 start = Vector3.ProjectOnPlane(endeffectorPosition, normal);
        start = spidercontroller.transform.InverseTransformPoint(start);
        start.y = defaultPositionLocal.y;
        start = spidercontroller.transform.TransformPoint(start);

        Vector3 overshoot = start + (defaultPosition - start) * velocityPrediction;
        prediction = overshoot + spidercontroller.getMovement() * stepTime;

        //Debug variables
        lastEndEffectorPos = endeffectorPosition;
        projPrediction = start;
        overshootPrediction = overshoot;

        //Now shoot rays using the prediction to find an actual point on a surface.
        RaycastHit hitInfo;
        int layer = spidercontroller.walkableLayer;

        //Update Rays for new prediction Point
        castOutward.setEnd(prediction);
        castDown.setOrigin(prediction + normal * height);
        castDown.setEnd(prediction - normal * height);
        castInwards.setOrigin(prediction);

        // Frontal Ray
        if (castFrontal.castRay(out hitInfo, layer)) {
            Debug.Log("Got Targetpoint from frontal.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        // Outwards to prediction
        if (castOutward.castRay(out hitInfo, layer)) {
            Debug.Log("Got Targetpoint shooting outwards to prediction.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        //Straight down through prediction point
        if (castDown.castRay(out hitInfo, layer)) {
            Debug.Log("Got Targetpoint shooting down to prediction.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        // Inwards from prediction
        if (castInwards.castRay(out hitInfo, layer)) {
            Debug.Log("Got Targetpoint shooting inwards from prediction.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        // Outwards to default position
        if (castDefaultOutward.castRay(out hitInfo, layer)) {
            Debug.Log("Got Targetpoint shooting outwards to default point.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        //Straight down to default point
        if (castDefaultDown.castRay(out hitInfo, layer)) {
            Debug.Log("Got Targetpoint shooting down to default point.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        // Inwards from default point
        if (castDefaultInward.castRay(out hitInfo, layer)) {
            Debug.Log("Got Targetpoint shooting inwards from default point.");
            return new TargetInfo(hitInfo.point, hitInfo.normal);
        }

        // Return default position
        Debug.Log("No ray was able to find a target position. Therefore i will return a default position.");
        return new TargetInfo(defaultPosition + 0.25f * height * normal, normal, false);
    }

    /*
    * If im walking so fast that  one legs keeps wanna step after step complete, one leg might not step at all since its never able to
    * Could implement a some sort of a queue where i enqueue chains that want to step next?
    */
    private void step(TargetInfo target) {
        if (isStepping) {
            return;
        }
        if (!allowedToStep()) {
            //I cant step but my target is not reachable. So i simply add the spiders movevector here
            ikChain.setTarget(new TargetInfo(ikChain.getTarget().position + spidercontroller.getMovement(), ikChain.getTarget().normal));
            return;
        }
        IEnumerator coroutineStepping = Step(target);
        StartCoroutine(coroutineStepping);
    }

    /*
    * Coroutine for stepping since i want to actually see the stepping process instead of it happening all within one frame
    */
    private IEnumerator Step(TargetInfo newTarget) {
        Debug.Log("Stepping");
        isStepping = true;
        TargetInfo lastTarget = ikChain.getTarget();
        TargetInfo lerpTarget;
        float time = Time.deltaTime;
        while (time < stepTime) {
            lerpTarget.position = Vector3.Lerp(lastTarget.position, newTarget.position, time / stepTime) + stepHeight * stepAnimation.Evaluate(time / stepTime) * spidercontroller.transform.up;
            lerpTarget.normal = Vector3.Lerp(lastTarget.normal, newTarget.normal, time / stepTime);
            lerpTarget.comfortable = true;

            time += Time.deltaTime;
            ikChain.setTarget(lerpTarget);
            yield return null;
        }
        ikChain.setTarget(newTarget);
        isStepping = false;
        timeSinceLastStep = 0.0f;
    }

    private bool allowedToStep() {
        if (timeSinceLastStep < stepCooldown || (asyncChain != null && asyncChain.getIsStepping())) {
            return false;
        }
        return true;
    }

    private bool getIsStepping() {
        return isStepping;
    }

    private Vector3 getDefault() {
        return spidercontroller.transform.TransformPoint(defaultPositionLocal);
    }

    private Vector3 getTop() {
        return spidercontroller.transform.TransformPoint(rayTopPosition);
    }

    private Vector3 getBottom() {
        return spidercontroller.transform.TransformPoint(rayBottomPosition);
    }

    private Vector3 getFrontalVector() {
        return spidercontroller.transform.TransformDirection(frontalVectorLocal);
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
            castFrontal.draw(Color.green);
            castOutward.draw(Color.yellow);
            castDown.draw(Color.yellow);
            castInwards.draw(Color.yellow);
            castDefaultOutward.draw(Color.magenta);
            castDefaultDown.draw(Color.magenta);
            castDefaultInward.draw(Color.magenta);
        }

        if (DOFArc) {
            Vector3 v = spidercontroller.transform.TransformDirection(minOrient);
            Vector3 w = spidercontroller.transform.TransformDirection(maxOrient);
            DebugShapes.DrawCircleSection(rootJoint.getRotationPoint(), v, w, minDistance, ikChain.getChainLength(), Color.red);
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
