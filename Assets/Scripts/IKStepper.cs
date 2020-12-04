/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Raycasting;

#if UNITY_EDITOR
using UnityEditor;
#endif

/*
 * This class requires an IKChain to function and supplies it with the ability to perform steps.
 * It contains all the logic in order for the IKChain to perform realistic steps to actual geometrical surface points.
 * Moreover, it supports asyncronicity to other legs.
 * 
 * This class itself does not perform any kind of stepping but rather allows another class to externally call the following functions:
 * stepCheck()      : Checks whether this chain desires to step or not
 * allowedToStep()  : Checks whether this chain is able/allowed to step (e.g. asynchronicity and step cooldown affect this)
 * step()           : Calculates a new surface points and performs a step towards it by updating the IKChains target.
 * 
 * The IKStepManager manages these calls.
 */

[RequireComponent(typeof(IKChain))]
public class IKStepper : MonoBehaviour {

    public Spider spider;

    [Header("Debug")]
    public bool printDebugLogs;
    public bool pauseOnStep = false;

    [Header("Step Layer")]
    public LayerMask stepLayer;

    [Header("Leg Synchronicity")]
    public IKStepper[] asyncLegs;

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


    public IKChain ikChain { get; private set; }

    public bool isStepping { get; private set; } = false;
    private float timeStandingStill;

    [Range(0, 1)]
    public float minDistanceFactor = 0.2f;
    public float minDistance { get; private set; }

    public Dictionary<string, Cast> casts { get; private set; }
    RaycastHit hitInfo;

    public JointHinge rootJoint { get; private set; }

    public Vector3 defaultPositionLocal { get; private set; }
    private Vector3 lastResortPositionLocal;
    private Vector3 frontalStartPositionLocal;
    public Vector3 prediction { get; private set; }

    // Debug Variables
    public Vector3 lastEndEffectorPos { get; private set; }
    public Vector3 projPrediction { get; private set; }
    public Vector3 overshootPrediction { get; private set; }
    public Vector3 minOrient { get; private set; }
    public Vector3 maxOrient { get; private set; }
    public string lastHitRay { get; private set; }


    public void Awake() {
        Debug.Log("Called Awake " + name + " on IKStepper");
        ikChain = GetComponent<IKChain>();

        rootJoint = ikChain.getRootJoint();
        timeSinceLastStep = 2 * stepCooldown; // Make sure im allowed to step at start
        timeStandingStill = 0f;

        //Set the distance which the root joint and the endeffector are allowed to have. If below this distance, stepping is forced.
        minDistance = minDistanceFactor * ikChain.getChainLength();

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

        //Set Start Target for IKChain
        ikChain.setTarget(getDefaultTarget());
    }

