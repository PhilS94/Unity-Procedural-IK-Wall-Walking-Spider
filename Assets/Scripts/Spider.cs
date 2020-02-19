using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Raycasting;

public class Spider : MonoBehaviour {
    private Rigidbody rb;

    [Header("Debug")]
    public bool showDebug;

    [Header("Scale of Transform")]
    public float scale = 1.0f;

    [Header("Movement")]
    public CapsuleCollider col;
    [Range(1, 10)]
    public float walkSpeed;
    [Range(1, 5)]
    public float turnSpeed;
    [Range(1, 10)]
    public float normalAdjustSpeed;
    public LayerMask walkableLayer;


    [Header("Ray Adjustments")]
    [Range(0.0f, 1.0f)]
    public float forwardRayLength;
    [Range(0.0f, 1.0f)]
    public float downRayLength;
    [Range(0.1f, 1.0f)]
    public float forwardRaySize = 0.66f;
    [Range(0.1f, 1.0f)]
    public float downRaySize = 0.9f;

    private Vector3 currentVelocity;
    private Vector3 lastNormal;
    private float gravityOffDist = 0.05f;

    private SphereCast downRay, forwardRay;
    private RaycastHit hitInfo;

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
        downRay = new SphereCast(transform.position, -transform.up, scale * downRayLength, downRaySize * scale * col.radius, transform, transform);
        forwardRay = new SphereCast(transform.position, transform.forward, scale * forwardRayLength, forwardRaySize * scale * col.radius, transform, transform);
    }

    void FixedUpdate() {
        //** Ground Check **//
        grdInfo = GroundCheckSphere();

        //** Rotation to normal **// 
        Vector3 slerpNormal = Vector3.Slerp(transform.up, grdInfo.groundNormal, normalAdjustSpeed * Time.fixedDeltaTime);
        Quaternion goalrotation = getLookRotation(transform.right, slerpNormal);

        // Save last Normal for access
        lastNormal = transform.up;

        //Apply the rotation to the spider
        transform.rotation = goalrotation;


        // Dont apply gravity if close enough to ground
        if (grdInfo.distanceToGround > gravityOffDist) {
            rb.AddForce(-grdInfo.groundNormal * 1000.0f * Time.fixedDeltaTime); //Important using the groundnormal and not the lerping currentnormal here!
        }
    }

    void Update() {
        //** Debug **//
        if (showDebug) drawDebug();
    }

    /*
     * Returns the rotation with specified right and up direction (right will be projected onto the plane given by up)
     */
    public Quaternion getLookRotation(Vector3 right, Vector3 up) {
        if (up == Vector3.zero || right == Vector3.zero) return Quaternion.identity;
        Vector3 projRight = Vector3.ProjectOnPlane(right, up);
        if (projRight == Vector3.zero) return Quaternion.identity;
        Vector3 forward = Vector3.Cross(projRight, up);
        return Quaternion.LookRotation(forward, up);
    }

    public void turn(Vector3 goalForward, float speed) {
        if (goalForward == Vector3.zero) return;

        Debug.DrawLine(transform.position, transform.position + goalForward * 5.0f, new Color(Time.time, 0, 0, 1.0f), 3.0f);
        goalForward = Vector3.ProjectOnPlane(goalForward, transform.up);
        DebugShapes.DrawPoint(transform.position, new Color(0, 0, Time.time, 1.0f), 0.03f, 3.0f);
        Debug.DrawLine(transform.position, transform.position + goalForward * 5.0f, new Color(0, 0, Time.time, 1.0f), 3.0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(goalForward, transform.up), 50.0f * turnSpeed * speed);
    }

    public void walk(Vector3 direction, float speed) {
        direction = direction.normalized;
        // Increase velocity as the direction and forward vector of spider get closer together
        float distance = Mathf.Pow(Mathf.Clamp(Vector3.Dot(direction, transform.forward), 0, 1), 4) * 0.1f * walkSpeed * speed * scale;
        //Make sure per frame we wont move more than our downsphereRay radius, or we might lose the floor.
        //It is advised to call this method every fixed frame since collision is calculated on a fixed frame basis.
        distance = Mathf.Clamp(distance, 0, 0.99f * downRaySize);
        currentVelocity = distance * direction;
        transform.position += currentVelocity;
    }

    public Vector3 getCurrentVelocityPerSecond() {
        return currentVelocity / Time.deltaTime;
    }

    public Vector3 getCurrentVelocityPerFrame() {
        return currentVelocity;
    }

    public Vector3 getLastNormal() {
        return lastNormal;
    }
    //** Ground Check Methods **//
    private groundInfo GroundCheckSphere() {
        if (forwardRay.castRay(out hitInfo, walkableLayer)) {
            return new groundInfo(true, hitInfo.normal.normalized, Vector3.Distance(transform.TransformPoint(col.center), hitInfo.point) - scale * col.radius);
        }

        if (downRay.castRay(out hitInfo, walkableLayer)) {
            return new groundInfo(true, hitInfo.normal.normalized, Vector3.Distance(transform.TransformPoint(col.center), hitInfo.point) - scale * col.radius);
        }

        return new groundInfo(false, Vector3.up, float.PositiveInfinity);
    }

    //** Get Methods **//
    public CapsuleCollider getCapsuleCollider() {
        return col;
    }

    public Vector3 getGroundNormal() {
        return grdInfo.groundNormal;
    }

    //** Debug Methods **//
    private void drawDebug() {
        downRay.draw(Color.green);
        forwardRay.draw(Color.blue);
        Vector3 borderpoint = transform.TransformPoint(col.center) + col.radius * scale * -transform.up;
        Debug.DrawLine(borderpoint, borderpoint + gravityOffDist * -transform.up, Color.black);
        Debug.DrawLine(transform.position, transform.position + 0.3f * scale * transform.up, new Color(1, 0.5f, 0, 1));
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected() {

        if (!showDebug) return;
        if (UnityEditor.EditorApplication.isPlaying) return;
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;

        Awake();
        Start();
        drawDebug();
    }
#endif

}
