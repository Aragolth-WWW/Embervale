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

            nm.NetworkConfig = new NetworkConfig
            {
                // Set via helper below; PlayerPrefab assigned at runtime
                MaxConnectedClients = (ushort)DefaultMaxPlayers,
            };

            ConfigureTransport(transport, DefaultAddress, DefaultPort);
            ConfigurePlayerPrefab(nm);

            // Minimal on-screen HUD for quick testing (IMGUI based)
            go.AddComponent<SimpleIpConnectHud>();

            AutoStartFromCli(nm);
        }

        private static void ConfigureExisting(NetworkManager nm)
        {
            var transport = nm.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = nm.gameObject.AddComponent<UnityTransport>();
            }
            if (nm.NetworkConfig == null)
            {
                nm.NetworkConfig = new NetworkConfig();
            }
            if (nm.NetworkConfig.MaxConnectedClients <= 0)
            {
                nm.NetworkConfig.MaxConnectedClients = (ushort)DefaultMaxPlayers;
            }
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
            if (nm.NetworkConfig.PlayerPrefab != null) return;

            // Create a lightweight runtime prefab to act as the player.
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "PlayerRuntimePrefab";
            UnityEngine.Object.DontDestroyOnLoad(player);

            UnityEngine.Object.Destroy(UnityEngine.Object.FindObjectOfType<Camera>());

            // Add networking components
            player.AddComponent<NetworkObject>();
            player.AddComponent<NetworkTransform>();
            player.AddComponent<SimplePlayerController>();

            player.SetActive(false); // mimic prefab disabled state
            nm.NetworkConfig.PlayerPrefab = player;
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
            nm.NetworkConfig.MaxConnectedClients = (ushort)Mathf.Clamp(maxPlayers, 1, 256);

            if (string.IsNullOrEmpty(mode)) return; // Manual start via HUD/Editor

            if (Application.isBatchMode)
            {
                Debug.Log($"[Embervale] Headless mode detected. Starting as '{mode}' on {ip}:{port}, max {nm.NetworkConfig.MaxConnectedClients}");
            }

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
    }
}
