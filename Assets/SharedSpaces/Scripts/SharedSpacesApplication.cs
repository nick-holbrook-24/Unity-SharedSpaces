// Copyright (c) Facebook, Inc. and its affiliates.
// Use of the material below is subject to the terms of the MIT License
// https://github.com/oculus-samples/Unity-SharedSpaces/tree/main/Assets/SharedSpaces/LICENSE

using Unity.Netcode;
using Oculus.Platform;
using UnityEngine;
using System.Collections;
using Oculus.Platform.Models;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SharedSpacesApplication : MonoBehaviour
{
    [Header("Core Components")]
    [Tooltip("Handles Photon & Netcode setup")]
    public SharedSpacesNetworkLayer networkLayer;
    [Tooltip("Handles scene loading transitions")]
    public SharedSpacesSceneLoader sceneLoader;
    [Tooltip("Spawns session & player objects")]
    public SharedSpacesSpawner spawner;
    [Tooltip("Voice chat integration")]
    public SharedSpacesVoip voip;

    // Manages Oculus group presence state
    public SharedSpacesGroupPresenceState groupPresenceState { get; private set; }

    private SharedSpacesSession session;
    private LaunchType launchType;
    private SharedSpacesLocalPlayerState LocalPlayerState => SharedSpacesLocalPlayerState.Instance;

#if UNITY_EDITOR
    // Telemetry ping in editor only
    [InitializeOnLoad]
    private static class SharedSpacesTelemetry
    {
        static SharedSpacesTelemetry() => Collect();
        private static void Collect()
        {
            const string key = "OculusTelemetry-module_loaded-SharedSpaces";
            if (!SessionState.GetBool(key, false))
            {
                OVRPlugin.SetDeveloperMode(OVRPlugin.Bool.True);
                OVRPlugin.SendEvent("module_loaded", "Unity-SharedSpaces", "integration");
                SessionState.SetBool(key, true);
            }
        }
    }
#endif

    private void OnEnable()
    {
        Debug.Log("[SSA] OnEnable");
        DontDestroyOnLoad(this);

        networkLayer.OnClientConnectedCallback += OnClientConnected;
        networkLayer.OnClientDisconnectedCallback += OnClientDisconnected;
        networkLayer.OnMasterClientSwitchedCallback = OnMasterClientSwitched;
        networkLayer.StartHostCallback += OnHostStarted;
        networkLayer.StartClientCallback += OnClientStarted;
        networkLayer.RestoreHostCallback += OnHostRestored;
        networkLayer.RestoreClientCallback += OnClientRestored;
    }

    private void OnDisable()
    {
        Debug.Log("[SSA] OnDisable");
        networkLayer.OnClientConnectedCallback -= OnClientConnected;
        networkLayer.OnClientDisconnectedCallback -= OnClientDisconnected;
    }

    private void Start()
    {
        Debug.Log("[SSA] Start() called");
        StartCoroutine(Init());
    }

    private IEnumerator Init()
    {
        Debug.Log("[SSA] Init() ▶️ enter coroutine");
        float initStart = Time.time;

        Debug.Log("[SSA] Calling Oculus.Core.AsyncInitialize()");
        Core.AsyncInitialize().OnComplete(OnOculusPlatformInitialized);

#if !UNITY_EDITOR && !UNITY_STANDALONE_WIN
        Debug.Log("[SSA] Waiting for LocalPlayerState.username...");
        float userWaitStart = Time.time;
        yield return new WaitUntil(() =>
        {
            bool ready = LocalPlayerState.username != "";
            if (!ready && Time.time - userWaitStart > 10f)
            {
                Debug.LogError($"[SSA] ❌ username never arrived after 10s: '{LocalPlayerState.username}'");
                return true; // break out so you can see next step
            }
            return ready;
        });
        Debug.Log($"[SSA] ✅ Got username = '{LocalPlayerState.username}' in {Time.time - userWaitStart:F1}s");
#else
        launchType = LaunchType.Normal;
#endif

        Debug.Log("[SSA] launchType = " + launchType);
        if (launchType == LaunchType.Normal)
        {
            Debug.Log("[SSA] Starting groupPresence.Set(\"Lobby\")");
            groupPresenceState = new SharedSpacesGroupPresenceState();
            StartCoroutine(groupPresenceState.Set(
                "Lobby",
                "Lobby-" + LocalPlayerState.applicationID,
                "",
                true
            ));
        }

        Debug.Log("[SSA] Waiting for groupPresence.destination...");
        float gpWaitStart = Time.time;
        yield return new WaitUntil(() =>
        {
            bool ready = groupPresenceState != null && groupPresenceState.destination != null;
            if (!ready && Time.time - gpWaitStart > 10f)
            {
                Debug.LogError("[SSA] ❌ groupPresence.destination never set after 10s");
                return true;
            }
            return ready;
        });
        Debug.Log($"[SSA] ✅ groupPresence.destination = '{groupPresenceState.destination}' in {Time.time - gpWaitStart:F1}s");

        Debug.Log($"[SSA] Loading scene '{groupPresenceState.destination}'");
        sceneLoader.LoadScene(groupPresenceState.destination);
        yield return new WaitUntil(() => sceneLoader.sceneLoaded);
        Debug.Log("[SSA] ✅ sceneLoader.sceneLoaded");

        string roomName = GetPhotonRoomName();
        Debug.Log($"[SSA] networkLayer.Init('{roomName}')");
        networkLayer.Init(roomName);

        Debug.Log($"[SSA] ▶️ Init() complete in {Time.time - initStart:F1}s");
    }

    private void OnOculusPlatformInitialized(Message<PlatformInitialize> msg)
    {
        Debug.Log("[SSA] OnOculusPlatformInitialized callback");
        if (msg.IsError)
        {
            LogError("Failed to initialize Oculus Platform SDK", msg.GetError());
            return;
        }

        Debug.Log("[SSA] Oculus Platform SDK initialized successfully");
        Debug.Log("[SSA] Checking entitlement");
        Entitlements.IsUserEntitledToApplication().OnComplete(ent =>
        {
            Debug.Log($"[SSA] Entitlement check returned IsError={ent.IsError}");
            if (ent.IsError)
            {
                LogError("You are not entitled to use this app", ent.GetError());
                return;
            }

            launchType = ApplicationLifecycle.GetLaunchDetails().LaunchType;
            Debug.Log("[SSA] launchType after entitlement = " + launchType);

            GroupPresence.SetJoinIntentReceivedNotificationCallback(OnJoinIntentReceived);
            GroupPresence.SetInvitationsSentNotificationCallback(OnInvitationsSent);

            Debug.Log("[SSA] Calling Users.GetLoggedInUser()");
            Users.GetLoggedInUser().OnComplete(OnLoggedInUser);
        });

        AbuseReport.SetReportButtonPressedNotificationCallback(OnReportButtonIntentNotif);
    }

    private void OnLoggedInUser(Message<User> msg)
    {
        Debug.Log($"[SSA] OnLoggedInUser: IsError={msg.IsError}");
        if (msg.IsError)
        {
            LogError("Cannot get user info", msg.GetError());
            return;
        }

        Debug.Log($"[SSA] Got user ID={msg.Data.ID}, fetching display name");
        Users.Get(msg.Data.ID).OnComplete(LocalPlayerState.Init);
    }

    private void OnReportButtonIntentNotif(Message<string> msg)
    {
        if (!msg.IsError)
        {
            Debug.Log("[SSA] Report button pressed (AUI)");
            AbuseReport.ReportRequestHandled(ReportRequestResponse.Unhandled);
        }
    }

    private void OnJoinIntentReceived(Message<GroupPresenceJoinIntent> msg)
    {
        Debug.Log("[SSA] OnJoinIntentReceived");
        Debug.Log($"    Destination: {msg.Data.DestinationApiName}");
        Debug.Log($"    LobbyID:     {msg.Data.LobbySessionId}");
        Debug.Log($"    MatchID:     {msg.Data.MatchSessionId}");
        Debug.Log($"    Deeplink:    {msg.Data.DeeplinkMessage}");

        string lobbyId = msg.Data.LobbySessionId;
        if (!lobbyId.Contains("Lobby"))
            lobbyId = "Lobby-" + lobbyId.Substring(0, 8);

        if (groupPresenceState == null)
        {
            string finalLobby = msg.Data.DestinationApiName == "Lobby"
                ? lobbyId
                : "Lobby-" + LocalPlayerState.applicationID;

            groupPresenceState = new SharedSpacesGroupPresenceState();
            StartCoroutine(groupPresenceState.Set(
                msg.Data.DestinationApiName,
                finalLobby,
                GetMatchSessionID(msg.Data.DestinationApiName, lobbyId),
                true
            ));
        }
        else
        {
            StartCoroutine(SwitchRoom(
                msg.Data.DestinationApiName,
                lobbyId,
                true
            ));
        }
    }

    private void OnInvitationsSent(Message<LaunchInvitePanelFlowResult> msg)
    {
        Debug.Log("[SSA] OnInvitationsSent, count=" + msg.Data.InvitedUsers.Count);
        foreach (var user in msg.Data.InvitedUsers)
            Debug.Log($"    Invited: {user.DisplayName} ({user.ID})");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[SSA] OnClientConnected: {clientId}");
        if (NetworkManager.Singleton.IsHost)
        {
            session.DetermineFallbackHost(clientId);
            session.SetPhotonVoiceRoom(clientId);
        }
        else if (NetworkManager.Singleton.IsClient && clientId == NetworkManager.Singleton.LocalClientId)
        {
            session = FindObjectOfType<SharedSpacesSession>();
            var pos = (networkLayer.clientState == SharedSpacesNetworkLayer.ClientState.RestoringClient)
                ? LocalPlayerState.transform.position
                : SharedSpacesSpawnPoint.singleton.SpawnPosition;
            var rot = (networkLayer.clientState == SharedSpacesNetworkLayer.ClientState.RestoringClient)
                ? LocalPlayerState.transform.rotation
                : SharedSpacesSpawnPoint.singleton.SpawnRotation;

            session.RequestSpawnServerRpc(clientId, pos, rot);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[SSA] OnClientDisconnected: {clientId}");
        session.RedetermineFallbackHost(clientId);
    }

    private ulong OnMasterClientSwitched()
    {
        Debug.Log("[SSA] OnMasterClientSwitched");
        return SharedSpacesSession.fallbackHostId;
    }

    private void OnHostStarted()
    {
        Debug.Log("[SSA] OnHostStarted");
        session = spawner.SpawnSession().GetComponent<SharedSpacesSession>();
        var player = spawner.SpawnPlayer(
            NetworkManager.Singleton.LocalClientId,
            SharedSpacesSpawnPoint.singleton.SpawnPosition,
            SharedSpacesSpawnPoint.singleton.SpawnRotation
        );
        voip.StartVoip(player.transform);
    }

    private void OnClientStarted()
    {
        Debug.Log("[SSA] OnClientStarted");
        var player = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
        voip.StartVoip(player.transform);
    }

    private void OnHostRestored()
    {
        Debug.Log("[SSA] OnHostRestored");
        session = spawner.SpawnSession().GetComponent<SharedSpacesSession>();
        var player = spawner.SpawnPlayer(
            NetworkManager.Singleton.LocalClientId,
            LocalPlayerState.transform.position,
            LocalPlayerState.transform.rotation
        );
        voip.StartVoip(player.transform);
    }

    private void OnClientRestored()
    {
        Debug.Log("[SSA] OnClientRestored");
        var player = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
        voip.StartVoip(player.transform);
    }

    public void OnPortalEnter(string portalName)
    {
        Debug.Log("[SSA] OnPortalEnter: " + portalName);
        StartCoroutine(SwitchRoom(portalName, groupPresenceState.lobbySessionID, false));
    }

    public void OnExternalPortalEnter(SharedSpacesExternalPortal portal)
    {
        Debug.Log("[SSA] OnExternalPortalEnter: " + portal.ApplicationId);
        var options = new ApplicationOptions();
        if (!string.IsNullOrEmpty(portal.DeepLinkMessage))
            options.SetDeeplinkMessage(portal.DeepLinkMessage);
        options.SetDestinationApiName(portal.DestinationAPI);
        options.SetLobbySessionId(groupPresenceState.lobbySessionID);
        options.SetMatchSessionId(groupPresenceState.matchSessionID);
        Oculus.Platform.Application.LaunchOtherApp(portal.ApplicationId, options);
    }

    private IEnumerator SwitchRoom(string destination, string lobbySessionID, bool resetSpawnPoint)
    {
        Debug.Log($"[SSA] SwitchRoom → {destination}");
        if (resetSpawnPoint) SharedSpacesSpawnPoint.Reset();
        else SharedSpacesSpawnPoint.Move(destination);

        string lobby = (destination == "Lobby") ? lobbySessionID : groupPresenceState.lobbySessionID;
        var sceneKey = sceneLoader.scenes[destination];

        sceneLoader.LoadScene(destination);
        yield return new WaitUntil(() => sceneLoader.sceneLoaded);

        yield return groupPresenceState.Set(
            sceneKey.ToString(),
            lobby,
            GetMatchSessionID(sceneKey.ToString(), lobbySessionID),
            true
        );

        networkLayer.SwitchPhotonRealtimeRoom(GetPhotonRoomName());
    }

    private string GetPhotonRoomName()
    {
        return string.IsNullOrEmpty(groupPresenceState.matchSessionID)
            ? groupPresenceState.lobbySessionID
            : groupPresenceState.matchSessionID;
    }

    private string GetMatchSessionID(string destination, string lobbySessionID)
    {
        if (destination == "Lobby") return "";
        if (destination == "PurpleRoom") return destination;
        return destination + lobbySessionID;
    }

    private void LogError(string message, Error error)
    {
        Debug.LogError(message);
        Debug.LogError($"ERROR MESSAGE:   {error.Message}");
        Debug.LogError($"ERROR CODE:      {error.Code}");
        Debug.LogError($"ERROR HTTP CODE: {error.HttpCode}");
    }
}
