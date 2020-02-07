using UnityEngine;
using System.Collections;

public class SpiderController : MonoBehaviour {

    public Camera cam;
    private Rigidbody rb;
    public SphereCollider sphereCol;

    [Range(1, 5)]
    public float walkSpeed;
    [Range(1, 5)]
    public float turnSpeed;
    [Range(1, 5)]
    public float XSensitivity;
    [Range(1, 5)]
    public float YSensitivity;

    [Range(1, 10)]
    public float normalAdjustSpeed;

    [Range(0.01f, 90.0f)]
    public float camUpperAngleMargin = 30.0f;
    [Range(0.01f, 90.0f)]
    public float camLowerAngleMargin = 60.0f;

    private Vector3 camLocalPosition;

    public float scale = 1.0f;

    public LayerMask groundedLayer;
    public LayerMask cameraClipLayer;

    private struct SphereRay {
        public Vector3 position;
        public Vector3 direction;
        public float radius;
        public float distance;

        public SphereRay(Vector3 p, Vector3 dir, float r, float dist) {
            position = p;
            direction = dir;
            radius = r;
            distance = dist;
        }
    }
    private SphereRay downRay, forwardRay;
    private RaycastHit hitInfo;
    private RaycastHit[] camObstructions;
    private Vector3 currentNormal;
    private float gravityOffDist = 0.1f;
    private Vector3 currentWalkVector;

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

    //Debug Variables
    public bool showDebug;

    void Start() {
        rb = GetComponent<Rigidbody>();
        currentNormal = Vector3.up;
        camLocalPosition = cam.transform.localPosition;
    }

    void FixedUpdate() {
        // No Gravity if close enough to ground
        if (grdInfo.distanceToGround < sphereCol.radius * scale + gravityOffDist) return;
            rb.AddForce(-grdInfo.groundNormal * 10000 * Time.fixedDeltaTime); //Important using the groundnormal and not the lerping currentnormal here!
    }

    void Update() {

        /** Movement **/
        Vector3 input = getInput();
        turn(input, Time.deltaTime * turnSpeed);

        // Only move when movevector and forward angle small enough
        currentWalkVector = 0.1f * Time.deltaTime * walkSpeed * scale * input;
        currentWalkVector *= Mathf.Pow(Mathf.Clamp(Vector3.Dot(input, transform.forward), 0, 1), 4);
        walk(currentWalkVector);

        //** Ground Check **//
        // Important doing this after the movement, since we want to know whats beneath us in the new position, as to not apply gravity if we walked too far over a wall 
        grdInfo = GroundCheckSphere();

        // I need to reduce the jittering that occurs using the spherecast every frame while adjusting.. disable gravity while grounded?


        //** Rotation to normal **// 
        Vector3 newNormal = Vector3.Slerp(currentNormal, grdInfo.groundNormal, normalAdjustSpeed * Time.deltaTime);
        float angle = Vector3.SignedAngle(currentNormal, newNormal, cam.transform.right);
        currentNormal = newNormal;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, currentNormal);
        Vector3 forward = Vector3.Cross(right, currentNormal);
        Quaternion goalrotation = Quaternion.LookRotation(forward, currentNormal);

        //Apply the rotation to the spider
        transform.rotation = goalrotation;

        //Adjust the camera as to not completely follow the rotation, This can lead to cam getting vertical rotation it shouldnt be allowed to get
        // Think about uing the coroutine: StartCoroutine(adjustCamera(-0.5f * angle, 1.0f));
        // Or lerp the angle independant of the normal slerp above. We want the camera to have completely independent lerping to the spider
        // Atleast fix the non allowed rotations
        //cam.transform.RotateAround(transform.position, cam.transform.right, -0.5f * angle);
        RotateCameraVerticalAroundPlayer(-0.5f * angle);

        if (showDebug)
            Debug.DrawLine(transform.position, transform.position + 0.3f * scale * currentNormal, Color.yellow);

        //** Camera movement **//

        RotateCameraHorizontalAroundPlayerLocal(Input.GetAxis("Mouse X") * XSensitivity);
        RotateCameraVerticalAroundPlayer(-Input.GetAxis("Mouse Y") * YSensitivity);
        clipCameraInvisible();