    /*
     * This function calculates the default position using the parameters: Stride, Length and Height
     * This default position is an important anchor point used for new step point calculation.
     */
    private Vector3 calculateDefault() {
        float diameter = ikChain.getChainLength() - minDistance;
        Vector3 rootRotAxis = rootJoint.getRotationAxis();

        //Be careful with the use of transform.up and rootjoint.getRotationAxis(). In my case they are equivalent with the exception of the right side being inverted.
        //However they can be different and this has to be noticed here. The code below is probably wrong for the general case.
        Vector3 normal = spider.transform.up;

        Vector3 toEnd = ikChain.endEffector.position - rootJoint.getRotationPoint();
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
        float chainLength = ikChain.getChainLength();

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

    private void Update() {
        timeSinceLastStep += Time.deltaTime;
        if (!spider.isMoving) timeStandingStill += Time.deltaTime;
        else timeStandingStill = 0f;
    }

    /*
     * This funciton will perform a step check for the leg.
     * It will return true if a step is needed right now and return false if no step is needed.
     * The main way this is determined is by the error of the IK solve,
     * that is if the current error is greater than the threshold specified, a step is needed.
     * This is under the assumption that the leg isnt currently stepping and is not forbidden to step.
     */
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

    /*
     * This functions determines wheter this leg is allowed to step or not.
     * If this legs target is currently airborne, stepping will always be allowed.
     * Under the assumption that this legs step cooldown period is done, it will only be allowed to step,
     * if the references to the async legs are not currently stepping.
     * 
     * TODO: Add a check of isStepping? I think i never call this if im stepping but still.
     */
    public bool allowedToStep() {
        if (isStepping) return false;

        if (!ikChain.getTarget().grounded) return true;

        if (timeSinceLastStep < stepCooldown) return false;

        foreach (var ikstepper in asyncLegs) {
            if (ikstepper.isStepping) {
                return false;
            }
        }
        return true;
    }

    /*
     * This function calls the coroutine of stepping.
     * It will not be called in this class itself but it will be called externally.
     */
    public void step(float stepTime) {
        StopAllCoroutines(); // Make sure it is ok to just stop the coroutine without finishing it
        StartCoroutine(Step(stepTime));
    }

    /*
    * Coroutine for stepping.
    * Below functions will be called to calculate a new target.
    * Then the current target will be lerping from the old to the new one in an arch, given by the stepAnimation, in the time frame given.
    */
    private IEnumerator Step(float stepTime) {
        if (pauseOnStep) Debug.Break();

        if (printDebugLogs) Debug.Log(gameObject.name + " starts stepping now.");

        //Calculate desired position
        Vector3 desiredPosition = calculateDesiredPosition();
        overshootPrediction = desiredPosition; // Save this value for debug

        //Get the current velocity of the end effector and correct the desired position with it since the spider will move away while stepping  
        //Set the this new value as the prediction
        Vector3 endEffectorVelocity = ikChain.getEndeffectorVelocityPerSecond();
        prediction = desiredPosition + endEffectorVelocity * stepTime;

        // Finally find an actual target which lies on a surface point using the calculated prediction with raycasting
        TargetInfo newTarget = findTargetOnSurface();

        // We only step if either the old target or new one is grounded. Otherwise we would be in the case of a leg in air where we dont want to step in an arch.
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
    }

    /* This function calculates the position the leg desires to step to if a step would be performed.
     * A line is drawn from the current end effector position to the default position,
     * but the line will be overextended a bit , where the amount is given by the overshootMultiplier.
     * All of this happens on the plane given by the spiders up direction at default position height.
     */
    private Vector3 calculateDesiredPosition() {
        Vector3 endeffectorPosition = ikChain.endEffector.position;
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
     * This function tries to find a new target on any valid surface.
     * The parameter 'prediction' is used to construct ray casts that will scan the surrounding topology.
     * This function will return the target it has found. If no surface point was found, a last resort target is returned.
     */
    private TargetInfo findTargetOnSurface() {

        LayerMask layer = stepLayer;

        //If there is no collider in reach there is no need to try to find a surface point, just return default here.
        //This should cut down runtime cost if the spider is not grounded (e.g. in the air).
        //However this does add an extra calculation if grounded, increasing it slighly.
        if (Physics.OverlapSphere(rootJoint.getRotationPoint(), ikChain.getChainLength(), layer, QueryTriggerInteraction.Ignore) == null) {
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

                if (printDebugLogs) Debug.Log("Got a target point from the cast '" + cast.Key);
                lastHitRay = cast.Key;
                return new TargetInfo(hitInfo.point, hitInfo.normal);

            }
        }

        // Return default position
        if (printDebugLogs) Debug.Log("No ray was able to find a target position. Therefore i will return a default position.");
        return getLastResortTarget();
    }

    // Getters for important references

    // Getters for important states

    public bool allowedTargetAccess() {
        if (ikChain == null) ikChain = GetComponent<IKChain>();
        return ikChain.isTargetHandledByIKStepper();
    }

    // Getters for important points
    public Vector3 getDefault() {
        return spider.transform.TransformPoint(defaultPositionLocal);
    }
    public TargetInfo getDefaultTarget() {
        return new TargetInfo(getDefault(), spider.transform.up);
    }
    public Vector3 getLastResort() {
        return spider.transform.TransformPoint(lastResortPositionLocal);
    }
    public TargetInfo getLastResortTarget() {
        return new TargetInfo(getLastResort(), spider.transform.up, false);
    }
    public Vector3 getTopFocalPoint() {
        return spider.transform.TransformPoint(rayTopFocalPoint);
    }
    public Vector3 getBottomFocalPoint() {
        return spider.transform.TransformPoint(rayBottomFocalPoint);
    }
    public Vector3 getFrontalStartPosition() {
        return spider.transform.TransformPoint(frontalStartPositionLocal);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(IKStepper))]
public class IKStepperEditor : Editor {

