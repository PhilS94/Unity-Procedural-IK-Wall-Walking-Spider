using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct TargetInfo {
    public Vector3 position;
    public Vector3 normal;

    public TargetInfo(Vector3 pos, Vector3 n) {
        position = pos;
        normal = n;
    }
}

public class IKSolver : MonoBehaviour {

    private static int maxIterations = 15;
    public static float tolerance = 0.05f;
    private static float weight = 1.0f;
    private static float footAngleToNormal = 20.0f; // 0 means parallel to ground (Orthogonal to plane normal)

    /*
     * Solves the IK Problem of the chain with given target using the CCD algorithm.
     * @param1 joints:  Contains all the Hinge Joints of the IK chain.
     * @param2 endEffector:  The end effector of the IK chain. It is not included in the list of hinge joints since it is not equipped with a AHingeJoint component.
     * @param3 target:       The target information the algorithm should solve for.
     * @param4 hasFoot:      If set to true, the last joint will adjust to the normal given by the target. 
     */
    public static void solveCCD(ref AHingeJoint[] joints, Transform endEffector, TargetInfo target, bool hasFoot = false) {
        AHingeJoint joint;
        Vector3 toEnd;
        Vector3 toTarget;
        Vector3 rotAxis;
        float angle;

        int iteration = 0;

        // If Endeffector is below Hyperplane defined by target i offset the target slightly above the plane, so the following algorithm will solve from above the hyperplane
        if (Vector3.Dot(endEffector.position - target.position, target.normal) < 0) {
            //target.position += tolerance * target.normal.normalized;
        }

        float error = Vector3.Distance(target.position, endEffector.position);

        /*
        //Too prevent self collision, Keep track of exterior angles of the spanned polygon and make sure the sum does not exceed 360
        // Only works for convex polygon though i think
        float[] interiorAngles = new float[joints.Length];
        float anglesum = 0;
        for (int j = 0; j < joints.Length; j++) {
            int j_l = mod(j - 1, joints.Length);
            int j_r = mod(j + 1, joints.Length);
            Vector3 v = joints[j].getRotationPoint() - joints[j_l].getRotationPoint();
            Vector3 w = joints[j_r].getRotationPoint() - joints[j].getRotationPoint();
            interiorAngles[j] = Vector3.SignedAngle(v, w, joints[j].getRotationAxis());
            anglesum += interiorAngles[j];
        }
        */

        //If only the normal changes but my error is within tolerance, i will not adjust the normal here, maybe fix this
        while (iteration < maxIterations && error > tolerance) {
            for (int i = 0; i < joints.Length; i++) {

                //This line ensures that the we start with the last joint, but then chronologically, e.g.
                // Length = 5
                // i = 0,1,2,3,4
                // k = i-1 mod 5,that is: 4 0 1 2 3
                int k = mod((i - 1), joints.Length);

                joint = joints[k];
                rotAxis = joint.getRotationAxis();
                toEnd = Vector3.ProjectOnPlane((endEffector.position - joint.getRotationPoint()), rotAxis);
                toTarget = Vector3.ProjectOnPlane(target.position - joint.getRotationPoint(), rotAxis);

                if (toTarget == Vector3.zero || toEnd == Vector3.zero) continue; // If singularity, skip. ToEnd should never be zero in my configuration though

                //This is a special case, where i want the foot, that is the last joint of the chain to adjust to the normal it hit
                if (k == joints.Length - 1 && hasFoot) {
                    angle = footAngleToNormal + 90.0f - Vector3.SignedAngle(Vector3.ProjectOnPlane(target.normal, rotAxis), toEnd, rotAxis); //Here toEnd only works because ill use this only for the last joint. instead you would want to use the vector from joint[i] to joint[i+1]
                }
                else {
                    angle = Vector3.SignedAngle(toEnd, toTarget, rotAxis);
                    angle *= weight;
                    angle *= joint.getWeight();
                    float kValue = 1.0f / (joints.Length * error);
                    angle *= Mathf.Clamp(kValue, float.Epsilon, 1.0f); // k-Faktor //Have to update the error every forloop here
                }

                joint.applyRotation(angle);
            }
            error = Vector3.Distance(target.position, endEffector.position); //Refresh the error so we can check if we are already close enough for the while loop check
            iteration++;
        }
        //if (iteration>0) Debug.Log("Completed CCD with " + iteration + " iterations and an error of "+ error);
    }


