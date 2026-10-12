using System.IO;
using UnityEditor;
using UnityEngine;

namespace FootballRpg.Editor
{
    public static class PitchAssets
    {
        [MenuItem("Football RPG/Create Missing Pitch Materials")]
        public static void Create()
        {
            const string folder = "Assets/FootballRpg/Runtime/Content/Resources";
            Directory.CreateDirectory(folder);
            CreateMaterial(folder + "/PreviewSurface.mat", "Standard");
            CreateMaterial(folder + "/PreviewLine.mat", "Unlit/Color");
            AssetDatabase.SaveAssets();
        }

        private static void CreateMaterial(string path, string shaderName)
        {
            if (File.Exists(path)) return;
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException("Missing shader: " + shaderName);
            var material = new Material(shader);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .15f);
            AssetDatabase.CreateAsset(material, path);
        }
    }
}