    private IKStepper ikstepper;

    private static bool showDebug = true;

    private static float debugIconScale = 3;
    private static bool showPoints = true;
    private static bool showSteppingProcess = true;
    private static bool showRayCasts = true;
    private static bool showDOFArc = true;

    public void OnEnable() {
        ikstepper = (IKStepper)target;
        if (showDebug && !EditorApplication.isPlaying) ikstepper.Awake();
    }

    // Naming Convention of Leg: Has to end with e.g. L1, R4, L2, .... (e.g. Leg_L1 or Leg_R4)
    // L stands for Left leg
    // R stands for right leg
    // Numbers increase in direction of back legs (Lowest are front legs, highest are back legs)
    // Only works for numbers in single digits (up to 9)

    public void findAsyncLegs() {
        List<IKStepper> temp = new List<IKStepper>();

        // e.g. "Leg_L4"
        char side1 = ikstepper.name[ikstepper.name.Length - 2]; // L
        char number1 = ikstepper.name[ikstepper.name.Length - 1]; // 4

        foreach (var p_ikStepper in ikstepper.spider.GetComponentsInChildren<IKStepper>()) {
            if (p_ikStepper == ikstepper) continue;

            // e.g. "Leg_R2"
            char side2 = p_ikStepper.name[p_ikStepper.name.Length - 2]; // R
            char number2 = p_ikStepper.name[p_ikStepper.name.Length - 1]; // 2

            //Is this an opposite leg?
            if (side1 != side2 && number1 == number2) {
                temp.Add(p_ikStepper);
            }

            //Is other leg in front of this class's leg?
            if (side1 == side2 && number1 == number2 + 1) {
                temp.Add(p_ikStepper);
            }
        }
        ikstepper.asyncLegs = temp.ToArray(); //Dont set this directly, rather use serialized object otherwise inspector wont save the changes
    }

    public void addAsyncLeg() {
        int n = ikstepper.asyncLegs.Length;
        IKStepper[] temp = new IKStepper[n + 1];
        for (int i = 0; i < n; i++) { temp[i] = ikstepper.asyncLegs[i]; }
        ikstepper.asyncLegs = temp;
    }

    public void removeAsyncLeg() {
        int n = ikstepper.asyncLegs.Length;
        if (n == 0) return;
        IKStepper[] temp = new IKStepper[n - 1];
        for (int i = 0; i < n - 1; i++) { temp[i] = ikstepper.asyncLegs[i]; }
        ikstepper.asyncLegs = temp;
    }

