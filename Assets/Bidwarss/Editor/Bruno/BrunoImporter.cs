using System;
using System.Linq;
using UnityEditor;

namespace Bidwarss.Editor
{
    public sealed class BrunoImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (assetPath.EndsWith("/Bruno/BrunoHands.fbx", StringComparison.Ordinal) || assetPath.EndsWith("/Bruno/BrunoBoxCutter.fbx", StringComparison.Ordinal) || assetPath.EndsWith("/Bruno/BrunoPryBar.fbx", StringComparison.Ordinal))
            {
                var hands = (ModelImporter)assetImporter;
                hands.globalScale=1;hands.useFileScale=true;hands.bakeAxisConversion=true;
                hands.importAnimation=false;hands.animationType=ModelImporterAnimationType.None;
                hands.importBlendShapes=true;
                hands.importNormals=ModelImporterNormals.Import;hands.addCollider=false;
                hands.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                return;
            }
            if (!assetPath.EndsWith("/Bruno/Bruno.fbx", StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importBlendShapeNormals = ModelImporterNormals.Import;
            importer.optimizeGameObjects = false; // Head is used by the look-around overlay.
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
        void OnPreprocessAnimation()
        {
            if (!assetPath.EndsWith("/Bruno/Bruno.fbx", StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var c in clips)
            {
                string name = c.name.Split('|').Last();
                c.name = name;
                c.loopTime = name != "Greet";
                c.loopPose = name != "Greet";
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Any(p => p.Contains("/Characters/Bruno/") && p.EndsWith(".fbx", StringComparison.Ordinal)))
                EditorApplication.delayCall += BrunoSetup.EnsureInstalled;
        }
    }
}
