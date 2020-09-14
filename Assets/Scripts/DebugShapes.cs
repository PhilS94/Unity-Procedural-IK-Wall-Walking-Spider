/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * It is an extension of DebugX by Hayden Scott-Baron (Dock)
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */


using UnityEngine;
using System.Collections;

/*
 * Class for debug drawing certain shapes.
 * Creates the specified shapes by the use of multiple Debug.DrawLine calls.
 */

public class DebugShapes : MonoBehaviour {

    public static void DrawLine(Vector3 pos, Vector3 end, Color col, float duration = 0f) {
        Debug.DrawLine(pos, end, col, duration);
    }

    public static void DrawRay(Vector3 pos, Vector3 direction, Color col, float duration = 0f) {
        Debug.DrawLine(pos, pos + direction, col, duration);
    }

    public static void DrawRay(Vector3 pos, Vector3 direction, float distance, Color col, float duration = 0f) {
        Debug.DrawLine(pos, pos + direction.normalized * distance, col, duration);
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

    public static void DrawCube(Vector3 pos, Color col, Vector3 scale, float duration = 0f) {
        Vector3 t = scale * 0.5f;

        Vector3[] p = new Vector3[]
        {
            pos + new Vector3(t.x,      t.y,    t.z),
            pos + new Vector3(-t.x,     t.y,    t.z),
            pos + new Vector3(-t.x,     -t.y,   t.z),
            pos + new Vector3(t.x,      -t.y,   t.z),
            pos + new Vector3(t.x,      t.y,    -t.z),
            pos + new Vector3(-t.x,     t.y,    -t.z),
            pos + new Vector3(-t.x,     -t.y,   -t.z),
            pos + new Vector3(t.x,      -t.y,   -t.z),
        };

        Debug.DrawLine(p[0], p[1], col, duration);
        Debug.DrawLine(p[1], p[2], col, duration);
        Debug.DrawLine(p[2], p[3], col, duration);
        Debug.DrawLine(p[3], p[0], col, duration);
    }

    public static void DrawRect(Rect rect, Color col) {
        Vector3 pos = new Vector3(rect.x + rect.width / 2, rect.y + rect.height / 2, 0.0f);
        Vector3 scale = new Vector3(rect.width, rect.height, 0.0f);

        DebugShapes.DrawRect(pos, col, scale);
    }

    public static void DrawRect(Vector3 pos, Color col, Vector3 scale) {
        Vector3 t = scale * 0.5f;

        Vector3[] p = new Vector3[]
        {
            pos + new Vector3(t.x,      t.y,    t.z),
            pos + new Vector3(-t.x,     t.y,    t.z),
            pos + new Vector3(-t.x,     -t.y,   t.z),
            pos + new Vector3(t.x,      -t.y,   t.z),
        };

        Debug.DrawLine(p[0], p[1], col);
        Debug.DrawLine(p[1], p[2], col);
        Debug.DrawLine(p[2], p[3], col);
        Debug.DrawLine(p[3], p[0], col);
    }

    public static void DrawPlane(Vector3 pos, Vector3 normal, Vector3 tangent, float size, Color col, float duration = 0) {

        tangent = tangent.normalized;
        normal = normal.normalized;
        Vector3 cross = Vector3.Cross(normal, tangent);

        Vector3 a = pos + 0.5f * size * tangent + 0.5f * size * cross;
        Vector3 b = pos - 0.5f * size * tangent + 0.5f * size * cross;
        Vector3 c = pos - 0.5f * size * tangent - 0.5f * size * cross;
        Vector3 d = pos + 0.5f * size * tangent - 0.5f * size * cross;

        Debug.DrawLine(a, b, col, duration);
        Debug.DrawLine(a, c, col, duration);
        Debug.DrawLine(a, d, col, duration);
        Debug.DrawLine(b, c, col, duration);
        Debug.DrawLine(b, d, col, duration);
        Debug.DrawLine(c, d, col, duration);
    }

    public static void DrawCircle(Vector3 pos, Vector3 normal, float radius, Color col, float duration = 0) {

        int l = 16;

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
            Debug.DrawLine(p[k], p[k + 1], col, duration);
        }
        Debug.DrawLine(p[l - 1], p[0], col, duration);
    }

