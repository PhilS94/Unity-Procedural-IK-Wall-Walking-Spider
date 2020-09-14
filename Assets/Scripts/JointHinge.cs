/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * This class represents a hinge joint with a specified rotation axis, rotation point and rotational limits.
 * The function applyRotation() is made to be called externally to rotate the gameobject this class is attached to.
 * It is supplied with a weigth parameter which can be accessed externally for e.g. IK solving purposes.
 */


public class JointHinge : MonoBehaviour {


    [Header("Debug")]
    [Range(1f, 10.0f)]
    public float debugIconScale = 1.0f;

    public bool deactivateJoint = false;
    public bool useRotationLimits = true;

    public Transform root;


    public enum rotationAxisMode {
        RootX,
        RootY,
        RootZ,
        LocalX,
        LocalY,
        LocalZ
    }

    [Header("Rotation Axis and Point")]
    public rotationAxisMode rotMode;
    public bool negative = false;
    public Vector3 rotationAxisOrientation;
    public Vector3 rotationPointOffset = Vector3.zero;

    [Header("Angle Restriction")]
    [Range(-180, 180)]
    public float startOrientation = 0;
    [Range(-90, 90)]
    public float minAngle = -90;
    [Range(-90, 90)]
    public float maxAngle = 90;

    [Header("Joint Weigth")]
    [Range(0.0f, 1.0f)]
    public float weight = 1.0f;

    private Vector3 rotationAxisLocal;
    private Vector3 perpendicularLocal;
    private Vector3 rotPointLocal;

    private Vector3 defaultOrientationLocal;
    private Vector3 orientationLocal;
    private Vector3 minOrientationLocal;
    private Vector3 maxOrientationLocal;

    //Keeps track of the current state of rotation and is important part of the angle clamping
    private float currentAngle = 0;

    private void Awake() {
        setupValues();
    }

    private void setupValues() {
        Vector3 r = Vector3.zero;
        Vector3 p = Vector3.zero; // Just for unassigned error, will never be zero vector

        if (rotMode == rotationAxisMode.RootX) {
            r = root.right;
            p = root.forward;
        }
        else if (rotMode == rotationAxisMode.RootY) {
            r = root.up;
            p = root.right;
        }
        else if (rotMode == rotationAxisMode.RootZ) {
            r = root.forward;
            p = root.up;
        }
        else if (rotMode == rotationAxisMode.LocalX) {
            r = transform.right;
            p = transform.forward;
        }
        else if (rotMode == rotationAxisMode.LocalY) {
            r = transform.up;
            p = transform.right;
        }
        else if (rotMode == rotationAxisMode.LocalZ) {
            r = transform.forward;
            p = transform.right;
        }
        if (negative) {
            r = -r;
            p = -p;
        }

        rotationAxisLocal = Quaternion.Euler(rotationAxisOrientation) * transform.InverseTransformDirection(r);
        perpendicularLocal = Quaternion.Euler(rotationAxisOrientation) * transform.InverseTransformDirection(p);

        if ((rotMode == rotationAxisMode.LocalX) || (rotMode == rotationAxisMode.LocalY) || (rotMode == rotationAxisMode.LocalZ)) {
            orientationLocal = Quaternion.AngleAxis(startOrientation, rotationAxisLocal) * perpendicularLocal;
            defaultOrientationLocal = Quaternion.AngleAxis(-currentAngle, rotationAxisLocal) * orientationLocal;
            minOrientationLocal = Quaternion.AngleAxis(minAngle - currentAngle, rotationAxisLocal) * orientationLocal;
            maxOrientationLocal = Quaternion.AngleAxis(maxAngle - currentAngle, rotationAxisLocal) * orientationLocal;
        }
        else {
            defaultOrientationLocal = Quaternion.AngleAxis(startOrientation, rotationAxisLocal) * perpendicularLocal;
            orientationLocal = Quaternion.AngleAxis(currentAngle, rotationAxisLocal) * defaultOrientationLocal;
            minOrientationLocal = Quaternion.AngleAxis(minAngle, rotationAxisLocal) * defaultOrientationLocal;
            maxOrientationLocal = Quaternion.AngleAxis(maxAngle, rotationAxisLocal) * defaultOrientationLocal;
        }
    }

