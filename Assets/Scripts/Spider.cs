using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Raycasting;

[DefaultExecutionOrder(0)]
public class Spider : MonoBehaviour {

    private Rigidbody rb;

    [Header("Debug")]
    public bool showDebug;

    [Header("Movement")]
    public CapsuleCollider col;
    [Range(1, 10)]
    public float walkSpeed;
    [Range(1, 5)]
    public float turnSpeed;
    [Range(1, 10)]
    public float normalAdjustSpeed;
    public LayerMask walkableLayer;
    [Range(0, 1)]
    public float gravityOffDistance;

    [Header("IK Legs")]
    public Transform body;
    public IKChain[] legs;
    public bool activateLegCentroidAdjustment;
    public bool activateLegNormalAdjustment;
    [Range(0, 10)]
    public float legNormalAdjustmentSpeed;
    [Range(0, 1)]
    public float legNormalWeight;
    private Vector3 bodyY;
    private Vector3 bodyZ;

    [Header("Breathing")]
    public bool activateBreathing;
    [Range(0.01f, 20)]
    public float period;
    [Range(0, 1)]
    public float breatheMagnitude;

    [Header("Ray Adjustments")]
    [Range(0.0f, 1.0f)]
    public float forwardRayLength;
    [Range(0.0f, 1.0f)]
    public float downRayLength;
    [Range(0.1f, 1.0f)]
    public float forwardRaySize = 0.66f;
    [Range(0.1f, 1.0f)]
    public float downRaySize = 0.9f;
    private float downRayRadius;

    private Vector3 currentVelocity;
    private bool isMoving = false;
    private Vector3 lastNormal;
    private Vector3 breathePivot;

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

        //Make sure the scale is uniform, since otherwise lossy scale will not be accurate.
        float x = transform.localScale.x; float y = transform.localScale.y; float z = transform.localScale.z;
        if (Mathf.Abs(x - y) > float.Epsilon || Mathf.Abs(x - z) > float.Epsilon || Mathf.Abs(y - z) > float.Epsilon) {
            Debug.LogWarning("The xyz scales of the Spider are not equal. Please make sure they are. The scale of the spider is defaulted to be the Y scale and a lot of values depend on this scale.");
        }

        rb = GetComponent<Rigidbody>();

        //Initialize the two Sphere Casts
        downRayRadius = downRaySize * getColliderRadius();
        float forwardRayRadius = forwardRaySize * getColliderRadius();
        downRay = new SphereCast(transform.position, -transform.up, downRayLength * getColliderLength(), downRayRadius, transform, transform);
        forwardRay = new SphereCast(transform.position, transform.forward, forwardRayLength * getColliderLength(), forwardRayRadius, transform, transform);

