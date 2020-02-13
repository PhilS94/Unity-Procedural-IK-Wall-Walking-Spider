using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class AHingeJoint : MonoBehaviour {

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

    [Header("Debug")]
    [Range(0.1f, 20.0f)]
    public float debugIconScale = 1.0f;

    private Vector3 rotationAxis;
    private Vector3 perpendicular;
    private Vector3 rotPoint;

    private Vector3 defaultOrientation;
    private Vector3 orientation;
    private Vector3 minOrientation;
    private Vector3 maxOrientation;

    //keeps track of the current state of rotation and is important part of the angle clamping
    //However i feel like this doesnt update sufficiently well, plus whenever im in a bad rotation state
    //i dont notice this
    private float currentAngle = 0;

    private void Awake() {
        updateValues();
    }

    // Start is called before the first frame update
    void Start() {
    }

    private void updateValues() {
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

        rotationAxis = transform.TransformVector(Quaternion.Euler(rotationAxisOrientation) * transform.InverseTransformVector(r)).normalized;
        perpendicular = transform.TransformVector(Quaternion.Euler(rotationAxisOrientation) * transform.InverseTransformVector(p)).normalized;
        rotPoint = getRotationPoint();

        if ((rotMode == rotationAxisMode.LocalX) || (rotMode == rotationAxisMode.LocalY) || (rotMode == rotationAxisMode.LocalZ)) {
            orientation = Quaternion.AngleAxis(startOrientation, rotationAxis) * perpendicular;
            defaultOrientation = Quaternion.AngleAxis(-currentAngle, rotationAxis) * orientation;
            minOrientation = Quaternion.AngleAxis(minAngle - currentAngle, rotationAxis) * orientation;
            maxOrientation = Quaternion.AngleAxis(maxAngle - currentAngle, rotationAxis) * orientation;
        }
        else {
            defaultOrientation = Quaternion.AngleAxis(startOrientation, rotationAxis) * perpendicular;
            orientation = Quaternion.AngleAxis(currentAngle, rotationAxis) * defaultOrientation;
            minOrientation = Quaternion.AngleAxis(minAngle, rotationAxis) * defaultOrientation;
            maxOrientation = Quaternion.AngleAxis(maxAngle, rotationAxis) * defaultOrientation;
        }

    }

    // Update is called once per frame
    void Update() {
        updateValues(); //to refresh all the values, since they are all dependant on the rotation axis which changes as the spider rotates

        if (minAngle > maxAngle) {
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
        if (deactivateJoint) {
            return;
        }

        updateValues(); // important to update here since this function is called from the ccdiksolver. However i do think i can do this in update

        angle = angle % 360; //Jetzt hab ich Winkel zwischen -360 und 360

        if (angle == -180) {
            angle = 180;
        }

        if (angle > 180) {
            angle -= 360;
        }

        if (angle < -180) {
            angle += 360;
        }

        //Now angle is of the form (-180,180]


        if (useRotationLimits) {
            // Example, say angle is 20degrees, and our currentAngle is 60,  but our maxAngle is 70
            // What we want is to get the angle which rotates to the maxAnglePos which is in this example is 10 degrees
            angle = Mathf.Clamp(currentAngle + angle, minAngle, maxAngle) - currentAngle;
            //                  10              -60     -30                    = -30-10
        }

        // Apply the rotation
        transform.RotateAround(rotPoint, rotationAxis, angle);
        //transform.rotation = Quaternion.AngleAxis(angle, rotationAxis) * transform.rotation;

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
        float angle1 = Vector3.SignedAngle(minOrientation, v, rotationAxis); // should be clockwise, thus positive
        float angle2 = Vector3.SignedAngle(v, maxOrientation, rotationAxis); // should also be clockwise, thus positive

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
        return rotationAxis;
    }

    // Normal i would return rotPoint, but i call this function from the awakre function of CCDiKSolver
    public Vector3 getRotationPoint() {
        return transform.position + rotationPointOffset.x * transform.right + rotationPointOffset.y * transform.up + rotationPointOffset.z * transform.forward;
    }

    public Vector3 getOrientation() {
        return orientation;
    }

    public Vector3 getMinOrientation() {
        return minOrientation;
    }

    public Vector3 getMaxOrientation() {
        return maxOrientation;
    }

    public Vector3 getMidOrientation() {
        return Quaternion.AngleAxis(0.5f*(maxAngle-minAngle), rotationAxis) * minOrientation;
    }

    public float getAngleRange() {
        return maxAngle - minAngle;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected() {
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;

        updateValues(); //to refresh all the below values to be drawn

        //RotAxis
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(rotPoint, rotPoint + debugIconScale * rotationAxis);

        //RotPoint
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(rotPoint, 0.01f * debugIconScale);

        // Rotation Limit Arc
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawSolidArc(rotPoint, rotationAxis, minOrientation, maxAngle - minAngle, 0.2f * debugIconScale);

        // Current Rotation Used Arc
        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawSolidArc(rotPoint, rotationAxis, minOrientation, currentAngle - minAngle, 0.1f * debugIconScale);

        // Current Rotation used (same as above) just an additional line to emphasize
        Gizmos.color = Color.red;
        Gizmos.DrawLine(rotPoint, rotPoint + 0.2f * debugIconScale * orientation);

        // Default Rotation
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(rotPoint, rotPoint + 0.2f * debugIconScale * defaultOrientation);
    }
#endif
}