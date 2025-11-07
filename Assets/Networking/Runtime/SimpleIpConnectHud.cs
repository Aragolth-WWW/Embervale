using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Embervale.Networking
{
    // Minimal IMGUI-based HUD for direct IP testing.
    // Hidden in headless (-batchmode) or when a session is running.
    public class SimpleIpConnectHud : MonoBehaviour
    {
        private string _address = "127.0.0.1";
        private string _port = "7777";
        private bool _show = true;

        private void Awake()
        {
            if (Application.isBatchMode) _show = false;
            DontDestroyOnLoad(gameObject);
        }

        private void OnGUI()
        {
            if (!_show) return;

            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            if (nm.IsServer || nm.IsClient)
            {
                GUILayout.BeginArea(new Rect(10, 10, 260, 120), GUI.skin.box);
                GUILayout.Label($"Mode: {(nm.IsServer && nm.IsClient ? "Host" : nm.IsServer ? "Server" : "Client")}");
                if (GUILayout.Button("Stop")) nm.Shutdown();
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 260, 180), GUI.skin.box);
            GUILayout.Label("Direct IP Connect");
            GUILayout.Label("IP Address");
            _address = GUILayout.TextField(_address);
            GUILayout.Label("Port");
            _port = GUILayout.TextField(_port);

            if (ushort.TryParse(_port, out var port))
            {
                var utp = nm.GetComponent<UnityTransport>();
                utp.SetConnectionData(_address, port);
            }

            if (GUILayout.Button("Start Server"))
            {
                Debug.Log("[Embervale] StartServer clicked");
                nm.StartServer();
                _show = false;
            }
            if (GUILayout.Button("Start Host"))
            {
                Debug.Log("[Embervale] StartHost clicked");
                nm.StartHost();
                _show = false;
            }
            if (GUILayout.Button("Connect Client"))
            {
                Debug.Log("[Embervale] StartClient clicked");
                nm.StartClient();
                _show = false;
            }
            GUILayout.EndArea();
        }
    }
}
