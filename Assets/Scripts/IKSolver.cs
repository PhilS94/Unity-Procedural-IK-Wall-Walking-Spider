using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct TargetInfo
{
    public Vector3 position;
    public Vector3 normal;

    public TargetInfo(Vector3 pos, Vector3 n)
    {
        position = pos;
        normal = n;
    }
}

public class IKSolver : MonoBehaviour
{

    private static int maxIterations = 10;
    private static float tolerance = 0.1f;
    private static float weight = 1.0f;

    /*
     * Solves the IK Problem of the chain with given target using the CCD algorithm.
     * @param1 joints:  Contains all the Hinge Joints of the IK chain.
     * @param2 endEffector:  The end effector of the IK chain. It is not included in the list of hinge joints since it is not equipped with a AHingeJoint component.
     * @param3 target:       The target information the algorithm should solve for.
     * @param4 hasFoot:      If set to true, the last joint will adjust to the normal given by the target. 
     */
    public static void solveCCD(ref AHingeJoint[] joints, Transform endEffector, TargetInfo target, bool hasFoot = false)
    {
        AHingeJoint joint;
        Vector3 toEnd;
        Vector3 toTarget;
        Vector3 rotAxis;
        float angle;

        int iteration = 0;
        float error = Vector3.Distance(target.position, endEffector.position);

        //If only the normal changes but my error is within tolerance, i will not adjust the normal here, maybe fix this
        while (iteration < maxIterations && error > tolerance)
        {
            for (int i = 0; i < joints.Length; i++) //How do i smartly configure the foor loop to start with joints.length-1, then 0 to joints.length-2?
            {

                //This line ensures that the we start with the last joint, but then chronologically, e.g.
                // Length = 5
                // i = 0,1,2,3,4
                // k = i-1 mod 5,that is: 4 0 1 2 3
                int k = mod((i - 1), joints.Length);

                joint = joints[k];
                rotAxis = joint.getRotationAxis();
                toEnd = (endEffector.position - joint.getRotationPoint()).normalized;
                toTarget = (target.position - joint.getRotationPoint()).normalized;

                //This is a special case, where i want the foot, that is the last joint of the chain to adjust to the normal it hit
                if (k == joints.Length - 1 && hasFoot)
                {
                    angle = 90.0f - Vector3.SignedAngle(Vector3.ProjectOnPlane(target.normal, rotAxis), Vector3.ProjectOnPlane(toEnd, rotAxis), rotAxis); //Here toEnd only works because ill use this only for the last joint. instead you would want to use the vector from joint[i] to joint[i+1]
                }
                else
                {
                    angle = weight * joint.getWeight() * Vector3.SignedAngle(Vector3.ProjectOnPlane(toEnd, rotAxis), Vector3.ProjectOnPlane(toTarget, rotAxis), rotAxis);
                }
                joint.applyRotation(angle);
            }
            error = Vector3.Distance(target.position, endEffector.position); //Refresh the error so we can check if we are already close enough for the while loop check
            iteration++;
        }
        //if (iteration > 0) Debug.Log("Completed CCD with" + iteration + " iterations.");
    }










    // Have to fix all of this
    public static void solveJacobianTranspose(ref AHingeJoint[] hingeJoints, Transform endEffector, TargetInfo target, bool hasFoot = false)
    {
        Vector3 error = target.position - endEffector.position;

        int amtAngles = (hingeJoints.Length - 1) * 3; //For every joint, except the endEffector we have three angles

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
                rotAxis = hingeJoints[col / 3].transform.right;
            }
            if (col % 3 == 1) //Y-Rotation angle
            {
                rotAxis = hingeJoints[col / 3].transform.up;
            }
            if (col % 3 == 2) //Z-Rotation angle
            {
                rotAxis = hingeJoints[col / 3].transform.forward;
            }
            Vector3 cross = Vector3.Cross(rotAxis, endEffector.position - hingeJoints[col / 3].transform.position); //Always use rotationPoint instead of position

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
                rotAxis = hingeJoints[col / 3].transform.right;
            }
            if (col % 3 == 1) //Y-Rotation angle
            {
                rotAxis = hingeJoints[col / 3].transform.up;
            }
            if (col % 3 == 2) //Z-Rotation angle
            {
                rotAxis = hingeJoints[col / 3].transform.forward;
            }

            //joints[col/3].Rotate(rotAxis, angleChange[col], Space.Self);
            Quaternion newRotation = Quaternion.AngleAxis(angleChange[col], rotAxis) * hingeJoints[col / 3].transform.localRotation;
            SwingTwistJoint swingTwistjoint = hingeJoints[col / 3].gameObject.GetComponent<SwingTwistJoint>();
            swingTwistjoint.SwingTwistJointLimit(ref newRotation); //This limits the rotation using the values in the swingTwistJoint
            hingeJoints[col / 3].transform.localRotation = newRotation; //Should do this smoothly via slerp for example
        }

    }

    private static void multiply(ref float[,] A, ref float[,] B, ref float[,] result)
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
    private static void multiply(ref float[,] A, ref float[] B, ref float[] result)
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

    private static void multiply(ref float[] A, float a)
    {
        for (int k = 0; k < A.Length; k++)
        {
            A[k] *= a;
        }
    }

    // Implemented this, since the % operator in C# returns the remainder, which can be negative if n is.
    // This functions returns the modulo, that is a positive number.
    private static int mod(int n, int m)
    {
        return ((n % m) + m) % m;
    }

}
