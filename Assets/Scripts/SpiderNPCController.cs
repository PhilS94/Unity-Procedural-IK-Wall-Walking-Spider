/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * This class needs a reference to the Spider class and calls the walk and turn functions depending on a random perlin generated input.
 * In essence, this class translates random input to spider movement.
 * 
 * Perlin Noise is used to generate a random yet smooth direction.
 * A random binarized speed parameter is used to mimic the "stop and go" nature of spiders.
 */

[DefaultExecutionOrder(-1)] // Make sure the input movement is applied before the spider itself will do a ground check and possibly add gravity
public class SpiderNPCController : MonoBehaviour {


    [Header("Debug")]
    public bool showDebug;

    [Header("Spider Reference")]
    public Spider spider;

    private float perlinDirectionStep = 0.07f;
    private float perlinSpeedStep = 0.5f;
    private float startValue;

    private Vector3 Z;
    private Vector3 X;
    private Vector3 Y;

    private void Awake() {
        Random.InitState(System.DateTime.Now.Millisecond);
        startValue = Random.value;

        //Initialize Coordinate System
        Z = transform.forward;
        X = transform.right;
        Y = transform.up;
    }

    private void FixedUpdate() {
        updateCoordinateSystem();

        Vector3 input = getRandomDirection() * getRandomBinaryValue(0, 1, 0.4f);
        spider.walk(input);
        spider.turn(input);

        if (showDebug) Debug.DrawLine(spider.transform.position, spider.transform.position + input * 0.1f *spider.getScale(), Color.cyan,Time.fixedDeltaTime);
    }

    private void Update() {
        if (showDebug) {
            Debug.DrawLine(spider.transform.position, spider.transform.position + X * 0.1f * spider.getScale(), Color.red);
            Debug.DrawLine(spider.transform.position, spider.transform.position + Z * 0.1f * spider.getScale(), Color.blue);
        }
    }
    private void updateCoordinateSystem() {
        Vector3 newY = spider.getGroundNormal();
        Quaternion fromTo = Quaternion.FromToRotation(Y, newY);
        X = Vector3.ProjectOnPlane(fromTo * X, newY).normalized;
        Z = Vector3.ProjectOnPlane(fromTo * Z, newY).normalized;
        Y = newY;
    }

    private Vector3 getRandomDirection() {
        //Get random values between [-1,1] using perlin noise
        float vertical = 2.0f * (Mathf.PerlinNoise(Time.time * perlinDirectionStep, startValue) - 0.5f);
        float horizontal = 2.0f * (Mathf.PerlinNoise(Time.time * perlinDirectionStep, startValue + 0.3f) - 0.5f);
        return (X * horizontal + Z * vertical).normalized;
    }

    // Threshold is between 0 and 1 and applies a threshold filter to the perlin noise. Min is the lower value and Max the higher value.
    private float getRandomBinaryValue(float min, float max, float threshold) {
        float value = Mathf.PerlinNoise(Time.time * perlinSpeedStep, startValue + 0.6f);
        if (value >= threshold) value = 1;
        else value = 0;
        return min + value * (max - min);// Range [min,max]
    }
}
