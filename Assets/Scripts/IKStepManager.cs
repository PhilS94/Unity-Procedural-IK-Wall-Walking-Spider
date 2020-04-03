using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[DefaultExecutionOrder(+1)] // Make sure this step checking is called after CCD solved
public class IKStepManager : MonoBehaviour {
    public bool printDebugLogs;

    public Spider spider;
    //Order is important here as this is the order stepCheck is performed, giving the first elements more priority
    public IKStepper[] ikSteppers;

    public IKStepper[] ikStepperGaitA;
    public IKStepper[] ikStepperGaitB;

    [Header("Steptime")]
    public bool dynamicStepTime = true;
    public float stepTimePerVelocity;
    [Range(0, 1.0f)]
    public float maxStepTime;
    private float stepTime;

    private Queue<IKStepper> stepQueue;
    private Dictionary<int, bool> waitingForStep;

    private IKStepper[] previousStepperGait;

    private void Awake() {
        stepQueue = new Queue<IKStepper>();
        waitingForStep = new Dictionary<int, bool>();

        foreach (var ikStepper in ikSteppers) {
            waitingForStep.Add(ikStepper.GetInstanceID(), false);
        }
    }

    private void Update() {
        // Calculate and set step time
        if (dynamicStepTime) {
            float k = stepTimePerVelocity * spider.getScale(); //At v=1, this is the steptime
            float magnitude = 0;
            foreach (var ikStepper in ikSteppers) {
                magnitude += ikStepper.getIKChain().getEndeffectorVelocityPerSecond().magnitude;
            }
            magnitude /= ikSteppers.Length;
            stepTime = (magnitude == 0) ? maxStepTime : Mathf.Clamp(k / magnitude, 0, maxStepTime);
        }
        else stepTime = maxStepTime;
    }

    // TODO: I currently dont care whether IKStepper is activated or not in IKChain
    private void LateUpdate() {
        //Uncomment for corresponding step mode
        gaitStepMode();
        //queueStepMode()
    }

    private void queueStepMode() {
        if (printDebugLogs) Debug.Log("Step Queue currently has " + stepQueue.Count + " elements.");

        /* Perform step checks for all legs not already waiting to step. */
        foreach (var ikStepper in ikSteppers) {

            // Check if Leg isnt already waiting for step.
            if (waitingForStep[ikStepper.GetInstanceID()] == true) continue;

            //Now perform check if a step is needed and if so enqueue the element
            if (ikStepper.stepCheck()) {
                stepQueue.Enqueue(ikStepper);
                waitingForStep[ikStepper.GetInstanceID()] = true;
                if (printDebugLogs) Debug.Log(ikStepper.name + " is enqueued to step at queue position " + stepQueue.Count);
            }
        }

        /* Perform stepping in queue order for all legs eligible to step. If one is eligible, break.*/
        int dequeueCount = 0;
        foreach (var ikStepper in stepQueue) {
            if (ikStepper.allowedToStep()) {
                waitingForStep[ikStepper.GetInstanceID()] = false;
                //ikStepper.ikchain.unpauseSolving();
                ikStepper.step(stepTime); //Important here that isStepping will be refreshed inside ikstepper
                dequeueCount++;
            }
            else break;
        }

        /* Dequeue all legs that started stepping above */
        while (dequeueCount > 0) {
            if (printDebugLogs) Debug.Log(stepQueue.Peek() + " was allowed to step and is thus dequeued.");
            stepQueue.Dequeue();
            dequeueCount--;
        }

        /* Iterate through all the legs that are still waiting for step to perform some kind of logic */
        foreach (var ikStepper in stepQueue) {
            if (printDebugLogs) Debug.Log(ikStepper.name + " is still waiting to step");
            //ikStepper.ikchain.pauseSolving();
        }
    }

    private void gaitStepMode() {
        IKStepper[] currentStepGroup = (Time.time % (2f * stepTime) < stepTime) ? ikStepperGaitA : ikStepperGaitB;

        if (currentStepGroup == previousStepperGait) return;

        foreach (var ikStepper in currentStepGroup) {
            if (ikStepper.stepCheck()) {
                ikStepper.step(stepTime);
            }
        }
        previousStepperGait = currentStepGroup;
    }
}

