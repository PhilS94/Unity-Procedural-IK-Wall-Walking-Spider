/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Raycasting;

#if UNITY_EDITOR
using UnityEditor;
#endif

/* 
 * An Abstract class for camera movement.
 * 
 * It is an implementation of a smoothly lerping camera towards a given target. This includes translational and rotational interpolation.
 * Limits to vertical rotation can be set, the interpolation type for smooth movement can be choosen,
 * as well as and sensitiviy to mouse movement.
 * 
 * The observed object must be set, which is usually the player (e.g. the spider) and this class handles other objects obstructing
 * the view to this observed object in two ways:
 * 
 * Obstruction Hiding: Simply render the geometry invisible while it is obstructing.
 * Clip Zoom: Zoom in more closely to avoid the obstruction.
 * 
 * Both have their own adjustable layers, so it is completely configurable for which objects what method should be used.
 * It is advised to use the Clip Zoom for level borders where the camera would otherwise be out of bounds, and use the obstruction 
 * hiding method for simple small objects.
 * 
 * The reason for this class to be abstract is that the camera target manipulation can have several different implementations.
 * E.g. simply parenting it to the observed objects or ignoring rotational change of the observed object but following translational change.
 * Moreover, the rotational axes for vertical and horizontal camera movement can vary, e.g. local Y or global Y axis, and must thus be defined separately.
 */
public abstract class CameraAbstract : MonoBehaviour {
    public Transform observedObject;

    public Transform camTarget { get; protected set; }
    public Camera cam { get; protected set; }

    [Header("Smoothness")]
    public float translationSpeed;
    public float rotationSpeed;

    [Header("Sensitivity")]
    [Range(1, 5)]
    public float XSensitivity;
    [Range(1, 5)]
    public float YSensitivity;

    public enum PositionInterpolation { Slerp, Lerp, SmoothDamp }
    [Header("Interpolation")]
    public PositionInterpolation positionInterpolationType;
    private Vector3 velocity = Vector3.zero;

    [Header("Angle Restrictions")]
    [Range(0.01f, 179.99f)]
    public float camUpperAngleMargin = 30.0f;
    [Range(0.01f, 179.99f)]
    public float camLowerAngleMargin = 60.0f;

    [Header("Camera Clip Zoom")]
    public bool enableClipZoom;
    public LayerMask clipZoomLayer;
    [Range(0, 1)]
    public float clipZoomPaddingFactor;
    [Range(0, 1)]
    public float clipZoomMinDistanceFactor;

    public RayCast clipZoomRayPlayerToCam { get; private set; }
    public float maxCameraDistance { get; private set; }
    private RaycastHit hitInfo;

    [Header("Obstruction Hiding")]
    public bool enableObstructionHiding;
    public LayerMask obstructionHidingLayer;
    [Range(0, 1f)]
    public float rayRadiusObstructionHiding;

    public SphereCast hideRayCamToPlayer { get; private set; }
    private RaycastHit[] camObstructions;
    private ShadowCastingMode[] camObstructionsCastingMode;

    protected virtual void Awake() {
        Debug.Log("Called Awake " + name + " on CameraAbstract");
        cam = GetComponent<Camera>();
        setupCamTarget();
        initializeRayCasting();
        transform.parent = null; // Unparent the camera itself so it can move freely and use the target to lerp smoothly

        if (camUpperAngleMargin >= camLowerAngleMargin) {
            Debug.LogError("Upper Angle has to be smaller than Lower Angle");
            camUpperAngleMargin = 45f;
            camLowerAngleMargin = 90f;
        }

    }

    /* Initialization methods */

    // Sets target to own transform, camera will not act smooth
    public void defaultCameraTarget() {
        camTarget = transform;
    }

    // For the target, create new Gameobject with same position and rotation as this camera currently is
    private void setupCamTarget() {
        GameObject g = new GameObject(gameObject.name + " Target");
        camTarget = g.transform;
        camTarget.position = transform.position;
        camTarget.rotation = transform.rotation;
    }

