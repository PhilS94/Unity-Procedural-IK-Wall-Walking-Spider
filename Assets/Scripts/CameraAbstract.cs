using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Raycasting;

/*
 * Abstract class for camera movement.
 * Important notice: This abstract class does not manipulate the camera target used for lerping and thus every
 * class inheriting from this has to take care of this itself. This is intended as different types of camera want to manipulate
 * the cameras target differently.
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

    /*
     * Late Update performs the interpolation of the camera to the camera target. This must not be overriden by inheriting classes.
     */
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

    //Clamp angle to (-180,180]
    private void clampAngle(ref float angle) {
        angle = angle % 360;
        if (angle == -180) angle = 180;
        if (angle > 180) angle -= 360;
        if (angle < -180) angle += 360;
    }

    private void clipZoom() {
        clipZoomRayPlayerToCam.setEnd(camTarget.position); // Could use actual cam position instead of target, but this works more cleanly with more control
        clipZoomRayPlayerToCam.setDistance(maxCameraDistance);

        if (clipZoomRayPlayerToCam.castRay(out hitInfo, clipZoomLayer)) {
            //Move the target and cam to the hit point.
            // I might want to have a small margin here though,
            //but this would introduce making sure to never get too close, or even pass the observed objects position
            transform.position = hitInfo.point;
            camTarget.position = hitInfo.point;
        } else {
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

    // Abstract rotation axis so every class which inherits from this has to define these
    protected abstract Vector3 getHorizontalRotationAxis();

    protected abstract Vector3 getVerticalRotationAxis();

    // Getter functions
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
        hideRayCamToPlayer.draw(Color.black);

        //Draw the ZoomClip Ray, this isnt up to date since the cam has already lerped before this is called
        clipZoomRayPlayerToCam.draw(Color.white);

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
