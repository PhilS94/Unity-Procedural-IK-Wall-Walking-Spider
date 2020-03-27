using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildDebugger : MonoBehaviour {

    public SpiderController spiderController;
    public SpiderNPCController spiderNPC;
    public Camera spiderCam;
    public Camera globalCam;

    void Start() {
        //Start with spider camera enabled
        if (spiderCam.enabled && globalCam.enabled) globalCam.enabled = false;
        if (!spiderCam.enabled && !globalCam.enabled) spiderCam.enabled = true;

        // Start with spider controller enabled
        if (spiderController.enabled && spiderNPC.enabled) spiderNPC.enabled = false;
        if (!spiderController.enabled && !spiderNPC.enabled) spiderController.enabled = true;
    }
    void Update() {

        if (Input.GetKeyDown(KeyCode.LeftShift)) {
            spiderCam.enabled = !spiderCam.enabled;
            globalCam.enabled = !globalCam.enabled;
            spiderNPC.enabled = !spiderNPC.enabled;
            spiderController.enabled = !spiderController.enabled;
        }
    }
}
