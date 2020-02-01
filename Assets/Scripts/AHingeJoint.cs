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

    public RotationAxis rotationAxisPosition = RotationAxis.XAxis;
    public Vector3 rotationPointOffset = Vector3.zero;
    public float currentOrientation = 0;

    // Current Stage
    [Range(-180, 180)]
    public float minAngle = -90;
    [Range(-180, 180)]
    public float maxAngle = 90;

    [Range(0.0f, 1.0f)]
    public float weight = 1.0f;

    public bool useRotationLimits = true;
    public bool deactivateJoint = false;

    [Range(0.1f, 10.0f)]
    public float debugIconScale = 1.0f;

    private Vector3 rotationAxis;
    private Vector3 perpendicular;
    private Vector3 orientation;
    private Vector3 minOrientation;
    private Vector3 maxOrientation;
    private Vector3 rotPoint;

    //keeps track of the current state of rotation and is important part of the angle clamping
    //However i feel like this doesnt update sufficiently well, plus whenever im in a bad rotation state
    //i dont notice this
    private float currentAngle = 0;

    private void Awake()
    {
        updateValues();
    }

    // Start is called before the first frame update
    void Start()
    {
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
        orientation = Quaternion.AngleAxis(currentOrientation, rotationAxis) * perpendicular;
        minOrientation = Quaternion.AngleAxis(minAngle - currentAngle, rotationAxis) * orientation;
        maxOrientation = Quaternion.AngleAxis(maxAngle - currentAngle, rotationAxis) * orientation;
    }

    // Update is called once per frame
    void Update()
    {
        if (minAngle > maxAngle)
        {
            Debug.LogError("The minimum hinge angle on " + gameObject.name + " is larger than the maximum hinge angle.");
            maxAngle = minAngle;
        }
    }


    /*
     * Checks whether the rotation around angle is a valid Orientation or not, and clamps it to a valid one if necessary
     * and then applies this rotation
     */
    public void applyRotation(float angle) //Würde gern als Parameter Quaternion haben.. Aber dann kann ich nicht wirklich sehen ob es sich um eine rot um die rotations Achse handelt ?
    {
        if (deactivateJoint)
        {
            return;
        }

        updateValues(); // important to update here since this function is called from the ccdiksolver. However i do think i can do this in update

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


        if (useRotationLimits)
        {
            // Example, say angle is 20degrees, and our currentAngle is 60,  but our maxAngle is 70
            // What we want is to get the angle which rotates to the maxAnglePos which is in this case 10degrees
            angle = Mathf.Clamp(currentAngle + angle, minAngle, maxAngle) - currentAngle;
            //                  10              -60     -30                    = -30-10
        }

        // Apply the rotation
        transform.RotateAround(rotPoint, rotationAxis, angle); //Think about using this since i actually do use the rotationPoints in other classes
        //transform.rotation = Quaternion.AngleAxis(angle, rotationAxis) * transform.rotation;

        // Refresh the current angle
        currentAngle += angle;

    }

    public float getWeight()
    {
        return weight;
    }

    void OnDrawGizmosSelected()
    {
        if (!UnityEditor.Selection.Contains(transform.gameObject))
        {
            return;
        }

        updateValues(); //to refresh all the below values to be drawn

        //RotAxis
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(rotPoint, rotPoint + debugIconScale * rotationAxis);

        //RotPoint
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(rotPoint, 0.01f * debugIconScale);


        //Gizmos.color = Color.yellow;
        //Gizmos.DrawLine(rotPoint, rotPoint + 0.2f * debugIconScale * minOrientation);
        //Gizmos.DrawLine(rotPoint, rotPoint + 0.2f * debugIconScale * maxOrientation);

        // Rotation Limit Arc
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawSolidArc(rotPoint, rotationAxis, minOrientation, maxAngle - minAngle, 0.2f * debugIconScale);

        // Current Rotation Used Arc
        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawSolidArc(rotPoint, rotationAxis, minOrientation, currentAngle - minAngle, 0.1f * debugIconScale);

        // Current Rotation used (same as above) just an additional line to emphasize
        Gizmos.color = Color.red;
        Gizmos.DrawLine(rotPoint, rotPoint + 0.2f * debugIconScale * orientation);
    }

    /*
     * This function fails if minOrientation and maxOrientation have an angle greather than 180
     * since signedangle returns the smaller angle, which i dont really want, but it suffices right now since i dont have these big of DOF
     * */
    public bool isVectorWithinScope(Vector3 v)
    {
        float angle1 = Vector3.SignedAngle(minOrientation, v, rotationAxis); // should be clockwise, thus positive
        float angle2 = Vector3.SignedAngle(v, maxOrientation, rotationAxis); // should also be clockwise, thus positive
        return (angle1 >= 0 && angle2 >= 0);
    }


    public Vector3 getRotationAxis()
    {
        return rotationAxis;
    }

    // Normal i would return rotPoint, but i call this function from the awakre function of CCDiKSolver
    public Vector3 getRotationPoint()
    {
        return transform.position + rotationPointOffset.x * transform.right + rotationPointOffset.y * transform.up + rotationPointOffset.z * transform.forward;
    }

    public Vector3 getOrientation()
    {
        return orientation;
    }

    public Vector3 getMinOrientation()
    {
        return minOrientation;
    }

    public Vector3 getMaxOrientation()
    {
        return maxOrientation;
    }

    public Vector3 getMidOrientation()
    {
        return (maxOrientation + minOrientation).normalized;
    }

    public float getAngleRange()
    {
        return maxAngle - minAngle;
    }
}