/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/*
 * This class holds references to each IKStepper of the legs and manages the stepping of them.
 * So instead of each leg managing its stepping on its own, this class acts as the brain and decides when each leg should step.
 * It uses the step checking function in the IKStepper to determine if a step is wanted for a leg, and then handles it by calling
 * the step function in the IKStepper when the time is right to step.
 */

[DefaultExecutionOrder(+1)] // Make sure all the stepping logic is called after the IK was solved in each IKChain
public class IKStepManager : MonoBehaviour {

    // Enums and Structs
    public enum GaitGroup { NotAssigned, GroupA, GroupB };
    public enum GaitStepForcing { NoForcing, ForceGroupIfOneLegSteps, ForceGroupEveryTick }

    [System.Serializable]
    public struct LegData {

        public LegData(IKStepper m_ikStepper, GaitGroup m_group = GaitGroup.NotAssigned) {
            ikStepper = m_ikStepper;
            group = m_group;
            waitingForStep = false;
        }

        public IKStepper ikStepper;
        public GaitGroup group;
        public bool waitingForStep;
    }

    public enum StepMode { AlternatingTetrapodGait, QueueWait, QueueNoWait }

    /*
     * Note the following about the stepping modes:
     * 
     * Alternating Tetrapod Gait:   This mode is inspired by a real life spider walk.
     *                              The legs are assigned one of two groups, A or B.
     *                              Then a timer switches between these groups on the timeinterval "stepTime".
     *                              Every group only has a specific frame at which stepping is allowed in each interval
     *                              With this, legs in the same group will always step at the same time if they need to step,
     *                              and will never step while the other group is.
     *                              If dynamic step time is selected, the average of each legs dyanamic step time is used.
     *                              This mode does not use the asynchronicity specified in each legs, since the asyncronicty is already given
     *                              by the groups.
     *                              
     * Queue Wait:  This mode stores the legs that want to step in a queue and performs the stepping in the order of the queue.
     *              This mode will always prioritize the next leg in the queue and will wait until it is able to step.
     *              This however can and will inhibit the other legs from stepping if the waiting period is too long.
     *              Unlike the above mode, this mode uses the asyncronicity defined in each leg to determine whether a leg is 
     *              allowed to step or not. Each leg will be inhibited to step as long as these async legs are stepping.
     *              
     * Queue No Wait:   This mode is analog to the above with the exception of not waiting for each next leg in the queue.
     *                  The legs will still be iterated through in queue order but if a leg is not able to step,
     *                  we still continue iterating and perform steps for the following legs if they are able to.
     *                  So to be more specific, this is not a queue in the usual sense. It is a list of legs that need stepping,
     *                  which will be iterated through in order and if the k-th leg is allowed to step, it will step
     *                  and the k-th element of this list will be removed.
     */

    public bool printDebugLogs;
    public Spider spider;

    [Header("Step Mode")]
    public StepMode stepMode;

    //Order is important here as this is the order stepCheck is performed, giving the first elements more priority in case of a same frame step desire
    [Header("Legs for Queue Modes")]
    public LegData[] legs;
    private List<int> stepQueue;

    [Header("Legs for Gait Mode")]
    private float nextSwitchTime;
    private GaitGroup currentGroup;
    public GaitStepForcing gaitStepForcing;

    [Header("Steptime")]
    public bool dynamicStepTime = true;
    [Range(0.001f, 0.01f)]
    public float stepTimePerVelocity;
    [Range(0f, 1f)]
    public float maximumStepTime;
    [Range(0f, 1f)]
    public float fixedStepTime;

    public void Awake() {

        /* Queue Mode Initialization */
        stepQueue = new List<int>();

        /* Alternating Tetrapod Gait Initialization */
        currentGroup = GaitGroup.GroupA; // Start with Group A 
        nextSwitchTime = 1f; // Set first timer to step to a second
    }

    private void LateUpdate() {
        if (stepMode == StepMode.AlternatingTetrapodGait) AlternatingTetrapodGait();
        else QueueStepMode();
    }

