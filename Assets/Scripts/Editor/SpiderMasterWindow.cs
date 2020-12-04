using UnityEngine;
using UnityEditor;

public class SpiderMasterWindow : EditorWindow {

    Spider spider;
    int count;
    IKStepper[] ikSteppers;
    IKChain[] ikChains;

    private AnimationCurve animCurve;

    [MenuItem("Window/Spider Master Window")]
    static void Init() {
        // Get existing open window or if none, make a new one:
        SpiderMasterWindow window = (SpiderMasterWindow)EditorWindow.GetWindow(typeof(SpiderMasterWindow));
        window.Show();
    }

    void findArrays() {
        ikChains = spider.GetComponentsInChildren<IKChain>();
        count = ikChains.Length;
        ikSteppers = new IKStepper[count];
        for (int i = 0; i < count; i++) {
            ikSteppers[i] = ikChains[i].GetComponent<IKStepper>();
        }
    }
    void OnGUI() {
        spider = (Spider)EditorGUILayout.ObjectField("Pick Spider", spider, typeof(Spider), true);

        if (spider != null) {
            if (EditorDrawing.DrawButton("Find IKSteppers")) findArrays();

            EditorGUILayout.LabelField("Found Scripts:", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            //Show IKSteppers
            GUI.enabled = false;
            EditorGUILayout.BeginVertical();
            {
                //Row1
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Name", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("IK Chains", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("IK Steppers", EditorStyles.boldLabel);
                }
                EditorGUILayout.EndHorizontal();

                //Row2+
                for (int i = 0; i < count; i++) {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField(ikChains[i].name);
                        EditorGUILayout.ObjectField(ikChains[i], typeof(IKChain), false);
                        EditorGUILayout.ObjectField(ikSteppers[i], typeof(IKStepper), false);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            GUI.enabled = true;
        }
    }
}