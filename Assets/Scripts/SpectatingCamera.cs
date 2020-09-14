/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Inherits from abstract class CameraAbstract and manipulates the camera target by mimicking position change of the observed object,
 * but ignoring rotational change. The camera targets rotation is always set to look at the observed object while forcing it
 * to stay upright to global Y.
 */
public class SpectatingCamera : CameraAbstract {

    private Vector3 lastPosition;

    protected override void Awake() {
        base.Awake();
        lastPosition = observedObject.position;
    }

    protected override void Update() {
        base.Update();
        updateCameraTarget();
    }

    private void updateCameraTarget() {

        // Position
        Vector3 translation = observedObject.position - lastPosition;
        camTarget.position += translation;
        lastPosition = observedObject.position;

        //Rotation
        Vector3 newForward = Vector3.ProjectOnPlane(observedObject.position - camTarget.position, Vector3.up);
        if (newForward != Vector3.zero)
            camTarget.rotation = Quaternion.LookRotation(observedObject.position - camTarget.position, Vector3.up);
    }

    protected override Vector3 getHorizontalRotationAxis() {
        return Vector3.up;
    }
    protected override Vector3 getVerticalRotationAxis() {
        return camTarget.right;
    }
}