    public void initializeRayCasting() {
        maxCameraDistance = Vector3.Distance(observedObject.position, transform.position);
        clipZoomRayPlayerToCam = new RayCast(observedObject.position, camTarget.position, observedObject, null);
        hideRayCamToPlayer = new SphereCast(transform.position, observedObject.position, rayRadiusObstructionHiding * transform.lossyScale.y * 0.01f, transform, observedObject);
    }

    /*
    * Update performs the cameras target movement. This includes mouse input as well as solving clipping problems.
    * Override this Update call in an inheriting class but call this base implementation first.
    * This allows the inheriting classes to implement their own camera target manipulation.
    */
    protected virtual void Update() {
        RotateCameraHorizontal(Input.GetAxis("Mouse X") * XSensitivity, false);
        RotateCameraVertical(-Input.GetAxis("Mouse Y") * YSensitivity, false);

        if (!cam.enabled) return; // I hope this only returns for this function and not the whole new update implementation of a inheriting class.
        if (enableClipZoom) clipZoom();
        if (enableObstructionHiding) hideObstructions();
    }

    /* Late Update performs the interpolation of the camera to the camera target. This must not be overriden by inheriting classes. */
    protected void LateUpdate() {
        // Translation Interpolation
        switch (positionInterpolationType) {
            case PositionInterpolation.Lerp:
                transform.position = Vector3.Lerp(transform.position, camTarget.position, Time.deltaTime * translationSpeed);
                break;
            case PositionInterpolation.Slerp:
                Vector3 a = observedObject.InverseTransformPoint(transform.position);
                Vector3 b = observedObject.InverseTransformPoint(camTarget.position);
                Vector3 c = Vector3.Slerp(a, b, Time.deltaTime * translationSpeed);
                transform.position = observedObject.TransformPoint(c);
                break;
            case PositionInterpolation.SmoothDamp:
                transform.position = Vector3.SmoothDamp(transform.position, camTarget.position, ref velocity, 1 / translationSpeed);
                break;
        }

        // Rotation Interpolation
        transform.rotation = Quaternion.Slerp(transform.rotation, camTarget.rotation, Time.deltaTime * rotationSpeed);
    }

    /* Camera Rotation methods */

    public void RotateCameraHorizontal(float angle, bool onlyTarget = true) {
        Vector3 rotationAxis = getHorizontalRotationAxis();

        //Apply Rotation
        camTarget.RotateAround(observedObject.position, rotationAxis, angle);
        if (!onlyTarget) transform.RotateAround(observedObject.position, rotationAxis, angle);
    }

    public void RotateCameraVertical(float angle, bool onlyTarget = true) {

        //Restrict Angle to Bounds
        clampAngle(ref angle);
        Vector3 zeroOrientation = getHorizontalRotationAxis();
        float currentAngle = Vector3.SignedAngle(zeroOrientation, camTarget.position - observedObject.transform.position, camTarget.right); //Should always be positive

        if (currentAngle + angle > -camUpperAngleMargin) {
            angle = -currentAngle - camUpperAngleMargin;
        }
        if (currentAngle + angle < -camLowerAngleMargin) {
            angle = -currentAngle - camLowerAngleMargin;
        }

        Vector3 rotationAxis = getVerticalRotationAxis();

        // Apply Rotation
        camTarget.RotateAround(observedObject.position, rotationAxis, angle);
        if (!onlyTarget) transform.RotateAround(observedObject.position, rotationAxis, angle);
    }

    // Clamps angle to (-180,180]
    private void clampAngle(ref float angle) {
        angle = angle % 360;
        if (angle == -180) angle = 180;
        if (angle > 180) angle -= 360;
        if (angle < -180) angle += 360;
    }

