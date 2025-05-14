using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject), typeof(Collider))]
public class SharedSpacesColorableObject : NetworkBehaviour
{
    [Tooltip("The Renderer whose material color you want to change.")]
    [SerializeField] private Renderer targetRenderer;

    [Tooltip("Duration of the ripple effect in seconds.")]
    [SerializeField] private float rippleDuration = 1f;

    // Server-authoritative color state, replicated to all clients
    private NetworkVariable<Color> objectColor = new NetworkVariable<Color>(
        Color.white,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private MaterialPropertyBlock block;
    private bool isRippling;
    private float rippleStartTime;

    private void Awake()
    {
        // Auto-assign renderer if none set
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }
        block = new MaterialPropertyBlock();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Initialize material to the current networked color
        block.SetColor("_Color", objectColor.Value);
        targetRenderer.SetPropertyBlock(block);
        // Listen for any future color updates
        objectColor.OnValueChanged += OnColorChanged;
    }

    private void OnColorChanged(Color oldColor, Color newColor)
    {
        // Update the material via the property block
        block.SetColor("_Color", newColor);
        targetRenderer.SetPropertyBlock(block);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only react if this client owns the colliding player object
        if (!other.TryGetComponent<NetworkObject>(out var netObj) || !netObj.IsOwner)
            return;

        var state = other.GetComponent<SharedSpacesPlayerState>();
        if (state == null)
            return;

        // Compute ripple center and request server to update color + ripple
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        PaintAndRippleServerRpc(state.color.Value, hitPoint);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PaintAndRippleServerRpc(Color newColor, Vector3 rippleCenter)
    {
        // Authoritatively set the new color (sync to clients)
        objectColor.Value = newColor;
        // Tell all clients to play a ripple at the given world position
        RippleClientRpc(rippleCenter);
    }

    [ClientRpc]
    private void RippleClientRpc(Vector3 rippleCenter)
    {
        // Begin the ripple effect locally
        isRippling = true;
        rippleStartTime = Time.time;
        block.SetVector("_RippleCenter", rippleCenter);
        targetRenderer.SetPropertyBlock(block);
    }

    private void Update()
    {
        if (!isRippling)
            return;

        float elapsed = Time.time - rippleStartTime;
        block.SetFloat("_RippleTime", elapsed);
        targetRenderer.SetPropertyBlock(block);

        if (elapsed > rippleDuration)
        {
            isRippling = false;
        }
    }

    /// <summary>
    /// Allows external systems (e.g. ambient manager) to subscribe
    /// to this object’s color‐change events on the server.
    /// </summary>
    public void SubscribeColorChange(NetworkVariable<Color>.OnValueChangedDelegate callback)
    {
        if (IsServer)
        {
            objectColor.OnValueChanged += callback;
        }
    }
}