    private void QueueStepMode() {

        /* Perform the step checks for all legs not already waiting to step.
         * If a step is needed, enqueue them.
         */
        for (int i = 0; i < legs.Length; i++) {

            // Check if Leg isnt already waiting for step.
            if (legs[i].waitingForStep == true) continue;

            //Now perform check if a step is needed and if so enqueue the element
            if (legs[i].ikStepper.stepCheck()) {
                stepQueue.Add(i);
                legs[i].waitingForStep = true;
                if (printDebugLogs) Debug.Log(legs[i].ikStepper.name + " is enqueued to step at queue position " + stepQueue.Count + " with value [" + legs[i].waitingForStep + "]");
            }
        }

        if (printDebugLogs) printQueue();

        /* Iterate through the step queue in order and check if legs are eligible to step.
         * If legs are able to step, let them step.
         * If not, we have two cases:   If the current mode selected is the QueueWait mode, then stop the iteration.
         *                              If the current mode selected is the QueueNoWait mode, simply continue with the iteration.
         */

        int removeIndex = 0;
        int[] temp = stepQueue.ToArray();
        foreach (int i in temp) {
            IKStepper ikStepper = legs[i].ikStepper;
            if (ikStepper.allowedToStep()) {
                ikStepper.ikChain.unpauseSolving();
                ikStepper.step(calculateStepTime(ikStepper));

                // Remove the stepping leg from the list:
                legs[i].waitingForStep = false;
                stepQueue.RemoveAt(removeIndex);
                if (printDebugLogs) Debug.Log(ikStepper.name + " was allowed to step and is thus removed.");
            }
            else {
                if (printDebugLogs) Debug.Log(ikStepper.name + " is not allowed to step.");

                // Stop iteration here if Queue Wait mode is selected
                if (stepMode == StepMode.QueueWait) {
                    if (printDebugLogs) Debug.Log("Wait selected, thus stepping ends for this frame.");
                    break;
                }
                removeIndex++; // Increment k by one here since i did not remove the current element from the list.
            }
        }

        /* Iterate through all the legs that are still in queue, and therefore werent allowed to step.
         * For them pause the IK solving while they are waiting.
         */
        foreach (int i in stepQueue) {
            legs[i].ikStepper.ikChain.pauseSolving();
        }
    }

    private void AlternatingTetrapodGait() {

        // If the next switch time isnt reached yet, do nothing.
        if (Time.time < nextSwitchTime) return;


        /* Since switch time is reached, switch groups and set new switch time.
         * Note that in the case of dynamic step time, it would not make sense to have each leg assigned its own step time
         * since i want the stepping to be completed at the same time in order to switch to next group again.
         * Thus, i simply calculate the average step time of the current group and use it for all legs.
         * TODO: Add a random offset to the steptime of each leg to imitate nature more closely and use the max value as the next switch time
         */

        currentGroup = (currentGroup == GaitGroup.GroupA) ? GaitGroup.GroupB : GaitGroup.GroupA;
        float stepTime = calculateAverageStepTime(currentGroup);
        nextSwitchTime = Time.time + stepTime;

        if (printDebugLogs) {
            string text = ((currentGroup == GaitGroup.GroupA) ? "Group: A" : "Group B") + " StepTime: " + stepTime;
            Debug.Log(text);
        }

        /* Now perform the stepping for the current gait group.
         * A leg in the gait group will only step if a step is needed.
         * However, depending on which force mode is selected the other legs can be forced to step anyway.
         */
        if (gaitStepForcing == GaitStepForcing.ForceGroupEveryTick) {
            foreach (var leg in legs) {
                if (leg.group == currentGroup) leg.ikStepper.step(stepTime);
            }
        }
        else if (gaitStepForcing == GaitStepForcing.ForceGroupIfOneLegSteps) {
            bool b = false;
            foreach (var leg in legs) {
                if (leg.group == currentGroup) {
                    b = b || leg.ikStepper.stepCheck();
                    if (b == true) break;
                }
            }
            if (b == true) {
                foreach (var leg in legs) {
                    if (leg.group == currentGroup) leg.ikStepper.step(stepTime);
                }
            }
        }
        else if (gaitStepForcing == GaitStepForcing.NoForcing) {
            foreach (var leg in legs) {
                if (leg.group == currentGroup) {
                    if (leg.ikStepper.stepCheck()) leg.ikStepper.step(stepTime);
                }
            }
        }
    }

    private float calculateStepTime(IKStepper ikStepper) {
        if (dynamicStepTime) {
            float k = stepTimePerVelocity * spider.getScale(); // At velocity=1, this is the steptime
            float velocityMagnitude = ikStepper.ikChain.getEndeffectorVelocityPerSecond().magnitude;
            return (velocityMagnitude == 0) ? maximumStepTime : Mathf.Clamp(k / velocityMagnitude, 0, maximumStepTime);
        }
        else return fixedStepTime;
    }

    private float calculateAverageStepTime(GaitGroup group) {
        if (dynamicStepTime) {
            float stepTime = 0;
            int k = 0;
            foreach (var leg in legs) {
                if (leg.group == group) {
                    stepTime += calculateStepTime(leg.ikStepper);
                    k++;
                }
            }
            return stepTime / k;
        }
        else return fixedStepTime;
    }

    private void printQueue() {
        if (stepQueue == null) return;
        string queueText = "[";
        if (stepQueue.Count != 0) {
            foreach (int i in stepQueue) {
                queueText += legs[i].ikStepper.name + ", ";
            }
            queueText = queueText.Substring(0, queueText.Length - 2);
        }
        queueText += "]";
        Debug.Log("Queue: " + queueText);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(IKStepManager))]
public class IKStepManagerEditor : Editor {

    private IKStepManager manager;

    public void OnEnable() {
        manager = (IKStepManager)target;

        if (manager.legs == null) findLegsInChildren();
    }

    private void findLegsInChildren() {
        List<IKStepManager.LegData> temp = new List<IKStepManager.LegData>();
        foreach (var ikStepper in manager.GetComponentsInChildren<IKStepper>()) {
            if (ikStepper.allowedTargetAccess()) {
                temp.Add(new IKStepManager.LegData(ikStepper, findGaitGroupOf(ikStepper)));

            }
        }
        manager.legs = temp.ToArray();
    }