    void Update() {
        if (minAngle > maxAngle) {
            Debug.LogError("The minimum hinge angle on " + gameObject.name + " is larger than the maximum hinge angle.");
            maxAngle = minAngle;
        }
    }


    /*
     * This is the main function called from other classes. It rotates the hinge joint by the given angle with respect to the limits given in this class.
     */
    public void applyRotation(float angle) {
        if (deactivateJoint) return;

        angle = angle % 360;

        if (angle == -180) angle = 180;

        if (angle > 180) angle -= 360;

        if (angle < -180) angle += 360;

        //Now angle is of the form (-180,180]

        Vector3 rotationAxis = getRotationAxis();
        Vector3 rotPoint = getRotationPoint();

        if (useRotationLimits) {
            //The angle gets clamped if its application to the current angle exceeds the limits.
            angle = Mathf.Clamp(currentAngle + angle, minAngle, maxAngle) - currentAngle;
        }

        // Apply the rotation
        transform.RotateAround(rotPoint, rotationAxis, angle);

        // Update the current angle
        currentAngle += angle;
    }

    public float getWeight() {
        return weight;
    }

    /*
     * This function fails if minOrientation and maxOrientation have an angle greather than 180
     * since signedangle returns the smaller angle, which i dont really want, but it suffices right now since i dont have these big of DOF
     * Returns -1 if v is to the left of Min, +1 if v is to the right of Max and 0 if v is between min and max
     * */
    public int isVectorWithinScope(Vector3 v) {
        Vector3 rotationAxis = getRotationAxis();
        float angle1 = Vector3.SignedAngle(getMinOrientation(), v, rotationAxis); // should be clockwise, thus positive
        float angle2 = Vector3.SignedAngle(v, getMaxOrientation(), rotationAxis); // should also be clockwise, thus positive

        if (angle1 >= 0 && angle2 >= 0) return 0;
        else if (angle1 < 0 && angle2 < 0) {
            // in the negative scope, therefore use midOrientation to solve this scenario
            float angle3 = Vector3.SignedAngle(getMidOrientation(), v, rotationAxis);
            if (angle3 > 0) return +1;
            else return -1;
        }
        else if (angle1 < 0) return -1;
        else return +1;
    }


    public Vector3 getRotationAxis() {
        return transform.TransformDirection(rotationAxisLocal);
    }

    public Vector3 getPerpendicular() {
        return transform.TransformDirection(perpendicularLocal);
    }

    public Vector3 getRotationPoint() {
        return transform.TransformPoint(0.01f * rotationPointOffset);
    }

    private Vector3 getOrientation() {
        return transform.TransformDirection(orientationLocal);
    }

    private Vector3 getDefaultOrientation() {
        return transform.TransformDirection(defaultOrientationLocal);
    }

    private Vector3 getMinOrientation() {
        return transform.TransformDirection(minOrientationLocal);
    }

    private Vector3 getMaxOrientation() {
        return transform.TransformDirection(maxOrientationLocal);
    }
    public Vector3 getMidOrientation() {
        return transform.TransformDirection(Quaternion.AngleAxis(0.5f * (maxAngle - minAngle), rotationAxisLocal) * minOrientationLocal);
    }

    public float getAngleRange() {
        return maxAngle - minAngle;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected() {
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;

        Awake();

        float scale = transform.lossyScale.y * 0.005f * debugIconScale;


        Vector3 rotationAxis = getRotationAxis();
        Vector3 rotPoint = getRotationPoint();
        Vector3 minOrientation = getMinOrientation();
        Vector3 orientation = getOrientation();
        Vector3 defaultOrientation = getDefaultOrientation();

        //RotAxis
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(rotPoint, rotPoint + scale * rotationAxis);

        //RotPoint
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(rotPoint, 0.01f * scale);

        // Rotation Limit Arc
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawSolidArc(rotPoint, rotationAxis, minOrientation, maxAngle - minAngle, 0.2f * scale);

        // Current Rotation Used Arc
        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawSolidArc(rotPoint, rotationAxis, minOrientation, currentAngle - minAngle, 0.1f * scale);

        // Current Rotation used (same as above) just an additional line to emphasize
        Gizmos.color = Color.red;
        Gizmos.DrawLine(rotPoint, rotPoint + 0.2f * scale * orientation);

        // Default Rotation
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(rotPoint, rotPoint + 0.2f * scale * defaultOrientation);
    }
#endif
}