    /* Anti camera clipping methods */
    private void clipZoom() {
        clipZoomRayPlayerToCam.setEnd(camTarget.position); // Could use actual cam position instead of target, but this works more cleanly with more control
        clipZoomRayPlayerToCam.setDistance(maxCameraDistance);

        Vector3 direction = clipZoomRayPlayerToCam.getDirection().normalized;
        if (clipZoomRayPlayerToCam.castRay(out hitInfo, clipZoomLayer)) {

            //Add padding
            Vector3 newPosition = hitInfo.point - direction * clipZoomPaddingFactor * maxCameraDistance;

            //If either the padding sent me beyond the observed object  OR if im too close to the object, resort to min distance
            Vector3 v = newPosition - observedObject.position;
            float minDistance = maxCameraDistance * clipZoomMinDistanceFactor;
            if (Vector3.Angle(v, direction) > 45f || Vector3.Distance(observedObject.position, newPosition) < minDistance) {
                newPosition = observedObject.position + direction * minDistance;
            }

            // Move the target and cam to the new point
            transform.position = newPosition;
            camTarget.position = newPosition;
        }
        else {
            // Move the target to the end point, so cam can lerp smoothly back. This works only because the ray is constructed through the target and not actual cam
            camTarget.position = clipZoomRayPlayerToCam.getEnd();
        }
    }

    private void hideObstructions() {

        //First make all previous obstructions visible again.
        if (camObstructions != null) {
            for (int k = 0; k < camObstructions.Length; k++) {
                MeshRenderer mesh = camObstructions[k].transform.GetComponent<MeshRenderer>();
                if (mesh == null) continue;
                mesh.shadowCastingMode = camObstructionsCastingMode[k];
            }
        }

        // Now transparent all new obstructions
        camObstructions = hideRayCamToPlayer.castRayAll(obstructionHidingLayer);
        camObstructionsCastingMode = new ShadowCastingMode[camObstructions.Length];
        for (int k = 0; k < camObstructions.Length; k++) {
            MeshRenderer mesh = camObstructions[k].transform.GetComponent<MeshRenderer>();
            if (mesh == null) continue;
            camObstructionsCastingMode[k] = mesh.shadowCastingMode;
            mesh.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }

    /* Abstract rotation axis: Every class which inherits from this has to define these. */
    public abstract Vector3 getHorizontalRotationAxis();
    public abstract Vector3 getVerticalRotationAxis();

    /* Setters */
    public void setTargetPosition(Vector3 pos) {
        camTarget.position = pos;
    }

    public void setTargetRotation(Quaternion rot) {
        camTarget.rotation = rot;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CameraAbstract), true)]
public class CameraAbstractEditor : Editor {

    private CameraAbstract cam;

    private static bool showDebug = true;

    private static float debugIconScale;
    private static bool showObstructionRay = true;
    private static bool showClipRay = true;
    private static bool showAngleRestrictions = true;
    private static bool showTarget = true;


    public void OnEnable() {
        cam = (CameraAbstract)target;
        if (showDebug && !EditorApplication.isPlaying) {
            cam.defaultCameraTarget();
            cam.initializeRayCasting();
        }
    }

    public override void OnInspectorGUI() {
        if (cam == null) return;

        Undo.RecordObject(cam, "Changes to Camera");

        EditorDrawing.DrawHorizontalLine();
        EditorGUILayout.LabelField("Debug Drawing", EditorStyles.boldLabel);
        showDebug = EditorGUILayout.Toggle("Show Debug Drawings", showDebug);
        if (showDebug) {
            EditorGUI.indentLevel++;
            debugIconScale = EditorGUILayout.Slider("Drawing Scale", debugIconScale, 0.1f, 1f);
            showObstructionRay = EditorGUILayout.Toggle("Draw Obstruction Ray", showObstructionRay);
            showClipRay = EditorGUILayout.Toggle("Draw Clip Ray", showClipRay);
            showAngleRestrictions = EditorGUILayout.Toggle("Draw Angle Restrictions", showAngleRestrictions);
            showTarget = EditorGUILayout.Toggle("Draw Target", showTarget);
            EditorGUI.indentLevel--;
        }
        EditorDrawing.DrawHorizontalLine();

        base.OnInspectorGUI();
        if (showDebug && !EditorApplication.isPlaying) {
            cam.defaultCameraTarget();
            cam.initializeRayCasting();
        }
    }

