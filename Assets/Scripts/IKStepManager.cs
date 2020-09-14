/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * This class holds references to each IKStepper of the legs and manages the stepping of them.
 * So instead of each leg managing its stepping on its own, this class acts as the brain and decides when each leg should step.
 * It uses the step checking function in the IKStepper to determine if a step is wanted for a leg, and then handles it by calling
 * the step function in the IKStepper when the time is right to step.
 */

[DefaultExecutionOrder(+1)] // Make sure all the stepping logic is called after the IK was solved in each IKChain
public class IKStepManager : MonoBehaviour {
    public bool printDebugLogs;

    public Spider spider;

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

    [Header("Step Mode")]
    public StepMode stepMode;

    //Order is important here as this is the order stepCheck is performed, giving the first elements more priority in case of a same frame step desire
    [Header("Legs for Queue Modes")]
    public List<IKStepper> ikSteppers;
    private List<IKStepper> stepQueue;
    private Dictionary<int, bool> waitingForStep;

    [Header("Legs for Gait Mode")]
    public List<IKStepper> gaitGroupA;
    public List<IKStepper> gaitGroupB;
    private List<IKStepper> currentGaitGroup;
    private float nextSwitchTime;

    [Header("Steptime")]
    public bool dynamicStepTime = true;
    public float stepTimePerVelocity;
    [Range(0, 1.0f)]
    public float maxStepTime;

    public enum GaitStepForcing { NoForcing, ForceIfOneLegSteps, ForceAlways }
    [Header("Debug")]
    public GaitStepForcing gaitStepForcing;

    private void Awake() {

        /* Queue Mode Initialization */

        stepQueue = new List<IKStepper>();

        // Remove all inactive IKSteppers
        int k = 0;
        foreach (var ikStepper in ikSteppers.ToArray()) {
            if (!ikStepper.allowedTargetManipulationAccess()) ikSteppers.RemoveAt(k);
            else k++;
        }

        // Initialize the hash map for step waiting with false
        waitingForStep = new Dictionary<int, bool>();
        foreach (var ikStepper in ikSteppers) {
            waitingForStep.Add(ikStepper.GetInstanceID(), false);
        }

        /* Alternating Tetrapod Gait Initialization */

        // Remove all inactive IKSteppers from the Groups
        k = 0;
        foreach (var ikStepper in gaitGroupA.ToArray()) {
            if (!ikStepper.allowedTargetManipulationAccess()) gaitGroupA.RemoveAt(k);
            else k++;
        }
        k = 0;
        foreach (var ikStepper in gaitGroupB.ToArray()) {
            if (!ikStepper.allowedTargetManipulationAccess()) gaitGroupB.RemoveAt(k);
            else k++;
        }

        // Start with Group A and set switch time to step time
        currentGaitGroup = gaitGroupA;
        nextSwitchTime = maxStepTime;
    }

    private void LateUpdate() {
        if (stepMode == StepMode.AlternatingTetrapodGait) AlternatingTetrapodGait();
        else QueueStepMode();
    }

