using UnityEngine;
using System.Collections;

public class DebugShapes : MonoBehaviour {
    public static void DrawCube(Vector3 pos, Color col, Vector3 scale) {
        Vector3 halfScale = scale * 0.5f;

        Vector3[] points = new Vector3[]
        {
            pos + new Vector3(halfScale.x,      halfScale.y,    halfScale.z),
            pos + new Vector3(-halfScale.x,     halfScale.y,    halfScale.z),
            pos + new Vector3(-halfScale.x,     -halfScale.y,   halfScale.z),
            pos + new Vector3(halfScale.x,      -halfScale.y,   halfScale.z),
            pos + new Vector3(halfScale.x,      halfScale.y,    -halfScale.z),
            pos + new Vector3(-halfScale.x,     halfScale.y,    -halfScale.z),
            pos + new Vector3(-halfScale.x,     -halfScale.y,   -halfScale.z),
            pos + new Vector3(halfScale.x,      -halfScale.y,   -halfScale.z),
        };

        Debug.DrawLine(points[0], points[1], col);
        Debug.DrawLine(points[1], points[2], col);
        Debug.DrawLine(points[2], points[3], col);
        Debug.DrawLine(points[3], points[0], col);
    }

    public static void DrawSphere(Vector3 pos, float radius, int sectorCount, int stackCount, Color col) {
        float x, y, z, xy;                              // vertex position

        float sectorStep = 2 * Mathf.PI / sectorCount;
        float stackStep = Mathf.PI / stackCount;
        float sectorAngle, stackAngle;

        Vector3[,] p = new Vector3[stackCount + 1, sectorCount + 1];

        for (int i = 0; i <= stackCount; ++i) {
            stackAngle = Mathf.PI / 2 - i * stackStep;        // starting from pi/2 to -pi/2
            xy = radius * Mathf.Cos(stackAngle);             // r * cos(u)
            z = radius * Mathf.Sin(stackAngle);              // r * sin(u)

            // add (sectorCount+1) vertices per stack
            // the first and last vertices have same position and normal, but different tex coords
            for (int j = 0; j <= sectorCount; ++j) {
                sectorAngle = j * sectorStep;           // starting from 0 to 2pi

                // vertex position (x, y, z)
                x = xy * Mathf.Cos(sectorAngle);             // r * cos(u) * cos(v)
                y = xy * Mathf.Sin(sectorAngle);             // r * cos(u) * sin(v)

                p[i, j] = new Vector3(x, y, z);
            }
        }

        for (int i = 0; i <= stackCount; ++i) {
            for (int j = 0; j <= sectorCount - 1; ++j) {
                Debug.DrawLine(p[i, j], p[i, j + 1], col);
            }
        }

        for (int j = 0; j <= sectorCount; ++j) {
            for (int i = 0; i <= stackCount - 1; ++i) {

                Debug.DrawLine(p[i, j], p[i + 1, j], col);
            }
        }
    }

    public static void DrawRect(Rect rect, Color col) {
        Vector3 pos = new Vector3(rect.x + rect.width / 2, rect.y + rect.height / 2, 0.0f);
        Vector3 scale = new Vector3(rect.width, rect.height, 0.0f);

        DebugShapes.DrawRect(pos, col, scale);
    }

    public static void DrawRect(Vector3 pos, Color col, Vector3 scale) {
        Vector3 halfScale = scale * 0.5f;

        Vector3[] points = new Vector3[]
        {
            pos + new Vector3(halfScale.x,      halfScale.y,    halfScale.z),
            pos + new Vector3(-halfScale.x,     halfScale.y,    halfScale.z),
            pos + new Vector3(-halfScale.x,     -halfScale.y,   halfScale.z),
            pos + new Vector3(halfScale.x,      -halfScale.y,   halfScale.z),
        };

        Debug.DrawLine(points[0], points[1], col);
        Debug.DrawLine(points[1], points[2], col);
        Debug.DrawLine(points[2], points[3], col);
        Debug.DrawLine(points[3], points[0], col);
    }

