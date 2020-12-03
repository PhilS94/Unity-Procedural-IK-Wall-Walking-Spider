#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class EditorDrawing {
    public static void DrawHorizontalLine(Color color, int thickness = 1, int padding = 10) {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
        r.height = thickness;
        r.y += padding / 2;
        r.x -= 2;
        r.width += 6;
        EditorGUI.DrawRect(r, color);
        EditorGUILayout.Space();
    }

    public static void DrawHorizontalLine() {
        DrawHorizontalLine(Color.gray);
    }

    public static void DrawText(Vector3 pos, string text, Color col, bool emphasize = false) {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = col;
        style.alignment = TextAnchor.MiddleCenter;
        if (emphasize) {
            style.fontStyle = FontStyle.Bold;
            style.normal.background = Texture2D.whiteTexture;
        }
        Handles.Label(pos, text, style);
    }

    public static bool DrawButton(string text) {
        Color originalColor = GUI.color;
        GUI.color = new Color(153f / 255, 168f / 255, 189f / 255);
        bool b = GUILayout.Button(text);
        GUI.color = originalColor;
        return b;
    }

    public static void DrawMonoScript(MonoBehaviour behaviour, System.Type className) {
        EditorGUILayout.Space();
        GUI.enabled = false;
        EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(behaviour), className, false);
        GUI.enabled = true;
        EditorGUILayout.Space();
    }
}
#endif