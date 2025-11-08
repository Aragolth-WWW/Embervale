using System.IO;
using UnityEditor;
using UnityEngine;

namespace Embervale.Game.Animation.Editor
{
    public static class FixUnarmedImport
    {
        private const string UnarmedFolder = "Assets/Game/Animation/Unarmed";

        [MenuItem("Tools/Embervale/Animation/Fix Unarmed FBX import (Humanoid)")]
        public static void Fix()
        {
            if (!Directory.Exists(UnarmedFolder))
            {
                Debug.LogWarning("[Embervale] Folder not found: " + UnarmedFolder);
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Model", new[] { UnarmedFolder });
            int fixedCount = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    dirty = true;
                }
                if (!importer.importAnimation)
                {
                    importer.importAnimation = true;
                    dirty = true;
                }

                // Ensure clip root transform settings are baked into pose for in-place upper body
                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    clips = importer.defaultClipAnimations;
                }
                if (clips != null && clips.Length > 0)
                {
                    for (int i = 0; i < clips.Length; i++)
                    {
                        clips[i].loopTime = false;
                        clips[i].lockRootRotation = true;
                        clips[i].lockRootHeightY = true;
                        clips[i].lockRootPositionXZ = true;
                    }
                    importer.clipAnimations = clips;
                    dirty = true;
                }

                if (dirty)
                {
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    importer.SaveAndReimport();
                    fixedCount++;
                }
            }
            Debug.Log($"[Embervale] Fixed Humanoid import settings for {fixedCount} unarmed model(s).");
        }
    }
}

