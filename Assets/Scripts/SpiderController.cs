using UnityEngine;
using System.Collections;
using Raycasting;

public class SpiderController : MonoBehaviour {

    private Rigidbody rb;

    [Header("Debug")]
    public bool showDebug;

    [Header("Scale of Transform")]
    public float scale = 1.0f;

    [Header("Movement")]
    public CapsuleCollider col;
    [Range(1, 5)]
    public float walkSpeed;
    [Range(1, 5)]
    public float turnSpeed;
    [Range(1, 10)]
    public float normalAdjustSpeed;
    public LayerMask walkableLayer;

    [Header("Camera")]
    public Camera cam;

    [Range(1, 5)]
    public float XSensitivity;
    [Range(1, 5)]
    public float YSensitivity;

    [Range(0.01f, 90.0f)]
    public float camUpperAngleMargin = 30.0f;
    [Range(0.01f, 90.0f)]
    public float camLowerAngleMargin = 60.0f;

    public LayerMask cameraInvisibleClipLayer;
    public LayerMask cameraClipLayer;

    [Header("Ray Adjustments")]
    [Range(0.0f, 1.0f)]
    public float forwardRayLength;
    [Range(0.0f, 1.0f)]
    public float downRayLength;
    [Range(0.1f, 1.0f)]
    public float forwardRaySize = 0.66f;
    [Range(0.1f, 1.0f)]
    public float downRaySize = 0.9f;

    private Vector3 currentDistancePerSecond;
    private Vector3 currentNormal;
    private float gravityOffDist = 0.05f;

    private SphereCast downRay, forwardRay;
    private RayCast camToPlayer, playerToCam;
    private RaycastHit hitInfo;
    private RaycastHit[] camObstructions;

    private struct groundInfo {
        public bool isGrounded;
        public Vector3 groundNormal;
        public float distanceToGround;

        public groundInfo(bool isGrd, Vector3 normal, float dist) {
            isGrounded = isGrd;
            groundNormal = normal;
            distanceToGround = dist;
        }
    }

    private groundInfo grdInfo;

    private void Awake() {
        rb = GetComponent<Rigidbody>();
    }

    void Start() {
        currentNormal = Vector3.up;

        downRay = new SphereCast();
        downRay.setRadius(downRaySize * col.radius * scale);
        downRay.setDistance(downRayLength * scale);

        forwardRay = new SphereCast();
        forwardRay.setDistance(forwardRayLength * scale);
        forwardRay.setRadius(forwardRaySize * col.radius * scale);

        float maxCameraDistance = Mathf.Abs(cam.transform.localPosition.magnitude) * scale;
        playerToCam = new RayCast();
        playerToCam.setDistance(maxCameraDistance);

        camToPlayer = new RayCast();
        camToPlayer.setDistance(maxCameraDistance);
    }

    void FixedUpdate() {
        // Dont apply gravity if close enough to ground
        if (grdInfo.distanceToGround > gravityOffDist) {
            rb.AddForce(-grdInfo.groundNormal * 1000.0f * Time.fixedDeltaTime); //Important using the groundnormal and not the lerping currentnormal here!
        }
    }

    void Update() {

        //** Movement **//
        Vector3 input = getInput();

        // Only move when movevector and forward angle small enough
        float distance = Mathf.Pow(Mathf.Clamp(Vector3.Dot(input, transform.forward), 0, 1), 4) * 0.1f * Time.deltaTime * walkSpeed * scale;
        //Make sure per frame we wont move more than our downsphereRay radius, or we might lose the floor. This can significantly slow down the spider when having low frame rates!
        distance = Mathf.Clamp(distance, 0, 0.99f * downRaySize);
        currentDistancePerSecond = distance / Time.deltaTime * input;
        walk(distance * input);
        turn(input, Time.deltaTime * turnSpeed);

        //** Ground Check **//
        // Important doing this after the movement, since we want to know whats beneath us in the new position, as to not apply gravity if we walked too far over a wall 
        grdInfo = GroundCheckSphere();


        //** Rotation to normal **// 
        Vector3 slerpNormal = Vector3.Slerp(currentNormal, grdInfo.groundNormal, normalAdjustSpeed * Time.deltaTime);
        float slerpAngle = Vector3.SignedAngle(currentNormal, slerpNormal, cam.transform.right);
        currentNormal = slerpNormal;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, currentNormal);
        Vector3 forward = Vector3.Cross(right, currentNormal);
        Quaternion goalrotation = Quaternion.LookRotation(forward, currentNormal);

        //Apply the rotation to the spider and rotate camera halfway back vertically
        transform.rotation = goalrotation;
        RotateCameraVertical(-0.5f * slerpAngle);


        //** Camera movement **//
        RotateCameraHorizontal(Input.GetAxis("Mouse X") * XSensitivity);
        RotateCameraVertical(-Input.GetAxis("Mouse Y") * YSensitivity);
        clipCamera();
        clipCameraInvisible();

