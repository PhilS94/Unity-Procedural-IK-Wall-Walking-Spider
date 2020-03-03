#if UNITY_STANDALONE
public static class GeneralSettings {

    static GeneralSettings() {
        UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.Locked;
    }

}
#endif