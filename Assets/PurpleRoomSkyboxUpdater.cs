using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AmbientColorSync))]
public class PurpleRoomSkyboxUpdater : MonoBehaviour
{
    [Tooltip("Assign your Procedural (Skybox/Procedural) material for the Purple Room here")]
    [SerializeField] private Material purpleDynamicSkybox;

    private Material runtimeSkybox;
    private AmbientColorSync ambientSync;

    private void Awake()
    {
        // We rely on AmbientColorSync being on the same GameObject (or find it)
        ambientSync = GetComponent<AmbientColorSync>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unsubscribe();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "PurpleRoom")
        {
            // Clone so we can tint at runtime
            runtimeSkybox = new Material(purpleDynamicSkybox);
            RenderSettings.skybox = runtimeSkybox;

            // Hook up to ambient updates
            ambientSync.AmbientColorChanged += OnAmbientColorChanged;
            // Kick off with current color
            OnAmbientColorChanged(Color.black, ambientSync.CurrentAmbientColor);
        }
        else if (runtimeSkybox != null)
        {
            // Leaving PurpleRoom: stop listening
            Unsubscribe();
        }
    }

    private void OnAmbientColorChanged(Color oldColor, Color newColor)
    {
        if (runtimeSkybox == null) return;
        runtimeSkybox.SetColor("_SkyTint", newColor);
        // Optionally tint the ground too:
        runtimeSkybox.SetColor("_GroundColor", newColor * 0.3f);
        DynamicGI.UpdateEnvironment();
    }

    private void Unsubscribe()
    {
        if (ambientSync != null)
            ambientSync.AmbientColorChanged -= OnAmbientColorChanged;
        runtimeSkybox = null;
    }
}