    public static void DrawPoint(Vector3 pos, Color col, float scale, float duration = 0.0f) {
        Vector3[] points = new Vector3[]
        {
            pos + (Vector3.up * scale),
            pos - (Vector3.up * scale),
            pos + (Vector3.right * scale),
            pos - (Vector3.right * scale),
            pos + (Vector3.forward * scale),
            pos - (Vector3.forward * scale)
        };

        Debug.DrawLine(points[0], points[1], col, duration);
        Debug.DrawLine(points[2], points[3], col, duration);
        Debug.DrawLine(points[4], points[5], col, duration);

        Debug.DrawLine(points[0], points[2], col, duration);
        Debug.DrawLine(points[0], points[3], col, duration);
        Debug.DrawLine(points[0], points[4], col, duration);
        Debug.DrawLine(points[0], points[5], col, duration);

        Debug.DrawLine(points[1], points[2], col, duration);
        Debug.DrawLine(points[1], points[3], col, duration);
        Debug.DrawLine(points[1], points[4], col, duration);
        Debug.DrawLine(points[1], points[5], col, duration);

        Debug.DrawLine(points[4], points[2], col, duration);
        Debug.DrawLine(points[4], points[3], col, duration);
        Debug.DrawLine(points[5], points[2], col, duration);
        Debug.DrawLine(points[5], points[3], col, duration);

    }

    public static void DrawCircle(Vector3 pos, Vector3 normal, float radius, int subDivisions, Color col) {

        int l = subDivisions + 4;

        if (subDivisions < 0) {
            Debug.LogWarning("Subdivisions not allowed to be negative.");
            return;
        }

        //Choose a perpendicular vector
        Vector3 perpendicular;
        perpendicular = Vector3.ProjectOnPlane(Vector3.forward, normal);
        if (perpendicular == Vector3.zero) {
            perpendicular = Vector3.ProjectOnPlane(Vector3.right, normal);
        }
        perpendicular = perpendicular.normalized;


        Vector3[] p = new Vector3[l];

        //Lerping is problematic with an angle greater than 180
        for (int k = 0; k < l; k++) {
            p[k] = pos + Quaternion.AngleAxis(360.0f * k / l, normal) * perpendicular * radius;
        }

        // Draw circle
        for (int k = 0; k < l - 1; k++) {
            Debug.DrawLine(p[k], p[k + 1], col);
        }
        Debug.DrawLine(p[l - 1], p[0], col);
    }

    public static void DrawCircleSection(Vector3 pos, Vector3 min, Vector3 max, float minRadius, float maxRadius, int subDivisions, Color col) {

        if (subDivisions < 0) {
            Debug.LogWarning("Subdivisions not allowed to be negative.");
            return;
        }

        if (min == Vector3.zero || max == Vector3.zero) {
            Debug.LogWarning("Min and Max Vector not allowed to be zero.");
            return;
        }
        Vector3 normal = Vector3.Cross(min, max).normalized;

        if (normal == Vector3.zero) {
            Debug.LogWarning("Min and Max Vector not allowed to be parallel.");
            return;
        }

        Vector3 projMin = Vector3.ProjectOnPlane(min, normal).normalized;
        Vector3 projMax = Vector3.ProjectOnPlane(max, normal).normalized;

        if (projMin == Vector3.zero || projMax == Vector3.zero) {
            Debug.LogWarning("Can't Draw Circle Section since one of the two Vectors are parallel to the normal.");
        }

        int l = subDivisions + 3;
        Vector3[] p = new Vector3[l];
        Vector3[] P = new Vector3[l];

        float angle = Vector3.Angle(projMin, projMax);

        for (int k = 0; k < l; k++) {
            p[k] = pos + Quaternion.AngleAxis(angle * k / (l - 1), normal) * projMin * minRadius;
            P[k] = pos + Quaternion.AngleAxis(angle * k / (l - 1), normal) * projMin * maxRadius;
        }

        // Draw circles
        for (int k = 0; k < l - 1; k++) {
            Debug.DrawLine(p[k], p[k + 1], col);
            Debug.DrawLine(P[k], P[k + 1], col);
        }

        // Connect inner to outer circle 
        Debug.DrawLine(p[0], P[0], col);
        Debug.DrawLine(p[l - 1], P[l - 1], col);
    }

