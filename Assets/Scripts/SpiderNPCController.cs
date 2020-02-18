using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiderNPCController : MonoBehaviour {
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

    // Update is called once per frame
    void Update() {


    }

    private void FixedUpdate() {
        Vector3 input = getDirection();
        float speed = getSpeed(0, spider.walkSpeed);
        spider.walk(input, speed * Time.fixedDeltaTime);
        spider.turn(input, speed * Time.fixedDeltaTime);

        //Debug.DrawLine(spider.transform.position, spider.transform.position + input * 5.0f, Color.cyan);
    }

    private Vector3 getDirection() {
        float vertical = 2.0f * (Mathf.PerlinNoise(Time.time * perlinStepDirection, startValue) - 0.5f); // Range [-1,1]
        float horizontal = 2.0f * (Mathf.PerlinNoise(Time.time * perlinStepDirection, startValue + 0.3f) - 0.5f); // Range [-1,1]
        return (Quaternion.FromToRotation(Y, spider.getGroundNormal()) * (X * horizontal + Z * vertical)).normalized;
    }

    private float getSpeed(float min, float max) {
        return min + Mathf.PerlinNoise(Time.time * perlinStepSpeed, startValue + 0.6f) * (max - min);// Range [min,max]
    }
}
