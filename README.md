Embervale — Networking M1

What’s included
- Netcode for GameObjects (NGO) + Unity Transport (UTP) in `Packages/manifest.json`.
- Runtime bootstrap that auto-creates `NetworkManager` + `UnityTransport`, configures defaults, and adds a simple player prefab at runtime.
- Direct-IP connect HUD (IMGUI) for quick testing.
- Command-line parsing to start as server/host/client with configurable IP/port/max players.

Defaults
- Address: `127.0.0.1`
- Port: `7777`
- Max players: `8`

Run in Editor
1) Open the project in Unity 6000.2.8f1.
2) Press Play; use the on-screen HUD (top-left) to Start Server/Host or Connect Client. Change IP/port as needed.

Headless Dedicated Server
Build (Windows or Linux):
- Create a normal player build; you can run it headless using flags below. (Optional: install Unity Dedicated Server support for smaller builds.)

Command-line flags:
- `-batchmode -nographics -mode server -ip 0.0.0.0 -port 7777 -maxPlayers 8`

Examples
- Windows: `Embervale.exe -batchmode -nographics -mode server -ip 0.0.0.0 -port 7777 -maxPlayers 8`
- Linux: `./Embervale.x86_64 -batchmode -nographics -mode server -ip 0.0.0.0 -port 7777 -maxPlayers 8`

Client connect
- Start the game normally and use the HUD. Enter the server’s IP and port, then press Connect Client.

Notes
- The temporary player uses a capsule with a simple server-authoritative controller and `NetworkTransform` replication. This will be replaced with a Synty-based character later.
- The HUD hides automatically on headless servers (batchmode).

Next steps (M2/M3)
- Replace temp player with Synty character prefab and add Cinemachine cameras (FPS/TPS toggle).
- Build out lobby/relay support and Steamworks integration for discovery.

