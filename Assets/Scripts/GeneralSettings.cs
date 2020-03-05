using UnityEngine;

public class GeneralSettings : MonoBehaviour {
    private void Awake() {
        Cursor.lockState = CursorLockMode.Locked;
#if UNITY_EDITOR
        Cursor.lockState = CursorLockMode.None;
#endif

    }
}
