/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * A simple class which allows switching between a player input spider controller (SpiderController) and a randomly
 * generated movement (SpiderNPCController).
 * To each mode a camera is bound and by the press of a button, here TAB, a switch of controller and camera is performed.
 */
[RequireComponent(typeof(SpiderController))]
[RequireComponent(typeof(SpiderNPCController))]
public class SpiderModeSwitch : MonoBehaviour {

    private SpiderController control;
    private SpiderNPCController npcControl;

    private Camera controlCam;
    public Camera npcCam;

    private void Awake() {
        control = GetComponent<SpiderController>();
        npcControl = GetComponent<SpiderNPCController>();
    }

    void Start() {
        controlCam = control.smoothCam.cam;

        //Start with spider camera enabled
        if (controlCam.enabled && npcCam.enabled) npcCam.enabled = false;
        if (!controlCam.enabled && !npcCam.enabled) controlCam.enabled = true;

        // Start with spider controller enabled
        if (control.enabled && npcControl.enabled) npcControl.enabled = false;
        if (!control.enabled && !npcControl.enabled) control.enabled = true;
    }
    void Update() {

        if (Input.GetKeyDown(KeyCode.Tab)) {
            controlCam.enabled = !controlCam.enabled;
            npcCam.enabled = !npcCam.enabled;
            npcControl.enabled = !npcControl.enabled;
            control.enabled = !control.enabled;
        }
    }
}
