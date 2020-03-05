using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiderNPCController : MonoBehaviour {


    [Header("Debug")]
    public bool showDebug;

    [Header("Spider Reference")]
    public Spider spider;

    private float perlinDirectionStep = 0.07f;
    private float perlinSpeedStep = 0.01f;
    private float startValue;

    private Vector3 Z;
    private Vector3 X;
    private Vector3 Y;

    private void Awake() {
        Random.InitState(System.DateTime.Now.Millisecond);
        startValue = Random.value;

        Z = transform.forward;
        X = transform.right;
        Y = transform.up;
    }

    private void FixedUpdate() {

        Vector3 input = getDirection() * getSpeed(0, 1, 0.3f);
        spider.walk(input);
        spider.turn(input);

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
        float vertical = 2.0f * (Mathf.PerlinNoise(Time.time * perlinDirectionStep, startValue) - 0.5f);
        float horizontal = 2.0f * (Mathf.PerlinNoise(Time.time * perlinDirectionStep, startValue + 0.3f) - 0.5f);
        return (getVectorInThisCoordinateSystem((X * horizontal + Z * vertical).normalized));
    }

    // Threshold is between 0 and 1 and applies a threshold filter to the perlin noise. Min is the lower value and Max the higher value.
    private float getSpeed(float min, float max, float threshold) {
        float value = Mathf.PerlinNoise(Time.time * perlinSpeedStep, startValue + 0.6f);
        if (value >= threshold) value = 1;
        else value = 0;
        return min + value * (max - min);// Range [min,max]
    }

    //Still buggy on ceilings e.g.
    private Vector3 getVectorInThisCoordinateSystem(Vector3 v) {
        return Quaternion.FromToRotation(Y, spider.getGroundNormal()) * v;
    }
}
