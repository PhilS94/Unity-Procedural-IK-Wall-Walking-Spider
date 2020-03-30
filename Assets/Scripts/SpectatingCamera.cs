using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpectatingCamera : CameraAbstract {

    private Vector3 lastPosition;
    private Vector3 velocity = Vector3.zero;

    new void Awake() {
        base.Awake();
        lastPosition = observedObject.position;
    }

    new void LateUpdate() {

        /* Update Target first*/

        // Position
        Vector3 translation = observedObject.position - lastPosition;
        camTarget.position += translation;
        lastPosition = observedObject.position;

        //Rotation
        Vector3 newForward = Vector3.ProjectOnPlane(observedObject.position - camTarget.position, Vector3.up);
        if (newForward != Vector3.zero)
            camTarget.rotation = Quaternion.LookRotation(observedObject.position - camTarget.position, Vector3.up);

        /* Now perform lerping 
         */
        // Call the base late update (performs translation lerping)
        base.LateUpdate();

        // Now Rotation Slerping
        transform.rotation = Quaternion.Slerp(transform.rotation, camTarget.rotation, Time.deltaTime * rotationSpeed);
    }

    public override void RotateCameraHorizontal(float angle, bool onlyTarget = true) {
        if (angle == 0) return;
        camTarget.RotateAround(observedObject.position, Vector3.up, angle);
        if (!onlyTarget) transform.RotateAround(observedObject.position, Vector3.up, angle);
    }

    public override void RotateCameraVertical(float angle, bool onlyTarget = true) {

        clampAngle(ref angle);
        if (angle == 0) return;

        // Adjust angle according to upper and lower bound
        float currentAngle = Vector3.SignedAngle(Vector3.up, observedObject.transform.position - camTarget.position, camTarget.right); //Should always be positive
        if (currentAngle + angle < camLowerAngleMargin) {
            angle = camLowerAngleMargin - currentAngle;
        }
        if (currentAngle + angle > 180.0f - camUpperAngleMargin) {
            angle = 180.0f - camUpperAngleMargin - currentAngle;
        }

        // Apply Rotation
        camTarget.RotateAround(observedObject.position, camTarget.right, angle);
        if (!onlyTarget) transform.RotateAround(observedObject.position, camTarget.right, angle);
    }
}
