using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RandomWalker {

    public static Vector3 getMovement(Vector3 forward, Vector3 right, Vector3 normal) {
        float perlinStep = 0.2f;
        float vertical = 2.0f * (Mathf.PerlinNoise(Time.time * perlinStep, 0.0f) - 0.5f);
        float horizontal = 2.0f * (Mathf.PerlinNoise(Time.time * perlinStep, 0.5f) - 0.5f);

        return (Vector3.ProjectOnPlane(forward, normal) * vertical + (Vector3.ProjectOnPlane(right, normal).normalized * horizontal)).normalized;
    }
}