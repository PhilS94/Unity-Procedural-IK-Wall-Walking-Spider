using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    public enum PositionInterpolation {Slerp,Lerp, SmoothDamp}
    [Header("Interpolation")]
    public PositionInterpolation positionInterpolationType;
    private Vector3 velocity = Vector3.zero;

    [Header("Angle Restrictions")]
    [Range(0.01f, 90.0f)]
    public float camUpperAngleMargin = 30.0f;
    [Range(0.01f, 90.0f)]
    public float camLowerAngleMargin = 60.0f;

    protected void Awake() {
        cam = GetComponent<Camera>();
        setupCamTarget();

        // Now unparent the camera itself so it can move freely and use the above target to lerp
        transform.parent = null;
    }

    protected void setupCamTarget() {
        // For the target, create new Gameobject with same position and rotation as camera starts with and parent to Spider
        GameObject g = new GameObject(gameObject.name + " Target");
        camTarget = g.transform;
        camTarget.position = transform.position;
        camTarget.rotation = transform.rotation;
    }

    protected void Update() {
        RotateCameraHorizontal(Input.GetAxis("Mouse X") * XSensitivity, false);
        RotateCameraVertical(-Input.GetAxis("Mouse Y") * YSensitivity, false);
    }

    protected void LateUpdate() {
        if (showDebug) drawDebug();

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
    }

    public abstract void RotateCameraHorizontal(float angle, bool onlyTarget = true);

    public abstract void RotateCameraVertical(float angle, bool onlyTarget = true);

    //Clamp angle to (-180,180]
    public void clampAngle(ref float angle) {
        angle = angle % 360;
        if (angle == -180) angle = 180;
        if (angle > 180) angle -= 360;
        if (angle < -180) angle += 360;
    }

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
    protected void drawDebug() {
        Debug.DrawLine(transform.position, observedObject.position, Color.white);
        Vector3 camToObject = observedObject.position - transform.position;

        //Something buggy here
        float currentAngle = Vector3.SignedAngle(observedObject.transform.up, observedObject.transform.position - camTarget.position, camTarget.right);
        Vector3 v = Quaternion.AngleAxis(-currentAngle, camTarget.right) * camToObject;
        Vector3 up = observedObject.TransformDirection(Quaternion.AngleAxis(camUpperAngleMargin, camTarget.right) * v);
        Vector3 down = observedObject.TransformDirection(Quaternion.AngleAxis(camLowerAngleMargin, camTarget.right) * v);
        DebugShapes.DrawRay(observedObject.transform.position, up, Color.black);
        DebugShapes.DrawRay(observedObject.transform.position, down, Color.black);

        DebugShapes.DrawPoint(camTarget.position, Color.magenta, 0.1f);
        DebugShapes.DrawRay(camTarget.position, camTarget.forward, Color.blue);
        DebugShapes.DrawRay(camTarget.position, camTarget.right, Color.red);
        DebugShapes.DrawRay(camTarget.position, camTarget.up, Color.green);
    }

#if UNITY_EDITOR
    protected void OnDrawGizmosSelected() {
        if (!showDebug) return;
        if (UnityEditor.EditorApplication.isPlaying) return;
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;
        if (observedObject == null) return;
        camTarget = transform;
        drawDebug();
    }
#endif
}
