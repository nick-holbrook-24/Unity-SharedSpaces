using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject), typeof(SharedSpacesPlayerState))]
public class PlayerLightBrush : NetworkBehaviour
{
    [Header("Drawing Settings")]
    [Tooltip("Seconds between networked brush samples")]
    [SerializeField] private float pointInterval = 0.05f;
    [Tooltip("Brush wobble dry duration")]
    [SerializeField] private float dryDuration = 2f;
    [Tooltip("Wobble amplitude")]
    [SerializeField] private float wobbleAmplitude = 0.05f;
    [Tooltip("Wobble frequency")]
    [SerializeField] private float wobbleFrequency = 20f;

    // References
    private Transform leftAnchor;
    private Transform rightAnchor;
    private MaterialPropertyBlock mpb;
    private LineRenderer lineRenderer;
    private GameObject brushObject;
    private SharedSpacesPlayerState playerState;

    // Drawing state
    private readonly List<Vector3> points = new List<Vector3>();
    private float sampleTimer;

    private void Awake()
    {
        playerState = GetComponent<SharedSpacesPlayerState>();

        // Find the local OVRCameraRig anchors
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null)
        {
            leftAnchor = rig.leftHandAnchor;
            rightAnchor = rig.rightHandAnchor;
        }
        else
        {
            Debug.LogWarning("[PlayerLightBrush] No OVRCameraRig found. Brush will not track hands.");
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            // Wait until InitializeBrush is called
            enabled = false;
        }
    }

    /// <summary>
    /// Call this as soon as your manager spawns the brush prefab for us.
    /// </summary>
    public void InitializeBrush(GameObject brushGO)
    {
        brushObject = brushGO;
        lineRenderer = brushGO.GetComponent<LineRenderer>();

        points.Clear();
        sampleTimer = 0f;

        // Dry-wobble setup
        mpb = new MaterialPropertyBlock();
        float dryTime = Time.time + dryDuration;
        mpb.SetFloat("_DryTime", dryTime);
        mpb.SetFloat("_DryDuration", dryDuration);
        mpb.SetFloat("_Amplitude", wobbleAmplitude);
        mpb.SetFloat("_Frequency", wobbleFrequency);
        mpb.SetColor("_Color", playerState.color.Value);
        lineRenderer.SetPropertyBlock(mpb);

        enabled = true;
    }

    private void Update()
    {
        if (!IsOwner || brushObject == null)
            return;

        // Check grips
        bool leftGrip = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
        bool rightGrip = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
        if (!leftGrip && !rightGrip)
            return;

        // Throttle sampling
        sampleTimer += Time.deltaTime;
        if (sampleTimer < pointInterval)
            return;
        sampleTimer = 0f;

        // Determine position
        Vector3 pos;
        if (leftGrip && leftAnchor != null)
            pos = leftAnchor.position;
        else if (rightGrip && rightAnchor != null)
            pos = rightAnchor.position;
        else
            pos = brushObject.transform.position;

        // Move the brush tip
        brushObject.transform.position = pos;

        // Draw locally
        points.Add(pos);
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPosition(points.Count - 1, pos);

        // Tell host → other clients
        SendBrushPointServerRpc(pos);

        // Ripple
        RipplePaintAtPoint(pos);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendBrushPointServerRpc(Vector3 point)
    {
        BroadcastBrushPointClientRpc(point);
    }

    [ClientRpc]
    private void BroadcastBrushPointClientRpc(Vector3 point)
    {
        if (IsOwner) return;
        points.Add(point);
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPosition(points.Count - 1, point);
    }

    /// <summary>
    /// Tear down when needed (e.g. on disconnect).
    /// </summary>
    public void ClearBrush()
    {
        if (brushObject != null)
        {
            var nob = brushObject.GetComponent<NetworkObject>();
            if (IsServer && nob != null && nob.IsSpawned)
                nob.Despawn(true);
            brushObject = null;
        }
        points.Clear();
        enabled = false;
    }

    private void RipplePaintAtPoint(Vector3 point)
    {
        var hits = Physics.OverlapSphere(point, 0.05f, LayerMask.GetMask("PaintableSurface"));
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<SharedSpacesColorableObject>(out var obj))
                obj.PaintAndRippleServerRpc(playerState.color.Value, point);
        }
    }
}