    public IKStepManager.GaitGroup findGaitGroupOf(IKStepper ikStepper) {
        for (int i = 0; i < manager.legs.Length; i++) {
            if (manager.legs[i].ikStepper == ikStepper) return manager.legs[i].group;
        }
        return IKStepManager.GaitGroup.NotAssigned;
    }
    public void addLeg() {
        int n = manager.legs.Length;
        IKStepManager.LegData[] temp = new IKStepManager.LegData[n + 1];
        for (int i = 0; i < n; i++) { temp[i] = manager.legs[i]; }
        temp[n] = new IKStepManager.LegData(null);
        manager.legs = temp;
    }

    public void removeLeg() {
        int n = manager.legs.Length;
        if (n == 0) return;
        IKStepManager.LegData[] temp = new IKStepManager.LegData[n - 1];
        for (int i = 0; i < n - 1; i++) { temp[i] = manager.legs[i]; }
        manager.legs = temp;
    }

    public override void OnInspectorGUI() {
        if (manager == null) return;


        EditorDrawing.DrawMonoScript(manager, typeof(IKStepManager));

        serializedObject.Update();

        EditorDrawing.DrawHorizontalLine();

        //Debug Prints
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        serializedObject.FindProperty("printDebugLogs").boolValue = (bool)EditorGUILayout.Toggle("Print Debug Logs", manager.printDebugLogs);

        EditorDrawing.DrawHorizontalLine();

        // Spider Reference (Only Used for scaling of velocity)
        serializedObject.FindProperty("spider").objectReferenceValue = (Spider)EditorGUILayout.ObjectField("Spider", manager.spider, typeof(Spider), true);
        EditorGUILayout.Space();

        // Stepping Mode:
        EditorGUILayout.LabelField("Choose a Stepping mode", EditorStyles.boldLabel);
        serializedObject.FindProperty("stepMode").enumValueIndex = (int)(IKStepManager.StepMode)EditorGUILayout.EnumPopup("Step Mode", manager.stepMode);
        EditorGUILayout.Space();

        // The Array of LegData
        EditorGUI.indentLevel++;
        {
            SerializedProperty legsArrayProperty = serializedObject.FindProperty("legs");
            for (int i = 0; i < legsArrayProperty.arraySize; i++) {
                SerializedProperty singleLegProperty = legsArrayProperty.GetArrayElementAtIndex(i);

                // Alternating Tetrapod Gait
                if (manager.stepMode == IKStepManager.StepMode.AlternatingTetrapodGait) {
                    EditorGUILayout.BeginHorizontal();
                    {
                        singleLegProperty.FindPropertyRelative("ikStepper").objectReferenceValue = (IKStepper)EditorGUILayout.ObjectField(manager.legs[i].ikStepper, typeof(IKStepper), true);
                        singleLegProperty.FindPropertyRelative("group").enumValueIndex = (int)(IKStepManager.StepMode)EditorGUILayout.EnumPopup(manager.legs[i].group);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // Queue Mode
                else {
                    singleLegProperty.FindPropertyRelative("ikStepper").objectReferenceValue = (IKStepper)EditorGUILayout.ObjectField(manager.legs[i].ikStepper, typeof(IKStepper), true);
                }
            }
            // Show Step Forcing post loop for tetrapod gait
            if (manager.stepMode == IKStepManager.StepMode.AlternatingTetrapodGait) {
                serializedObject.FindProperty("gaitStepForcing").enumValueIndex = (int)(IKStepManager.GaitStepForcing)EditorGUILayout.EnumPopup("Force Step on Tick?", manager.gaitStepForcing);
            }

            // Buttons for leg array
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("");
                if (EditorDrawing.DrawButton("Add Leg")) addLeg();
                if (EditorDrawing.DrawButton("Remove Leg")) removeLeg();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("");
                if (EditorDrawing.DrawButton("Find Automatically")) findLegsInChildren();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        // StepTime
        EditorGUILayout.LabelField("Step Time", EditorStyles.boldLabel);
        serializedObject.FindProperty("dynamicStepTime").boolValue = EditorGUILayout.Toggle("Dynamic Step Time", manager.dynamicStepTime);
        EditorGUI.indentLevel++;
        {
            if (manager.dynamicStepTime) {
                serializedObject.FindProperty("stepTimePerVelocity").floatValue = EditorGUILayout.Slider("Step Time Per Velocity", manager.stepTimePerVelocity, 0f, 0.01f);
                serializedObject.FindProperty("maximumStepTime").floatValue = EditorGUILayout.Slider("Maximum Step Time", manager.maximumStepTime, 0f, 1f);
            }
            else {
                serializedObject.FindProperty("fixedStepTime").floatValue = EditorGUILayout.Slider("Fixed Step Time", manager.fixedStepTime, 0f, 1f);
            }
        }
        EditorGUI.indentLevel--;

        // Apply Changes
        serializedObject.ApplyModifiedProperties();
    }
}
#endif

