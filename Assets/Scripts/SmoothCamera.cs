using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Raycasting;

[RequireComponent(typeof(Camera))]
public class SmoothCamera : CameraAbstract {

    [Header("Camera Roll Damp")]
    [Range(0, 1)]
    public float rollDamp;

    [Header("Camera Clipping")]
    public bool hideObstructions;
    public bool clipToLevelBorder;
    [Range(0, 1f)]
    public float rayRadiusForObstructions;
    public LayerMask cameraInvisibleClipLayer;
    public LayerMask cameraClipLayer;

    [Range(0, 0.5f)]
    public float cameraClipMargin = 0.1f;

    private SphereCast camToPlayer;
    private RayCast playerToCam;
    float maxCameraDistance;
    private RaycastHit hitInfo;
    private RaycastHit[] camObstructions;
    private ShadowCastingMode[] camObstructionsCastingMode;

    private Vector3 lastParentNormal;

    new void Awake() {
        base.Awake();
        initializeRayCasting();
        lastParentNormal = observedObject.up;
        camTarget.parent = observedObject;
    }

    private void initializeRayCasting() {
        maxCameraDistance = Vector3.Distance(observedObject.position, transform.position);
        playerToCam = new RayCast(observedObject.position, camTarget.position, observedObject, null);
        camToPlayer = new SphereCast(transform.position, observedObject.position, rayRadiusForObstructions * transform.lossyScale.y * 0.01f, transform, observedObject);
    }

    new void Update() {
        base.Update();

        if (rollDamp != 0) {
            float angle = Vector3.SignedAngle(lastParentNormal, observedObject.up, camTarget.right);
            RotateCameraVertical(rollDamp * -angle);
            lastParentNormal = observedObject.up;
        }

        if (!cam.enabled) return;
        if (clipToLevelBorder) clipCamera();
        if (hideObstructions) clipCameraInvisible();
    }

    new void LateUpdate() {
        base.LateUpdate();

        // Rotation Interpolation
        transform.rotation = Quaternion.Slerp(transform.rotation, camTarget.rotation, Time.deltaTime * rotationSpeed);
    }

    public override void RotateCameraHorizontal(float angle, bool onlyTarget = true) {
        if (angle == 0) return;
        camTarget.RotateAround(observedObject.position, observedObject.transform.up, angle);
        if (!onlyTarget) transform.RotateAround(observedObject.position, observedObject.transform.up, angle);
    }

    public override void RotateCameraVertical(float angle, bool onlyTarget = true) {

        clampAngle(ref angle);
        if (angle == 0) return;

        // Adjust angle according to upper and lower bound
        float currentAngle = Vector3.SignedAngle(observedObject.transform.up, observedObject.transform.position - camTarget.position, camTarget.right); //Should always be positive
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

    void clipCamera() {
        playerToCam.setEnd(camTarget.position);
        playerToCam.setDistance(maxCameraDistance);

        if (playerToCam.castRay(out hitInfo, cameraClipLayer)) {
            float margin = Mathf.Min(cameraClipMargin, 0.9f * Vector3.Distance(hitInfo.point, observedObject.position));
            setTargetPosition(hitInfo.point - margin * playerToCam.getDirection());
        }
        else setTargetPosition(playerToCam.getEnd());

    }

    void clipCameraInvisible() {

        //First make all previous obstructions visible again.
        if (camObstructions != null) {
            for (int k = 0; k < camObstructions.Length; k++) {
                MeshRenderer mesh = camObstructions[k].transform.GetComponent<MeshRenderer>();
                if (mesh == null) continue;
                mesh.shadowCastingMode = camObstructionsCastingMode[k];
            }
        }

        // Now transparent all new obstructions
        camObstructions = camToPlayer.castRayAll(cameraInvisibleClipLayer);
        camObstructionsCastingMode = new ShadowCastingMode[camObstructions.Length];
        for (int k = 0; k < camObstructions.Length; k++) {
            MeshRenderer mesh = camObstructions[k].transform.GetComponent<MeshRenderer>();
            if (mesh == null) continue;
            camObstructionsCastingMode[k] = mesh.shadowCastingMode;
            mesh.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }

    new void drawDebug() {
        base.drawDebug();
        DebugShapes.DrawRay(camToPlayer.getOrigin(), cameraClipMargin * camToPlayer.getDirection(), Color.black);
    }
}
