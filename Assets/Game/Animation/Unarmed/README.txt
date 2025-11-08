Drop your Mixamo (or other Humanoid) unarmed clips in this folder.

Recommended filenames (will be referenced by the controller):
- A_Unarmed_Light_Punch.fbx (tap LMB)
- A_Unarmed_Heavy_Punch.fbx (hold LMB)

Mixamo download settings:
- Format: FBX for Unity
- Skin: Without Skin
- FPS: 30
- Keyframe Reduction: None
- In Place: Enabled

Unity import settings per file:
- Rig: Animation Type = Humanoid; Avatar Definition = Copy From Other Avatar → select your player Avatar
- Animations: Loop Time OFF; Root Transform Rotation/Position = Bake Into Pose (Based Upon Original); Foot IK ON; Root Motion OFF

Once added, tell Codex to wire the Unarmed layer to these clips.
