using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CCDIKSolver : MonoBehaviour
{

    public Transform[] joints; //make sure every element in here has swingtwistjoint script, execpt for last which is the foot

    public enum weigthType
    {
        Constant,
        Decreasing,
        Increasing,
    }

    public weigthType weigthTypeJoints = weigthType.Constant;

    private float[] weightJoints;

    public bool useRotationsLimits = true;

    [Range(0, 1)]
    public float weight = 1.0f;

    [Range(0, 1)]
    public float tolerance = 0.01f;

    [Range(1, 100)]
    public int maxIterations = 20;

    public Transform debugTarget;

    private bool validChain = true;

    // Start is called before the first frame update
    void Start()
    {
        validChain = checkJointListValid();
        initializeJointWeigths();
    }

    // Update is called once per frame
    void Update()
    {
        if (!validChain)
        {
            return;
        }

        if (debugTarget != null && debugTarget.hasChanged)
        {
            debugTarget.transform.hasChanged = false;
            solveCCD(debugTarget.position);
            //solveJacobianTranspose(debugTarget.position);
        }
    }

    bool checkJointListValid()
    {
        for (int i = 0; i < joints.Length - 1; i++)
        {
            if (joints[i].GetComponent<AHingeJoint>() == null)
            {
                Debug.LogError("For the CCD chain " + this.name + " the joint number " + joints[i].gameObject.name + " does not have a AHingeJoint attached. Please attach one.");
                return false;
            }
        }
        if (joints[joints.Length - 1].GetComponent<AHingeJoint>() != null)
        {
            Debug.Log("For the CCD chain " + this.name + " the last joint " + joints[joints.Length - 1].gameObject.name + " has an attached AHingeJoint. The last joint will be used as the foot and therefore is not a joint. You might forgot to add the foot.");
        }
        return true;
    }

    void initializeJointWeigths()
    {
        weightJoints = new float[joints.Length - 1]; //foot doesnt need weigth
        float weightIncrement = 1.0f / weightJoints.Length;  //We want to set the weigths linearly increasing or decreasing
        for (int i = 0; i < weightJoints.Length; i++)
        {
            if (weigthTypeJoints == weigthType.Constant)
            {
                weightJoints[i] = 1.0f;
            }
            if (weigthTypeJoints == weigthType.Increasing)
            {
                weightJoints[i] = (i + 1) * weightIncrement; //should range from weightIncrement to 1 linearly
            }
            if (weigthTypeJoints == weigthType.Decreasing)
            {
                weightJoints[i] = (weightJoints.Length - i) * weightIncrement; //should range from 1 to weightIncrement linearly
            }
            Debug.Log("Joint Weight " + i + ": " + weightJoints[i]);
        }
    }

    void solveCCD(Vector3 targetPoint)
    {
        Debug.Log("Solving CCD brb");
        int k = 0;
        Transform endPoint = joints[joints.Length - 1];
        float distance = Vector3.Distance(targetPoint, endPoint.position);
        Vector3 toEnd;
        Vector3 toTarget;
        Vector3 rotAxis;
        float angle;
        AHingeJoint hinge;

        while (k < maxIterations && distance > tolerance)
        {
            for (int i = joints.Length - 2; i >= 0; i--) //Starts with last joint, since last element is just the foot
            {
                hinge = joints[i].gameObject.GetComponent<AHingeJoint>();
                rotAxis = hinge.getRotationAxis();
                toEnd = (endPoint.position - joints[i].position).normalized;
                toTarget = Vector3.ProjectOnPlane((targetPoint - joints[i].position),hinge.getRotationAxis()).normalized;

                //angle = Mathf.Acos(Vector3.Dot(Vector3.ProjectOnPlane(toEnd, hinge.getRotationAxis()) toTarget));
                angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(toEnd, rotAxis), Vector3.ProjectOnPlane(toTarget, rotAxis), rotAxis);
                angle *= weight * weightJoints[i]; //Multiplies the angle by the weight of the joint, should still stay in bounds since i starts with joints.length-2

                Debug.DrawLine(joints[i].position, targetPoint, Color.green);
                Debug.DrawLine(joints[i].position, joints[i + 1].position, Color.red);

                hinge.applyRotation(angle);

                //joints[i].Rotate(rotAxis, angle, Space.Self);
                //newRotation = Quaternion.AngleAxis(angle, rotAxis) * joints[i].localRotation;

                //swingTwistjoint.SwingTwistJointLimit(ref newRotation); //This limits the rotation using the values in the swingTwistJoint
                //joints[i].localRotation = newRotation; //Should do this smoothly via slerp for example


            }

            distance = Vector3.Distance(targetPoint, endPoint.position); //Refresh the distance so we can check if we are already close enough for the while loop check
            k++;
            Debug.Log("Completed " + k + "th iteration.");
        }
    }

    void solveJacobianTranspose(Vector3 targetPoint)
    {
        Transform endEffector = joints[joints.Length - 1];
        Vector3 error = targetPoint - endEffector.position;

        int amtAngles = (joints.Length - 1) * 3; //For every joint, except the endEffector we have three angles

        float[,] J = new float[3, amtAngles];

        Vector3 rotAxis = Vector3.zero;

        // Jacobian Form:
        //  a1  a2  a3  a4  a4  a6  a7  a8  a9  a10 a11 a12     Angles 1-12 sorted like this : a1 = Xrot angle1, a2 = Yrot angle1, a3 = Zrot angle1, a4 = Xrot angle2, ..
        //  *   *   *   *   *   *   *   *   *   *   *   *
        //  *   *   *   *   *   *   *   *   *   *   *   *
        //  *   *   *   *   *   *   *   *   *   *   *   *

        for (int col = 0; col < amtAngles; col++)
        {
            if (col % 3 == 0) //X-Rotation angle
            {
                rotAxis = joints[col / 3].right;
            }
            if (col % 3 == 1) //Y-Rotation angle
            {
                rotAxis = joints[col / 3].up;
            }
            if (col % 3 == 2) //Z-Rotation angle
            {
                rotAxis = joints[col / 3].forward;
            }
            Vector3 cross = Vector3.Cross(rotAxis, endEffector.position - joints[col / 3].position);

            J[0, col] = cross.x;
            J[1, col] = cross.y;
            J[2, col] = cross.z;
        }

        //Print Jacobian:
        string jacobianString = "";
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < amtAngles; col++)
            {

                jacobianString += J[row, col] + " ";
            }
            jacobianString += "\n";
        }
        Debug.Log(jacobianString);


        // Jacobian Transpose Form:

        //  *   *   *       a1
        //  *   *   *       a2
        //  *   *   *       a3  
        //  *   *   *       a4
        //  *   *   *       a5
        //  *   *   *
        //  *   *   *
        //  *   *   *
        //  *   *   *
        //  *   *   *
        //  *   *   *
        //  *   *   *

        float[,] JT = new float[amtAngles, 3];

        for (int row = 0; row < amtAngles; row++)
        {
            JT[row, 0] = J[0, row];
            JT[row, 1] = J[1, row];
            JT[row, 2] = J[2, row];
        }

        // Jacobian times Jacobian Transpose has Form:

        //  *   *   *
        //  *   *   *
        //  *   *   *

        float[,] JJT = new float[3, 3];
        multiply(ref J, ref JT, ref JJT);

        // Calculate needed multiplications
        float[] err = new float[] { error.x, error.y, error.z };
        float[] JJTe = new float[3];
        multiply(ref JJT, ref err, ref JJTe);

        Vector3 m_JJTe = new Vector3(JJTe[0], JJTe[1], JJTe[2]);

        //Calc the alpha value
        float alpha = 10 * Vector3.Dot(error, m_JJTe) / Vector3.Dot(m_JJTe, m_JJTe);

        //Calc the change in angle
        float[] angleChange = new float[amtAngles];
        multiply(ref JT, ref err, ref angleChange);
        multiply(ref angleChange, alpha);


        for (int col = 0; col < amtAngles; col++)
        {
            if (col % 3 == 0) //X-Rotation angle
            {
                rotAxis = joints[col / 3].right;
            }
            if (col % 3 == 1) //Y-Rotation angle
            {
                rotAxis = joints[col / 3].up;
            }
            if (col % 3 == 2) //Z-Rotation angle
            {
                rotAxis = joints[col / 3].forward;
            }

            //joints[col/3].Rotate(rotAxis, angleChange[col], Space.Self);
            Quaternion newRotation = Quaternion.AngleAxis(angleChange[col], rotAxis) * joints[col / 3].localRotation;
            SwingTwistJoint swingTwistjoint = joints[col / 3].gameObject.GetComponent<SwingTwistJoint>();
            swingTwistjoint.SwingTwistJointLimit(ref newRotation); //This limits the rotation using the values in the swingTwistJoint
            joints[col / 3].localRotation = newRotation; //Should do this smoothly via slerp for example
        }

    }

    void multiply(ref float[,] A, ref float[,] B, ref float[,] result)
    {
        if (A.GetLength(1) != B.GetLength(0) || result.GetLength(0) != A.GetLength(0) || result.GetLength(1) != B.GetLength(1))
        {
            Debug.Log("Can't multiply these matrices.");
            return;
        }

        for (int row = 0; row < result.GetLength(0); row++)
        {
            for (int col = 0; col < result.GetLength(1); col++)
            {
                float sum = 0;

                for (int k = 0; k < A.GetLength(1); k++)
                {
                    sum += A[row, k] * B[k, col];
                }
                result[row, col] = sum;
            }
        }

    }
    void multiply(ref float[,] A, ref float[] B, ref float[] result)
    {
        if (A.GetLength(1) != B.Length || result.Length != A.GetLength(0))
        {
            Debug.Log("Can't multiply these matrices.");
            return;
        }

        for (int row = 0; row < result.GetLength(0); row++)
        {
            float sum = 0;

            for (int k = 0; k < A.GetLength(1); k++)
            {
                sum += A[row, k] * B[k];
            }
            result[row] = sum;
        }
    }

    void multiply(ref float[] A, float a)
    {
        for (int k = 0; k < A.Length; k++)
        {
            A[k] *= a;
        }
    }

}
