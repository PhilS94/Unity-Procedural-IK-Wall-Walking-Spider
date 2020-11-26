#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class EditorDrawing
{
    public static void DrawHorizontalLine(Color color, int thickness = 2, int padding = 10) {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
        r.height = thickness;
        r.y += padding / 2;
        r.x -= 2;
        r.width += 6;
        EditorGUI.DrawRect(r, color);
    }

    public static void DrawText(Vector3 pos, string text, Color col,bool emphasize=false) {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = col;
        style.alignment = TextAnchor.MiddleCenter;
        if (emphasize) {
            style.fontStyle = FontStyle.Bold;
            style.normal.background = Texture2D.whiteTexture;
        }
        Handles.Label(pos, text, style);
    }
}
#endif