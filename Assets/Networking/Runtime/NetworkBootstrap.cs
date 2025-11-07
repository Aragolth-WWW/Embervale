using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Networking.Transport;
using Unity.Netcode.Transports.UTP;

namespace Embervale.Networking
{
    public static class NetworkBootstrap
    {
        private const string DefaultAddress = "127.0.0.1";
        private const ushort DefaultPort = 7777;
        private const int DefaultMaxPlayers = 8;
        private static int s_maxPlayers = DefaultMaxPlayers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // Ensure a NetworkManager + Transport exist, configured, and persist across scenes.
            if (NetworkManager.Singleton != null)
            {
                ConfigureExisting(NetworkManager.Singleton);
                AutoStartFromCli(NetworkManager.Singleton);
                return;
            }

            var go = new GameObject("NetworkManager");
            UnityEngine.Object.DontDestroyOnLoad(go);

            var nm = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();

            nm.NetworkConfig = new NetworkConfig();
            // Explicitly assign transport (NGO 1.9.x doesn't auto-bind)
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.ConnectionApproval = true;

            ConfigureTransport(transport, DefaultAddress, DefaultPort);
            ConfigurePlayerPrefab(nm);
            SetupConnectionApproval(nm);

            // Minimal on-screen HUD for quick testing (IMGUI based)
            go.AddComponent<SimpleIpConnectHud>();
            // Ensure local player camera gets attached after start
            go.AddComponent<EnsureLocalPlayerCamera>();

            AutoStartFromCli(nm);
        }

        private static void ConfigureExisting(NetworkManager nm)
        {
            var transport = nm.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = nm.gameObject.AddComponent<UnityTransport>();
            }
            // Ensure the transport is wired into the network config
            nm.NetworkConfig.NetworkTransport = transport;
            if (nm.NetworkConfig == null)
            {
                nm.NetworkConfig = new NetworkConfig();
            }
            nm.NetworkConfig.ConnectionApproval = true;
            SetupConnectionApproval(nm);
            ConfigureTransport(transport, DefaultAddress, DefaultPort);
            ConfigurePlayerPrefab(nm);
        }

        private static void ConfigureTransport(UnityTransport transport, string address, ushort port)
        {
            if (transport == null) return;
            transport.SetConnectionData(address, port);
            transport.MaxConnectAttempts = 10;
        }

        private static void ConfigurePlayerPrefab(NetworkManager nm)
        {
            // Always attempt to use the Synty prefab from Resources
            var synty = Resources.Load<GameObject>("Player/Player_Synty");
            if (synty != null)
            {
                if (synty.GetComponent<NetworkObject>() == null)
                {
                    Debug.LogError("[Embervale] Player_Synty.prefab found but missing NetworkObject. Add NetworkObject (and NetworkTransform + SimplePlayerController) to the prefab.");
                }
                else
                {
                    nm.NetworkConfig.PlayerPrefab = synty;
                    Debug.Log("[Embervale] Using PlayerPrefab: Player_Synty (Resources)");
                    return;
                }
            }

            // Fallback: create a simple runtime capsule (scene instance). Not ideal, but keeps testing unblocked.
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "PlayerRuntimePrefab";
            UnityEngine.Object.DontDestroyOnLoad(player);
            if (player.GetComponent<NetworkObject>() == null) player.AddComponent<NetworkObject>();
            if (player.GetComponent<NetworkTransform>() == null) player.AddComponent<NetworkTransform>();
            if (player.GetComponent<SimplePlayerController>() == null) player.AddComponent<SimplePlayerController>();
            nm.NetworkConfig.PlayerPrefab = player;
            Debug.LogWarning("[Embervale] Using fallback PlayerRuntimePrefab (capsule). Place Player_Synty in Resources to override.");
        }

        private static void AutoStartFromCli(NetworkManager nm)
        {
            var args = Environment.GetCommandLineArgs();
            string mode = null; // server|client|host
            string ip = DefaultAddress;
            ushort port = DefaultPort;
            int maxPlayers = DefaultMaxPlayers;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-mode":
                        if (i + 1 < args.Length) mode = args[++i].ToLowerInvariant();
                        break;
                    case "-ip":
                        if (i + 1 < args.Length) ip = args[++i];
                        break;
                    case "-port":
                        if (i + 1 < args.Length && ushort.TryParse(args[++i], out var p)) port = p;
                        break;
                    case "-maxPlayers":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var m)) maxPlayers = m;
                        break;
                }
            }

            var transport = nm.GetComponent<UnityTransport>();
            ConfigureTransport(transport, ip, port);
            s_maxPlayers = Mathf.Clamp(maxPlayers, 1, 256);

            if (string.IsNullOrEmpty(mode)) return; // Manual start via HUD/Editor

            Debug.Log($"[Embervale] Starting as '{mode}' on {ip}:{port}, max {s_maxPlayers}");

            switch (mode)
            {
                case "server":
                    nm.StartServer();
                    break;
                case "host":
                    nm.StartHost();
                    break;
                case "client":
                    nm.StartClient();
                    break;
                default:
                    Debug.LogWarning($"[Embervale] Unknown -mode '{mode}'.");
                    break;
            }
        }
        
        private static void SetupConnectionApproval(NetworkManager nm)
        {
            // avoid multiple subscriptions
            nm.ConnectionApprovalCallback -= ApprovalCheck;
            nm.ConnectionApprovalCallback += ApprovalCheck;
        }

        private static void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var nm = NetworkManager.Singleton;
            int connected = nm.ConnectedClientsIds.Count;
            bool approve = connected < s_maxPlayers;
            response.Approved = approve;
            response.CreatePlayerObject = true;
            if (!approve) response.Reason = "Server full";
        }
    }
}
