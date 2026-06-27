using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

/// <summary>
/// Exports GameObjects to FBX with textures embedded.
/// For materials that only use solid colors (no texture), bakes the albedo color
/// into a real PNG texture file so the FBX exporter can embed it.
///
/// Usage: Select a GameObject in Hierarchy, then menu Tools > Export FBX With Textures (Color Baked)
///
/// After importing the FBX into Unity:
///   1. Select the FBX in Project window
///   2. In Inspector → Materials tab → click "Extract Textures"
///   3. Then click "Extract Materials"
/// </summary>
public static class FbxExportWithTextures
{
    const string MENU_PATH = "Tools/Export FBX With Textures (Color Baked)";
    const string TEMP_FOLDER = "Assets/__FbxExportTemp";

    static Assembly fbxEditorAssembly;
    static Type exportModelSettingsType;
    static Type exportSettingsType;
    static Type modelExporterType;
    static Type iExportOptionsType;

    static void EnsureTypes()
    {
        if (fbxEditorAssembly != null) return;

        fbxEditorAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == "Unity.Formats.Fbx.Editor");

        exportModelSettingsType = fbxEditorAssembly.GetType("UnityEditor.Formats.Fbx.Exporter.ExportModelSettings");
        exportSettingsType = fbxEditorAssembly.GetType("UnityEditor.Formats.Fbx.Exporter.ExportSettings");
        modelExporterType = fbxEditorAssembly.GetType("UnityEditor.Formats.Fbx.Exporter.ModelExporter");
        iExportOptionsType = fbxEditorAssembly.GetType("UnityEditor.Formats.Fbx.Exporter.IExportOptions");
    }

    [MenuItem(MENU_PATH, true)]
    static bool ValidateExport()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem(MENU_PATH, false)]
    static void ExportSelected()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Export FBX", "请先在 Hierarchy 中选中要导出的 GameObject", "确定");
            return;
        }

        string defaultPath = EditorPrefs.GetString("FbxExportWithTextures_LastPath", "");
        string exportPath = EditorUtility.SaveFilePanel(
            "导出 FBX (含纹理)",
            defaultPath,
            go.name + ".fbx",
            "fbx");

        if (string.IsNullOrEmpty(exportPath))
            return;

        EditorPrefs.SetString("FbxExportWithTextures_LastPath", Path.GetDirectoryName(exportPath));
        DoExport(go, exportPath);
    }

    static void DoExport(GameObject root, string exportPath)
    {
        EnsureTypes();

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var backups = new List<(Renderer renderer, Material[] originalMats)>();
        foreach (var r in renderers)
            backups.Add((r, r.sharedMaterials));

        var bakedAssetPaths = new List<string>(); // PNG files saved in temp folder
        var matToBaked = new Dictionary<Material, Material>();

        try
        {
            // 1. Create temp folder for baked textures
            if (!Directory.Exists(TEMP_FOLDER))
                Directory.CreateDirectory(TEMP_FOLDER);
            else
            {
                // Clean up any previous temp files
                var oldFiles = Directory.GetFiles(TEMP_FOLDER, "*.png");
                foreach (var f in oldFiles)
                {
                    AssetDatabase.DeleteAsset(f);
                }
            }

            // 2. Bake solid-color materials into PNG textures on disk
            int texIndex = 0;
            foreach (var (renderer, originalMats) in backups)
            {
                var newMats = new Material[originalMats.Length];
                for (int i = 0; i < originalMats.Length; i++)
                {
                    var mat = originalMats[i];
                    if (mat == null) { newMats[i] = null; continue; }

                    if (matToBaked.ContainsKey(mat))
                    {
                        newMats[i] = matToBaked[mat];
                        continue;
                    }

                    Texture existingTex = mat.GetTexture("_MainTex");
                    if (existingTex != null)
                    {
                        newMats[i] = mat;
                        matToBaked[mat] = mat;
                        continue;
                    }

                    // Bake albedo color into a PNG texture file
                    Color albedo = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    var bakedTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var pixels = bakedTex.GetPixels();
                    for (int p = 0; p < pixels.Length; p++)
                        pixels[p] = albedo;
                    bakedTex.SetPixels(pixels);
                    bakedTex.Apply();

                    // Save as PNG to disk
                    string safeName = string.Join("_", mat.name.Split(Path.GetInvalidFileNameChars()));
                    string pngPath = TEMP_FOLDER + "/" + safeName + "_" + (texIndex++) + ".png";
                    byte[] pngData = bakedTex.EncodeToPNG();
                    File.WriteAllBytes(pngPath, pngData);

                    // Destroy the in-memory texture
                    UnityEngine.Object.DestroyImmediate(bakedTex);

                    // Import the PNG as a Unity asset
                    AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
                    var importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                    bakedAssetPaths.Add(pngPath);

                    // Create a temporary material with the baked texture
                    var bakedMat = new Material(mat);
                    bakedMat.name = mat.name + "_baked";
                    bakedMat.SetTexture("_MainTex", importedTex);
                    newMats[i] = bakedMat;
                    matToBaked[mat] = bakedMat;
                }
                renderer.sharedMaterials = newMats;
            }

            // 3. Force refresh so exporter can find the textures
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 4. Create export settings via reflection
            var settingsObj = ScriptableObject.CreateInstance(exportModelSettingsType);

            var infoProp = exportModelSettingsType.GetProperty("info",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var info = infoProp.GetValue(settingsObj);

            // EmbedTextures = true
            SetInfoProperty(info, "EmbedTextures", true);

            // ExportFormat = Binary (required for embedding)
            var formatEnumType = exportSettingsType.GetNestedType("ExportFormat",
                BindingFlags.Public | BindingFlags.NonPublic);
            var binaryField = formatEnumType.GetField("Binary",
                BindingFlags.Public | BindingFlags.Static);
            SetInfoProperty(info, "ExportFormat", binaryField.GetValue(null));

            // Include = Model
            var includeEnumType = exportSettingsType.GetNestedType("Include",
                BindingFlags.Public | BindingFlags.NonPublic);
            var modelField = includeEnumType.GetField("Model",
                BindingFlags.Public | BindingFlags.Static);
            SetInfoProperty(info, "ModelAnimIncludeOption", modelField.GetValue(null));

            // LODExportType = Highest
            var lodEnumType = exportSettingsType.GetNestedType("LODExportType",
                BindingFlags.Public | BindingFlags.NonPublic);
            var highestField = lodEnumType.GetField("Highest",
                BindingFlags.Public | BindingFlags.Static);
            SetInfoProperty(info, "LODExportType", highestField.GetValue(null));

            // ObjectPosition = WorldAbsolute
            var posEnumType = exportSettingsType.GetNestedType("ObjectPosition",
                BindingFlags.Public | BindingFlags.NonPublic);
            var worldAbsField = posEnumType.GetField("WorldAbsolute",
                BindingFlags.Public | BindingFlags.Static);
            SetInfoProperty(info, "ObjectPosition", worldAbsField.GetValue(null));

            infoProp.SetValue(settingsObj, info);

            // 5. Export
            var exportMethod = modelExporterType.GetMethod("ExportObject",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(string), typeof(UnityEngine.Object), iExportOptionsType },
                null);

            string result = (string)exportMethod.Invoke(null, new object[] { exportPath, root, settingsObj });

            int bakedCount = matToBaked.Count(kvp => kvp.Value != kvp.Key);
            Debug.Log($"[FbxExportWithTextures] 导出成功: {exportPath}");
            Debug.Log($"[FbxExportWithTextures] 已为 {bakedCount} 个纯色材质烘焙颜色纹理并嵌入 FBX");
            Debug.Log($"[FbxExportWithTextures] 导入新项目后，请在 FBX 的 Inspector → Materials 中点击 'Extract Textures' 和 'Extract Materials'");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FbxExportWithTextures] 导出失败: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            // 6. Restore original materials
            foreach (var (renderer, originalMats) in backups)
                renderer.sharedMaterials = originalMats;

            // 7. Clean up temporary baked materials (in-memory)
            foreach (var kvp in matToBaked)
            {
                if (kvp.Value != kvp.Key)
                    UnityEngine.Object.DestroyImmediate(kvp.Value);
            }

            // 8. Clean up temp folder on disk
            CleanupTempFolder();
        }
    }

    static void SetInfoProperty(object info, string propertyName, object value)
    {
        var prop = info.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null)
            prop.SetValue(info, value);
        else
            Debug.LogWarning($"[FbxExportWithTextures] Could not set property {propertyName}");
    }

    static void CleanupTempFolder()
    {
        try
        {
            if (Directory.Exists(TEMP_FOLDER))
            {
                // Delete all files first
                foreach (var file in Directory.GetFiles(TEMP_FOLDER))
                {
                    var assetPath = "Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/');
                    AssetDatabase.DeleteAsset(assetPath);
                }

                // Delete the folder's .meta
                var metaFile = TEMP_FOLDER + ".meta";
                if (File.Exists(metaFile))
                    AssetDatabase.DeleteAsset(TEMP_FOLDER);
                else
                    AssetDatabase.DeleteAsset(TEMP_FOLDER);

                AssetDatabase.Refresh();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FbxExportWithTextures] 清理临时文件夹失败: {e.Message}");
        }
    }
}
