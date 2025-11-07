using UnityEngine;
using Unity.Netcode;
using Embervale.CameraSystem;

namespace Embervale.Networking
{
    // Helper to guarantee a local camera exists after host/client starts.
    public class EnsureLocalPlayerCamera : MonoBehaviour
    {
        private bool _done;

        private void Update()
        {
            if (_done) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || (!nm.IsClient && !nm.IsHost)) return;

            var localPlayer = nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (localPlayer == null) return;

            var ctrl = localPlayer.GetComponent<PlayerCameraController>();
            if (ctrl == null)
            {
                ctrl = localPlayer.gameObject.AddComponent<PlayerCameraController>();
            }
            ctrl.EnsureSetup();

            _done = true;
            enabled = false;
        }
    }
}
