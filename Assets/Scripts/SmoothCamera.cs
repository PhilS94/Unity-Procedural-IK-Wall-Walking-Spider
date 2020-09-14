/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Inherits from abstract class CameraAbstract and manipulates the camera target by simply parenting it to the observed object.
 * Moreover, a rollDamp is implemented which will rotate the camera target back vertically whenever the observed object rotates.
 * E.g. for rollDamp=0.5f the camera will only rotate halfway, which is useful for the spider which climbs walls, as the camera
 * then only somewhat follows the spiders rotation but still "guides".
 * This is what we want since completely sticking the camera to the spiders change of orientation feels clunky.
 */

[RequireComponent(typeof(Camera))]
public class SmoothCamera : CameraAbstract {

    [Header("Camera Roll Damp")]
    [Range(0, 1)]
    public float rollDamp;

    private Vector3 lastObservedObjectNormal;

    protected override void Awake() {
        base.Awake();
        lastObservedObjectNormal = observedObject.up;
        camTarget.parent = observedObject;
    }

    protected override void Update() {
        base.Update();
        if (rollDamp != 0) {
            float angle = Vector3.SignedAngle(lastObservedObjectNormal, observedObject.up, camTarget.right);
            RotateCameraVertical(rollDamp * -angle);
            lastObservedObjectNormal = observedObject.up;
        }
    }

    protected override Vector3 getHorizontalRotationAxis() {
        return observedObject.transform.up;
    }
    protected override Vector3 getVerticalRotationAxis() {
        return camTarget.right;
    }
}
