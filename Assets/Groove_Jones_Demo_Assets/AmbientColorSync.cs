using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;

public class AmbientColorSync : NetworkBehaviour
{
    [Tooltip("How fast to nudge the ambient color toward each update")]
    [Range(0.01f, 1f)] public float lerpFactor = 0.1f;

    public delegate void AmbientColorChangeHandler(Color oldCol, Color newCol);
    public event AmbientColorChangeHandler AmbientColorChanged;

    // Expose current for the skybox controller
    public Color CurrentAmbientColor => ambientColor.Value;

    private NetworkVariable<Color> ambientColor = new NetworkVariable<Color>(
        Color.gray,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Light sunLight;

    private void Awake()
    {
        // Tag your directional in scene "SunLight"
        sunLight = GameObject.FindWithTag("SunLight")?.GetComponent<Light>()
                   ?? RenderSettings.sun;
    }

    public override void OnNetworkSpawn()
    {
        // Always track the variable, but only apply locally in PurpleRoom
        ambientColor.OnValueChanged += OnAmbientChanged;
        // Apply initial only if already in PurpleRoom
        if (SceneManager.GetActiveScene().name == "PurpleRoom")
            OnAmbientChanged(Color.black, ambientColor.Value);

        if (IsServer)
        {
            foreach (var obj in FindObjectsOfType<SharedSpacesColorableObject>())
            {
                obj.SubscribeColorChange(OnObjectPainted);
            }
        }

        // Re-apply when you cross scenes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ambientColor.OnValueChanged -= OnAmbientChanged;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // When entering PurpleRoom, immediately apply current ambient
        if (scene.name == "PurpleRoom")
            OnAmbientChanged(Color.black, ambientColor.Value);
    }

    private void OnObjectPainted(Color oldCol, Color newCol)
    {
        // Only the server drives the network variable
        ambientColor.Value = Color.Lerp(ambientColor.Value, newCol, lerpFactor);
    }

    private void OnAmbientChanged(Color oldC, Color newC)
    {
        // Only update RenderSettings & fire the event if in the PurpleRoom
        if (SceneManager.GetActiveScene().name != "PurpleRoom")
            return;

        AmbientColorChanged?.Invoke(oldC, newC);
        StopAllCoroutines();
        StartCoroutine(LerpAmbient(oldC, newC, 1f));
    }

    private IEnumerator LerpAmbient(Color from, Color to, float duration)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            var c = Color.Lerp(from, to, t / duration);
            RenderSettings.ambientLight = c;
            if (sunLight) sunLight.color = c;
            yield return null;
        }
    }
}
