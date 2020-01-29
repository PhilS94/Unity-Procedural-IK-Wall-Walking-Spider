using UnityEngine;
using System.Collections;

public class DebugShapes : MonoBehaviour
{
    public static void DrawCube(Vector3 pos, Color col, Vector3 scale)
    {
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

    public static void DrawRect(Rect rect, Color col)
    {
        Vector3 pos = new Vector3(rect.x + rect.width / 2, rect.y + rect.height / 2, 0.0f);
        Vector3 scale = new Vector3(rect.width, rect.height, 0.0f);

        DebugShapes.DrawRect(pos, col, scale);
    }

    public static void DrawRect(Vector3 pos, Color col, Vector3 scale)
    {
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

    public static void DrawPoint(Vector3 pos, Color col, float scale)
    {
        Vector3[] points = new Vector3[]
        {
            pos + (Vector3.up * scale),
            pos - (Vector3.up * scale),
            pos + (Vector3.right * scale),
            pos - (Vector3.right * scale),
            pos + (Vector3.forward * scale),
            pos - (Vector3.forward * scale)
        };

        Debug.DrawLine(points[0], points[1], col);
        Debug.DrawLine(points[2], points[3], col);
        Debug.DrawLine(points[4], points[5], col);

        Debug.DrawLine(points[0], points[2], col);
        Debug.DrawLine(points[0], points[3], col);
        Debug.DrawLine(points[0], points[4], col);
        Debug.DrawLine(points[0], points[5], col);

        Debug.DrawLine(points[1], points[2], col);
        Debug.DrawLine(points[1], points[3], col);
        Debug.DrawLine(points[1], points[4], col);
        Debug.DrawLine(points[1], points[5], col);

        Debug.DrawLine(points[4], points[2], col);
        Debug.DrawLine(points[4], points[3], col);
        Debug.DrawLine(points[5], points[2], col);
        Debug.DrawLine(points[5], points[3], col);

    }

    public static void DrawScope(Vector3 pos, Vector3 minVector, Vector3 maxVector, Vector3 normal, float minDistance, float maxDistance, float heigth, int subDivisions, Color col)
    {
        if (subDivisions < 0)
        {
            Debug.LogError("Negative values for subDivsions not allowed.");
        }

        int l = subDivisions + 2;

        Vector3[] v = new Vector3[l];
        Vector3[] p = new Vector3[l];
        Vector3[] p_up = new Vector3[l];
        Vector3[] p_down = new Vector3[l];
        Vector3[] q = new Vector3[l];
        Vector3[] q_up = new Vector3[l];
        Vector3[] q_down = new Vector3[l];

        // Beispiel subdiv=3, also length=5, step=1/5,
        // k=0 4*min + 0*max = min
        // k=1 3*min + 1*max = min
        // k=2 2*min + 2*max = min
        // k=3 1*min + 3*max = min
        // k=4 0*min + 4*max = min
        for (int k = 0; k < l; k++)
        {
            v[k] = ((l - 1 - k) * minVector + k * maxVector).normalized;
            p[k] = pos + minDistance * v[k];
            p_up[k] = p[k] + normal * heigth;
            p_down[k] = p[k] - normal * heigth;
            q[k] = p[k] + v[k] * (maxDistance - minDistance);
            q_up[k] = q[k] + normal * heigth;
            q_down[k] = q[k] - normal * heigth;
        }

        //Connect the p's
        for (int k = 0; k < l - 1; k++)
        {
            Debug.DrawLine(p[k], p[k + 1], col);
            Debug.DrawLine(p_up[k], p_up[k + 1], col);
            Debug.DrawLine(p_down[k], p_down[k + 1], col);
            Debug.DrawLine(p_up[k], p_down[k], col);
        }
        for (int k = 0; k < l; k++)
        {
            Debug.DrawLine(p_up[k], p_down[k], col);
        }

        //Connect the q's
        for (int k = 0; k < l - 1; k++)
        {
            Debug.DrawLine(q[k], q[k + 1], col);
            Debug.DrawLine(q_up[k], q_up[k + 1], col);
            Debug.DrawLine(q_down[k], q_down[k + 1], col);
        }
        for (int k = 0; k < l; k++)
        {
            Debug.DrawLine(q_up[k], q_down[k], col);
        }


        //Connect the p's with q's  Sides
        Debug.DrawLine(p[0], q[0], col);
        Debug.DrawLine(p[l - 1], q[l - 1], col);

        Debug.DrawLine(p_up[0], q_up[0], col);
        Debug.DrawLine(p_up[l - 1], q_up[l - 1], col);

        Debug.DrawLine(p_down[0], q_down[0], col);
        Debug.DrawLine(p_down[l - 1], q_down[l - 1], col);

        //Connect the p's with q's  Top and Bottom
        for (int k = 0; k < l; k++)
        {
            Debug.DrawLine(p_up[k], q_up[k], col);
            Debug.DrawLine(p_down[k], q_down[k], col);
        }
    }
}