# Animation Setup Troubleshooting (Synty + Mixamo)

Use this checklist when a clip won’t preview, punches don’t fire, or characters freeze/t-pose.

Import settings (FBX)
- Rig: Humanoid
  - Avatar Definition: Create From This Model
  - Apply
- Animation tab:
  - Import Animations: ON
  - Root Transform (Rotation/Position Y/Position XZ): Bake Into Pose = ON
  - Based Upon: Original (Rotation), Center Of Mass or Original (Position)
  - Apply
- Preview window: the character stands on top of the grid and plays the clip. If not, recheck Rig and Bake Into Pose.

Controller build/assign
- Base-layer only (simplest):
  - Tools -> Embervale -> Animation -> Build Player Combat Controller (Base Layer Only)
  - Tools -> Embervale -> Animation -> Assign BaseOnly Controller To Player
  - Resulting asset: `Assets/Game/Animation/Controllers/PlayerHumanoid_Combat_BaseOnly.controller`
- Upper-layer (arms-only, masked):
  - Tools -> Embervale -> Animation -> Build Player Combat Controller
  - Tools -> Embervale -> Animation -> Assign UpperLayer Controller To Player
  - Mask: `Assets/Game/Animation/Masks/UpperBody.mask`

Common errors
- Controller transition uses parameter that does not exist (AttackLight/AttackHeavy): run the builder to create triggers and transitions.
- Statemachine for layer 'UnarmedUpper' is missing: rebuild the upper-layer controller; the builder recreates the state machine.
- Mixamo “Copied Avatar Rig Configuration mis-match… Hips not found”: switch the clip to Humanoid with its own Avatar (Create From This Model), don’t copy an Avatar from a different skeleton.

Runtime verification
- In Play, select the player and open the Animator window:
  - Parameters: `AttackLight` pulses on LMB tap, `AttackHeavy` pulses on LMB hold (>0.35s).
  - Base Layer: Any State transitions to `Unarmed_Light_Base` / `Unarmed_Heavy_Base` fire then return to default.
- If `AttackLight` never pulses, double-check the Input System `Player` action map: the `Attack` action must be bound (typically to LMB). The temporary `G` key fallback used for debugging has been removed.
- If using Upper-layer: ensure the layer `UnarmedUpper` exists, weight returns to 0 when idle, and mask includes arms/fingers only.

Foot IK (optional)
- Add `Assets/Animation/Runtime/SimpleFootIK.cs` to the Animator GameObject.
- Enable IK Pass on the Animator’s base layer.
- Feet should align to ground; adjust raycast distance/ground layers as needed.

Notes
- Our movement is server-authoritative; keep Animator Root Motion OFF.
- In FPS mode, body can be hidden. Press `V` to toggle TPS and verify arms/upper body.