        if (showDebug) {
            drawDebug();
        }

    }


    //** Movement Methods **//
    private Vector3 getInput() {
        return (Vector3.ProjectOnPlane(cam.transform.forward, currentNormal) * Input.GetAxis("Vertical") + (Vector3.ProjectOnPlane(cam.transform.right, currentNormal).normalized * Input.GetAxis("Horizontal"))).normalized;
    }


    void turn(Vector3 forward, float speed) {
        if (forward == Vector3.zero)
            return;

        Quaternion tempCamRotation = cam.transform.rotation;
        Vector3 tempCamPosition = cam.transform.position;
        //transform.rotation = Quaternion.LookRotation(forward,currentNormal);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(forward, currentNormal), 100.0f * speed);
        cam.transform.rotation = tempCamRotation;
        cam.transform.position = tempCamPosition;
    }

    void walk(Vector3 moveVector) {
        if (moveVector != Vector3.zero) {
            transform.position += moveVector;
            //rb.AddForce(moveVector * speed);
        }
    }

    //Implemented so the IKStepper can use this to predict 
    public Vector3 getMovement() {
        return currentWalkVector;
    }

    //** Camera Methods **//
    void RotateCameraHorizontalAroundPlayerLocal(float angle) {
        cam.transform.RotateAround(transform.position, transform.up, angle);
    }

    void RotateCameraVerticalAroundPlayer(float angle) {

        angle = angle % 360;        //Now angle is of the form (-360,360]
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

        //angle = Mathf.Clamp(angle, -angleMargin, angleMargin);
        //Quaternion q = Quaternion.AngleAxis(angle, cam.transform.right);
        //float alpha = Vector3.Angle(q * cam.transform.forward, transform.up);
        //if ((angle > 0 && alpha <= angleMargin) || angle < 0 && alpha >= 180.0f - angleMargin) return;

        cam.transform.RotateAround(transform.position, cam.transform.right, angle);
    }

    void clipCamera() {
        Vector3 toCameraVector = cam.transform.position - transform.position;
        float MaxCameraDistance = Mathf.Abs(camLocalPosition.magnitude) * scale;
        Ray rayToCam = new Ray(transform.position, toCameraVector);

        Debug.DrawLine(rayToCam.GetPoint(0), rayToCam.GetPoint(0) + (toCameraVector.normalized * MaxCameraDistance), Color.magenta);

        //Think about instead changing alpha of the found objects
        if (Physics.Raycast(rayToCam, out hitInfo, MaxCameraDistance, cameraClipLayer, QueryTriggerInteraction.Ignore)) {
            cam.transform.position = hitInfo.point - 0.05f * toCameraVector;
            //GameObject hitGO = hitInfo.collider.gameObject;
            //Debug.Log("Cameraraycast hit " + hitGO + ". Adjusting Position of Camera.");
        }
        else {
            cam.transform.position = transform.position + (MaxCameraDistance * toCameraVector.normalized);
        }
    }

    void clipCameraInvisible() {
        Vector3 to = transform.position - cam.transform.position;
        Ray fromSpiderToCam = new Ray(cam.transform.position, to.normalized);

        Debug.DrawLine(fromSpiderToCam.origin, fromSpiderToCam.origin + to, Color.magenta);

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
        camObstructions = Physics.RaycastAll(fromSpiderToCam, to.magnitude, cameraClipLayer, QueryTriggerInteraction.Ignore);

        for (int k = 0; k < camObstructions.Length; k++) {
            MeshRenderer mesh = camObstructions[k].transform.GetComponent<MeshRenderer>();
            if (mesh != null) {
                mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }
    }

    //** Ground Check Methods **//
    // The Distance in GroundInfo is the distance from transform.position to hitinfo.position
    private groundInfo GroundCheckSphere() {

        downRay.position = transform.position;
        downRay.direction = -transform.up;
        downRay.radius = 0.9f * sphereCol.radius * scale;
        downRay.distance = 0.15f * scale;

        forwardRay.position = transform.position;
        forwardRay.direction = transform.forward;
        forwardRay.radius = 0.66f * sphereCol.radius * scale;
        forwardRay.distance = 0.05f * scale;

        if (shootSphere(forwardRay)) {
            return new groundInfo(true, hitInfo.normal.normalized, hitInfo.distance + forwardRay.radius);
        }

        if (shootSphere(downRay)) {
            return new groundInfo(true, hitInfo.normal.normalized, hitInfo.distance + downRay.radius);
        }

        //If SphereRays miss
        return new groundInfo(false, Vector3.up, float.PositiveInfinity);
    }

    bool shootSphere(SphereRay sphereRay) {
        return Physics.SphereCast(sphereRay.position, sphereRay.radius, sphereRay.direction, out hitInfo, sphereRay.distance, groundedLayer, QueryTriggerInteraction.Ignore);
    }

    private void drawDebug() {
        DebugShapes.DrawSphereRay(downRay.position, downRay.direction, downRay.distance, downRay.radius, 3, Color.green);
        DebugShapes.DrawSphereRay(forwardRay.position, forwardRay.direction, forwardRay.distance, forwardRay.radius, 3, Color.blue);
        Debug.DrawLine(transform.position, transform.position - (sphereCol.radius * scale + gravityOffDist) * transform.up, Color.black);
    }

    private IEnumerator adjustCamera(float angle, float time) {
        float t = 0;
        while (t < time) {
            cam.transform.RotateAround(transform.position, cam.transform.right, angle * t / time);

            t += Time.deltaTime;
            yield return null;
        }
    }
}