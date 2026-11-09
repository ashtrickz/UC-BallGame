using UnityEditor;

namespace Game.Gameplay.Editor
{
    /// <summary>Import policy only for the Blender-authored canyon kit, validated with AxisProbe.fbx.</summary>
    public sealed class CanyonModelImporter : AssetPostprocessor
    {
        public const string Models = "Assets/_Game/Art/Environment/Canyon/Models/";

        private void OnPreprocessModel()
        {
            if (assetPath.StartsWith(Models, System.StringComparison.Ordinal))
                Configure((ModelImporter)assetImporter);
        }

        public static void Configure(ModelImporter importer)
        {
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