    private void QueueStepMode() {

        /* Perform the step checks for all legs not already waiting to step.
         * If a step is needed, enqueue them.
         */
        foreach (var ikStepper in ikSteppers) {

            // Check if Leg isnt already waiting for step.
            if (waitingForStep[ikStepper.GetInstanceID()] == true) continue;

            //Now perform check if a step is needed and if so enqueue the element
            if (ikStepper.stepCheck()) {
                stepQueue.Add(ikStepper);
                waitingForStep[ikStepper.GetInstanceID()] = true;
                if (printDebugLogs) Debug.Log(ikStepper.name + " is enqueued to step at queue position " + stepQueue.Count);
            }
        }

        if (printDebugLogs) printQueue();

        /* Iterate through the step queue in order and check if legs are eligible to step.
         * If legs are able to step, let them step.
         * If not, we have two cases:   If the current mode selected is the QueueWait mode, then stop the iteration.
         *                              If the current mode selected is the QueueNoWait mode, simply continue with the iteration.
         */
        int k = 0;
        foreach (var ikStepper in stepQueue.ToArray()) {
            if (ikStepper.allowedToStep()) {
                ikStepper.getIKChain().unpauseSolving();
                ikStepper.step(calculateStepTime(ikStepper));
                // Remove the stepping leg from the list:
                waitingForStep[ikStepper.GetInstanceID()] = false;
                stepQueue.RemoveAt(k);
                if (printDebugLogs) Debug.Log(ikStepper.name + " was allowed to step and is thus removed.");
            }
            else {
                if (printDebugLogs) Debug.Log(ikStepper.name + " is not allowed to step.");

                // Stop iteration here if Queue Wait mode is selected
                if (stepMode == StepMode.QueueWait) {
                    if (printDebugLogs) Debug.Log("Wait selected, thus stepping ends for this frame.");
                    break;
                }
                k++; // Increment k by one here since i did not remove the current element from the list.
            }
        }

        /* Iterate through all the legs that are still in queue, and therefore werent allowed to step.
         * For them pause the IK solving while they are waiting.
         */
        foreach (var ikStepper in stepQueue) {
            ikStepper.getIKChain().pauseSolving();
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
        currentGaitGroup = (currentGaitGroup == gaitGroupA) ? gaitGroupB : gaitGroupA;
        float stepTime = calculateAverageStepTime(currentGaitGroup);
        nextSwitchTime = Time.time + stepTime;

        if (printDebugLogs) {
            string text = ((currentGaitGroup == gaitGroupA) ? "Group: A" : "Group B") + " StepTime: " + stepTime;
            Debug.Log(text);
        }

        /* Now perform the stepping for the current gait group.
         * A leg in the gait group will only step if a step is needed.
         * However, for debug purposes depending on which force mode is selected the other legs can be forced to step anyway.
         */
        if (gaitStepForcing == GaitStepForcing.ForceAlways) {
            foreach (var ikStepper in currentGaitGroup) ikStepper.step(stepTime);
        }
        else if (gaitStepForcing == GaitStepForcing.ForceIfOneLegSteps) {
            bool b = false;
            foreach (var ikStepper in currentGaitGroup) {
                b = b || ikStepper.stepCheck();
                if (b == true) break;
            }
            if (b == true) foreach (var ikStepper in currentGaitGroup) ikStepper.step(stepTime);
        }
        else {
            foreach (var ikStepper in currentGaitGroup) {
                if (ikStepper.stepCheck()) ikStepper.step(stepTime);
            }
        }
    }

    private float calculateStepTime(IKStepper ikStepper) {
        if (dynamicStepTime) {
            float k = stepTimePerVelocity * spider.getScale(); // At velocity=1, this is the steptime
            float velocityMagnitude = ikStepper.getIKChain().getEndeffectorVelocityPerSecond().magnitude;
            return (velocityMagnitude == 0) ? maxStepTime : Mathf.Clamp(k / velocityMagnitude, 0, maxStepTime);
        }
        else return maxStepTime;
    }

    private float calculateAverageStepTime(List<IKStepper> ikSteppers) {
        if (dynamicStepTime) {
            float stepTime = 0;
            foreach (var ikStepper in ikSteppers) {
                stepTime += calculateStepTime(ikStepper);
            }
            return stepTime / ikSteppers.Count;
        }
        else return maxStepTime;
    }

    private void printQueue() {
        if (stepQueue == null) return;
        string queueText = "[";
        if (stepQueue.Count != 0) {
            foreach (var ikStepper in stepQueue) {
                queueText += ikStepper.name + ", ";
            }
            queueText = queueText.Substring(0, queueText.Length - 2);
        }
        queueText += "]";
        Debug.Log("Queue: " + queueText);
    }
}