    // Slighly messy since Unity does not provide Matrix class so i had to work with two dimensional arrays and convert to Vector3 if needed
    public static void solveJacobianTranspose(ref AHingeJoint[] joints, Transform endEffector, TargetInfo target, bool hasFoot = false) {
        Vector3 error = target.position - endEffector.position;
        float[] err = new float[] { error.x, error.y, error.z };
        float[,] J = new float[3, joints.Length];
        float[,] JT = new float[joints.Length, 3];
        float[,] JJT = new float[3, 3];
        float[] JJTe = new float[3];
        float[] angleChange = new float[joints.Length];
        float alpha;

        int iteration = 0;
        while (iteration < maxIterations || error.magnitude < tolerance) {

            // Jacobian Form:
            //  a1  a2  a3  a4  a4 
            //  *   *   *   *   *   
            //  *   *   *   *   *  
            //  *   *   *   *   * 
            for (int k = 0; k < joints.Length; k++) {
                Vector3 rotAxis = joints[k].getRotationAxis();
                Vector3 cross = Vector3.Cross(rotAxis, endEffector.position - joints[k].getRotationPoint());
                J[0, k] = cross.x;
                J[1, k] = cross.y;
                J[2, k] = cross.z;
            }

            //Print Jacobian:
            string jacobianString = "";
            for (int i = 0; i < 3; i++) {
                for (int k = 0; k < joints.Length; k++) {
                    jacobianString += J[i, k] + " ";
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
            Transpose(J, ref JT);

            // Jacobian times Jacobian Transpose has Form:
            //  *   *   *
            //  *   *   *
            //  *   *   *
            multiply(J, JT, ref JJT);

            // Calculate needed multiplications
            multiply(JJT, err, ref JJTe);
            Vector3 m_JJTe = new Vector3(JJTe[0], JJTe[1], JJTe[2]);

            //Calc the alpha value
            alpha = Vector3.Dot(error, m_JJTe) / Vector3.Dot(m_JJTe, m_JJTe);

            //Calc the change in angle
            multiply(JT, err, ref angleChange);
            multiply(ref angleChange, alpha);

            // Now apply the angle rotations
            for (int k = 0; k < joints.Length; k++) {
                joints[k].applyRotation(angleChange[k]);
            }

            error = target.position - endEffector.position; //Refresh the error so we can check if we are already close enough for the while loop check
            iteration++;
        }
    }

    // Multiplies A with its Transpose Matrix and saves the product in result
    private static void Transpose(float[,] A, ref float[,] result) {

        if (A.GetLength(1) != result.GetLength(0) || A.GetLength(0) != result.GetLength(1)) {
            Debug.Log("Transpose matrix not the right dimensions.");
            return;
        }

        for (int col = 0; col < A.GetLength(0); col++) {
            for (int row = 0; row < A.GetLength(1); row++) {
                result[row, col] = A[col, row];
            }
        }
    }

    // Matrix Multiplication
    private static void multiply(float[,] A, float[,] B, ref float[,] result) {
        if (A.GetLength(1) != B.GetLength(0) || result.GetLength(0) != A.GetLength(0) || result.GetLength(1) != B.GetLength(1)) {
            Debug.Log("Can't multiply these matrices.");
            return;
        }

        for (int row = 0; row < result.GetLength(0); row++) {
            for (int col = 0; col < result.GetLength(1); col++) {
                float sum = 0;

                for (int k = 0; k < A.GetLength(1); k++) {
                    sum += A[row, k] * B[k, col];
                }
                result[row, col] = sum;
            }
        }

    }

    // Matrix - Vector Multiplication
    private static void multiply(float[,] A, float[] B, ref float[] result) {
        if (A.GetLength(1) != B.Length || result.Length != A.GetLength(0)) {
            Debug.Log("Can't multiply these matrices.");
            return;
        }

        for (int row = 0; row < result.GetLength(0); row++) {
            float sum = 0;

            for (int k = 0; k < A.GetLength(1); k++) {
                sum += A[row, k] * B[k];
            }
            result[row] = sum;
        }
    }

    // Vector - Scalar Multiplication
    private static void multiply(ref float[] A, float a) {
        for (int k = 0; k < A.Length; k++) {
            A[k] *= a;
        }
    }

    // Implemented this, since the % operator in C# returns the remainder, which can be negative if n is.
    // This functions returns the modulo, that is a positive number.
    private static int mod(int n, int m) {
        return ((n % m) + m) % m;
    }

}