        //Initialize the bodyupLocal as the spiders transform.up parented to the body. Initialize the breathePivot as the body position parented to the spider
        bodyY = body.transform.InverseTransformDirection(transform.up);
        bodyZ = body.transform.InverseTransformDirection(transform.forward);
        breathePivot = transform.InverseTransformPoint(body.transform.position);
    }

    void FixedUpdate() {
        //** Ground Check **//
        grdInfo = GroundCheckSphere();

        //** Rotation to normal **// 
        Vector3 slerpNormal = Vector3.Slerp(transform.up, grdInfo.groundNormal, 0.02f * normalAdjustSpeed);
        Quaternion goalrotation = getLookRotation(transform.right, slerpNormal);

        // Save last Normal for access
        lastNormal = transform.up;

        //Apply the rotation to the spider
        transform.rotation = goalrotation;

        // Dont apply gravity if close enough to ground
        if (grdInfo.distanceToGround > getGravityOffDistance()) {
            rb.AddForce(-grdInfo.groundNormal * 9.81f); //Important using the groundnormal and not the lerping normal here!
        }
    }

    void Update() {
        //** Debug **//
        if (showDebug) drawDebug();

        Vector3 Y = body.TransformDirection(bodyY);
        Vector3 Z = body.TransformDirection(bodyZ);

        //Doesnt work the way i want it too! On sphere i go underground. I jiggle around when i go down my centroid moves down to.(Depends on errortolerance of IKSolver)
        if (activateLegCentroidAdjustment) {
            Vector3 centroid = getLegCentroid();
            Vector3 heightOffset = Vector3.Project((centroid + getColliderRadius() * Y) - body.transform.position, Y);
            body.transform.position += heightOffset * Mathf.Clamp(Time.deltaTime * (0.1f * normalAdjustSpeed * getScale()), 0f, 1f);
            // What if im underground?
        }

        //Doesnt work, gets a Twist.
        if (activateLegNormalAdjustment) {
            Vector3 newNormal = GetLegsPlaneNormal();

            float angleX = Vector3.SignedAngle(Vector3.ProjectOnPlane(Y, transform.right), Vector3.ProjectOnPlane(newNormal, transform.right), transform.right);
            body.transform.rotation = Quaternion.AngleAxis(angleX, transform.right) * body.transform.rotation;

            float angleZ = Vector3.SignedAngle(Y, Vector3.ProjectOnPlane(newNormal, Z), Z);
            body.transform.rotation = Quaternion.AngleAxis(angleZ, Z) * body.transform.rotation;
        }

        if (activateBreathing) {
            float t = (Time.time * 2 * Mathf.PI / period) % (2 * Mathf.PI);
            float amplitude = breatheMagnitude * getColliderRadius();
            Vector3 direction = body.TransformDirection(bodyY);

            body.transform.position = transform.TransformPoint(breathePivot) + amplitude * Mathf.Sin(t) * direction;
        }

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

    public void turn(Vector3 goalForward, float speed = 1f) {
        if (goalForward == Vector3.zero || speed == 0) return;
        goalForward = Vector3.ProjectOnPlane(goalForward, transform.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(goalForward, transform.up), turnSpeed * speed);
    }

    //Only call this in fixed frame!
    public void walk(Vector3 direction) {
        if (direction == Vector3.zero) {
            currentVelocity = Vector3.zero;
            isMoving = false;
            return;
        }
        isMoving = true;
        float magnitude = direction.magnitude;
        // Increase velocity as the direction and forward vector of spider get closer together
        float distance = Mathf.Pow(Mathf.Clamp(Vector3.Dot(direction, transform.forward), 0, 1), 4) * 0.0004f * walkSpeed * magnitude * getScale();
        //Make sure per frame we wont move more than our downsphereRay radius, or we might lose the floor.
        distance = Mathf.Clamp(distance, 0, 0.99f * downRayRadius);
        currentVelocity = distance * (direction / magnitude);
        transform.position += currentVelocity;
    }

    public bool getIsMoving() {
        return isMoving;
    }

    public Vector3 getCurrentVelocityPerSecond() {
        return currentVelocity / Time.fixedDeltaTime;
    }

    public Vector3 getCurrentVelocityPerFixedFrame() {
        return currentVelocity;
    }

    public Vector3 getLastNormal() {
        return lastNormal;
    }

    //** Ground Check Methods **//
    private groundInfo GroundCheckSphere() {
        if (forwardRay.castRay(out hitInfo, walkableLayer)) {
            return new groundInfo(true, hitInfo.normal.normalized, Vector3.Distance(transform.TransformPoint(col.center), hitInfo.point) - getColliderRadius());
        }

        if (downRay.castRay(out hitInfo, walkableLayer)) {
            return new groundInfo(true, hitInfo.normal.normalized, Vector3.Distance(transform.TransformPoint(col.center), hitInfo.point) - getColliderRadius());
        }

        return new groundInfo(false, Vector3.up, float.PositiveInfinity);
    }

    //** IK-Chains (Legs) Methods **//

    // Calculate the centroid (center of gravity) given by all end effector positions of the legs
    private Vector3 getLegCentroid() {
        if (legs == null) {
            Debug.LogError("Cant calculate leg centroid, legs not assigned.");
            return body.transform.position;
        }

        Vector3 position = Vector3.zero;
        float weigth = 1 / (float)legs.Length;

        // Go through all the legs, rotate the normal by it's offset
        for (int i = 0; i < legs.Length; i++) {
            position += legs[i].getEndEffector().position * weigth;
        }
        return position;
    }

    // Calculate the normal of the plane defined by leg positions, so we know how to rotate the body
    private Vector3 GetLegsPlaneNormal() {
        if (legs == null) {
            Debug.LogError("Cant calculate normal, legs not assigned.");
            return transform.up;
        }

        // float legRotWeigth = 1.0f;
        //if (legRotWeigth <= 0f) return transform.up; 
        //float legWeight = 1f / Mathf.Lerp(legs.Length,1f, legRotWeigth); // ???

        Vector3 normal = Vector3.zero;
        float legWeight = 1f / legs.Length;

        for (int i = 0; i < legs.Length; i++) {
            normal += legWeight * legs[i].getTarget().normal;
            //normal += legWeight * -legs[i].getEndEffector().transform.up; // The minus comes from the endeffectors local coordinate system being reversed
        }
        return Vector3.Slerp(transform.up, normal, legNormalWeight);
    }

    //** Get Methods **//
    public float getScale() {
        return transform.lossyScale.y;
    }

    public float getColliderRadius() {
        return getScale() * col.radius;
    }

    public float getNonScaledColliderRadius() {
        return col.radius;
    }

    public float getColliderLength() {
        return getScale() * col.height;
    }

    public Vector3 getColliderCenter() {
        return transform.TransformPoint(col.center);
    }

    public Vector3 getColliderBottomPoint() {
        return transform.TransformPoint(col.center - col.radius * new Vector3(0, 1, 0));
    }

    public float getGravityOffDistance() {
        return gravityOffDistance * getColliderRadius();
    }
    public Vector3 getGroundNormal() {
        return grdInfo.groundNormal;
    }

    //** Debug Methods **//
    private void drawDebug() {
        //Draw the two Sphere Rays
        downRay.draw(Color.green);
        forwardRay.draw(Color.blue);

        //Draw the Gravity off distance
        Vector3 borderpoint = getColliderBottomPoint();
        Debug.DrawLine(borderpoint, borderpoint + getGravityOffDistance() * -transform.up, Color.magenta);

        //Draw the current transform.up and the bodys current Y orientation
        Debug.DrawLine(transform.position, transform.position + 2f * getColliderRadius() * transform.up, new Color(1, 0.5f, 0, 1));
        Debug.DrawLine(transform.position, transform.position + 2f * getColliderRadius() * body.TransformDirection(bodyY), Color.blue);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected() {

        if (!showDebug) return;
        if (UnityEditor.EditorApplication.isPlaying) return;
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;

        Awake();
        drawDebug();
    }
#endif

}
