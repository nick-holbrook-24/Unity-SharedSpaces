using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AmbientColorSync))]
public class PurpleRoomSkyboxUpdater : MonoBehaviour
{
    [Tooltip("Assign your Procedural (Skybox/Procedural) material for the Purple Room here")]
    [SerializeField] private Material purpleDynamicSkybox;

    private AmbientColorSync ambientSync;

    private void Awake()
    {
        ambientSync = GetComponent<AmbientColorSync>();
    }

    private void OnEnable()
    {
        StartCoroutine(WaitForSkybox());
    }

    private IEnumerator WaitForSkybox()
    {
        yield return new WaitForSeconds(1f);

        RenderSettings.skybox = purpleDynamicSkybox;

        ambientSync.AmbientColorChanged += OnAmbientColorChanged;

        OnAmbientColorChanged(Color.black, ambientSync.CurrentAmbientColor);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnAmbientColorChanged(Color oldColor, Color newColor)
    {
        purpleDynamicSkybox.SetColor("_SkyTint", newColor);
        purpleDynamicSkybox.SetColor("_GroundColor", newColor * 0.64f);
        DynamicGI.UpdateEnvironment();
    }

    private void Unsubscribe()
    {
        if (ambientSync != null)
            ambientSync.AmbientColorChanged -= OnAmbientColorChanged;
    }
}
