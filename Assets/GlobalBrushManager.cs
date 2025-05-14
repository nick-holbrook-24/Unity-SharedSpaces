using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GlobalBrushManager : NetworkBehaviour
{
    [Tooltip("Drag in your LightBrush NetworkObject prefab here (with PlayerLightBrush)")]
    [SerializeField] private NetworkObject lightBrushPrefab;

    // Keep track of brushes so we can clean up
    private List<NetworkObject> _spawnedBrushes = new List<NetworkObject>();

    private void Awake()
    {
        // Persist through scene loads
        DontDestroyOnLoad(this.gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // 1) Spawn for everyone already connected
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            SpawnBrushForClient(client.ClientId);

        // 2) Spawn for any newcomers
        NetworkManager.Singleton.OnClientConnectedCallback += SpawnBrushForClient;

        // 3) Clean up on disconnect
        NetworkManager.Singleton.OnClientDisconnectCallback += RemoveBrushForClient;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= SpawnBrushForClient;
        NetworkManager.Singleton.OnClientDisconnectCallback -= RemoveBrushForClient;
    }

    private void SpawnBrushForClient(ulong clientId)
    {
        // instantiate on server, owned by that client
        var brush = Instantiate(lightBrushPrefab);
        brush.SpawnWithOwnership(clientId);
        _spawnedBrushes.Add(brush);

        // tell that client to initialize
        AssignBrushClientRpc(brush.NetworkObjectId, clientId);
    }

    private void RemoveBrushForClient(ulong clientId)
    {
        // despawn any brush owned by clientId
        for (int i = _spawnedBrushes.Count - 1; i >= 0; --i)
        {
            if (_spawnedBrushes[i].OwnerClientId == clientId)
            {
                _spawnedBrushes[i].Despawn(true);
                _spawnedBrushes.RemoveAt(i);
            }
        }
        // clean up on client side
        ClearBrushClientRpc(clientId);
    }

    [ClientRpc]
    private void AssignBrushClientRpc(ulong brushId, ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId)
            return;

        // Start a coroutine that waits for PlayerObject to exist
        StartCoroutine(AssignWhenReady(brushId));
    }

    private IEnumerator AssignWhenReady(ulong brushId)
    {
        // Declare netObj outside the loop so we can use it afterwards
        NetworkObject netObj = null;

        // 1) Wait until SpawnManager has the brush
        while (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                   .TryGetValue(brushId, out netObj))
        {
            yield return null;
        }

        // 2) Wait until the local player object exists
        while (NetworkManager.Singleton.LocalClient == null
            || NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            yield return null;
        }

        // 3) Now it's safe to initialize
        var plBrush = NetworkManager.Singleton.LocalClient.PlayerObject
                         .GetComponent<PlayerLightBrush>();
        if (plBrush != null)
            plBrush.InitializeBrush(netObj.gameObject);
    }

    [ClientRpc]
    private void ClearBrushClientRpc(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;
        var plBrush = NetworkManager.Singleton.LocalClient.PlayerObject
                         .GetComponent<PlayerLightBrush>();
        plBrush?.ClearBrush();
    }
}
