using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AIAssetPipeline
{
    /// <summary>
    /// Handles importing pipeline-generated 3D assets into the Unity project.
    /// <para>
    /// Responsibilities:
    /// <list type="bullet">
    ///   <item>Copy .obj / .mtl / .png from the output directory into
    ///         <c>Assets/GeneratedAssets/&lt;timestamp&gt;/</c></item>
    ///   <item>Force-refresh the AssetDatabase so Unity recognises the new files</item>
    ///   <item>Instantiate the imported mesh in the current scene</item>
    ///   <item>Auto-select the new GameObject and register Undo</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class AssetImporter
    {
        // =================================================================
        // Public API
        // =================================================================

        /// <summary>
        /// Import generated assets based on the pipeline result JSON.
        /// </summary>
        /// <param name="resultJson">
        /// Raw JSON string emitted by the Python backend, e.g.
        /// <c>{"status":"success","mesh_path":"…","output_dir":"…"}</c>
        /// </param>
        public static void ImportGeneratedAssets(string resultJson)
        {
            try
            {
                var result = JsonUtility.FromJson<PipelineResult>(resultJson);

                if (result == null || result.status != "success")
                {
                    Debug.LogError("[AssetImporter] Pipeline did not report success.");
                    return;
                }

                string outputDir = result.output_dir;
                if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
                {
                    Debug.LogError($"[AssetImporter] Output directory not found: {outputDir}");
                    return;
                }

                // Determine Unity-relative path
                string assetsPath = Application.dataPath;  // …/Assets
                string relativePath;

                if (outputDir.Replace('\\', '/').StartsWith(assetsPath.Replace('\\', '/')))
                {
                    // Already inside Assets/
                    relativePath = "Assets" + outputDir.Substring(assetsPath.Length).Replace('\\', '/');
                }
                else
                {
                    // Copy into Assets/GeneratedAssets/<timestamp>/
                    string stamp   = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string destDir = Path.Combine(assetsPath, "GeneratedAssets", stamp);
                    Directory.CreateDirectory(destDir);
                    CopyDirectoryContents(outputDir, destDir);
                    relativePath = "Assets/GeneratedAssets/" + stamp;
                }

                // Refresh so Unity sees the new files
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                // Find and import the .obj
                string objAssetPath = FindAsset(relativePath, "*.obj");

                if (string.IsNullOrEmpty(objAssetPath))
                {
                    Debug.LogWarning("[AssetImporter] No .obj file found in output directory.");
                    return;
                }

                AssetDatabase.ImportAsset(objAssetPath, ImportAssetOptions.ForceUpdate);

                // Load and instantiate
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(objAssetPath);

                if (prefab == null)
                {
                    Debug.LogWarning($"[AssetImporter] Could not load mesh asset: {objAssetPath}");
                    return;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "Generated_3D_Asset_" + DateTime.Now.ToString("HHmmss");

                // Position at the scene-view pivot
                SceneView sv = SceneView.lastActiveSceneView;
                if (sv != null)
                    instance.transform.position = sv.pivot;

                // Undo support
                Undo.RegisterCreatedObjectUndo(instance, "Generate 3D Asset");

                // Select and frame the new object
                Selection.activeGameObject = instance;
                if (sv != null) sv.FrameSelected();

                Debug.Log($"[AssetImporter] \u2705 Asset instantiated: {instance.name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AssetImporter] Import failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // =================================================================
        // Helpers
        // =================================================================

        /// <summary>Copy every file (non-recursive) from <paramref name="src"/> to <paramref name="dst"/>.</summary>
        private static void CopyDirectoryContents(string src, string dst)
        {
            foreach (string file in Directory.GetFiles(src))
            {
                string destFile = Path.Combine(dst, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }
        }

        /// <summary>
        /// Find the first file matching <paramref name="pattern"/> inside a Unity-relative directory.
        /// </summary>
        /// <returns>Unity asset path (e.g. <c>Assets/GeneratedAssets/xxx/model.obj</c>), or <c>null</c>.</returns>
        private static string FindAsset(string relativeDir, string pattern)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullDir = Path.Combine(projectRoot, relativeDir.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(fullDir)) return null;

            string[] files = Directory.GetFiles(fullDir, pattern);
            if (files.Length == 0) return null;

            return relativeDir + "/" + Path.GetFileName(files[0]);
        }

        // =================================================================
        // JSON DTO
        // =================================================================

        [Serializable]
        private class PipelineResult
        {
            public string status;
            public string mesh_path;
            public string mtl_path;
            public string texture_path;
            public string output_dir;
        }
    }
}
