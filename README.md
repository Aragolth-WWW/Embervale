Embervale — Networking + URP + Synty

Overview
- Unity 6 (6000.2.8f1), URP, Netcode for GameObjects 1.9.1, Unity Transport 2.3.0.
- Direct‑IP HUD for quick testing; CLI flags to start server/host/client.
- Player uses Synty Base Locomotion with server‑auth movement and client‑driven animation parameters.

Editor Run
- Open in Unity 6000.2.8f1 (URP configured).
- Press Play; HUD (top‑left) can Start Server/Start Host/Connect Client.

Controls
- Movement: WASD
- Look/lock: RMB to lock, Esc to unlock
- View: V toggles FPS/TPS
- Sprint: Shift (hold)
- Crouch: Ctrl (hold)

Networking
- Bootstrap creates/configures `NetworkManager` + `UnityTransport` at runtime.
- CLI flags (headless/desktop): `-mode server|client|host -ip 127.0.0.1 -port 7777 -maxPlayers 8`
- Examples:
  - Windows: `Embervale.exe -batchmode -nographics -mode server -ip 0.0.0.0 -port 7777 -maxPlayers 8`
  - Linux: `./Embervale.x86_64 -batchmode -nographics -mode server -ip 0.0.0.0 -port 7777 -maxPlayers 8`

Player & Camera
- Prefab: `Assets/Game/Prefabs/Player/Resources/Player_Synty.prefab` (Resources‑loaded).
- Animator Controller: Synty `AC_Polygon_Masculine.controller` assigned, Apply Root Motion OFF.
- Camera: local‑only rig with FPS/TPS toggle and URP camera data; body hidden in FPS.
- Optional foot IK: lightweight `SimpleFootIK` added to child Animator; enable IK Pass on base layer.

Keybinds & Animation
- See `Docs/KeybindsAndAnimation.md` for the current input map and all Animator parameter names used by locomotion + sword/bow packs.

Combat Animator (Build/Assign)
- Upper-layer (arms-only) builder: Tools -> Embervale -> Animation -> Build Player Combat Controller
  - Creates/refreshes layer `UnarmedUpper` with triggers `AttackLight`/`AttackHeavy` and assigns to the player prefab.
- Base-layer-only builder: Tools -> Embervale -> Animation -> Build Player Combat Controller (Base Layer Only)
  - Wires Any State -> `Unarmed_Light_Base` / `Unarmed_Heavy_Base` on the base layer for a simple, reliable punch test.
- Quick assign menus:
  - Assign BaseOnly Controller To Player
  - Assign UpperLayer Controller To Player
  Use these to switch approaches quickly when testing.
- Input path: `SimpleAnimatorDriver` now listens only to the Input System attack action (`PlayerInputBridge`). The temporary `G` key fallback used during debugging has been removed, so bind LMB (or your preferred device) inside the actions asset when testing combat.

Movement (Server Authoritative)
- `SimplePlayerController` now drives a Unity `CharacterController` on the server so the player collides with Synty walls/props and keeps the same feel as the demo scenes. Radius/height/step offset are configurable on the component (defaults: radius 0.4, height 1.8, center 0.9).

Animation (Synty Base Locomotion)
- Driver feeds Synty parameters (MoveSpeed, Strafe X/Z, MovementInputHeld/Pressed/Tapped, IsGrounded, IsCrouching, IsWalking, IsStopped, IsStarting, CurrentGait, IsStrafing, ForwardStrafe, CameraRotationOffset) with Synty-like damping.
- Run/Crouch speeds match Synty sample (2.5 m/s, 1.4 m/s); Sprint 7 m/s.
- Jump apex height now comes from `SimplePlayerController.jumpApexHeight` (default 0.9 m), so the feet rise roughly 0.8–1.0 m off the ground even though movement stays server-authoritative.

Combat Scaffold
- Data: `Assets/Game/Combat/Runtime` (WeaponType, AttackDef, WeaponDef, AttackEvent, EquipmentState, AttackController, WeaponRegistry).
- Create sample assets via menu: `Tools → Embervale → Combat → Create Sample Assets`.
- Runtime: owner sends `TryAttackServerRpc` (light/heavy/charged); server replicates `LastAttack` which triggers remote animators.
- Equip replication: `EquipmentState` holds `EquippedItemId`, `WeaponType`, `IsAiming`, `IsBlocking` as NetworkVariables.

Key Files
- Bootstrap: `Assets/Networking/Runtime/NetworkBootstrap.cs`
- Player controller: `Assets/Networking/Runtime/SimplePlayerController.cs`
- Animator driver: `Assets/Networking/Runtime/SimpleAnimatorDriver.cs`
- Camera controller: `Assets/Camera/Runtime/PlayerCameraController.cs`
- Foot IK (optional): `Assets/Animation/Runtime/SimpleFootIK.cs`
- HUD: `Assets/Networking/Runtime/SimpleIpConnectHud.cs`

Resources Prefab Override
- The game tries to load `Player_Synty` from Resources. If not found, it falls back to a runtime capsule.
- Place the prefab at `Assets/Game/Prefabs/Player/Resources/Player_Synty.prefab`.

Build & Headless
- Create a normal player build and use CLI flags above for dedicated servers.
- The HUD is editor‑only/headless‑aware and won’t interfere on servers.

Troubleshooting
- Fallback capsule spawning: confirm prefab path and that it contains `NetworkObject`, `NetworkTransform`, `SimplePlayerController`, and `PlayerCameraController`.
- Two AudioListeners warning: the player camera disables other listeners at runtime.
- T-pose: ensure an Animator Controller is assigned (Synty AC) and Root Motion is OFF.
- Foot IK: enable IK Pass on the Animator base layer for `SimpleFootIK` to take effect.
 - No punch on LMB: build the Base Layer Only controller and assign it, confirm the two unarmed FBX clips import as Humanoid and preview plays, then watch Animator Parameters for `AttackLight/AttackHeavy` during Play.
 - Mixamo or Synty clip not animating in Preview: set the FBX Rig = Humanoid (Avatar Definition = Create From This Model), in Animation tab enable Bake Into Pose (Rotation, Y, XZ). See `Docs/Troubleshooting/AnimationSetup.md`.

Repository Hygiene
- `.gitignore` ignores Unity cache/build/IDE files. Addressables cache is ignored; Packages/ProjectSettings are tracked.
- Large import `.unitypackage` files are removed from tracking and ignored going forward.
