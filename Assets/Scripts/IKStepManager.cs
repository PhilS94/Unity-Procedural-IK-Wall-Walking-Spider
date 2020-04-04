using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[DefaultExecutionOrder(+1)] // Make sure this step checking is called after CCD solved
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
     *                              
     * Queue Wait:  This mode stores the legs that want to step in a queue and performs the stepping in the order of the queue.
     *              This mode will always prioritie the next leg in the queue and will wait until it is able to step.
     *              This however can and will inhibit the other legs from stepping if the waiting period is too long.
     *              
     * Queue No Wait:   This mode is analog to the above with the exception of not waiting for each next leg in the queue.
     *                  The legs will still be iterated through in queue order but if a leg is not able to step,
     *                  we still continue iterating and perform steps for the following legs if they are able to.
     */

    [Header("Step Mode")]
    public StepMode stepMode;

    //Order is important here as this is the order stepCheck is performed, giving the first elements more priority in case of a same frame step desire
    [Header("Legs for Queue Modes")]
    public IKStepper[] ikSteppers;
    private List<IKStepper> stepQueue;
    private Dictionary<int, bool> waitingForStep;

    [Header("Legs for Gait Mode")]
    public IKStepper[] ikStepperGaitA;
    public IKStepper[] ikStepperGaitB;
    private IKStepper[] currentStepGroup;
    private float nextSwitchTime;

    [Header("Steptime")]
    public bool dynamicStepTime = true;
    public float stepTimePerVelocity;
    [Range(0, 1.0f)]
    public float maxStepTime;
    public bool forceStepInGaitMode=false;

    private void Awake() {
        stepQueue = new List<IKStepper>();

        waitingForStep = new Dictionary<int, bool>();
        foreach (var ikStepper in ikSteppers) {
            waitingForStep.Add(ikStepper.GetInstanceID(), false);
        }

        currentStepGroup = ikStepperGaitA;
        nextSwitchTime = 0f;
    }

    // TODO: I currently dont care whether IKStepper is activated or not in IKChain
    private void LateUpdate() {

        if (stepMode == StepMode.AlternatingTetrapodGait) gaitStepMode();
        else queueStepMode();
    }

    private void queueStepMode() {

        /* Perform step checks for all legs not already waiting to step. */
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

        /* Perform stepping in queue order for all legs eligible to step. If one is eligible, break.*/
        int k = 0;
        foreach (var ikStepper in stepQueue.ToArray()) {
            if (ikStepper.allowedToStep()) {
                ikStepper.getIKChain().unpauseSolving();
                ikStepper.step(calculateStepTimeFromLeg(ikStepper)); //Important here that isStepping will be refreshed inside ikstepper
                // Remove the stepping leg from the list:
                waitingForStep[ikStepper.GetInstanceID()] = false;
                stepQueue.RemoveAt(k);
                if (printDebugLogs) Debug.Log(ikStepper.name + " was allowed to step and is thus removed.");
            }
            else {
                if (printDebugLogs) Debug.Log(ikStepper.name + " is not allowed to step.");

                // Stop here if i selected to wait for first element in queue to step first
                if (stepMode == StepMode.QueueWait) {
                    if (printDebugLogs) Debug.Log("Wait selected, thus stepping ends for this frame.");
                    break;
                }
                k++; // Increment k by one here since i do not remove the current element from the list.
            }
        }

        /* Iterate through all the legs that are still waiting for step to perform logic on them */
        foreach (var ikStepper in stepQueue) {
            ikStepper.getIKChain().pauseSolving();
        }
    }

    private void gaitStepMode() {
        if (Time.time < nextSwitchTime) return;

        //Switch groups and set new switch time
        currentStepGroup = (currentStepGroup == ikStepperGaitA) ? ikStepperGaitB : ikStepperGaitA;
        float stepTime = calculateStepTimeFromLegsAverage();
        nextSwitchTime = Time.time + stepTime;

        if (printDebugLogs) {
            string text = ((currentStepGroup == ikStepperGaitA) ? "Group: A" : "Group B") + " StepTime: " + stepTime;
            Debug.Log(text);
        }

        foreach (var ikStepper in currentStepGroup) {
            if (forceStepInGaitMode || ikStepper.stepCheck())
                ikStepper.step(stepTime);
        }
    }

    private float calculateStepTimeFromLeg(IKStepper ikStepper) {
        if (dynamicStepTime) {
            float k = stepTimePerVelocity * spider.getScale(); // At velocity=1, this is the steptime
            float velocityMagnitude = ikStepper.getIKChain().getEndeffectorVelocityPerSecond().magnitude;
            return (velocityMagnitude == 0) ? maxStepTime : Mathf.Clamp(k / velocityMagnitude, 0, maxStepTime);
        }
        else return maxStepTime;
    }

    private float calculateStepTimeFromLegsAverage() {
        if (dynamicStepTime) {
            float stepTime = 0;
            foreach (var ikStepper in ikSteppers) {
                stepTime += calculateStepTimeFromLeg(ikStepper);
            }
            stepTime /= ikSteppers.Length;
            return stepTime;
        }
        else return maxStepTime;
    }

    private float calculateStepTimeFromSpiderVelocity() {
        if (dynamicStepTime) {
            float k = stepTimePerVelocity * spider.getScale(); // At velocity=1, this is the steptime
            float velocityMagnitude = spider.getCurrentVelocityPerSecond().magnitude;
            return (velocityMagnitude == 0) ? maxStepTime : Mathf.Clamp(k / velocityMagnitude, 0, maxStepTime);
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

