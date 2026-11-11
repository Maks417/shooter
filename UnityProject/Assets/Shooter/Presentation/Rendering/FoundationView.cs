using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation
{
    /// <summary>Read-only M0 view: analytic boxes, numeric material, one camera and sun. No authority references.</summary>
    public sealed class FoundationView : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        public static FoundationView Create()
        {
            var root = new GameObject("Foundation presentation").AddComponent<FoundationView>();
            root.Initialize(); return root;
        }
        private void Initialize()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Required locked URP Lit shader missing; no art fallback");
            material = new Material(shader) { name = "Numeric matte concrete" };
            material.SetColor("_BaseColor", new Color(.42f, .46f, .48f, 1));
            material.SetFloat("_Smoothness", .12f); material.SetFloat("_Metallic", 0);
            mesh = CreateUnitBox();
            Box("Floor visual", new Vector3(0, -.05f, 0), new Vector3(10, .1f, 10));
            Box("Box visual", new Vector3(0, .5f, 0), Vector3.one);
            var camera = new GameObject("Foundation camera").AddComponent<Camera>();
            camera.transform.SetParent(transform, false); camera.transform.position = new Vector3(0, 1.65f, -6);
            camera.fieldOfView = 60; camera.nearClipPlane = .1f; camera.farClipPlane = 200;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.11f, .14f, .18f, 1);
            camera.allowHDR = false; camera.allowMSAA = QualitySettings.GetQualityLevel() == 2;
            var sun = new GameObject("Foundation sun").AddComponent<Light>(); sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(45, 135, 0); sun.intensity = 1;
            sun.shadows = LightShadows.Hard;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.22f, .24f, .27f, 1);
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            int profile = QualitySettings.GetQualityLevel();
            Screen.SetResolution(profile == 0 ? 1280 : profile == 1 ? 1600 : 1920, profile == 0 ? 720 : profile == 1 ? 900 : 1080, FullScreenMode.Windowed);
            Debug.Log("SHOOTER_RENDER_SETUP gpu=" + SystemInfo.graphicsDeviceName + " api=" + SystemInfo.graphicsDeviceType + " profile=" + QualitySettings.names[profile]);
        }
        private void Box(string label, Vector3 center, Vector3 scale)
        {
            var box = new GameObject(label) { layer = 11 }; box.transform.SetParent(transform, false);
            box.transform.position = center; box.transform.localScale = scale;
            box.AddComponent<MeshFilter>().sharedMesh = mesh; box.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        public static Mesh CreateUnitBox()
        {
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3[] u = { Vector3.back, Vector3.forward, Vector3.right, Vector3.right, Vector3.right, Vector3.left };
            Vector3[] v = { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };
            var vertices = new Vector3[24]; var faceNormals = new Vector3[24]; var uv = new Vector2[24]; var indices = new int[36];
            for (int face = 0; face < 6; face++)
            {
                int i = face * 4, t = face * 6; Vector3 center = normals[face] * .5f;
                vertices[i] = center - u[face] * .5f - v[face] * .5f; vertices[i + 1] = center + u[face] * .5f - v[face] * .5f;
                vertices[i + 2] = center + u[face] * .5f + v[face] * .5f; vertices[i + 3] = center - u[face] * .5f + v[face] * .5f;
                for (int j = 0; j < 4; j++) faceNormals[i + j] = normals[face];
                uv[i] = Vector2.zero; uv[i + 1] = Vector2.right; uv[i + 2] = Vector2.one; uv[i + 3] = Vector2.up;
                indices[t] = i; indices[t + 1] = i + 1; indices[t + 2] = i + 2; indices[t + 3] = i; indices[t + 4] = i + 2; indices[t + 5] = i + 3;
            }
            var result = new Mesh { name = "Analytic unit box v1", vertices = vertices, normals = faceNormals, uv = uv, triangles = indices };
            result.RecalculateBounds(); return result;
        }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); }
    }
}
