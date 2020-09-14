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

public class SpiderModeSwitch : MonoBehaviour {

    public SpiderController spiderController;
    public SpiderNPCController spiderNPC;
    public Camera controllerCam;
    public Camera npcCam;

    void Start() {
        //Start with spider camera enabled
        if (controllerCam.enabled && npcCam.enabled) npcCam.enabled = false;
        if (!controllerCam.enabled && !npcCam.enabled) controllerCam.enabled = true;

        // Start with spider controller enabled
        if (spiderController.enabled && spiderNPC.enabled) spiderNPC.enabled = false;
        if (!spiderController.enabled && !spiderNPC.enabled) spiderController.enabled = true;
    }
    void Update() {

        if (Input.GetKeyDown(KeyCode.Tab)) {
            controllerCam.enabled = !controllerCam.enabled;
            npcCam.enabled = !npcCam.enabled;
            spiderNPC.enabled = !spiderNPC.enabled;
            spiderController.enabled = !spiderController.enabled;
        }
    }
}
