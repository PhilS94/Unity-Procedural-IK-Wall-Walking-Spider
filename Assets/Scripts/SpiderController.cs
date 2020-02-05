using UnityEngine;
using System.Collections;

public class SpiderController : MonoBehaviour {

    public Camera cam;
    private Rigidbody rb;
    private SphereCollider sphereCol;

    public float walkSpeed;
    public float turnSpeed;
    [Range(1, 5)]
    public float XSensitivity;
    [Range(1, 5)]
    public float YSensitivity;
    private Vector3 camLocalPosition;
    private float gravity = 100;

    public float scale = 1.0f;
    public float raycastGroundedLength = 0.15f;
    public float raycastForwardLength = 0.1f;
    public float raycastHeightLegs;
    public float raycastDistanceLegs;

    public LayerMask groundedLayer;
    public LayerMask cameraClipLayer;
    private struct SphereRay { public Vector3 position; public Vector3 direction; public float radius; public float distance; };
    private SphereRay downRay, forwardRay;
    private RaycastHit hitInfo;
    private Vector3 currentNormal;

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
    private GameObject[] debugSphereRaysForward;
    private GameObject[] debugSphereRaysDown;

    void Start() {
        rb = GetComponent<Rigidbody>();
        sphereCol = GetComponent<SphereCollider>();
        currentNormal = Vector3.up;
        camLocalPosition = cam.transform.localPosition;

        if (showDebug) {
            setupSphereRayDraw(ref debugSphereRaysDown, 3);
            setupSphereRayDraw(ref debugSphereRaysForward, 3);
        }
    }

    void FixedUpdate() {
        rb.AddForce(-currentNormal * 200 * gravity * Time.fixedDeltaTime); //Gravity
    }

    void Update() {

        //** Ground Check **//
        grdInfo = GroundCheckSphere();

        /** Movement **/
        Vector3 input = getInput();
        turn(input, Time.deltaTime * turnSpeed);
        walk(input, Time.deltaTime * walkSpeed * scale);




        //** Rotation to normal **// 
        Vector3 newNormal = Vector3.Slerp(currentNormal, grdInfo.groundNormal, 10.0f * Time.deltaTime);
        float angle = Vector3.SignedAngle(currentNormal, newNormal,cam.transform.right);
        currentNormal = newNormal;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, currentNormal);
        Vector3 forward = Vector3.Cross(right, currentNormal);
        Quaternion goalrotation = Quaternion.LookRotation(forward, currentNormal);

        //Apply the rotation to the spider
        transform.rotation = goalrotation;

        //Adjust the camera as to not completely follow the rotation, This can lead to cam getting vertical rotation it shouldnt be allowed to get
            // Think about uing the coroutine: StartCoroutine(adjustCamera(-0.5f * angle, 1.0f));
            // Or lerp the angle independant of the normal slerp above. We want the camera to have completely independent lerping to the spider
        cam.transform.RotateAround(transform.position, cam.transform.right, -0.5f*angle);

        if (showDebug)
            Debug.DrawLine(transform.position, transform.position + 0.3f * scale * currentNormal, Color.yellow, 0.1f);