        //** Debug **//
        if (showDebug) drawDebug();
    }



    //** Movement Methods **//
    private Vector3 getInput() {
        return (Vector3.ProjectOnPlane(cam.transform.forward, currentNormal) * Input.GetAxis("Vertical") + (Vector3.ProjectOnPlane(cam.transform.right, currentNormal).normalized * Input.GetAxis("Horizontal"))).normalized;
    }


    void turn(Vector3 forward, float speed) {
        if (forward == Vector3.zero) return;

        Quaternion tempCamRotation = cam.transform.rotation;
        Vector3 tempCamPosition = cam.transform.position;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(forward, currentNormal), 50.0f * speed);
        cam.transform.rotation = tempCamRotation;
        cam.transform.position = tempCamPosition;
    }

    void walk(Vector3 moveVector) {
        transform.position += moveVector;
    }

    //Implemented so the IKStepper can use this to predict 
    public Vector3 getMovement() {
        return currentDistancePerSecond;
    }

    //** Camera Methods **//
    void RotateCameraHorizontal(float angle) {
        cam.transform.RotateAround(transform.position, transform.up, angle);
    }

    void RotateCameraVertical(float angle) {

        angle = angle % 360;
        if (angle == -180) angle = 180;
        if (angle > 180) angle -= 360;
        if (angle < -180) angle += 360;
        //Now angle is of the form (-180,180]

        float currentAngle = Vector3.SignedAngle(transform.up, transform.position - cam.transform.position, cam.transform.right); //Should always be positive
        if (currentAngle + angle < camLowerAngleMargin) {
            angle = camLowerAngleMargin - currentAngle;
        }
        if (currentAngle + angle > 180.0f - camUpperAngleMargin) {
            angle = 180.0f - camUpperAngleMargin - currentAngle;
        }
        cam.transform.RotateAround(transform.position, cam.transform.right, angle);
    }

    void clipCamera() {
        float margin = 0.05f;

        updateRays();
        if (playerToCam.castRay(out hitInfo, cameraClipLayer)) {
            cam.transform.position = hitInfo.point - margin * playerToCam.getDirection();
        }
        else {
            cam.transform.position = playerToCam.getEnd();
        }
    }

    void clipCameraInvisible() {
        //First make all previous obstructions visible again.
        if (camObstructions != null) {
            for (int k = 0; k < camObstructions.Length; k++) {
                MeshRenderer mesh = camObstructions[k].transform.GetComponent<MeshRenderer>();
                if (mesh != null) {
                    mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
        }

        // Now transparent all new obstructions
        updateRays();
        camObstructions = camToPlayer.castRayAll(cameraInvisibleClipLayer);

        for (int k = 0; k < camObstructions.Length; k++) {
            MeshRenderer mesh = camObstructions[k].transform.GetComponent<MeshRenderer>();
            if (mesh != null) {
                mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }
    }

    //** Ground Check Methods **//
    private groundInfo GroundCheckSphere() {
        updateRays();
        if (forwardRay.castRay(out hitInfo, walkableLayer)) {
            return new groundInfo(true, hitInfo.normal.normalized, Vector3.Distance(transform.TransformPoint(col.center), hitInfo.point) - scale * col.radius);
        }

        if (downRay.castRay(out hitInfo, walkableLayer)) {
            return new groundInfo(true, hitInfo.normal.normalized, Vector3.Distance(transform.TransformPoint(col.center), hitInfo.point) - scale * col.radius);
        }

        return new groundInfo(false, Vector3.up, float.PositiveInfinity);
    }

    private void updateRays() {
        downRay.setOrigin(transform.position);
        downRay.setDirection(-transform.up);

        forwardRay.setOrigin(transform.position);
        forwardRay.setDirection(transform.forward);

        camToPlayer.setOrigin(cam.transform.position);
        camToPlayer.setLookDirection(transform.position);

        playerToCam.setOrigin(transform.position);
        playerToCam.setLookDirection(cam.transform.position);
    }

    //** Get Methods **//
    public CapsuleCollider getCapsuleCollider() {
        return col;
    }

    //** Debug Methods **//
    private void drawDebug() {
        downRay.draw(Color.green);
        forwardRay.draw(Color.blue);
        camToPlayer.draw(Color.magenta);

        Vector3 borderpoint = transform.TransformPoint(col.center) + col.radius * scale * -transform.up;
        Debug.DrawLine(borderpoint, borderpoint + gravityOffDist * -transform.up, Color.black);
        Debug.DrawLine(transform.position, transform.position + 0.3f * scale * currentNormal, new Color(1, 0.5f, 0, 1));    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected() {

        if (!showDebug) return;
        if (UnityEditor.EditorApplication.isPlaying) return;
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;

        Awake();
        Start();
        updateRays();
        drawDebug();
    }
#endif
}