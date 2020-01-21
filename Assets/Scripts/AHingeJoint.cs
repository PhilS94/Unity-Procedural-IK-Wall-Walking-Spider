using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class AHingeJoint : MonoBehaviour
{

    public enum RotationAxis
    {
        XAxis,
        YAxis,
        ZAxis
    }

    public bool showDebug = true;

    public RotationAxis rotationAxisPosition = RotationAxis.XAxis;
    public float currentOrientation = 0;

    // Current Stage
    [Range(-180, 180)]
    public float minAngle = -90;
    [Range(-180, 180)]
    public float maxAngle = 90;

    [Range(-1.0f, 1.0f)]
    public float debugAngle=0;

    private Vector3 minVector;
    private Vector3 maxVector;

    private Vector3 rotationAxis;
    private Vector3 perpendicular;
    private Vector3 orientation;

    private float currentAngle = 0;

    // Start is called before the first frame update
    void Start()
    {
        if (rotationAxisPosition==RotationAxis.XAxis)
        {
            rotationAxis = transform.right;
            perpendicular = transform.up;
        }
        if (rotationAxisPosition == RotationAxis.YAxis)
        {
            rotationAxis = transform.up;
            perpendicular = transform.forward;
        }
        if (rotationAxisPosition == RotationAxis.ZAxis)
        {
            rotationAxis = transform.forward;
            perpendicular = transform.right;
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (minAngle > maxAngle)
        {
            Debug.LogError("The minimum hinge angle on " + gameObject.name + " is larger than the maximum hinge angle.");
            maxAngle = minAngle;
        }

        //Just for debugging
        //applyRotation(debugAngle);

    }


    /*
     * Checks whether the rotation around angle is a valid Orientation or not, and clamps it to a valid one if necessary
     * returns a Quaternion which we are allowed to apply
     */
    public void applyRotation(float angle) //Würde gern als Parameter Quaternion haben.. Aber dann kann ich nicht wirklich sehen ob es sich um eine rot um die rotations Achse handelt ?
    {

        angle = angle % 360; //Jetzt hab ich Winkel zwischen -360 und 360

        if (angle == -180)
        {
            angle = 180;
        }

        if (angle > 180)
        {
            angle -= 360;
        }

        if (angle < -180)
        {
            angle += 360;
        }

        //Jetzt sollte angle in der form (-180,180] sein

        // Example, say angle is 20degrees, and our currentAngle is 60,  but our maxAngle is 70
        // What we want is to get the angle which rotates to the maxAnglePos which is in this case 10degrees
        angle = Mathf.Clamp(currentAngle + angle, minAngle, maxAngle) -currentAngle;

        // Apply the rotation
        transform.rotation = Quaternion.AngleAxis(angle, rotationAxis) * transform.rotation;

        // Refresh the current angle
        currentAngle += angle;

    }

    public Vector3 getRotationAxis()
    {
        return rotationAxis;
    }

    void OnDrawGizmosSelected()
    {
        if (UnityEditor.Selection.activeGameObject != transform.gameObject || !showDebug)
        {
            return;
        }



        //Maybe update the rotationAxis in the Update or at the Start

        // This means that the rotAxis changes as this joint rotates, that is changing its forward, very bad!
        //rotationAxis = Quaternion.Euler(rotationAxisPosition) * transform.forward;

        Start(); //to initialize the rotationaxis
        orientation = Quaternion.AngleAxis(currentOrientation, rotationAxis) * perpendicular;
        minVector = Quaternion.AngleAxis(minAngle, rotationAxis) * orientation;
        maxVector = Quaternion.AngleAxis(maxAngle, rotationAxis) * orientation;

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + 100.0f * rotationAxis.normalized);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + 100.0f * orientation.normalized);

        Gizmos.color = Color.yellow;
        Vector3 minV = Quaternion.AngleAxis(-currentAngle, rotationAxis) * minVector;
        Vector3 maxV = Quaternion.AngleAxis(-currentAngle, rotationAxis) * maxVector;
        Gizmos.DrawLine(transform.position, transform.position + 100.0f * minV.normalized);
        Gizmos.DrawLine(transform.position, transform.position + 100.0f * maxV.normalized);
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawSolidArc(transform.position, rotationAxis, minV, maxAngle - minAngle, 10.0f);
    }
}