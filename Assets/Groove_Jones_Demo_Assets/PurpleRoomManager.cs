using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class PurpleRoomManager : MonoBehaviour
{
    [Header("Assign your LightBrush prefab (NetworkObject + PlayerLightBrush)")]
    [SerializeField] private NetworkObject lightBrushPrefab;

    // Track all spawned brushes so we can despawn them later
    private readonly List<NetworkObject> _spawnedBrushes = new();

    private void Awake()
    {
        // Keep this manager alive through scene changes
        DontDestroyOnLoad(this);

        // Also listen for Unity scene loads (fallback)
        SceneManager.sceneLoaded += OnUnitySceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.IsServer)
        {
            // Netcode scene‐load callback
            NetworkManager.Singleton.SceneManager.OnLoadComplete += OnNetcodeSceneLoaded;
            // Client connect/disconnect
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    // Unity scene load (non‐Netcode path)
    private void OnUnitySceneLoaded(Scene s, LoadSceneMode m)
        => HandleSceneChange(s.name);

    // Netcode scene load: note the signature includes clientId first
    private void OnNetcodeSceneLoaded(ulong clientId, string sceneName, LoadSceneMode mode)
    {
        // Only the server cares about spawning brushes
        if (!NetworkManager.Singleton.IsServer) return;
        HandleSceneChange(sceneName);
    }

    private void OnClientConnected(ulong clientId)
    {
        // If PurpleRoom is already active, give them a brush now
        if (SceneManager.GetActiveScene().name == "PurpleRoom" &&
            NetworkManager.Singleton.IsServer)
        {
            SpawnBrushForClient(clientId);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Server: clean up any brushes owned by that client
        for (int i = _spawnedBrushes.Count - 1; i >= 0; --i)
        {
            if (_spawnedBrushes[i].OwnerClientId == clientId)
            {
                _spawnedBrushes[i].Despawn(true);
                _spawnedBrushes.RemoveAt(i);
            }
        }
        // And tell the client (if still alive) to clear its brush
        ClearBrushClientRpc(clientId);
    }

    // Central handler for switching in/out of PurpleRoom
    private void HandleSceneChange(string sceneName)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (sceneName == "PurpleRoom")
        {
            // Spawn one brush per connected client
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                SpawnBrushForClient(client.ClientId);
        }
        else
        {
            // Left PurpleRoom: despawn all and clear clients
            foreach (var brush in _spawnedBrushes)
                if (brush.IsSpawned)
                    brush.Despawn(true);
            _spawnedBrushes.Clear();
            ClearAllBrushesClientRpc();
        }
    }

    private void SpawnBrushForClient(ulong clientId)
    {
        var brush = Instantiate(lightBrushPrefab);
        brush.SpawnWithOwnership(clientId);
        _spawnedBrushes.Add(brush);
        AssignBrushClientRpc(brush.NetworkObjectId, clientId);
    }

    [ClientRpc]
    private void AssignBrushClientRpc(ulong brushNetworkId, ulong clientId)
    {
        // Only run on the intended client
        if (NetworkManager.Singleton.LocalClientId != clientId)
            return;

        // Try to look up the spawned NetworkObject
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(brushNetworkId, out var netObj)
            || netObj == null
            || !netObj.IsSpawned
            || netObj.gameObject == null)
        {
            // Brush was despawned or never spawned—nothing to do
            return;
        }

        // Also guard against missing PlayerObject or missing script
        var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (playerObj == null)
            return;

        var plBrush = playerObj.GetComponent<PlayerLightBrush>();
        if (plBrush == null)
            return;

        // Finally, initialize
        plBrush.InitializeBrush(netObj.gameObject);
    }

    // Clears only this client’s brush
    [ClientRpc]
    private void ClearBrushClientRpc(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;
        var plBrush = NetworkManager.Singleton.LocalClient.PlayerObject
                         .GetComponent<PlayerLightBrush>();
        plBrush?.ClearBrush();
    }

    // Clears all clients’ brushes
    [ClientRpc]
    private void ClearAllBrushesClientRpc()
    {
        var plBrush = NetworkManager.Singleton.LocalClient.PlayerObject
                         .GetComponent<PlayerLightBrush>();
        plBrush?.ClearBrush();
    }
}