        //** Camera movement **//
        RotateCameraHorizontalAroundPlayerLocal();
        RotateCameraVerticalAroundPlayer();
        clipCamera();
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
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(forward, currentNormal), speed);
        cam.transform.rotation = tempCamRotation;
        cam.transform.position = tempCamPosition;
    }

    void walk(Vector3 moveVector, float speed) {
        if (moveVector != Vector3.zero) {
            transform.position += moveVector * speed;
            //rb.AddForce(moveVector * speed);
        }
    }

    //Implemented so the IKStepper can use this to predict 
    public Vector3 getMovement() {
        return getInput() * walkSpeed * scale;
    }

    //** Camera Methods **//
    void RotateCameraHorizontalAroundPlayerLocal() {
        float angle = Input.GetAxis("Mouse X") * XSensitivity;
        cam.transform.RotateAround(transform.position, transform.up, angle);
    }

    void RotateCameraVerticalAroundPlayer() {
        float angle = Input.GetAxis("Mouse Y") * YSensitivity;

        float angleMargin = 30.0f;
        angle = Mathf.Clamp(angle, -angleMargin, angleMargin);
        Quaternion q = Quaternion.AngleAxis(angle, cam.transform.right);
        float alpha = Vector3.Angle(q * cam.transform.forward, transform.up);
        if ((angle > 0 && alpha <= angleMargin) || angle < 0 && alpha >= 180.0f-angleMargin) return;

        cam.transform.RotateAround(transform.position, cam.transform.right, -angle);
    }

    void clipCamera() {
        Vector3 toCameraVector = cam.transform.position - transform.position;
        float MaxCameraDistance = Mathf.Abs(camLocalPosition.magnitude) * scale;
        Ray rayToCam = new Ray(transform.position, toCameraVector);

        Debug.DrawLine(rayToCam.GetPoint(0), rayToCam.GetPoint(0) + (toCameraVector.normalized * MaxCameraDistance), Color.magenta);
        if (Physics.Raycast(rayToCam, out hitInfo, MaxCameraDistance, cameraClipLayer, QueryTriggerInteraction.Ignore)) {
            cam.transform.position = hitInfo.point - 0.05f * toCameraVector;
            //GameObject hitGO = hitInfo.collider.gameObject;
            //Debug.Log("Cameraraycast hit " + hitGO + ". Adjusting Position of Camera.");
        }
        else {
            cam.transform.position = transform.position + (MaxCameraDistance * toCameraVector.normalized);
        }
    }

    //** Ground Check Methods **//
    private groundInfo GroundCheckSphere() {

        downRay.position = transform.position;
        downRay.direction = -transform.up;
        downRay.radius = 0.9f * sphereCol.radius * scale;
        downRay.distance = raycastGroundedLength * scale;

        forwardRay.position = transform.position;
        forwardRay.direction = transform.forward;
        forwardRay.radius = (sphereCol.radius / 1.5f) * scale;
        forwardRay.distance = raycastForwardLength * scale;

        if (showDebug) {
            DrawSphereRay(ref debugSphereRaysDown, downRay, Color.green);
            DrawSphereRay(ref debugSphereRaysForward, forwardRay, Color.blue);
        }

        if (shootSphere(forwardRay)) {
            return new groundInfo(true, hitInfo.normal.normalized, hitInfo.distance);
        }

        if (shootSphere(downRay)) {
            return new groundInfo(true, hitInfo.normal.normalized, hitInfo.distance);
        }

        //If SphereRays miss
        return new groundInfo(false, Vector3.up, float.PositiveInfinity);
    }

    bool shootSphere(SphereRay sphereRay) {
        return Physics.SphereCast(sphereRay.position, sphereRay.radius, sphereRay.direction, out hitInfo, sphereRay.distance, groundedLayer, QueryTriggerInteraction.Ignore);
    }

    private IEnumerator adjustCamera(float angle, float time) {
        float t = 0;
        while (t < time) {
            cam.transform.RotateAround(transform.position, cam.transform.right, angle* t/time);

            t += Time.deltaTime;
            yield return null;
        }
    }

    void setupSphereRayDraw(ref GameObject[] sphereRay, int amount) {
        GameObject group = new GameObject();
        group.name = "SphereRay";
        sphereRay = new GameObject[amount];
        for (int i = 0; i < amount; i++) {
            sphereRay[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sphereRay[i].GetComponent<SphereCollider>());
            sphereRay[i].GetComponent<MeshRenderer>().material.SetFloat("_Mode", 2);
            float value = ((float)i + 1) / (float)amount;
            sphereRay[i].transform.parent = group.transform;
        }
    }

    void DrawSphereRay(ref GameObject[] debugSphereRay, SphereRay sphereRay, Color color) {
        int amount = debugSphereRay.Length;

        Vector3 endPoint = sphereRay.position + (sphereRay.radius + sphereRay.distance) * sphereRay.direction;
        Vector3 endPointSphereCenter = endPoint - (sphereRay.radius * sphereRay.direction);

        for (int i = 0; i < amount; i++) {
            debugSphereRay[i].transform.localScale = new Vector3(1, 1, 1) * 2 * sphereRay.radius;
            float value = (float)i / (float)(amount - 1);
            debugSphereRay[i].transform.position = sphereRay.position + (value * (endPointSphereCenter - sphereRay.position));
            debugSphereRay[i].GetComponent<MeshRenderer>().material.color = new Color(value * color.r, value * color.g, value * color.b, 0.4f);
        }

        Debug.DrawLine(sphereRay.position, endPoint, Color.cyan);
    }
}