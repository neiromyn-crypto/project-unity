using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DawnGuard.BlackwoodV2.Editor
{
    /// <summary>
    /// Repairs Meshy FBX imports that appear white in URP by creating external
    /// URP/Lit materials from the texture set stored next to each model and
    /// remapping every FBX material slot to that material.
    ///
    /// Source FBX and source textures are never modified or moved.
    /// Generated materials/masks live in Assets/GameArt/_GeneratedMaterials.
    /// </summary>
    public static class BlackwoodMeshyMaterialFixer
    {
        private const string GameArtRoot = "Assets/GameArt";
        private const string OutputRoot = "Assets/GameArt/_GeneratedMaterials";

        [MenuItem("Dawn Guard/8 - Fix GameArt materials (URP)")]
        public static void FixAll()
        {
            if (!AssetDatabase.IsValidFolder(GameArtRoot))
                throw new InvalidOperationException("Assets/GameArt was not found.");

            EnsureFolder(OutputRoot);
            string[] models = AssetDatabase.FindAssets("t:Model", new[] { GameArtRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.StartsWith(OutputRoot, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(p => p)
                .ToArray();

            int fixedCount = 0;
            var unresolved = new List<string>();
            foreach (string modelPath in models)
            {
                if (FixModel(modelPath)) fixedCount++;
                else unresolved.Add(modelPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("BLACKWOOD MATERIALS: repaired " + fixedCount + "/" + models.Length +
                      " FBX imports." + (unresolved.Count == 0 ? "" :
                      " No BaseColor texture found for: " + string.Join(", ", unresolved)));
        }

        public static bool FixModel(string modelPath)
        {
            string folder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return false;

            Texture2D baseColor = FindTexture(folder, TextureKind.BaseColor);
            if (baseColor == null) return false;
            Texture2D normal = FindTexture(folder, TextureKind.Normal);
            Texture2D metallic = FindTexture(folder, TextureKind.Metallic);
            Texture2D roughness = FindTexture(folder, TextureKind.Roughness);

            ConfigureTexture(baseColor, false, true);
            ConfigureTexture(normal, true, false);
            ConfigureTexture(metallic, false, false);
            ConfigureTexture(roughness, false, false);

            string safe = Sanitize(Path.GetFileNameWithoutExtension(modelPath));
            string role = Sanitize(new DirectoryInfo(folder).Name);
            string materialFolder = OutputRoot + "/" + role;
            EnsureFolder(materialFolder);
            string materialPath = materialFolder + "/" + safe + "_URP.mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP/Lit shader not found. The project must use URP.");
            if (material == null)
            {
                material = new Material(shader) { name = safe + "_URP", enableInstancing = true };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader) material.shader = shader;

            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", baseColor);
            material.SetFloat("_Metallic", metallic != null ? 1f : 0f);
            material.SetFloat("_Smoothness", roughness != null ? .5f : .35f);

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
            }

            Texture2D packed = null;
            if (metallic != null)
                packed = BuildMetallicSmoothness(metallic, roughness, materialFolder + "/" + safe + "_MetallicSmoothness.png");
            if (packed != null)
            {
                material.SetTexture("_MetallicGlossMap", packed);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                material.SetTexture("_MetallicGlossMap", null);
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
            }
            EditorUtility.SetDirty(material);

            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) return false;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return false;
            string[] slotNames = model.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null)
                .Select(m => m.name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToArray();
            if (slotNames.Length == 0) slotNames = new[] { "Material", "Material.001", "material_0" };

            foreach (string slot in slotNames)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), material);
            importer.SaveAndReimport();
            return true;
        }

        private enum TextureKind { BaseColor, Normal, Metallic, Roughness }

        private static Texture2D FindTexture(string folder, TextureKind kind)
        {
            string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetDirectoryName(p)?.Replace('\\', '/') == folder)
                .Where(p => !p.StartsWith(OutputRoot, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            bool IsMap(string p, string token) => Path.GetFileNameWithoutExtension(p)
                .IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

            IEnumerable<string> candidates;
            switch (kind)
            {
                case TextureKind.Normal: candidates = paths.Where(p => IsMap(p, "normal")); break;
                case TextureKind.Metallic: candidates = paths.Where(p => IsMap(p, "metallic")); break;
                case TextureKind.Roughness: candidates = paths.Where(p => IsMap(p, "roughness")); break;
                default:
                    candidates = paths.Where(p => !IsMap(p, "normal") && !IsMap(p, "metallic") &&
                                                  !IsMap(p, "roughness") && !IsMap(p, "mask") &&
                                                  !IsMap(p, "metallicsmoothness"));
                    break;
            }

            string best = candidates
                .OrderByDescending(p => kind == TextureKind.BaseColor && p.IndexOf("image-to-3d-texture", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenByDescending(p => kind == TextureKind.BaseColor && Path.GetFileNameWithoutExtension(p).Equals("texture_0", StringComparison.OrdinalIgnoreCase))
                .ThenBy(p => p.Length)
                .FirstOrDefault();
            return best == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(best);
        }

        private static void ConfigureTexture(Texture2D texture, bool normal, bool sRgb)
        {
            if (texture == null) return;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if (importer == null) return;
            bool changed = false;
            TextureImporterType desired = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != desired) { importer.textureType = desired; changed = true; }
            if (!normal && importer.sRGBTexture != sRgb) { importer.sRGBTexture = sRgb; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        private static Texture2D BuildMetallicSmoothness(Texture2D metallic, Texture2D roughness, string outputPath)
        {
            if (metallic == null) return null;
            bool mReadable = MakeReadable(metallic);
            bool rReadable = roughness != null && MakeReadable(roughness);
            try
            {
                int width = metallic.width, height = metallic.height;
                if (roughness != null && (roughness.width != width || roughness.height != height)) roughness = null;
                Color32[] m = metallic.GetPixels32();
                Color32[] r = roughness != null ? roughness.GetPixels32() : null;
                var pixels = new Color32[m.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    byte metal = m[i].r;
                    byte smooth = r == null ? (byte)115 : (byte)(255 - r[i].r);
                    pixels[i] = new Color32(metal, metal, metal, smooth);
                }
                var packed = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                packed.SetPixels32(pixels); packed.Apply(false, false);
                string full = Path.GetFullPath(outputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, packed.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(packed);
                AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
                if (importer != null)
                {
                    importer.sRGBTexture = false;
                    importer.textureType = TextureImporterType.Default;
                    importer.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
            }
            finally
            {
                RestoreReadable(metallic, mReadable);
                if (roughness != null) RestoreReadable(roughness, rReadable);
            }
        }

        // Returns the original readable state.
        private static bool MakeReadable(Texture2D texture)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if (importer == null) return true;
            bool original = importer.isReadable;
            if (!original) { importer.isReadable = true; importer.SaveAndReimport(); }
            return original;
        }

        private static void RestoreReadable(Texture2D texture, bool original)
        {
            if (texture == null || original) return;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if (importer != null && importer.isReadable) { importer.isReadable = false; importer.SaveAndReimport(); }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}
