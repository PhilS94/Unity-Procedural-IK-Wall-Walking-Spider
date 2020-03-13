using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildDebugger : MonoBehaviour {

    public SpiderNPCController spiderNPC;
    public Camera spiderCam;
    public Camera globalCam;

    void Start() {
        if (spiderCam.enabled && globalCam.enabled) globalCam.enabled = false;
        if (!spiderCam.enabled && !globalCam.enabled) spiderCam.enabled = true;
    }
    void Update() {
        if (Input.GetKeyDown(KeyCode.Return)) spiderNPC.enabled = !spiderNPC.enabled;

        if (Input.GetKeyDown(KeyCode.LeftShift)) {
            spiderCam.enabled = !spiderCam.enabled;
            globalCam.enabled = !globalCam.enabled;
        }
    }
}
