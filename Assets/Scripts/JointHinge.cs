/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


/*
 * This class represents a hinge joint with a specified rotation axis, rotation point and rotational limits.
 * The function applyRotation() is made to be called externally to rotate the gameobject this class is attached to.
 * It is supplied with a weigth parameter which can be accessed externally for e.g. IK solving purposes.
 */


public class JointHinge : MonoBehaviour {

    [Header("General")]
    public bool deactivateJoint = false;
    public bool useRotationLimits = true;

    [Header("Root Reference")]
    [Tooltip("The root reference is used for the determining the rotation axis if a Root rotation mode is selected.")]
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
    [Tooltip("Set the rotation axis to a pre defined X Y or Z vector and fine adjust by change of orientation.")]
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
    private Vector3 defaultOrientationLocal;
    private Vector3 orientationLocal;
    private Vector3 minOrientationLocal;
    private Vector3 maxOrientationLocal;

    // Keeps track of the current state of rotation and is important part of the angle clamping
    private float currentAngle = 0;

    public void Awake() {
        Debug.Log("Called Awake " + name + " on JointHinge");
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

    //Clamps the angle to (-180,180]
    public void clampAngle(ref float angle) {
        angle = angle % 360;

        if (angle == -180) angle = 180;

        if (angle > 180) angle -= 360;

        if (angle < -180) angle += 360;
    }

    /*
     * This is the main function called from other classes. It rotates the hinge joint by the given angle with respect to the limits given in this class.
     */
    public void applyRotation(float angle) {
        if (deactivateJoint) return;

        clampAngle(ref angle);
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

    public Vector3 getOrientation() {
        return transform.TransformDirection(orientationLocal);
    }

    public Vector3 getDefaultOrientation() {
        return transform.TransformDirection(defaultOrientationLocal);
    }

    public Vector3 getMinOrientation() {
        return transform.TransformDirection(minOrientationLocal);
    }

    public Vector3 getMaxOrientation() {
        return transform.TransformDirection(maxOrientationLocal);
    }

    public Vector3 getMidOrientation() {
        return transform.TransformDirection(Quaternion.AngleAxis(0.5f * (maxAngle - minAngle), rotationAxisLocal) * minOrientationLocal);
    }

    public float getAngleRange() {
        return maxAngle - minAngle;
    }

    public float getCurrentAngleRange() {
        return currentAngle - minAngle;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(JointHinge))]
public class JointHingeEditor : Editor {

    private JointHinge joint;

    private static float debugIconScale = 5;

    private static bool showDebug = true;
    private static bool showRotationAxis = true;
    private static bool showRotationPoint = true;
    private static bool showAngleArc = true;

    public void OnEnable() {
        joint = (JointHinge)target;
        if (showDebug && !EditorApplication.isPlaying) joint.Awake();
    }

    public override void OnInspectorGUI() {
        if (joint == null) return;

        serializedObject.Update();

        EditorDrawing.DrawMonoScript(joint, typeof(JointHinge));

        EditorDrawing.DrawHorizontalLine();

        //Debug
        EditorGUILayout.LabelField("Debug Drawing", EditorStyles.boldLabel);
        showDebug = EditorGUILayout.Toggle("Show Debug Drawings", showDebug);
        if (showDebug) {
            EditorGUI.indentLevel++;
            {
                debugIconScale = EditorGUILayout.Slider("Drawing Scale", debugIconScale, 1f, 10f);
                showRotationAxis = EditorGUILayout.Toggle("Draw Rotation Axis", showRotationAxis);
                showRotationPoint = EditorGUILayout.Toggle("Draw Rotation Point", showRotationPoint);
                showAngleArc = EditorGUILayout.Toggle("Draw Joint Restricton Arc", showAngleArc);
            }
            EditorGUI.indentLevel--;
        }
        EditorDrawing.DrawHorizontalLine();

        //General
        EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
        serializedObject.FindProperty("deactivateJoint").boolValue = EditorGUILayout.Toggle("Deactivate Joint?", joint.deactivateJoint);
        EditorGUILayout.Space();

        if (!joint.deactivateJoint) {
            EditorGUI.indentLevel++;
            {
                // Rotation Axis
                EditorGUILayout.LabelField("Rotation Axis", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                {
                    serializedObject.FindProperty("rotMode").enumValueIndex = (int)(JointHinge.rotationAxisMode)EditorGUILayout.EnumPopup("Rotation Axis", joint.rotMode);
                    serializedObject.FindProperty("negative").boolValue = EditorGUILayout.Toggle("Negative?", joint.negative);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel++;
                {
                    if (joint.rotMode == JointHinge.rotationAxisMode.RootX || joint.rotMode == JointHinge.rotationAxisMode.RootY || joint.rotMode == JointHinge.rotationAxisMode.RootZ) {
                        serializedObject.FindProperty("root").objectReferenceValue = (Transform)EditorGUILayout.ObjectField("Root", joint.root, typeof(Transform), true);
                    }
                    serializedObject.FindProperty("rotationAxisOrientation").vector3Value = EditorGUILayout.Vector3Field("Rotation Axis Adjustment", joint.rotationAxisOrientation);
                    serializedObject.FindProperty("rotationPointOffset").vector3Value = EditorGUILayout.Vector3Field("Rotation Point Offset", joint.rotationPointOffset);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();

                //Joint Restrictions
                EditorGUILayout.LabelField("Joint Restrictions", EditorStyles.boldLabel);
                serializedObject.FindProperty("useRotationLimits").boolValue = EditorGUILayout.Toggle("Use Rotation Limits?", joint.useRotationLimits);
                if (joint.useRotationLimits) {
                    EditorGUI.indentLevel++;
                    {
                        serializedObject.FindProperty("startOrientation").floatValue = EditorGUILayout.Slider("Start Orientation", joint.startOrientation, -180, 180);
                        float min = joint.minAngle;
                        float max = joint.maxAngle;

                        EditorGUIUtility.labelWidth = 1;
                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.LabelField("Angles");
                            min = EditorGUILayout.FloatField(min);
                            EditorGUILayout.MinMaxSlider(ref min, ref max, -90, 90);
                            max = EditorGUILayout.FloatField(max);
                        }
                        EditorGUILayout.EndHorizontal();
                        EditorGUIUtility.labelWidth = 0;

                        serializedObject.FindProperty("minAngle").floatValue = min;
                        serializedObject.FindProperty("maxAngle").floatValue = max;
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space();

                //Weigth
                EditorGUILayout.LabelField("Joint Solving", EditorStyles.boldLabel);
                serializedObject.FindProperty("weight").floatValue = (float)EditorGUILayout.Slider("Joint Weight", joint.weight, 0, 1);
            }
            EditorGUI.indentLevel--;
        }

        // Apply Changes
        serializedObject.ApplyModifiedProperties();

        if (showDebug && !EditorApplication.isPlaying) joint.Awake();
    }

    void OnSceneGUI() {
        if (!showDebug || joint == null) return;

        float scale = joint.transform.lossyScale.y * 0.0002f * debugIconScale;

        Vector3 rotationAxis = joint.getRotationAxis();
        Vector3 rotPoint = joint.getRotationPoint();
        Vector3 minOrientation = joint.getMinOrientation();
        Vector3 orientation = joint.getOrientation();
        Vector3 defaultOrientation = joint.getDefaultOrientation();

        // Rotation Axis
        if (showRotationAxis) {
            Handles.color = Color.blue;
            Handles.DrawLine(rotPoint, rotPoint + scale * rotationAxis);
        }

        // Rotation Point
        if (showRotationPoint) {
            Handles.color = Color.green;
            Handles.RadiusHandle(joint.transform.rotation, rotPoint, 0.1f * scale, false);
        }

        // Joint Angle Arc
        if (showAngleArc) {
            // Rotation Limit Arc and start orientation
            Handles.color = Color.yellow;
            Handles.DrawSolidArc(rotPoint, rotationAxis, minOrientation, joint.getAngleRange(), scale);
            Handles.DrawLine(rotPoint, rotPoint + 1.3f * scale * defaultOrientation);

            // Rotation Current Arc
            Handles.color = Color.red;
            Handles.DrawSolidArc(rotPoint, rotationAxis, minOrientation, joint.getCurrentAngleRange(), 0.5f * scale);
            Handles.DrawLine(rotPoint, rotPoint + scale * orientation);
        }

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.yellow;
        Handles.Label(joint.transform.position, joint.name, style);
    }
}
#endif