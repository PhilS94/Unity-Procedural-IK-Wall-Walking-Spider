/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using UnityEngine;
using UnityEngine.SceneManagement;

/* Simple class for some general game settings. */
public class GeneralSettings : MonoBehaviour {
    private void Awake() {
        // Lock Cursor in Build
        Cursor.lockState = CursorLockMode.Locked;

        //Unlock Cursor in Editor
#if UNITY_EDITOR
        Cursor.lockState = CursorLockMode.None;
#endif
    }

    private void Update() {
        //On Press reset scene
        if (Input.GetKeyDown(KeyCode.Escape)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}