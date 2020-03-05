using UnityEngine;
using System.Collections;
using Raycasting;

[DefaultExecutionOrder(-1)] //Make sure the players input spider movement is applied before the spider itself will do a ground check and possibly add gravity
public class SpiderController : MonoBehaviour {

    public Spider spider;

    [Header("Camera")]
    public SmoothCamera smoothCam;

    private Vector3 velocity = Vector3.zero;

    private void FixedUpdate() {
        //** Movement **//
        Vector3 input = getInput();

        //Adds an acceleration/deceleration to smooth out the movement.
        velocity = Vector3.Slerp(velocity, input, 0.1f);
        if (velocity.magnitude < 0.01f) velocity = Vector3.zero;
        spider.walk(velocity);

        Quaternion tempCamTargetRotation = smoothCam.getCamTargetRotation();
        Vector3 tempCamTargetPosition = smoothCam.getCamTargetPosition();
        spider.turn(input);
        smoothCam.setTargetRotation(tempCamTargetRotation);
        smoothCam.setTargetPosition(tempCamTargetPosition);
    }

    void Update() {
        // Since the spider might have adjusted its normal, rotate camera target halfway back here (More smooth experience instead of camera freezing in place with every normal adjustment)
        Vector3 n = spider.getLastNormal();
        if (n == Vector3.zero) return;
        float angle = Vector3.SignedAngle(spider.getLastNormal(), spider.transform.up, smoothCam.getCameraTarget().right);
        smoothCam.RotateCameraVertical(0.5f * -angle);
    }

    private Vector3 getInput() {
        Vector3 input = (Vector3.ProjectOnPlane(smoothCam.transform.forward, spider.transform.up) * Input.GetAxis("Vertical") + (Vector3.ProjectOnPlane(smoothCam.transform.right, spider.transform.up) * Input.GetAxis("Horizontal"))).normalized;
        Quaternion fromTo = spider.getLookRotation(spider.transform.right, spider.getGroundNormal()) * Quaternion.Inverse(spider.transform.rotation);
        return fromTo * input;
    }

}