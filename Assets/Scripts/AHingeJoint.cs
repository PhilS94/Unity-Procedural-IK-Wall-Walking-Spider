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
    public Vector3 rotationPointOffset = Vector3.zero;
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
    private Vector3 rotPoint;

    //keeps track of the current state of rotation and is important part of the angle clamping
    //However i feel like this doesnt update sufficiently well, plus whenever im in a bad rotation state
    //i dont notice this
    private float currentAngle = 0;

    // Start is called before the first frame update
    void Start() {
    }

    private void updateValues()
    {
        if (rotationAxisPosition == RotationAxis.XAxis)
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
        rotPoint = transform.position + rotationPointOffset.x * transform.right + rotationPointOffset.y * transform.up + rotationPointOffset.z * transform.forward;
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
        if (debugAngle != 0)
        {
            applyRotation(debugAngle);
        }

    }


    /*
     * Checks whether the rotation around angle is a valid Orientation or not, and clamps it to a valid one if necessary
     * and then applies this rotation
     */
    public void applyRotation(float angle) //Würde gern als Parameter Quaternion haben.. Aber dann kann ich nicht wirklich sehen ob es sich um eine rot um die rotations Achse handelt ?
    {
        updateValues();

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
        //                  10              -60     -30                    = -30-10
        // Apply the rotation
        //transform.RotateAround(rotPoint, rotationAxis, angle);
        transform.rotation = Quaternion.AngleAxis(angle, rotationAxis) * transform.rotation;

        // Refresh the current angle
        currentAngle += angle;

    }

    public Vector3 getRotationAxis()
    {
        return rotationAxis;
    }

    public Vector3 getRotationPoint()
    {
        return rotPoint;
    }
    void OnDrawGizmosSelected()
    {
        if (UnityEditor.Selection.activeGameObject != transform.gameObject && !showDebug)
        {
            return;
        }

        //Maybe update the rotationAxis in the Update or at the Start

        // This means that the rotAxis changes as this joint rotates, that is changing its forward, very bad!
        //rotationAxis = Quaternion.Euler(rotationAxisPosition) * transform.forward;

        updateValues(); //to initialize the rotationaxis and rotpoint
        orientation = Quaternion.AngleAxis(currentOrientation, rotationAxis) * perpendicular;
        minVector = Quaternion.AngleAxis(minAngle, rotationAxis) * orientation;
        maxVector = Quaternion.AngleAxis(maxAngle, rotationAxis) * orientation;

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(rotPoint,rotPoint+ 1.0f * rotationAxis.normalized);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(rotPoint, 0.01f);

        Gizmos.color = Color.yellow;
        Vector3 minV = Quaternion.AngleAxis(-currentAngle, rotationAxis) * minVector;
        Vector3 maxV = Quaternion.AngleAxis(-currentAngle, rotationAxis) * maxVector;
        Gizmos.DrawLine(rotPoint, rotPoint + 0.2f * minV.normalized);
        Gizmos.DrawLine(rotPoint, rotPoint + 0.2f * maxV.normalized);

        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawSolidArc(rotPoint, rotationAxis, minV, maxAngle - minAngle, 0.2f);


        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawSolidArc(rotPoint, rotationAxis, minV, currentAngle-minAngle, 0.1f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(rotPoint, rotPoint+0.2f*orientation);
    }
}