    public static void DrawCircleSection(Vector3 pos, Vector3 min, Vector3 max, Vector3 normal, float minRadius, float maxRadius, Color col, float duration = 0) {

        if (min == Vector3.zero || max == Vector3.zero) {
            Debug.LogWarning("Min and Max Vector not allowed to be zero.");
            return;
        }

        if (normal == Vector3.zero) {
            Debug.LogWarning("Normal Vector not allowed to be parallel.");
            return;
        }

        Vector3 projMin = Vector3.ProjectOnPlane(min, normal).normalized;
        Vector3 projMax = Vector3.ProjectOnPlane(max, normal).normalized;

        if (projMin == Vector3.zero || projMax == Vector3.zero) {
            Debug.LogWarning("Can't Draw Circle Section since one of the two Vectors are parallel to the normal.");
        }

        int l = 9;
        Vector3[] p = new Vector3[l];
        Vector3[] P = new Vector3[l];

        float angle = Vector3.Angle(projMin, projMax);

        for (int k = 0; k < l; k++) {
            p[k] = pos + Quaternion.AngleAxis(angle * k / (l - 1), normal) * projMin * minRadius;
            P[k] = pos + Quaternion.AngleAxis(angle * k / (l - 1), normal) * projMin * maxRadius;
        }

        // Draw circles
        for (int k = 0; k < l - 1; k++) {
            Debug.DrawLine(p[k], p[k + 1], col, duration);
            Debug.DrawLine(P[k], P[k + 1], col, duration);
        }

        // Connect inner to outer circle 
        Debug.DrawLine(p[0], P[0], col, duration);
        Debug.DrawLine(p[l - 1], P[l - 1], col, duration);
    }

    public static void DrawSphere(Vector3 pos, float radius, Color col, float duration = 0) {

        int l = 3;

        Vector3[] horiz = new Vector3[l];
        Vector3[] vert = new Vector3[l];
        Vector3[] Z = new Vector3[l];

        float step = 2 * radius / (l + 1);

        for (int k = 0; k < l; k++) {
            horiz[k] = pos - Vector3.right * radius + (k + 1) * step * Vector3.right;
            vert[k] = pos - Vector3.up * radius + (k + 1) * step * Vector3.up;
            Z[k] = pos - Vector3.forward * radius + (k + 1) * step * Vector3.forward;
            float angle = Mathf.Lerp(-Mathf.PI / 2, Mathf.PI / 2, (float)(k + 1) / (l + 1));
            float r = Mathf.Cos(angle) * radius; //Calculated wrong
            DrawCircle(horiz[k], Vector3.right, r, col, duration);
            DrawCircle(vert[k], Vector3.up, r, col, duration);
            DrawCircle(Z[k], Vector3.forward, r, col, duration);
        }
        //Camera cam = UnityEditor.SceneView.lastActiveSceneView.camera;
        //if (cam != null) DrawCircle(pos, -cam.transform.forward, radius, col);
    }

    public static void DrawSphereSection(Vector3 pos, Vector3 lowLeft, Vector3 lowRight, Vector3 upLeft, Vector3 upRight, float minRadius, float maxRadius, Color col) {

        int l = 9;

        // Make surecorners make sense
        // Make sure every vector is normalized

        Vector3[] v = new Vector3[l];
        Vector3[] w = new Vector3[l];

        for (int k = 0; k < l; k++) {
            v[k] = Vector3.Lerp(lowLeft, upLeft, (float)k / (l - 1)).normalized;
            w[k] = Vector3.Lerp(lowRight, upRight, (float)k / (l - 1)).normalized;
            DrawCircleSection(pos, v[k], w[k], Vector3.Cross(v[k], w[k]), minRadius, maxRadius, col);
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
        Debug.DrawLine(p_up[l - 1], p_down[l - 1], col);

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

    public static void DrawSphereRay(Vector3 start, Vector3 direction, float distance, float radius, int amount, Color col, float duration = 0) {

        Vector3 endPoint = start + (radius + distance) * direction;
        Vector3 endPointSphereCenter = endPoint - (radius * direction);

        for (int i = 0; i < amount; i++) {
            DrawSphere(Vector3.Lerp(start, endPointSphereCenter, (float)i / (amount - 1)), radius, new Color(col.r, col.g, col.b, 0.5f), duration);
        }
        Debug.DrawLine(start, endPoint, col, duration);
    }

    public static void DrawSphereRay(Vector3 start, Vector3 end, float radius, int amount, Color col, float duration = 0) {
        Vector3 v = end - start;
        DrawSphereRay(start, v.normalized, v.magnitude, radius, amount, col, duration);
    }
}