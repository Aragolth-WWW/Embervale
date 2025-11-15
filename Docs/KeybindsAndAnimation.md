# Embervale — Keybinds and Animation Parameters

This document tracks the current player keybinds and the Animator parameters our runtime sets. It serves as a single source of truth when wiring animation controllers (Synty Base Locomotion + Sword/Bow packs) and when changing input.

Primary implementation: `Assets/Networking/Runtime/SimpleAnimatorDriver.cs`, `Assets/Networking/Runtime/SimplePlayerController.cs`, `Assets/Camera/Runtime/PlayerCameraController.cs`.

## Keybinds

- Movement: `WASD`
- Look/lock cursor: `RMB` to lock, `Esc` to unlock
- View mode: `V` (toggle FPS/TPS)
- Sprint: `Shift` (hold)
- Crouch: `Ctrl` (hold)
- Jump: `Space`
  - Server-authoritative hop: feet peak around 0.9 m (adjust `SimplePlayerController.jumpApexHeight`).
- Aim (Bow): `RMB` (hold)
- Light attack (Sword): `LMB` (tap) when not aiming
- Heavy attack (Sword): `LMB` (hold) + `Shift` when not aiming
- Draw/charge (Bow): `LMB` (hold) while aiming
- Fire (Bow): release `LMB` while aiming
- Cancel bow (optional): `Esc` while aiming
- Block: `Q` (hold)
- Roll/Dodge: `Left Alt` (press)

## Animator Parameters (names and types)

These are written by `SimpleAnimatorDriver` if present in the assigned Animator Controller.

Locomotion (Synty Base Locomotion)
- Float `MoveSpeed` — smoothed meters/second (owner input; remote estimated)
- Float `StrafeDirectionX` — camera-relative right/left (-1..1)
- Float `StrafeDirectionZ` — camera-relative forward/back (-1..1)
- Int `CurrentGait` — 0 Idle, 1 Walk, 2 Run, 3 Sprint
- Bool `MovementInputHeld` — true while input magnitude > threshold
- Bool `IsGrounded` — ground raycast result
- Bool `IsCrouching` — networked crouch state
- Bool `IsWalking` — true when gait == 1
- Bool `IsStopped` — true when smoothed speed < 0.5 and no input
- Bool `IsStarting` — brief hint on movement start/turn-in-place
- Float `IsStrafing` — 0/1 (always 1.0 in our TPS pattern)
- Float `ForwardStrafe` — 0/1 depending on strafe angle
- Float `CameraRotationOffset` — degrees offset to camera when idle

Jump
- Bool `IsJumping` — pulsed true when `Space` pressed while grounded

Sword Combat
- Trigger `AttackLight` — fired on `LMB` tap (not aiming)
- Trigger `AttackHeavy` — fired on `LMB`+`Shift` (not aiming)
- Bool `IsBlocking` — true while `Q` held
- Trigger `Roll` — fired on `Left Alt` press

Bow Combat
- Bool `IsAiming` — true while `RMB` held
- Float `BowDraw` — 0..1 while `LMB` is held during aim
- Trigger `BowFire` — fired on `LMB` release during aim
- Trigger `BowCancel` — optional cancel on `Esc` during aim

## Speed Calibration (matches Synty sample)

- Walk: `1.4 m/s`
- Run: `2.5 m/s`
- Sprint: `7.0 m/s`
- Damping: `speedChangeDamping = 10`, `strafeDampRate = 20`, `rotationSmoothing = 10`

## Notes

- Animator must be Humanoid and have these parameters defined to respond; missing parameters are ignored safely.
- Attack input comes entirely from `PlayerInputBridge` (Input System). The short-lived `G` key fallback used while debugging owner issues has been removed, so bind an `Attack` action in the asset when testing without a mouse.
- Root Motion should remain OFF (server-authoritative movement). Jump is animation-only pulse unless vertical motion is later added.
- Foot IK: enable IK Pass on the base layer to use `Assets/Animation/Runtime/SimpleFootIK.cs`.
- Combat authoring: use `Tools → Embervale → Combat → Create Sample Assets` to generate starter `WeaponDef_*.asset` and `AttackDef_*.asset` files under `Assets/Game/Combat/Resources`.


## Update Process

When keybinds or parameters change, update this file and the constants in:
- `Assets/Networking/Runtime/SimpleAnimatorDriver.cs` (parameter names, timing, damping)
- `Assets/Networking/Runtime/SimplePlayerController.cs` (movement speeds, sprint/crouch inputs)
- `Assets/Camera/Runtime/PlayerCameraController.cs` (view, cursor handling)
