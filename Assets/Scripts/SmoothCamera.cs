using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SmoothCamera : MonoBehaviour {

    public Transform followingObject;
    public float distanceToObject;
    public bool correctTwist =true;

    public float lerpTime;
    public float rotationSpeed;

    private Camera cam;
    private Vector3 targetPosition;
    private Quaternion targetRotation;

    private Vector3 velocity;

    void Awake() {
        cam = GetComponent<Camera>();
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        transform.parent = null;
    }

    private void LateUpdate() {
        updateTargetPosition();
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, lerpTime);

        if (correctTwist) targetRotation = Quaternion.LookRotation(targetRotation * Vector3.forward, followingObject.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

        DebugShapes.DrawPoint(targetPosition, Color.magenta,1f);
    }

    private void updateTargetPosition() {
        Vector3 toObject = (followingObject.position - targetPosition);
        float distance = toObject.magnitude;
        addToTargetPosition((distance - distanceToObject) * toObject / distance);
    }

    public void setTargetPosition(Vector3 target) {
        targetPosition = target;
    }

    private void setTargetRotation(Quaternion target) {
        targetRotation = target;
    }

    public void addToTargetPosition(Vector3 add) {
        targetPosition += add;
    }

    public void rollAround(Vector3 pivot, float angle) {
        Quaternion fromTo = Quaternion.AngleAxis(angle, transform.forward);
        setTargetPosition(fromTo * (targetPosition - pivot) + pivot);
        setTargetRotation(fromTo * targetRotation);
    }

    public void pitchAround(Vector3 pivot, float angle) {
        Quaternion fromTo = Quaternion.AngleAxis(angle, transform.right);
        setTargetPosition(fromTo * (targetPosition - pivot) + pivot);
        setTargetRotation(fromTo * targetRotation);
    }

    public void yawAround(Vector3 pivot, float angle) {
        Quaternion fromTo = Quaternion.AngleAxis(angle, transform.up);
        setTargetPosition(fromTo * (targetPosition - pivot) + pivot);
        setTargetRotation(fromTo * targetRotation);
    }

    public void rotateAround(Vector3 pivot, Vector3 normal, float angle) {
        Quaternion fromTo = Quaternion.AngleAxis(angle, normal);
        setTargetPosition(fromTo * (targetPosition - pivot) + pivot);
        setTargetRotation(fromTo * targetRotation);
    }

    public Camera getCamera() {
        return cam;
    }
}
