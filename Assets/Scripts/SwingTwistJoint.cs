using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwingTwistJoint : MonoBehaviour {

    public bool showDebug;
    // Current Stage
    [Range(-180,180)]
    public float twistMin = -90;
    [Range(-180,180)]
    public float twistMax = 90;

    [Range(0.0001f,180)]
    public float maxSwingHorizontal = 90;
    [Range(0.0001f,180)]
    public float maxSwingVertical = 90;




    private float twistLimitMin;
    private float twistLimitMax;
    private Ellipse ellipse;

    //Debug
    GameObject ellipseObject;
    GameObject point1;
    GameObject point2;


    // Start is called before the first frame update
    void Start() {
        if (twistMin > twistMax) {
            Debug.LogError("The minimum twist angle is larger than the maximum twist angle.");
        }

        twistLimitMin = Mathf.Sin(Mathf.Deg2Rad * twistMin) * 0.5f;
        twistLimitMax = Mathf.Sin(Mathf.Deg2Rad * twistMax) * 0.5f;

        ellipse = new Ellipse(Mathf.Sin(Mathf.Deg2Rad * maxSwingHorizontal * 0.5f),Mathf.Sin(Mathf.Deg2Rad * maxSwingVertical * 0.5f));

        //Debug
        if (showDebug) {
            generateEllipseDebug();
        }
    }

    // Update is called once per frame
    void Update() {
        if ((twistMin > twistMax) || (twistMin < -180) || (twistMax) > 180) {
            return;
        }


        if (showDebug) {
            //Tests Ellipse Function
            Vector2 point = new Vector2(Random.Range(-1.5f,1.5f),Random.Range(-1.5f,1.5f));
            Vector2 temp = point;
            ellipse.clamp(ref point.x,ref point.y);

            point1.transform.position = new Vector3(temp.x,temp.y,0);
            point2.transform.position = new Vector3(point.x,point.y,0);
            Debug.DrawLine(point1.transform.position,point2.transform.position,Color.green);
        }

        Quaternion rot = transform.rotation;
        SwingTwistJointLimit(ref rot);
        transform.rotation = rot;

    }

    public void SwingTwistJointLimit(ref Quaternion q) {

        // Make sure the scalar part is positive. Since quaternions have a double covering, q and -q represent the same orientation.
        if (q.w<0) {
            q = new Quaternion(-q.x,-q.y,-q.z,-q.w);
        }

        // Here swing and twist are dependent. The twist can be applied before or after the swing. After (parent ->swing -> twist -> child) makes the most sense
        float rx, ry, rz;
        float s = q.x * q.x + q.w * q.w;

        if (s == 0) {
            // swing by 180 degrees is a singularity. We assume twist is zero.
            rx = 0;
            ry = q.y;
            rz = q.z;
        } else {
            float r = 1/Mathf.Sqrt(s); // im Code steht mt::rsqrt(s) ??
            rx = q.x * r;
            ry = (q.w * q.y + q.x * q.z) * r;
            rz = (q.w * q.z - q.x * q.y) * r; //Twist before Swing
        }

        rx = Mathf.Clamp(rx,twistLimitMin,twistLimitMax);
        ellipse.clamp(ref ry,ref rz);

        Quaternion qTwist = new Quaternion(rx,0,0,Mathf.Sqrt(Mathf.Max(0,1 - rx * rx)));
        Quaternion qSwing = new Quaternion(0,ry,rz,Mathf.Sqrt(Mathf.Max(0,1 - ry * ry - rz * rz)));

        q = qTwist * qSwing;    //Twist before Swing
    }

    void generateEllipseDebug() {
        GameObject myDebugEllipse = new GameObject();
        myDebugEllipse.name = "Debug Ellipse";

        point1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        point1.name = this.name + " Point Before Clamping";
        point1.transform.localScale = new Vector3(0.05f,0.05f,0.05f);
        point1.GetComponent<MeshRenderer>().material.color = Color.blue;
        point1.transform.SetParent(myDebugEllipse.transform);

        point2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        point2.name = this.name + " Point After Clamping";
        point2.transform.localScale = new Vector3(0.05f,0.05f,0.05f);
        point2.GetComponent<MeshRenderer>().material.color = Color.red;
        point2.transform.SetParent(myDebugEllipse.transform);


        GameObject[] pointsOnEllipse = new GameObject[20];
        float t = 0;
        for (int i = 0; i < 20; i++) {
            pointsOnEllipse[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pointsOnEllipse[i].transform.position = new Vector3(ellipse.a * Mathf.Cos(t),ellipse.b * Mathf.Sin(t),0);
            pointsOnEllipse[i].name = "Point " + i;
            pointsOnEllipse[i].transform.localScale = new Vector3(0.05f,0.05f,0.05f);
            pointsOnEllipse[i].GetComponent<MeshRenderer>().material.color = Color.gray;
            pointsOnEllipse[i].transform.SetParent(myDebugEllipse.transform);
            t += 2 * Mathf.PI / 20;
        }
    }
}

class Ellipse {

    public float a;
    public float b;
    private static float tolerance = 0.0001f; // Mathf.Epsilon * 10;

    public Ellipse(float horizontalHalfAxis,float verticalHalfAxis) {

        if (0 > a || a > 1 || 0 < b || b > 1) {
            Debug.LogError("Ellipse not defined right.");

        }
        a = horizontalHalfAxis;
        b = verticalHalfAxis;
        Debug.Log("Created ellipse with a:" + a + " and b: " + b);
    }



    public void clamp(ref float x,ref float y,int k = 50) {
        if (eval(x,y) <= 0) {
            return;
        }

        Vector2 dir = new Vector2(x,y).normalized;
        float p = 0;
        float q = Mathf.Max(a,b);         //Good Border limit where (x,y) is still not in ellipse
        float guess = q;
        float t = eval(guess * dir.x,guess * dir.y);

        while (k != 0 && !(-tolerance < t && t < 0)) { //Warning: If while end with the k condition, then t might be positive, that is it is outside of the ellipse
            if (t > 0) {
                q = guess;
            }
            else {
                p = guess;
            }
            guess = p + (q - p) / 2;
            t = eval(guess * dir.x,guess * dir.y);
            k--;
        }
        x = guess*dir.x;
        y = guess*dir.y;
    }

    /**
     * Returns a scalar that denotes the location wrt the ellipse. 
     * @param x           horizontal coordinate of point
     * @param y           vertical coordinate of point
     * @return            negative value denotes a point inside the ellipse. Zero denotes a point on the boundary. Positive value denotes outside the ellipse.
     */
    public float eval(float x,float y) {
        return Mathf.Pow(x / a,2) + Mathf.Pow(y / b,2) - 1;
    }
};
