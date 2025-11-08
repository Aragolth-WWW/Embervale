using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Embervale.Game.Animation.Editor
{
    public static class BuildCombatAnimator
    {
        private const string BaseControllerPath = "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/AC_Polygon_Masculine.controller";
        private const string TargetControllerPath = "Assets/Game/Animation/Controllers/PlayerHumanoid_Combat.controller";
        private const string TargetControllerBaseOnlyPath = "Assets/Game/Animation/Controllers/PlayerHumanoid_Combat_BaseOnly.controller";
        private const string UpperBodyMaskPath = "Assets/Game/Animation/Masks/UpperBody.mask";
        private const string UnarmedLightPath = "Assets/Game/Animation/Unarmed/A_Unarmed_Light_Punch.fbx";
        private const string UnarmedHeavyPath = "Assets/Game/Animation/Unarmed/A_Unarmed_Heavy_Punch.fbx";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Player/Resources/Player_Synty.prefab";

        [MenuItem("Tools/Embervale/Animation/Build Player Combat Controller")]
        public static void Build()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Embervale] Stop Play Mode before building the combat controller.");
                return;
            }
            // Create target folders
            Directory.CreateDirectory("Assets/Game/Animation/Controllers");
            Directory.CreateDirectory("Assets/Game/Animation/Masks");

            // Copy base controller if target missing
            if (!File.Exists(TargetControllerPath))
            {
                if (!File.Exists(BaseControllerPath))
                {
                    Debug.LogError("Base controller not found: " + BaseControllerPath);
                    return;
                }
                AssetDatabase.CopyAsset(BaseControllerPath, TargetControllerPath);
            }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetControllerPath);
            if (ctrl == null)
            {
                Debug.LogError("Failed to load target controller: " + TargetControllerPath);
                return;
            }

            // Ensure required parameters exist on the controller
            EnsureParameter(ctrl, "AttackLight", AnimatorControllerParameterType.Trigger);
            EnsureParameter(ctrl, "AttackHeavy", AnimatorControllerParameterType.Trigger);

            // Create or load upper body mask
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                // Humanoid body parts
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                {
                    mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
                }
                // Only arms and fingers so we don't freeze spine/head when idle
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }

            // Rebuild Unarmed layer fresh to avoid stale, non-persisted state machines
            RemoveLayerByName(ctrl, "UnarmedUpper");
            var layer = new AnimatorControllerLayer
            {
                name = "UnarmedUpper",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = new AnimatorStateMachine { name = "SM_UnarmedUpper" }
            };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, ctrl);
            ctrl.AddLayer(layer);
            layer.avatarMask = mask;
            // reassign to persist avatar mask as well
            { var layers = ctrl.layers; for (int i = 0; i < layers.Length; i++) if (layers[i].name == layer.name) layers[i] = layer; ctrl.layers = layers; }

            // Load clips
            var lightClip = LoadFirstClip(UnarmedLightPath);
            var heavyClip = LoadFirstClip(UnarmedHeavyPath);
            if (lightClip == null || heavyClip == null)
            {
                Debug.LogWarning("[Embervale] Unarmed clips not found as AnimationClips within FBX. Expected FBX files with embedded clips at:\n" + UnarmedLightPath + "\n" + UnarmedHeavyPath + "\nMake sure they import with Animations enabled (Rig=Humanoid). The builder will still proceed but states will have no motion.");
            }

            // Create states
            var sm = layer.stateMachine;
            var idleState = FindOrCreateState(sm, "UpperIdle", null);
            sm.defaultState = idleState;
            var lightState = FindOrCreateState(sm, "Unarmed_Light", lightClip);
            var heavyState = FindOrCreateState(sm, "Unarmed_Heavy", heavyClip);

            // Clear existing AnyState transitions to avoid duplicates/conflicts
            foreach (var t in sm.anyStateTransitions)
            {
                sm.RemoveAnyStateTransition(t);
            }

            // Idle -> Attacks (for easy preview)
            CreateTransition(sm, idleState, lightState, hasExitTime: false, duration: 0.05f, trigger: "AttackLight");
            CreateTransition(sm, idleState, heavyState, hasExitTime: false, duration: 0.05f, trigger: "AttackHeavy");
            // Allow escalation from Light to Heavy if player holds
            CreateTransition(sm, lightState, heavyState, hasExitTime: false, duration: 0.05f, trigger: "AttackHeavy");

            // Attacks -> Idle (return after clip end)
            CreateTransition(sm, lightState, idleState, hasExitTime: true, exitTime: 0.95f, duration: 0.05f);
            CreateTransition(sm, heavyState, idleState, hasExitTime: true, exitTime: 0.95f, duration: 0.05f);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TargetControllerPath);

            // Optional: add base-layer fallback states so we can verify triggers even if upper-layer weight logic changes
            AddBaseLayerFallback(ctrl, lightClip, heavyClip);

            // Assign to player prefab
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (player != null)
            {
                var animator = player.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.runtimeAnimatorController = ctrl;
                    EditorUtility.SetDirty(player);
                    AssetDatabase.SaveAssets();
                    Debug.Log("Assigned PlayerHumanoid_Combat.controller to Player_Synty.prefab");
                }
            }

            Debug.Log("[Embervale] Combat controller ready: " + TargetControllerPath);
        }

        [MenuItem("Tools/Embervale/Animation/Build Player Combat Controller (Base Layer Only)")]
        public static void BuildBaseOnly()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Embervale] Stop Play Mode before building the combat controller.");
                return;
            }

            Directory.CreateDirectory("Assets/Game/Animation/Controllers");

            // Copy Synty locomotion controller as starting point
            if (!File.Exists(TargetControllerBaseOnlyPath))
            {
                if (!File.Exists(BaseControllerPath))
                {
                    Debug.LogError("Base controller not found: " + BaseControllerPath);
                    return;
                }
                AssetDatabase.CopyAsset(BaseControllerPath, TargetControllerBaseOnlyPath);
            }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetControllerBaseOnlyPath);
            if (ctrl == null)
            {
                Debug.LogError("Failed to load base-only controller: " + TargetControllerBaseOnlyPath);
                return;
            }

            // Ensure required parameters exist on the controller
            EnsureParameter(ctrl, "AttackLight", AnimatorControllerParameterType.Trigger);
            EnsureParameter(ctrl, "AttackHeavy", AnimatorControllerParameterType.Trigger);

            // Load clips
            var lightClip = LoadFirstClip(UnarmedLightPath);
            var heavyClip = LoadFirstClip(UnarmedHeavyPath);
            if (lightClip == null || heavyClip == null)
            {
                Debug.LogWarning(
                    "[Embervale] Unarmed clips not found as AnimationClips within FBX. Expected FBX files with embedded clips at:\n" +
                    UnarmedLightPath + "\n" + UnarmedHeavyPath +
                    "\nMake sure they import with Animations enabled (Rig=Humanoid). The builder will still proceed but states will have no motion.");
            }

            // Do NOT add an upper-body layer; just wire AnyState -> base states
            AddBaseLayerFallback(ctrl, lightClip, heavyClip);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TargetControllerBaseOnlyPath);
            Debug.Log("[Embervale] Combat controller (base-only) ready: " + TargetControllerBaseOnlyPath);
        }

        [MenuItem("Tools/Embervale/Animation/Assign BaseOnly Controller To Player")]
        public static void AssignBaseOnlyToPlayer()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetControllerBaseOnlyPath);
            if (ctrl == null)
            {
                Debug.LogError("Base-only controller missing. Run 'Build Player Combat Controller (Base Layer Only)' first.");
                return;
            }
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (player == null)
            {
                Debug.LogError("Player prefab not found at: " + PlayerPrefabPath);
                return;
            }
            var animator = player.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator component not found on Player_Synty prefab or its children.");
                return;
            }
            animator.runtimeAnimatorController = ctrl;
            EditorUtility.SetDirty(player);
            AssetDatabase.SaveAssets();
            Debug.Log("[Embervale] Assigned PlayerHumanoid_Combat_BaseOnly.controller to Player_Synty.prefab");
        }

        [MenuItem("Tools/Embervale/Animation/Assign UpperLayer Controller To Player")]
        public static void AssignUpperLayerToPlayer()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetControllerPath);
            if (ctrl == null)
            {
                Debug.LogError("Upper-layer controller missing. Run 'Build Player Combat Controller' first.");
                return;
            }
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (player == null)
            {
                Debug.LogError("Player prefab not found at: " + PlayerPrefabPath);
                return;
            }
            var animator = player.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator component not found on Player_Synty prefab or its children.");
                return;
            }
            animator.runtimeAnimatorController = ctrl;
            EditorUtility.SetDirty(player);
            AssetDatabase.SaveAssets();
            Debug.Log("[Embervale] Assigned PlayerHumanoid_Combat.controller to Player_Synty.prefab");
        }

        private static AnimatorControllerLayer FindLayer(AnimatorController ctrl, string name)
        {
            foreach (var l in ctrl.layers) if (l.name == name) return l;
            return null;
        }

        private static void RemoveLayerByName(AnimatorController ctrl, string name)
        {
            var layers = ctrl.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == name)
                {
                    ctrl.RemoveLayer(i);
                    break;
                }
            }
        }

        private static void AddBaseLayerFallback(AnimatorController ctrl, AnimationClip lightClip, AnimationClip heavyClip)
        {
            if (ctrl.layers == null || ctrl.layers.Length == 0) return;
            var baseLayer = ctrl.layers[0];
            var sm = baseLayer.stateMachine ?? new AnimatorStateMachine { name = "BaseLayerSM" };
            if (baseLayer.stateMachine == null)
            {
                AssetDatabase.AddObjectToAsset(sm, ctrl);
                baseLayer.stateMachine = sm; var layers = ctrl.layers; layers[0] = baseLayer; ctrl.layers = layers;
            }

            var baseLight = FindOrCreateState(sm, "Unarmed_Light_Base", lightClip);
            var baseHeavy = FindOrCreateState(sm, "Unarmed_Heavy_Base", heavyClip);

            // AnyState -> base attack states
            CreateAnyTransition(sm, baseLight, "AttackLight");
            CreateAnyTransition(sm, baseHeavy, "AttackHeavy");

            // Return to default state after clip end
            var back = sm.defaultState != null ? sm.defaultState : baseLight; // fallback if missing
            CreateTransition(sm, baseLight, back, hasExitTime: true, duration: 0.05f, trigger: null, exitTime: 0.95f);
            CreateTransition(sm, baseHeavy, back, hasExitTime: true, duration: 0.05f, trigger: null, exitTime: 0.95f);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            foreach (var st in sm.states)
                if (st.state != null && st.state.name == name) return st.state;
            var s = sm.AddState(name);
            s.motion = clip;
            s.writeDefaultValues = false;
            s.speed = 1f;
            s.mirror = false;
            s.iKOnFeet = true;
            return s;
        }

        private static void CreateAnyTransition(AnimatorStateMachine sm, AnimatorState target, string triggerName)
        {
            foreach (var t in sm.anyStateTransitions)
            {
                if (t.destinationState == target) return;
            }
            var tr = sm.AddAnyStateTransition(target);
            tr.hasExitTime = false;
            tr.hasFixedDuration = true;
            tr.duration = 0.05f;
            tr.canTransitionToSelf = false;
            var p = new AnimatorCondition
            {
                mode = AnimatorConditionMode.If,
                parameter = triggerName,
                threshold = 0f
            };
            tr.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
        }

        private static void CreateTransition(AnimatorStateMachine sm, AnimatorState from, AnimatorState to, bool hasExitTime, float duration, string trigger = null, float exitTime = 0.9f)
        {
            foreach (var t in from.transitions)
            {
                if (t.destinationState == to) return;
            }
            var tr = from.AddTransition(to);
            tr.hasExitTime = hasExitTime;
            if (hasExitTime)
            {
                tr.exitTime = exitTime;
            }
            tr.hasFixedDuration = true;
            tr.duration = duration;
            tr.canTransitionToSelf = false;
            if (!string.IsNullOrEmpty(trigger))
            {
                tr.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            }
        }

        private static void EnsureParameter(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in ctrl.parameters)
            {
                if (p.name == name) return;
            }
            ctrl.AddParameter(name, type);
        }

        private static AnimationClip LoadFirstClip(string fbxPath)
        {
            // FBX clips are sub-assets; LoadAllAssetsAtPath is required
            var all = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var a in all)
            {
                if (a is AnimationClip clip)
                {
                    var name = clip.name;
                    if (name != null && !name.StartsWith("__preview__")) return clip;
                }
            }
            return null;
        }
    }
}
