using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiderNPCController : MonoBehaviour {


    [Header("Debug")]
    public bool showDebug;

    [Header("Spider Reference")]
    public Spider spider;

    private float perlinStepDirection = 0.07f;
    private float perlinStepSpeed = 0.2f;
    private float startValue;

    private Vector3 Z;
    private Vector3 X;
    private Vector3 Y;

    void Start() {
        Random.InitState(System.DateTime.Now.Millisecond);
        startValue = Random.value;

        Z = transform.forward;
        X = transform.right;
        Y = transform.up;
    }

    private void FixedUpdate() {
        Vector3 input = getDirection();
        float speed = getSpeed(0, 1);
        spider.walk(input, speed * Time.fixedDeltaTime);
        spider.turn(input, speed * Time.fixedDeltaTime);

        //Debug.DrawLine(spider.transform.position, spider.transform.position + input * 5.0f, Color.cyan);
    }

    private void Update() {
        if (showDebug) {
            Debug.DrawLine(spider.transform.position, spider.transform.position + getVectorInThisCoordinateSystem(X) * 0.5f * spider.getScale(), Color.red);
            Debug.DrawLine(spider.transform.position, spider.transform.position + getVectorInThisCoordinateSystem(Z) * 0.5f * spider.getScale(), Color.blue);
        }
    }
    private Vector3 getDirection() {

        //Get random values between [-1,1] using perlin noise
        float vertical = 2.0f * (Mathf.PerlinNoise(Time.time * perlinStepDirection, startValue) - 0.5f);
        float horizontal = 2.0f * (Mathf.PerlinNoise(Time.time * perlinStepDirection, startValue + 0.3f) - 0.5f);
        return (getVectorInThisCoordinateSystem((X * horizontal + Z * vertical).normalized));
    }

    private float getSpeed(float min, float max) {
        return min + Mathf.PerlinNoise(Time.time * perlinStepSpeed, startValue + 0.6f) * (max - min);// Range [min,max]
    }

    private Vector3 getVectorInThisCoordinateSystem(Vector3 v) {
        return Quaternion.FromToRotation(Y, spider.getGroundNormal()) * v;
    }
}
