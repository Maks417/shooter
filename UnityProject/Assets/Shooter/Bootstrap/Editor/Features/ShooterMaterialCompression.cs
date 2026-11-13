using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only platform compression experiment; immutable SGAF v1 planes remain RGBA8.</summary>
public static class ShooterMaterialCompression
{
    [Serializable] public sealed class Mip
    {
        public int level, width, height;
        public bool normalErrorApplicable;
        public double meanAbsoluteChannelError, normalDegreesMean, normalDegreesP95, normalDegreesMax;
    }
    [Serializable] public sealed class Plane
    {
        public string family, plane, format, sourceHash, uncompressedSha256, compressedSha256, compressedPath;
        public int rgbaBytes, compressedBytes;
        public Mip[] mips;
    }
    [Serializable] public sealed class Proof
    {
        public string result = "Verified", scope = "Editor BC7/BC5 compression and GPU decode experiment; no shipping compressed cache or release performance acceptance";
        public string toolchainHash, graphicsDevice, graphicsApi;
        public Plane[] planes;
    }
    public static void Measure()
    {
        try
        {
            ShooterBuild.CheckToolchain();
            if (!SystemInfo.SupportsTextureFormat(TextureFormat.BC7) || !SystemInfo.SupportsTextureFormat(TextureFormat.BC5))
                throw new NotSupportedException("Actual selected graphics device does not support BC7 and BC5.");
            var planes = new List<Plane>();
            foreach (string family in GeneratedGallery.Families)
            {
                var recipe = MaterialRecipe.Parse(File.ReadAllBytes(Path.Combine(Application.dataPath, "Shooter/Generation/FeatureRecipes/" + family + ".json")));
                var bake = MaterialBaker.Bake(recipe);
                planes.Add(MeasurePlane(recipe, "albedo", bake.Albedo, TextureFormat.BC7));
                planes.Add(MeasurePlane(recipe, "normal", bake.Normal, TextureFormat.BC5));
                planes.Add(MeasurePlane(recipe, "mask", bake.Mask, TextureFormat.BC7));
            }
            ShooterBuild.WriteJson(ShooterBuild.Argument("-shooterReport"), new Proof { toolchainHash = ShooterBuild.HashFile(ShooterBuild.LockPath),
                graphicsDevice = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), planes = planes.ToArray() });
        }
        catch (Exception error) { Debug.LogError("SHOOTER_FAILURE: " + error); EditorApplication.Exit(1); }
    }
    private static Plane MeasurePlane(MaterialRecipe recipe, string plane, MaterialData data, TextureFormat format)
    {
        // Numeric channel comparison uses linear textures, including albedo's encoded bytes.
        // Production albedo still has the separately pinned sRGB interpretation.
        var texture = new Texture2D(data.Width, data.Height, TextureFormat.RGBA32, true, true);
        try
        {
            int rgbaBytes = 0;
            for (int i = 0; i < data.MipBytes.Count; i++) { var bytes = data.MipBytes[i].ToArray(); rgbaBytes += bytes.Length; texture.SetPixelData(bytes, i); }
            texture.Apply(false, false);
            byte[] raw = texture.GetRawTextureData();
            int checkWidth = data.Width, checkHeight = data.Height;
            for (int level = 0; level < data.MipBytes.Count; level++)
            {
                var decoded = ReadMip(texture, checkWidth, checkHeight, level); var expected = data.MipBytes[level];
                for (int p = 0; p < decoded.Length; p++)
                    if (Math.Abs(decoded[p].r - expected[p * 4]) > 1 || Math.Abs(decoded[p].g - expected[p * 4 + 1]) > 1 ||
                        Math.Abs(decoded[p].b - expected[p * 4 + 2]) > 1 || Math.Abs(decoded[p].a - expected[p * 4 + 3]) > 1)
                        throw new InvalidOperationException("Uncompressed mip readback calibration failed (orientation/channel/color-space).");
                checkWidth = Math.Max(1, checkWidth / 2); checkHeight = Math.Max(1, checkHeight / 2);
            }
            EditorUtility.CompressTexture(texture, format, TextureCompressionQuality.Best);
            texture.Apply(false, false);
            if (texture.format != format) throw new InvalidOperationException("Compression did not produce requested format.");
            byte[] packed = texture.GetRawTextureData();
            string packedPath = Path.Combine(Path.GetDirectoryName(ShooterBuild.Argument("-shooterReport")), recipe.Family + "-" + plane + ".bc.bin");
            File.WriteAllBytes(packedPath, packed);
            var mips = new List<Mip>();
            int width = data.Width, height = data.Height;
            for (int level = 0; level < data.MipBytes.Count; level++)
            {
                // Explicit mip sampling avoids derivative-selected LOD hiding errors in small levels.
                Color32[] decoded = ReadMip(texture, width, height, level);
                var source = data.MipBytes[level];
                double error = 0, angleSum = 0, angleMax = 0; var angles = new double[decoded.Length];
                for (int p = 0; p < decoded.Length; p++)
                {
                    var c = decoded[p];
                    error += Math.Abs(c.r - source[p * 4]) + Math.Abs(c.g - source[p * 4 + 1]);
                    if (plane != "normal") error += Math.Abs(c.b - source[p * 4 + 2]) + Math.Abs(c.a - source[p * 4 + 3]);
                    if (plane == "normal")
                    {
                        Vector3 a = new Vector3(source[p * 4] / 127.5f - 1, source[p * 4 + 1] / 127.5f - 1, source[p * 4 + 2] / 127.5f - 1).normalized;
                        float x = c.r / 127.5f - 1, y = c.g / 127.5f - 1;
                        Vector3 b = new Vector3(x, y, Mathf.Sqrt(Mathf.Max(0, 1 - x * x - y * y))).normalized;
                        double angle = Math.Acos(Math.Max(-1, Math.Min(1, Vector3.Dot(a, b)))) * 180 / Math.PI;
                        angles[p] = angle; angleSum += angle; angleMax = Math.Max(angleMax, angle);
                    }
                }
                Array.Sort(angles);
                mips.Add(new Mip { level = level, width = width, height = height,
                    normalErrorApplicable = plane == "normal",
                    meanAbsoluteChannelError = error / (decoded.Length * (plane == "normal" ? 2d : 4d)),
                    normalDegreesMean = angleSum / decoded.Length, normalDegreesP95 = angles[(int)Math.Ceiling(.95 * angles.Length) - 1], normalDegreesMax = angleMax });
                width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
            }
            return new Plane { family = recipe.Family, plane = plane, format = format.ToString(), sourceHash = recipe.SourceHash.ToString(),
                rgbaBytes = rgbaBytes, compressedBytes = packed.Length, compressedPath = packedPath,
                uncompressedSha256 = ContentIdentity.Hash(raw).ToString(), compressedSha256 = ContentIdentity.Hash(packed).ToString(), mips = mips.ToArray() };
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
    private static Color32[] ReadMip(Texture2D source, int width, int height, int mip)
    {
        // Explicit LOD sampling also handles the 2x2/1x1 tail, which cannot be standalone BC textures.
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shooter/Bootstrap/Editor/Features/CompressionReadback.shader");
        if (shader == null || !shader.isSupported) throw new InvalidOperationException("Compression readback shader unavailable.");
        var material = new Material(shader); material.SetFloat("_Mip", mip);
        var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active; var result = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        try
        {
            Graphics.Blit(source, target, material); RenderTexture.active = target;
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0); result.Apply(false, false);
            return result.GetPixels32();
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(result); }
    }
}
