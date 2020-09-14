/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using UnityEngine;
using System.Collections;
using Raycasting;

/*
 * This class needs a reference to the Spider class and calls the walk and turn functions depending on player input.
 * So in essence, this class translates player input to spider movement. The input direction is relative to a camera and so a 
 * reference to one is needed.
 */

[DefaultExecutionOrder(-1)] // Make sure the players input movement is applied before the spider itself will do a ground check and possibly add gravity
public class SpiderController : MonoBehaviour {

    public Spider spider;

    [Header("Camera")]
    public SmoothCamera smoothCam;

    void FixedUpdate() {
        //** Movement **//
        Vector3 input = getInput();

        if (Input.GetKey(KeyCode.LeftShift)) spider.run(input);
        else spider.walk(input);

        Quaternion tempCamTargetRotation = smoothCam.getCamTargetRotation();
        Vector3 tempCamTargetPosition = smoothCam.getCamTargetPosition();
        spider.turn(input);
        smoothCam.setTargetRotation(tempCamTargetRotation);
        smoothCam.setTargetPosition(tempCamTargetPosition);
    }

    void Update() {
        //Hold down Space to deactivate ground checking. The spider will fall while space is hold.
        spider.setGroundcheck(!Input.GetKey(KeyCode.Space));
    }

    private Vector3 getInput() {
        Vector3 up = spider.transform.up;
        Vector3 right = spider.transform.right;
        Vector3 input = Vector3.ProjectOnPlane(smoothCam.getCameraTarget().forward, up).normalized * Input.GetAxis("Vertical") + (Vector3.ProjectOnPlane(smoothCam.getCameraTarget().right, up).normalized * Input.GetAxis("Horizontal"));
        Quaternion fromTo = Quaternion.AngleAxis(Vector3.SignedAngle(up, spider.getGroundNormal(), right), right);
        input = fromTo * input;
        float magnitude = input.magnitude;
        return (magnitude <= 1) ? input : input /= magnitude;
    }
}