    public static void DrawSphereSection(Vector3 pos, Vector3 lowLeft, Vector3 lowRight, Vector3 upLeft, Vector3 upRight, float minRadius, float maxRadius, int subDivisions, Color col) {

        int l = subDivisions + 3;

        // Make surecorners make sense
        // Make sure every vector is normalized

        Vector3[] v = new Vector3[l];
        Vector3[] w = new Vector3[l];

        for (int k = 0; k < l; k++) {
            v[k] = Vector3.Lerp(lowLeft, upLeft, (float)k / (l - 1)).normalized;
            w[k] = Vector3.Lerp(lowRight, upRight, (float)k / (l - 1)).normalized;
            DrawCircleSection(pos, v[k], w[k], minRadius, maxRadius, subDivisions, col);
        }


        for (int k = 0; k < l - 1; k++) {
            Debug.DrawLine(pos + minRadius * v[k], pos + minRadius * v[k + 1], col);
            Debug.DrawLine(pos + maxRadius * v[k], pos + maxRadius * v[k + 1], col);
            Debug.DrawLine(pos + minRadius * w[k], pos + minRadius * w[k + 1], col);
            Debug.DrawLine(pos + maxRadius * w[k], pos + maxRadius * w[k + 1], col);
        }

    }

    public static void DrawCylinderSection(Vector3 pos, Vector3 min, Vector3 max, Vector3 normal, float minDistance, float maxDistance, float lowHeight, float highHeight, int subDivisions, Color col) {

        if (subDivisions < 0) {
            Debug.LogWarning("Subdivisions not allowed to be negative.");
            return;
        }

        if (min == Vector3.zero || max == Vector3.zero) {
            Debug.LogWarning("Min and Max Vector not allowed to be zero.");
            return;
        }

        if (normal == Vector3.zero) {
            Debug.LogWarning("Normal not allowed to be zero.");
            return;
        }

        Vector3 projMin = Vector3.ProjectOnPlane(min, normal).normalized;
        Vector3 projMax = Vector3.ProjectOnPlane(max, normal).normalized;

        if (projMin == Vector3.zero || projMax == Vector3.zero) {
            Debug.LogWarning("Can't Draw since one of the two Vectors are parallel to the normal.");
        }

        int l = subDivisions + 2;

        Vector3[] v = new Vector3[l];
        Vector3[] p = new Vector3[l];
        Vector3[] p_up = new Vector3[l];
        Vector3[] p_down = new Vector3[l];
        Vector3[] q = new Vector3[l];
        Vector3[] q_up = new Vector3[l];
        Vector3[] q_down = new Vector3[l];

        for (int k = 0; k < l; k++) {
            v[k] = Vector3.Lerp(projMin, projMax, (float)k / (l - 1)).normalized;
            p[k] = pos + minDistance * v[k];
            p_up[k] = p[k] + normal * highHeight;
            p_down[k] = p[k] - normal * lowHeight;
            q[k] = p[k] + v[k] * (maxDistance - minDistance);
            q_up[k] = q[k] + normal * highHeight;
            q_down[k] = q[k] - normal * lowHeight;
        }

        //Connect the p's
        for (int k = 0; k < l - 1; k++) {
            Debug.DrawLine(p[k], p[k + 1], col);
            Debug.DrawLine(p_up[k], p_up[k + 1], col);
            Debug.DrawLine(p_down[k], p_down[k + 1], col);
        }
        /*
        for (int k = 0; k < l; k++) {
            Debug.DrawLine(p_up[k], p_down[k], col);
        }
        */
        Debug.DrawLine(p_up[0], p_down[0], col);
        Debug.DrawLine(p_up[l-1], p_down[l-1], col);

        //Connect the q's
        for (int k = 0; k < l - 1; k++) {
            Debug.DrawLine(q[k], q[k + 1], col);
            Debug.DrawLine(q_up[k], q_up[k + 1], col);
            Debug.DrawLine(q_down[k], q_down[k + 1], col);
        }
        /*
        for (int k = 0; k < l; k++) {
            Debug.DrawLine(q_up[k], q_down[k], col);
        }
        */
        Debug.DrawLine(q_up[0], q_down[0], col);
        Debug.DrawLine(q_up[l - 1], q_down[l - 1], col);


        //Connect the p's with q's  Sides
        Debug.DrawLine(p[0], q[0], col);
        Debug.DrawLine(p[l - 1], q[l - 1], col);

        Debug.DrawLine(p_up[0], q_up[0], col);
        Debug.DrawLine(p_up[l - 1], q_up[l - 1], col);

        Debug.DrawLine(p_down[0], q_down[0], col);
        Debug.DrawLine(p_down[l - 1], q_down[l - 1], col);

        /*
        //Connect the p's with q's  Top and Bottom
        for (int k = 0; k < l; k++) {
            Debug.DrawLine(p_up[k], q_up[k], col);
            Debug.DrawLine(p_down[k], q_down[k], col);
        }
        */
    }
}