    void OnSceneGUI() {
        if (!showDebug || cam == null || cam.observedObject == null) return;

        //Draw the hide obstruction Ray
        if (showObstructionRay && cam.enableObstructionHiding) {
            Vector3 origin = cam.hideRayCamToPlayer.getOrigin();
            Vector3 end = cam.hideRayCamToPlayer.getEnd();
            Vector3 dir = cam.hideRayCamToPlayer.getDirection().normalized;
            Handles.color = Color.white;
            Handles.RadiusHandle(cam.transform.rotation, origin, cam.hideRayCamToPlayer.getRadius());
            Handles.DrawDottedLine(origin, end, 2);
            EditorDrawing.DrawText(origin, "Obstruction\nRay", Color.white);

            //Draw Arrows
            Quaternion rot = Quaternion.LookRotation(dir);
            Handles.ArrowHandleCap(0, origin, rot, debugIconScale, EventType.Repaint);
            Handles.ArrowHandleCap(0, end - dir * debugIconScale, rot, debugIconScale, EventType.Repaint);
        }

        //Draw the ZoomClip Ray, this isnt up to date since the cam has already lerped before this is called
        if (showClipRay && cam.enableClipZoom) {
            Vector3 origin = cam.clipZoomRayPlayerToCam.getOrigin();
            Vector3 end = cam.clipZoomRayPlayerToCam.getEnd();
            Vector3 dir = cam.clipZoomRayPlayerToCam.getDirection().normalized;

            // Whole Ray
            Handles.color = Color.cyan;
            Handles.DrawDottedLine(origin, end, 2);
            EditorDrawing.DrawText(origin, "Clip\nRay", Color.cyan);

            //Draw Arrows
            Quaternion rot = Quaternion.LookRotation(dir);
            Handles.ArrowHandleCap(0, origin, rot, debugIconScale, EventType.Repaint);
            Handles.ArrowHandleCap(0, end - dir * debugIconScale, rot, debugIconScale, EventType.Repaint);

            // Minimum Distance cam has to keep to observed object
            Vector3 minDistance = origin + dir * cam.clipZoomMinDistanceFactor * cam.maxCameraDistance;
            Handles.color = Color.blue;
            Handles.DrawDottedLine(origin, minDistance, 2);
            EditorDrawing.DrawText(minDistance, "Clip\nMin Distance", Color.blue);

            // Padding for camera repositioning
            Vector3 paddingEnd = cam.camTarget.position + dir * cam.clipZoomPaddingFactor * cam.maxCameraDistance;
            Handles.color = Color.red;
            Handles.DrawDottedLine(cam.camTarget.position, paddingEnd, 3);
            EditorDrawing.DrawText(paddingEnd, "Clip\nPadding", Color.red);
        }

        //Draw the angle restrictions
        if (showAngleRestrictions) {
            Vector3 targetRight = cam.camTarget.right;
            Vector3 zeroOrientation = cam.getHorizontalRotationAxis();
            Vector3 up = Quaternion.AngleAxis(-cam.camUpperAngleMargin, targetRight) * zeroOrientation;
            Vector3 down = Quaternion.AngleAxis(-cam.camLowerAngleMargin, targetRight) * zeroOrientation;
            Vector3 currentOrientation = cam.camTarget.position - cam.observedObject.position;

            Handles.color = Color.yellow;
            Handles.DrawSolidArc(cam.observedObject.position, targetRight, down, Vector3.SignedAngle(down, up, targetRight), 0.5f * debugIconScale);
            Handles.color = Color.red;
            Handles.DrawSolidArc(cam.observedObject.position, targetRight, down, Vector3.SignedAngle(down, currentOrientation, targetRight), 0.25f * debugIconScale);
            Handles.DrawLine(cam.observedObject.position, cam.observedObject.position + 0.5f * debugIconScale * currentOrientation.normalized);
        }

        //Draw Target Transform
        if (showTarget) {
            Handles.color = Color.magenta;
            Handles.DrawWireCube(cam.camTarget.position, 0.2f * debugIconScale * Vector3.one);

            Handles.color = Color.blue;
            Handles.DrawLine(cam.camTarget.position, cam.camTarget.position + cam.camTarget.forward);

            Handles.color = Color.red;
            Handles.DrawLine(cam.camTarget.position, cam.camTarget.position + cam.camTarget.right);

            Handles.color = Color.green;
            Handles.DrawLine(cam.camTarget.position, cam.camTarget.position + cam.camTarget.up);
        }
    }
}
#endif