    public override void OnInspectorGUI() {
        if (ikstepper == null) return;

        serializedObject.Update();

        EditorDrawing.DrawMonoScript(ikstepper, typeof(IKStepper));

        EditorDrawing.DrawHorizontalLine();

        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        showDebug = EditorGUILayout.Toggle("Show Debug Drawings", showDebug);
        if (showDebug) {
            EditorGUI.indentLevel++;
            {
                debugIconScale = EditorGUILayout.Slider("Drawing Scale", debugIconScale, 1f, 10f);
                showPoints = EditorGUILayout.Toggle("Draw Points", showPoints);
                showSteppingProcess = EditorGUILayout.Toggle("Draw Stepping Process", showSteppingProcess);
                showRayCasts = EditorGUILayout.Toggle("Draw Raycasts", showRayCasts);
                showDOFArc = EditorGUILayout.Toggle("Draw Degree of Freedom Arc", showDOFArc);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            serializedObject.FindProperty("printDebugLogs").boolValue = EditorGUILayout.Toggle("Print Debug Logs", ikstepper.printDebugLogs);
            serializedObject.FindProperty("pauseOnStep").boolValue = EditorGUILayout.Toggle("Pause Editor On Step", ikstepper.pauseOnStep);
        }
        EditorDrawing.DrawHorizontalLine();

        //Leg Asynchronicity
        EditorGUILayout.LabelField("Leg Asynchronicity", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUI.indentLevel++;
        {
            SerializedProperty asyncLegsProperty = serializedObject.FindProperty("asyncLegs");
            for (int i = 0; i < asyncLegsProperty.arraySize; i++) {
                SerializedProperty singleLegProperty = asyncLegsProperty.GetArrayElementAtIndex(i);
                singleLegProperty.objectReferenceValue = (IKStepper)EditorGUILayout.ObjectField(ikstepper.asyncLegs[i], typeof(IKStepper), true);
            }

            // Buttons for asynch legs array
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("");
                if (EditorDrawing.DrawButton("Add Leg")) addAsyncLeg();
                if (EditorDrawing.DrawButton("Remove Leg")) removeAsyncLeg();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
                //EditorGUILayout.LabelField("");
                //if (EditorDrawing.DrawButton("Find Automatically")) findAsyncLegs();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel--;
        EditorDrawing.DrawHorizontalLine();

        //Step Process
        EditorGUILayout.LabelField("Stepping", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUI.indentLevel++;
        {
            //Min distance
            EditorGUILayout.LabelField("Step Restriction", EditorStyles.boldLabel);
            serializedObject.FindProperty("minDistanceFactor").floatValue = EditorGUILayout.Slider("Minimum Distance to Root", ikstepper.minDistanceFactor, 0, 1);
            EditorGUILayout.Space();

            //Step Cooldown and Force Still
            EditorGUILayout.LabelField("Step Timing", EditorStyles.boldLabel);
            serializedObject.FindProperty("stepCooldown").floatValue = EditorGUILayout.Slider("Step Cooldown Time", ikstepper.stepCooldown, 0, 1);
            serializedObject.FindProperty("stopSteppingAfterSecondsStill").floatValue = EditorGUILayout.Slider("Stop Stepping After X Seconds Still", ikstepper.stopSteppingAfterSecondsStill, 0, 2);
            EditorGUILayout.Space();

            //Step Animation Curve
            EditorGUILayout.LabelField("Step Animation", EditorStyles.boldLabel);
            serializedObject.FindProperty("stepHeight").floatValue = EditorGUILayout.Slider("Step Height", ikstepper.stepHeight, 0, 10);
            serializedObject.FindProperty("stepAnimation").animationCurveValue = EditorGUILayout.CurveField("Animation Curve", ikstepper.stepAnimation);
            EditorGUILayout.Space();

            //Coordinates of Anchor position and overshoot multiplier
            EditorGUILayout.LabelField("Anchor Position", EditorStyles.boldLabel);
            serializedObject.FindProperty("defaultOffsetLength").floatValue = EditorGUILayout.Slider("Anchor Length (X)", ikstepper.defaultOffsetLength, 0, 1);
            serializedObject.FindProperty("defaultOffsetHeight").floatValue = EditorGUILayout.Slider("Anchor Height (Y)", ikstepper.defaultOffsetHeight, 0, 1);
            serializedObject.FindProperty("defaultOffsetStride").floatValue = EditorGUILayout.Slider("Anchor Stride (Z)", ikstepper.defaultOffsetStride, 0, 1);

            serializedObject.FindProperty("defaultOvershootMultiplier").floatValue = EditorGUILayout.Slider("Overshoot Multiplier", ikstepper.defaultOvershootMultiplier, 1, 2);
            EditorGUILayout.Space();

            //Last Resort position
            EditorGUILayout.LabelField("Last Resort Target Position", EditorStyles.boldLabel);
            serializedObject.FindProperty("lastResortHeight").floatValue = EditorGUILayout.Slider("Last Resort Offset Height", ikstepper.lastResortHeight, 0, 1);
        }
        EditorGUI.indentLevel--;
        EditorDrawing.DrawHorizontalLine();


        //RayCasting
        EditorGUILayout.LabelField("Raycasting System", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUI.indentLevel++;
        {
            serializedObject.FindProperty("castMode").enumValueIndex = (int)(CastMode)EditorGUILayout.EnumPopup("Cast Mode", ikstepper.castMode);
            EditorGUILayout.Space();

            EditorGUI.indentLevel++;
            {
                //Radius if applicable
                if (ikstepper.castMode == CastMode.SphereCast) {
                    serializedObject.FindProperty("radius").floatValue = EditorGUILayout.FloatField("Sphere Radius", ikstepper.radius);
                    EditorGUILayout.Space();
                }

                //FrontalRay
                EditorGUILayout.LabelField("Frontal Ray", EditorStyles.boldLabel);
                serializedObject.FindProperty("rayFrontalHeight").floatValue = EditorGUILayout.Slider("Frontal Height", ikstepper.rayFrontalHeight, 0, 1);
                serializedObject.FindProperty("rayFrontalLength").floatValue = EditorGUILayout.Slider("Frontal Length", ikstepper.rayFrontalLength, 0, 1);
                serializedObject.FindProperty("rayFrontalOriginOffset").floatValue = EditorGUILayout.Slider("Frontal Origin Offset", ikstepper.rayFrontalOriginOffset, 0, 1);
                EditorGUILayout.Space();

                //Outwards Ray
                EditorGUILayout.LabelField("Outwards Ray", EditorStyles.boldLabel);
                serializedObject.FindProperty("rayTopFocalPoint").vector3Value = EditorGUILayout.Vector3Field("Outwards Focal Point", ikstepper.rayTopFocalPoint);
                serializedObject.FindProperty("rayOutwardsOriginOffset").floatValue = EditorGUILayout.Slider("Outwards Origin Offset", ikstepper.rayOutwardsOriginOffset, 0, 1);
                serializedObject.FindProperty("rayOutwardsEndOffset").floatValue = EditorGUILayout.Slider("Outwards End Offset", ikstepper.rayOutwardsEndOffset, 0, 1);
                EditorGUILayout.Space();

                //Downwards Ray
                EditorGUILayout.LabelField("Downwards Ray", EditorStyles.boldLabel);
                serializedObject.FindProperty("downRayHeight").floatValue = EditorGUILayout.Slider("Downwards Height", ikstepper.downRayHeight, 0, 1);
                serializedObject.FindProperty("downRayDepth").floatValue = EditorGUILayout.Slider("Downwards Depth", ikstepper.downRayDepth, 0, 1);
                EditorGUILayout.Space();

                //Inwards Rays
                EditorGUILayout.LabelField("Inwards Ray", EditorStyles.boldLabel);
                serializedObject.FindProperty("rayBottomFocalPoint").vector3Value = EditorGUILayout.Vector3Field("Inwards Focal Point", ikstepper.rayBottomFocalPoint);
                serializedObject.FindProperty("rayInwardsEndOffset").floatValue = EditorGUILayout.Slider("Inwards End Offset", ikstepper.rayInwardsEndOffset, 0, 1);
                EditorGUILayout.Space();

            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;

        //Apply Changes
        serializedObject.ApplyModifiedProperties();

        if (showDebug && !EditorApplication.isPlaying) ikstepper.Awake();
    }

    void OnSceneGUI() {
        if (!showDebug || ikstepper == null) return;

        float scale = ikstepper.spider.getScale() * 0.0001f * debugIconScale;

        if (showSteppingProcess) DrawSteppingProcess(ref ikstepper, new Color(0.2f, 0.2f, 0.2f, 1f), Color.white, Color.green, Color.yellow, scale);
        if (showRayCasts) DrawRaycasts(ref ikstepper, Color.Lerp(Color.magenta, Color.white, 0.2f), Color.Lerp(Color.yellow, Color.white, 0.2f), scale);
        if (showDOFArc) DrawDegreeOfFreedomArc(ref ikstepper, Color.red);

        if (showPoints) {
            DrawLastResort(ref ikstepper, Color.red, scale);
            DrawFocalPoints(ref ikstepper, Color.green, scale);
            DrawTarget(ref ikstepper, Color.cyan, scale);
            DrawDefaultPoint(ref ikstepper, Color.magenta, scale);
        }
    }

    public void DrawDefaultPoint(ref IKStepper ikstepper, Color col, float scale) {
        Vector3 pos = ikstepper.getDefault();
        Handles.color = col;
        Handles.DrawWireCube(pos, scale * Vector3.one);
        EditorDrawing.DrawText(pos, "Default", col);
    }

    public void DrawLastResort(ref IKStepper ikstepper, Color col, float scale) {
        Vector3 pos = ikstepper.getLastResortTarget().position;
        Handles.color = col;
        Handles.DrawWireCube(pos, scale * Vector3.one);
        EditorDrawing.DrawText(pos, "Last Resort", col);
    }

    public void DrawFocalPoints(ref IKStepper ikstepper, Color col, float scale) {
        Vector3 top = ikstepper.getTopFocalPoint();
        Vector3 bottom = ikstepper.getBottomFocalPoint();
        Handles.color = col;
        Handles.DrawWireCube(top, scale * Vector3.one);
        Handles.DrawWireCube(bottom, scale * Vector3.one);
        EditorDrawing.DrawText(top, "Top Focal Point", col);
        EditorDrawing.DrawText(bottom, "Bottom Focal Point", col);
    }

    public void DrawTarget(ref IKStepper ikstepper, Color col, float scale) {
        Vector3 pos = ikstepper.ikChain.getTarget().position;
        Handles.color = col;
        Handles.DrawWireCube(pos, scale * Vector3.one);
        EditorDrawing.DrawText(pos, "Target", col);
    }

    public void DrawSteppingProcess(ref IKStepper ikstepper, Color color1, Color color2, Color color3, Color color4, float scale) {
        Handles.color = color1;
        Handles.DrawWireCube(ikstepper.lastEndEffectorPos, scale * Vector3.one);
        EditorDrawing.DrawText(ikstepper.lastEndEffectorPos, "Last Position", color1);

        Handles.color = color2;
        Handles.DrawWireCube(ikstepper.projPrediction, scale * Vector3.one);
        Handles.DrawDottedLine(ikstepper.lastEndEffectorPos, ikstepper.projPrediction, 2);

        Handles.color = color3;
        Handles.DrawWireCube(ikstepper.overshootPrediction, scale * Vector3.one);
        Handles.DrawDottedLine(ikstepper.projPrediction, ikstepper.overshootPrediction, 2);
        EditorDrawing.DrawText(ikstepper.overshootPrediction, "Overshoot", color3);

        Handles.color = color4;
        Handles.DrawWireCube(ikstepper.prediction, scale * Vector3.one);
        Handles.DrawDottedLine(ikstepper.overshootPrediction, ikstepper.prediction, 2);
        EditorDrawing.DrawText(ikstepper.prediction, "Prediction", color4);
    }

    public void DrawRaycasts(ref IKStepper ikstepper, Color color1, Color color2, float scale) {
        Color col = Color.black;
        foreach (var cast in ikstepper.casts) {
            if (cast.Key.Contains("Default")) col = color1;
            else if (cast.Key.Contains("Prediction")) col = color2;

            Handles.color = col;
            Vector3 end = cast.Value.getEnd();
            Vector3 origin = cast.Value.getOrigin();
            Vector3 dir = cast.Value.getDirection().normalized;
            Handles.DrawLine(origin, end);

            //If cast is sphere cast add a radius handle...

            float t = 5f * scale;
            Quaternion rot = Quaternion.LookRotation(dir);
            Handles.ArrowHandleCap(0, end - t * dir, rot, t, EventType.Repaint);
            Handles.ArrowHandleCap(0, origin, rot, t, EventType.Repaint);

            EditorDrawing.DrawText(end, cast.Key, col, cast.Key == ikstepper.lastHitRay);
        }
    }

    public void DrawDegreeOfFreedomArc(ref IKStepper ikstepper, Color col) {
        Vector3 v = ikstepper.spider.transform.TransformDirection(ikstepper.minOrient);
        Vector3 w = ikstepper.spider.transform.TransformDirection(ikstepper.maxOrient);
        Vector3 p = ikstepper.spider.transform.InverseTransformPoint(ikstepper.rootJoint.getRotationPoint());
        p.y = ikstepper.defaultPositionLocal.y;
        p = ikstepper.spider.transform.TransformPoint(p);
        float chainLength = ikstepper.ikChain.getChainLength();
        float minDistance = ikstepper.minDistance;

        Handles.color = col;
        Handles.DrawWireArc(p, ikstepper.spider.transform.up, v, ikstepper.rootJoint.getAngleRange(), chainLength);
        Handles.DrawWireArc(p, ikstepper.spider.transform.up, v, ikstepper.rootJoint.getAngleRange(), minDistance);

        Vector3 v1 = p + v * minDistance;
        Vector3 w1 = p + w * minDistance;
        Vector3 v2 = p + v * chainLength;
        Vector3 w2 = p + w * chainLength;
        Handles.DrawDottedLine(p, v1, 2);
        Handles.DrawDottedLine(p, w1, 2);
        Handles.DrawLine(v1, v2);
        Handles.DrawLine(w1, w2);
    }
}
#endif
