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
    [Range(0.001f, 1)]
    public float walkDrag;
    [Range(1, 10)]
    public float groundNormalAdjustSpeed;
    [Range(1, 10)]
    public float forwardNormalAdjustSpeed;
    public LayerMask walkableLayer;
    [Range(0, 1)]
    public float gravityOffDistance;

    [Header("IK Legs")]
    public Transform body;
    public IKChain[] legs;

    [Header("Body Offset Height")]
    public float bodyOffsetHeight;

    [Header("Leg Centroid")]
    public bool legCentroidAdjustment;
    [Range(0, 10)]
    public float legCentroidSpeed;
    [Range(0, 1)]
    public float legCentroidNormalWeight;
    [Range(0, 1)]
    public float legCentroidTangentWeight;

    [Header("Leg Normal")]
    public bool legNormalAdjustment;
    [Range(0, 10)]
    public float legNormalSpeed;
    [Range(0, 1)]
    public float legNormalWeight;

    private Vector3 bodyY;
    private Vector3 bodyZ;

    [Header("Breathing")]
    public bool breathing;
    [Range(0.01f, 20)]
    public float breathePeriod;
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
    private bool isWalking = false;
    private bool isTurning = false;
    private bool isFalling = false;
    private bool groundCheckOn = true;

    private Vector3 lastNormal;
    private Vector3 bodyDefaultCentroid;
    private Vector3 bodyCentroid;

    private SphereCast downRay, forwardRay;
    private RaycastHit hitInfo;

    private enum RayType {None, ForwardRay, DownRay};
    private struct groundInfo {
        public bool isGrounded;
        public Vector3 groundNormal;
        public float distanceToGround;
        public RayType rayType;

        public groundInfo(bool isGrd, Vector3 normal, float dist, RayType m_rayType) {
            isGrounded = isGrd;
            groundNormal = normal;
            distanceToGround = dist;
            rayType = m_rayType;
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
        bodyCentroid = body.transform.position + getScale() * bodyOffsetHeight * transform.up;
        bodyDefaultCentroid = transform.InverseTransformPoint(bodyCentroid);
    }

    void FixedUpdate() {
        //** Ground Check **//
        grdInfo = GroundCheckSphere();

        //** Rotation to normal **// 
        float normalAdjustSpeed = (grdInfo.rayType == RayType.ForwardRay)? forwardNormalAdjustSpeed :groundNormalAdjustSpeed;

        Vector3 slerpNormal = Vector3.Slerp(transform.up, grdInfo.groundNormal, 0.02f * normalAdjustSpeed);
        Quaternion goalrotation = getLookRotation(Vector3.ProjectOnPlane(transform.right, slerpNormal), slerpNormal);

        // Save last Normal for access
        lastNormal = transform.up;

        //Apply the rotation to the spider
        transform.rotation = goalrotation;

        // Dont apply gravity if close enough to ground
        if (grdInfo.distanceToGround > getGravityOffDistance()) {
            isFalling = true;
            rb.AddForce(-grdInfo.groundNormal * 0.0981f * getScale()); //Important using the groundnormal and not the lerping normal here!
        }
        else isFalling = false;
    }

    /*
    * Returns the rotation with specified right and up direction   
    * May have to make more error catches here. Whatif not orthogonal?
    */
    public Quaternion getLookRotation(Vector3 right, Vector3 up) {
        if (up == Vector3.zero || right == Vector3.zero) return Quaternion.identity;
        // If vectors are parallel return identity
        float angle = Vector3.Angle(right, up);
        if (angle == 0 || angle == 180) return Quaternion.identity;
        Vector3 forward = Vector3.Cross(right, up);
        return Quaternion.LookRotation(forward, up);
    }

    void Update() {
        //** Debug **//
        if (showDebug) drawDebug();

        Vector3 Y = body.TransformDirection(bodyY);
        bodyCentroid = getDefaultCentroid();

        //Doesnt work the way i want it too! On sphere i go underground. I jiggle around when i go down my centroid moves down to.(Depends on errortolerance of IKSolver)
        if (legCentroidAdjustment) {
            bodyCentroid = Vector3.Lerp(bodyCentroid, getLegCentroid(), Time.deltaTime * legCentroidSpeed);
            body.transform.position = bodyCentroid;
        }

        if (legNormalAdjustment) {
            Vector3 newNormal = GetLegsPlaneNormal();

            //Use Global X for  pitch
            Vector3 X = transform.right;
            float angleX = Vector3.SignedAngle(Vector3.ProjectOnPlane(Y, X), Vector3.ProjectOnPlane(newNormal, X), X);
            angleX = Mathf.LerpAngle(0, angleX, Time.deltaTime * legNormalSpeed);
            body.transform.rotation = Quaternion.AngleAxis(angleX, X) * body.transform.rotation;

            //Use Local Z for roll. With the above global X for pitch, this avoids any kind of yaw happening.
            Vector3 Z = body.TransformDirection(bodyZ);
            float angleZ = Vector3.SignedAngle(Y, Vector3.ProjectOnPlane(newNormal, Z), Z);
            angleZ = Mathf.LerpAngle(0, angleZ, Time.deltaTime * legNormalSpeed);
            body.transform.rotation = Quaternion.AngleAxis(angleZ, Z) * body.transform.rotation;
        }

        if (breathing) {
            float t = (Time.time * 2 * Mathf.PI / breathePeriod) % (2 * Mathf.PI);
            float amplitude = breatheMagnitude * getColliderRadius();
            Vector3 direction = body.TransformDirection(bodyY);

            body.transform.position = bodyCentroid + amplitude * (Mathf.Sin(t) + 1f) * direction;
        }

    }

    public void turn(Vector3 goalForward, float speed = 1f) {
        //Make sure goalForward is orthogonal to transform up
        goalForward = Vector3.ProjectOnPlane(goalForward, transform.up);
        if (goalForward == Vector3.zero || speed == 0) {
            isTurning = false;
            return;
        }
        isTurning = true;
        goalForward = Vector3.ProjectOnPlane(goalForward, transform.up);

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(goalForward, transform.up), turnSpeed * speed);
    }

    //Only call this in fixed frame!
    public void walk(Vector3 direction) {
        // TODO: Make sure direction is on the XZ plane of spider! For this maybe refactor the logic from input from spidercontroller to this function.
        if (direction == Vector3.zero) isWalking = false;
        else isWalking = true;

        // Scale the magnitude and Clamp to not move more than down ray radius (Makes sure the ground is not lost due to moving too fast)
        if (direction != Vector3.zero) {
            float magnitude = direction.magnitude;
            float directionDamp = Mathf.Pow(Mathf.Clamp(Vector3.Dot(direction / magnitude, transform.forward), 0, 1), 2);
            float distance = 0.0004f * walkSpeed * magnitude * directionDamp * getScale();
            distance = Mathf.Clamp(distance, 0, 0.99f * downRayRadius);
            direction = distance * (direction / magnitude);
        }

        //Slerp from old to new velocity using the acceleration
        currentVelocity = Vector3.Slerp(currentVelocity, direction, 1f - walkDrag);

        //Apply the resulting velocity
        transform.position += currentVelocity;
    }

    public bool getIsMoving() {
        return isWalking || isTurning || isFalling;
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
        if (groundCheckOn) {
            if (forwardRay.castRay(out hitInfo, walkableLayer)) {
                return new groundInfo(true, hitInfo.normal.normalized, Vector3.Distance(transform.TransformPoint(col.center), hitInfo.point) - getColliderRadius(), RayType.ForwardRay);
            }

            if (downRay.castRay(out hitInfo, walkableLayer)) {
                return new groundInfo(true, hitInfo.normal.normalized, Vector3.Distance(transform.TransformPoint(col.center), hitInfo.point) - getColliderRadius(), RayType.DownRay);
            }
        }
        return new groundInfo(false, Vector3.up, float.PositiveInfinity,RayType.None);
    }

    //** IK-Chains (Legs) Methods **//

    // Calculate the centroid (center of gravity) given by all end effector positions of the legs
    private Vector3 getLegCentroid() {
        if (legs == null) {
            Debug.LogError("Cant calculate leg centroid, legs not assigned.");
            return body.transform.position;
        }

        Vector3 defaultCentroid = getDefaultCentroid();
        Vector3 defaultOffset = Vector3.zero;
        float k = 0;

        //Calculate centroid from only grounded legs. Careful since this can lead to one sided centroid
        for (int i = 0; i < legs.Length; i++) {
            //if (!legs[i].getTarget().grounded) continue;

            defaultOffset += legs[i].getEndEffector().position - defaultCentroid;
            k++;
        }
        defaultOffset = defaultOffset / k;

        Vector3 normalOffset = Vector3.Project(defaultOffset, transform.up);
        Vector3 tangentialOffset = Vector3.ProjectOnPlane(defaultOffset, transform.up);

        return defaultCentroid + Vector3.Lerp(Vector3.zero, normalOffset, legCentroidNormalWeight) + Vector3.Lerp(Vector3.zero, tangentialOffset, legCentroidTangentWeight);
    }

    // Calculate the normal of the plane defined by leg positions, so we know how to rotate the body
    private Vector3 GetLegsPlaneNormal() {

        if (legs == null) {
            Debug.LogError("Cant calculate normal, legs not assigned.");
            return transform.up;
        }

        if (legNormalWeight <= 0f) return transform.up;

        Vector3 newNormal = transform.up;
        Vector3 toEnd;
        Vector3 currentTangent;

        for (int i = 0; i < legs.Length; i++) {
            //normal += legWeight * legs[i].getTarget().normal;
            toEnd = legs[i].getEndEffector().position - transform.position;
            currentTangent = Vector3.ProjectOnPlane(toEnd, transform.up);

            if (currentTangent == Vector3.zero) continue; // Actually here we would have a 90degree rotation but there is no choice of a tangent.

            newNormal = Quaternion.Lerp(Quaternion.identity, Quaternion.FromToRotation(currentTangent, toEnd), legNormalWeight) * newNormal;
        }
        return newNormal;
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

    public Vector3 getDefaultCentroid() {
        return transform.TransformPoint(bodyDefaultCentroid);
    }

    public float getGravityOffDistance() {
        return gravityOffDistance * getColliderRadius();
    }
    public Vector3 getGroundNormal() {
        return grdInfo.groundNormal;
    }

    public void setGroundcheck(bool b) {
        groundCheckOn = b;
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
