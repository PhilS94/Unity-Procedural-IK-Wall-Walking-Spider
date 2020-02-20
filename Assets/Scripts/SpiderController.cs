using UnityEngine;
using System.Collections;
using Raycasting;

public class SpiderController : MonoBehaviour {

    public Spider spider;

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

    private RayCast camToPlayer, playerToCam;
    float maxCameraDistance;
    private RaycastHit hitInfo;
    private RaycastHit[] camObstructions;
    
    private struct ShaderInfo {
        public Shader shader;
        public Color color;
        
        public ShaderInfo(Shader m_Shader, Color m_Color)
        {
            shader = m_Shader;
            color = m_Color;
        }
    }

    private ShaderInfo[] camObstructionsShaders;

    void Start() {
        maxCameraDistance = Vector3.Distance(spider.transform.position, cam.transform.position);
        playerToCam = new RayCast(spider.transform.position, cam.transform.position, spider.transform, cam.transform);
        camToPlayer = new RayCast(cam.transform.position, spider.transform.position, cam.transform, spider.transform);
    }

    private void FixedUpdate() {
        //** Movement **//
        Vector3 input = getInput();
        spider.walk(input, Time.fixedDeltaTime);

        Quaternion tempCamRotation = cam.transform.rotation;
        Vector3 tempCamPosition = cam.transform.position;
        spider.turn(input, Time.fixedDeltaTime);
        cam.transform.rotation = tempCamRotation;
        cam.transform.position = tempCamPosition;

        // Since the spider might have adjusted its normal, rotate halfway back with the camera here (More smooth experience instead of camera freezing in place with every normal adjustment)
        rotateCameraBack(0.5f);
    }

    void Update() {


        //** Camera movement **//
        RotateCameraHorizontal(Input.GetAxis("Mouse X") * XSensitivity);
        RotateCameraVertical(-Input.GetAxis("Mouse Y") * YSensitivity);
        //clipCamera();
        clipCameraInvisible();

        if (spider.showDebug) drawDebug();
    }

    //I feel like heres a bug since the spider when climbing walls sways to the right or left slighlty
    private Vector3 getInput() {
        Vector3 input = (Vector3.ProjectOnPlane(cam.transform.forward, spider.transform.up) * Input.GetAxis("Vertical") + (Vector3.ProjectOnPlane(cam.transform.right, spider.transform.up) * Input.GetAxis("Horizontal"))).normalized;
        Quaternion fromTo = spider.getLookRotation(spider.transform.right, spider.getGroundNormal()) * Quaternion.Inverse(spider.transform.rotation);
        return fromTo * input;
    }

    //** Camera Methods **//
    void RotateCameraHorizontal(float angle) {
        cam.transform.RotateAround(spider.transform.position, spider.transform.up, angle);
    }

    void RotateCameraVertical(float angle) {

        angle = angle % 360;
        if (angle == -180) angle = 180;
        if (angle > 180) angle -= 360;
        if (angle < -180) angle += 360;
        //Now angle is of the form (-180,180]

        float currentAngle = Vector3.SignedAngle(spider.transform.up, spider.transform.position - cam.transform.position, cam.transform.right); //Should always be positive
        if (currentAngle + angle < camLowerAngleMargin) {
            angle = camLowerAngleMargin - currentAngle;
        }
        if (currentAngle + angle > 180.0f - camUpperAngleMargin) {
            angle = 180.0f - camUpperAngleMargin - currentAngle;
        }
        cam.transform.RotateAround(spider.transform.position, cam.transform.right, angle);
    }


    /*
     * The Spider adjusts its normal to its surroundings every frame. Since the cameras transform is a child of the spiders transform,
     * the camera will completely follow every rotation of the spider.
     * This methods rotates the camera back to its original rotation before the normal adjustment by t,
     * where t=0 is no rotation applied (that is as if this method was never called)
     * and t=1 means rotating the camera completely back to its original rotation
     */
    void rotateCameraBack(float t) {
        Vector3 n = spider.getLastNormal();
        if (n == Vector3.zero) return;
        float angle = Vector3.SignedAngle(spider.getLastNormal(), spider.transform.up, cam.transform.right);
        RotateCameraVertical(Mathf.Clamp(t, 0, 1) * -angle);
    }

    void clipCamera() {
        float margin = 0.05f;

        playerToCam.setDistance(maxCameraDistance);
        if (playerToCam.castRay(out hitInfo, cameraClipLayer)) {
            cam.transform.position = hitInfo.point - margin * playerToCam.getDirection().normalized;
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

                    mesh.material.shader = camObstructionsShaders[k].shader;
                    mesh.material.color = camObstructionsShaders[k].color;
                    //mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
        }

        // Now transparent all new obstructions
        camObstructions = camToPlayer.castRayAll(cameraInvisibleClipLayer);
        camObstructionsShaders = new ShaderInfo[camObstructions.Length];
        for (int k = 0; k < camObstructions.Length; k++) {
            MeshRenderer mesh = camObstructions[k].transform.GetComponent<MeshRenderer>();
            if (mesh != null) {
                camObstructionsShaders[k] = new ShaderInfo(mesh.material.shader, mesh.material.color);
                mesh.material.shader = Shader.Find("Transparent/Diffuse");
                Color tempColor = mesh.material.color;
                tempColor.a = 0.8F;
                mesh.material.color = tempColor;
                //mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }
    }


    //** Debug Methods **//
    private void drawDebug() {
        camToPlayer.draw(Color.white);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected() {
        if (UnityEditor.EditorApplication.isPlaying) return;
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;
        if (spider == null || !spider.showDebug) return;

        Start();
        drawDebug();
    }
#endif
}