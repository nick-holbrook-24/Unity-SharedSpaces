using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class SceneSkyboxController : MonoBehaviour
{
    [Header("Static Skyboxes")]
    public Material lobbySkybox;
    public Material redRoomSkybox;
    public Material greenRoomSkybox;
    public Material blueRoomSkybox;

    [Header("Purple Room Dynamic")]
    public Material purpleDynamicSkybox; // assign your Procedural skybox here

    private Material runtimePurpleSkybox;
    private AmbientColorSync ambientSync;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        // find your ambient manager to subscribe later
        ambientSync = FindObjectOfType<AmbientColorSync>();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case "Lobby":
                RenderSettings.skybox = lobbySkybox;
                break;
            case "RedRoom":
                RenderSettings.skybox = redRoomSkybox;
                break;
            case "GreenRoom":
                RenderSettings.skybox = greenRoomSkybox;
                break;
            case "BlueRoom":
                RenderSettings.skybox = blueRoomSkybox;
                break;
            case "PurpleRoom":
                // Create an instance so we can tint it at runtime
                runtimePurpleSkybox = new Material(purpleDynamicSkybox);
                RenderSettings.skybox = runtimePurpleSkybox;

                if (ambientSync != null)
                {
                    // Subscribe to ambient color changes
                    ambientSync.AmbientColorChanged += OnAmbientColorChanged;
                    // Initialize tint immediately
                    OnAmbientColorChanged(Color.black, ambientSync.CurrentAmbientColor);
                }
                break;
            default:
                // fallback: clear to black
                RenderSettings.skybox = null;
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                break;
        }
        // Update the skybox immediately
        DynamicGI.UpdateEnvironment();
    }

    private void OnAmbientColorChanged(Color oldColor, Color newColor)
    {
        // Procedural skybox uses _SkyTint
        runtimePurpleSkybox.SetColor("_SkyTint", newColor);
        DynamicGI.UpdateEnvironment();
    }
}
