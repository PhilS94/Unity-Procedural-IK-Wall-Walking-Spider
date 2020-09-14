/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Raycasting;

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
    public bool showDebug;
    public Transform observedObject;

    protected Transform camTarget;
    protected Camera cam;

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

    private RayCast clipZoomRayPlayerToCam;
    private float maxCameraDistance;
    private RaycastHit hitInfo;

    [Header("Obstruction Hiding")]
    public bool enableObstructionHiding;
    public LayerMask obstructionHidingLayer;
    [Range(0, 1f)]
    public float rayRadiusObstructionHiding;

    private SphereCast hideRayCamToPlayer;
    private RaycastHit[] camObstructions;
    private ShadowCastingMode[] camObstructionsCastingMode;

    protected virtual void Awake() {
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

    // For the target, create new Gameobject with same position and rotation as this camera currently is
    private void setupCamTarget() {
        GameObject g = new GameObject(gameObject.name + " Target");
        camTarget = g.transform;
        camTarget.position = transform.position;
        camTarget.rotation = transform.rotation;
    }

    private void initializeRayCasting() {
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

        if (showDebug && cam.enabled) drawDebug();
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

    /* Abstract rotation axis so every class which inherits from this has to define these. */
    protected abstract Vector3 getHorizontalRotationAxis();
    protected abstract Vector3 getVerticalRotationAxis();

    /* Getters */
    public Camera getCamera() {
        return cam;
    }

    public Transform getCameraTarget() {
        return camTarget;
    }

    public Vector3 getCamTargetPosition() {
        return camTarget.position;
    }

    public Quaternion getCamTargetRotation() {
        return camTarget.rotation;
    }

    public Transform getObservedObject() {
        return observedObject;
    }

    /* Setters */
    public void setTargetPosition(Vector3 pos) {
        camTarget.position = pos;
    }

    public void setTargetRotation(Quaternion rot) {
        camTarget.rotation = rot;
    }

    //** Debug Methods **//
    private void drawDebug() {
        //Draw line from this cam to the observed object
        Debug.DrawLine(transform.position, observedObject.position, Color.gray);

        //Draw the hide obstruction Ray
        //hideRayCamToPlayer.draw(Color.black);

        //Draw the ZoomClip Ray, this isnt up to date since the cam has already lerped before this is called
        clipZoomRayPlayerToCam.draw(Color.white);
        DebugShapes.DrawRay(clipZoomRayPlayerToCam.getOrigin(), clipZoomRayPlayerToCam.getDirection(), clipZoomMinDistanceFactor*maxCameraDistance, Color.blue);
        DebugShapes.DrawRay(clipZoomRayPlayerToCam.getEnd(), -clipZoomRayPlayerToCam.getDirection(), clipZoomPaddingFactor*maxCameraDistance, Color.red);

        //Draw the angle restrictions
        Vector3 zeroOrientation = getHorizontalRotationAxis();
        Vector3 up = Quaternion.AngleAxis(-camUpperAngleMargin, camTarget.right) * zeroOrientation;
        Vector3 down = Quaternion.AngleAxis(-camLowerAngleMargin, camTarget.right) * zeroOrientation;

#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlaying) {
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.DrawSolidArc(observedObject.position, camTarget.right, down, Vector3.SignedAngle(down, up, camTarget.right), maxCameraDistance / 4);

        }
#endif

        //Draw Target Transform
        DebugShapes.DrawPoint(camTarget.position, Color.magenta, 0.1f);
        DebugShapes.DrawRay(camTarget.position, camTarget.forward, Color.blue);
        DebugShapes.DrawRay(camTarget.position, camTarget.right, Color.red);
        DebugShapes.DrawRay(camTarget.position, camTarget.up, Color.green);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected() {
        if (!showDebug) return;
        if (UnityEditor.EditorApplication.isPlaying) return;
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;
        if (observedObject == null) return;
        camTarget = transform;
        initializeRayCasting();
        drawDebug();
    }
#endif
}
