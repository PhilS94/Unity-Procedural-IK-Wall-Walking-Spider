using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Inherits from abstract class CameraAbstract and manipulates the camera target by simply parenting it to the observed object.
 * Moreover a rollDamp is implemented which will rotate the camera target back vertically.
 * E.g. for rollDamp=0.5f the camera will only rotate halfway, which is useful for the spider which climbs walls.
 * We do not want the camera to completely stick to the spiders change of orientation but only follow it to a certain degree.
 */
[RequireComponent(typeof(Camera))]
public class SmoothCamera : CameraAbstract {

    [Header("Camera Roll Damp")]
    [Range(0, 1)]
    public float rollDamp;

    private Vector3 lastParentNormal;

    protected override void Awake() {
        base.Awake();
        lastParentNormal = observedObject.up;
        camTarget.parent = observedObject;
    }

    protected override void Update() {
        base.Update();
        if (rollDamp != 0) {
            float angle = Vector3.SignedAngle(lastParentNormal, observedObject.up, camTarget.right);
            RotateCameraVertical(rollDamp * -angle);
            lastParentNormal = observedObject.up;
        }
    }

    protected override Vector3 getHorizontalRotationAxis() {
        return observedObject.transform.up;
    }
    protected override Vector3 getVerticalRotationAxis() {
        return camTarget.right;
    }
}
