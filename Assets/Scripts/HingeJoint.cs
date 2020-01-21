using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class HingeJoint : MonoBehaviour
{

    public bool showDebug = true;

    public Vector3 rotationAxisPosition = Vector3.zero;
    public float currentOrientation = 0;

    // Current Stage
    [Range(-180, 180)]
    public float minAngle = -90;
    [Range(-180, 180)]
    public float maxAngle = 90;

    private Vector3 minVector;
    private Vector3 maxVector;

    private Vector3 rotationAxis;
    private Vector3 orientation;

    // Start is called before the first frame update
    void Start()
    {
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
        Quaternion rot = transform.rotation;
        clampRotation(rot);
        transform.rotation.Set(rot.x,rot.y,rot.z,rot.w);
        
    }

    void OnDrawGizmosSelected()
    {
        if (UnityEditor.Selection.activeGameObject != transform.gameObject || !showDebug)
        {
            return;
        }

        rotationAxis = Quaternion.Euler(rotationAxisPosition) * transform.forward;
        orientation = Quaternion.AngleAxis(currentOrientation, rotationAxis) * transform.right;
        minVector = Quaternion.AngleAxis(minAngle, rotationAxis) * orientation;
        maxVector = Quaternion.AngleAxis(maxAngle, rotationAxis) * orientation;

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + 0.5f* rotationAxis.normalized);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + 0.5f * orientation.normalized);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + 0.5f * minVector.normalized);
        Gizmos.DrawLine(transform.position, transform.position + 0.5f * maxVector.normalized);
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawSolidArc(transform.position, rotationAxis, minVector, maxAngle-minAngle, 0.05f);
    }

    /*
     * Checks whether the Quaternion q is a valid Orientation or not, and clamps it to a valid one
     */
    Quaternion clampRotation(Quaternion q) //Würde gern als Parameter Quaternion haben.. Aber dann kann ich nicht wirklich sehen ob es sich um eine rot um die rotations Achse handelt ?
    {
        if (q==Quaternion.identity)
        {
            return Quaternion.identity;
        }
  
        q.ToAngleAxis(out float angle, out Vector3 axisQ);

        //Does not work like this
        if (axisQ.normalized != rotationAxis.normalized)
        {
            Debug.Log("Quaternion passed into the clamp Method is not one using the axis of the joint.");
            return Quaternion.identity;
        }


        angle = angle % 360; //Jetzt hab ich Winkel zwischen -360 und 360

        if (angle ==-180)
        {
            angle = 180;
        }

        if (angle>180)
        {
            angle -= 360;
        }

        if (angle < -180)
        {
            angle += 360;
        }

        //Jetzt sollte angle in der form (-180,180] sein

        angle = Mathf.Clamp(angle, minAngle, maxAngle);
        return Quaternion.AngleAxis(angle, rotationAxis);
    }
}