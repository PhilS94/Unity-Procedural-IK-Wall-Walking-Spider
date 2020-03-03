using UnityEngine;

public class GeneralSettings : MonoBehaviour
{
#if UNITY_STANDALONE
    private void Awake() {
        Cursor.lockState = CursorLockMode.Locked;
    }
#endif
}
