using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Shooter.Content;
using Shooter.Contracts;
using Shooter.Generation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Presentation
{
    /// <summary>Verified M1 soldier spike view. Scripted diagnostic roots are unrelated to gameplay authority.</summary>
    public sealed class SoldierGallery : MonoBehaviour
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<Avatar> avatars = new List<Avatar>();
        private readonly List<int[]> skinMaps = new List<int[]>();
        private readonly Ground ground = new Ground();
        private GeneratedSky sky;
        private GeneratedReflectionProbe reflectionProbe;
        private float originTime;
        private Transform floorRoot;
        private GameObject stepsRoot;
        public Camera ViewCamera { get; private set; }
        public string Scenario = "walk";
        public bool Animate = true;
        public float MaxGripError { get; private set; }
        public float MaxPlantSlide { get; private set; }
        public float MaxPlantTargetError { get; private set; }
        public float MinArmBodyClearance { get; private set; } = float.MaxValue;
        public float MaxEyeSightLineError { get; private set; }
        public float MaxStockError { get; private set; }
        public double LastEightRigMilliseconds { get; private set; }
        public uint PoseSequence { get; private set; }
        public string ContentPlanHash { get; private set; }
        public int RigCount => avatars.Count;
        private sealed class Avatar
        {
            public Transform Root, Weapon;
            public Transform[] Bones;
            public SkinnedMeshRenderer Renderer;
            public SoldierMotion Motion;
            public Mesh CaptureMesh;
            public MeshRenderer CaptureRenderer;
        }
        private sealed class Ground : ISoldierGround
        {
            public string Scenario;
            public bool TrySample(float x, float z, out float y) { y = Height(z); return true; }
            public float Height(float z) => Scenario == "slope" ? z * Mathf.Tan(15 * Mathf.Deg2Rad) : Scenario == "steps" ? Mathf.Floor(z / .8f) * .2f : 0;
        }
        public static SoldierGallery Create(string cacheRoot, ContentId toolchain, int count = 1)
        {
            if (count != 1 && count != 8) throw new ArgumentOutOfRangeException(nameof(count));
            var view = new GameObject("M1 verified soldier spike").AddComponent<SoldierGallery>();
            try { view.Initialize(cacheRoot, toolchain, count); return view; }
            catch { DestroyObject(view.gameObject); throw; }
        }
        private void Initialize(string root, ContentId toolchain, int count)
        {
            var files = new CacheFiles(root, toolchain); string path = Path.Combine(files.Root, "soldier-gallery.json"); files.AssertSafePath(path);
            if (new FileInfo(path).Length > CanonicalJson.MaxBytes) throw new InvalidDataException("Soldier plan exceeds limit.");
            var plan = JsonValue.Parse(File.ReadAllBytes(path)).Object;
            ContentPlanHash = ContentIdentity.Hash(File.ReadAllBytes(path)).ToString();
            if (plan["schema"].String != "shooter.soldier-gallery.v1" || new ContentId(plan["toolchainHash"].String) != toolchain)
                throw new InvalidDataException("Soldier gallery identity mismatch.");
            var id = new ContentId(plan["soldierVisual"].String); var manifest = files.ReadManifest(id);
            if (manifest.GeneratorId != SoldierRecipe.GeneratorId || manifest.GeneratorVersion != SoldierRecipe.GeneratorVersion || manifest.RecipeId != "soldiers/baseline")
                throw new InvalidDataException("Unsupported soldier generator identity.");
            using var store = new FileContentStore(files);
            byte[] Body(ContentId content, ContentKind kind, string variant)
            {
                var acquired = store.AcquireVariantAsync(content, kind, variant, CancellationToken.None).GetAwaiter().GetResult();
                if (acquired.Error != ErrorCode.None || acquired.Lease == null) throw new InvalidDataException(acquired.Diagnostic);
                using (acquired.Lease)
                {
                    var bytes = new byte[acquired.Lease.Length]; acquired.Lease.CopyTo(bytes);
                    return ArtifactCodec.Decode(bytes, kind, files.ReadManifest(content).GeneratorHash, toolchain);
                }
            }
            var skeleton = GeometryCodec.DecodeSkeleton(Body(id, ContentKind.Skeleton, "main"));
            if (!GeometryCodec.EncodeSkeleton(skeleton).SequenceEqual(GeometryCodec.EncodeSkeleton(SoldierSkeleton.Definition)))
                throw new InvalidDataException("Soldier semantic joint baseline mismatch.");
            var meshes = new Mesh[3]; for (int i = 0; i < 3; i++) meshes[i] = Upload(GeometryCodec.DecodeMesh(Body(id, ContentKind.Mesh, "lod" + i)), true);
            var weaponId = new ContentId(plan["rifleVisual"].String);
            var weaponManifest = files.ReadManifest(weaponId);
            if (weaponManifest.RecipeId != "weapons/rifle" || weaponManifest.GeneratorId != "geometry.weapon" || weaponManifest.GeneratorVersion != WeaponRecipe.GeneratorVersion)
                throw new InvalidDataException("Soldier preview requires the baseline rifle.");
            var rifleData = GeometryCodec.DecodeMesh(Body(weaponId, ContentKind.Mesh, "lod0"));
            Mesh rifleMesh = Upload(rifleData, false);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Required locked Lit shader unavailable.");
            Material Solid(string label, Color color, float metallic = 0, float smoothness = .2f)
            {
                var material = Own(new Material(shader) { name = label }); material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness); return material;
            }
            var palette = new[] { Solid("Numeric olive uniform", new Color(.19f,.23f,.12f)), Solid("Numeric dark equipment", new Color(.08f,.10f,.065f)),
                Solid("Numeric covered skin",new Color(.48f,.32f,.22f)), Solid("Numeric opaque goggles",new Color(.035f,.06f,.07f),.3f,.55f) };
            var metal = Solid("Numeric rifle metal", new Color(.08f,.09f,.10f), .15f);
            var polymer = Solid("Numeric rifle polymer", new Color(.05f,.055f,.045f));
            var recipe = WeaponRecipe.Parse(Encoding.ASCII.GetBytes("{\"schemaId\":\"shooter.recipe.weapon.v1\",\"recipeId\":\"weapons/rifle\",\"generatorId\":\"geometry.weapon\",\"generatorVersion\":\"1.0.4\",\"seed\":\"460072030\",\"parameters\":{\"kind\":\"rifle\"},\"dependencies\":[]}"));
            var weaponPalette = Shooter.Presentation.Rendering.ProductSurfaceMaterial.WeaponFinishes(metal, polymer,
                Solid("Numeric steel", new Color(.35f,.38f,.41f), .9f, .6f),
                Solid("Numeric recess", new Color(.01f,.012f,.013f)),
                Solid("Numeric enamel", new Color(.1f,.15f,.05f)));
            var movingParts = new[] { "magazine", "cycle" }.Select(part => GeometryCodec.DecodeMesh(Body(weaponId, ContentKind.Mesh, part))).ToArray();
            var movingMeshes = movingParts.Select(part => Upload(part, false)).ToArray();
            for (int i = 0; i < count; i++)
            {
                var avatar = new Avatar { Root = new GameObject("Generated soldier " + i).transform, Bones = new Transform[23], Motion = new SoldierMotion(recipe) };
                avatar.Root.SetParent(transform, false);
                for (int j = 0; j < 23; j++)
                {
                    var definition = skeleton.Joints[j]; var bone = new GameObject(SoldierSkeleton.Names[j]).transform; avatar.Bones[j] = bone;
                    bone.SetParent(definition.ParentId == ushort.MaxValue ? avatar.Root : avatar.Bones[definition.ParentId], false);
                    bone.localPosition = V(definition.LocalBindPosition); bone.localRotation = Quaternion.identity;
                }
                avatar.Renderer = avatar.Root.gameObject.AddComponent<SkinnedMeshRenderer>(); avatar.Renderer.sharedMesh = meshes[0];
                avatar.Renderer.bones = skinMaps[0].Select(j=>avatar.Bones[j]).ToArray(); avatar.Renderer.rootBone = avatar.Bones[0]; avatar.Renderer.sharedMaterials = palette;
                avatar.Renderer.quality = SkinQuality.Bone4; avatar.Renderer.updateWhenOffscreen = true;
                // Includes all supported scripted poses; bind bounds are deliberately not reused as posed bounds.
                avatar.Renderer.localBounds = new Bounds(new Vector3(0, 1, 0), new Vector3(4, 4, 4));
                avatar.Weapon = new GameObject("Verified rifle").transform; avatar.Weapon.SetParent(avatar.Root, false);
                avatar.Weapon.gameObject.AddComponent<MeshFilter>().sharedMesh = rifleMesh;
                var renderer = avatar.Weapon.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterials = rifleData.Submeshes.Select(sub => weaponPalette[sub.MaterialSlot]).ToArray();
                for (int part = 0; part < movingParts.Length; part++)
                {
                    var child = new GameObject(part == 0 ? "Verified magazine" : "Verified cycle");
                    child.transform.SetParent(avatar.Weapon, false);
                    child.AddComponent<MeshFilter>().sharedMesh = movingMeshes[part];
                    child.AddComponent<MeshRenderer>().sharedMaterials = movingParts[part].Submeshes.Select(sub => weaponPalette[sub.MaterialSlot]).ToArray();
                }
                avatars.Add(avatar);
            }
            ViewCamera = new GameObject("Soldier diagnostic camera").AddComponent<Camera>(); ViewCamera.transform.SetParent(transform, false);
            ViewCamera.fieldOfView = 45; ViewCamera.nearClipPlane = .05f; ViewCamera.farClipPlane = 200;
            ViewCamera.clearFlags = CameraClearFlags.SolidColor; ViewCamera.backgroundColor = new Color(.14f,.18f,.23f); ViewCamera.allowHDR = false;
            var sun = new GameObject("Soldier fixed sun").AddComponent<Light>(); sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(45,135,0); sun.intensity = 1; sun.shadows = LightShadows.Hard;
            sky = GeneratedSky.Apply(transform);
            reflectionProbe = GeneratedReflectionProbe.Apply(transform);
            if (RenderSettings.skybox != null) ViewCamera.clearFlags = CameraClearFlags.Skybox;
            var floor = new GameObject("Numeric ground"); floor.transform.SetParent(transform, false);
            floor.AddComponent<MeshFilter>().sharedMesh = Own(FoundationView.CreateUnitBox()); floor.AddComponent<MeshRenderer>().sharedMaterial = palette[1];
            floor.transform.localPosition = new Vector3(0,-.07f,0); floor.transform.localScale = new Vector3(100,.1f,100);
            floorRoot = floor.transform;
            stepsRoot = new GameObject("Analytic twenty-centimeter steps"); stepsRoot.transform.SetParent(transform,false);
            for(int step=-10;step<80;step++)
            {
                var tile=new GameObject("Numeric step "+step);tile.transform.SetParent(stepsRoot.transform,false);
                tile.AddComponent<MeshFilter>().sharedMesh=floor.GetComponent<MeshFilter>().sharedMesh;
                tile.AddComponent<MeshRenderer>().sharedMaterial=palette[1];
                tile.transform.localScale=new Vector3(20,.2f,.8f);tile.transform.localPosition=new Vector3(0,step*.2f-.1f,(step+.5f)*.8f);
            }
            originTime = Time.time; PoseAt(0,"stand",true); Focus("three-quarter");
            if (Application.isPlaying) Screen.SetResolution(1280,720,FullScreenMode.Windowed);
            Debug.Log("SHOOTER_SOLDIER_GALLERY_READY rigs=" + count + " joints=23 lods=3 bakeRan=" + reflectionProbe.BakeRan);
        }
        private void Update() { if (Animate) { PoseAt(Time.time - originTime, Scenario); Focus("three-quarter"); } }
        public void SetLod(int lod)
        {
            if (lod < 0 || lod > 2) throw new ArgumentOutOfRangeException(nameof(lod));
            // Shared generated meshes are the first three owned objects.
            foreach (var avatar in avatars)
            {
                avatar.Renderer.sharedMesh = (Mesh)owned[lod];
                avatar.Renderer.bones = skinMaps[lod].Select(j=>avatar.Bones[j]).ToArray();
            }
        }
        public void BakeForCapture()
        {
            foreach (var avatar in avatars)
            {
                if (avatar.CaptureMesh == null)
                {
                    avatar.CaptureMesh = Own(new Mesh { name = "Generated pose capture bake" });
                    var item = new GameObject("Explicit Editor pose capture"); item.transform.SetParent(avatar.Root,false);
                    item.AddComponent<MeshFilter>().sharedMesh = avatar.CaptureMesh;
                    avatar.CaptureRenderer = item.AddComponent<MeshRenderer>(); avatar.CaptureRenderer.sharedMaterials = avatar.Renderer.sharedMaterials;
                }
                // Render requests in EditMode do not advance the normal skinning update loop.
                // BakeMesh evaluates the real generated weights/bindposes and current bones on CPU.
                avatar.Renderer.BakeMesh(avatar.CaptureMesh);
                avatar.CaptureRenderer.enabled = true; avatar.Renderer.enabled = false;
            }
        }
        public void PoseAt(float seconds, string scenario, bool reset = false)
        {
            if (seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            switch (scenario)
            {
                case "bind": case "stand": case "walk": case "run": case "crouch": case "aim": case "reload": case "reload-down": case "reload-up": case "slope": case "steps": case "turn": break;
                default: throw new ArgumentException("Unknown soldier scenario.");
            }
            ground.Scenario = scenario;
            floorRoot.localRotation = Quaternion.Euler(scenario == "slope" ? -15 : 0,0,0);
            floorRoot.localPosition = -floorRoot.up*.05f;
            floorRoot.gameObject.SetActive(scenario!="steps");stepsRoot.SetActive(scenario=="steps");
            float speed = scenario == "run" ? 5.5f : scenario == "walk" || scenario == "slope" || scenario == "steps" ? 3.5f : 0;
            float z = seconds * speed;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < avatars.Count; i++)
            {
                var avatar = avatars[i]; float x = avatars.Count == 1 ? 0 : (i % 4 - 1.5f) * 1.6f;
                float dz = z + (avatars.Count == 1 ? 0 : (i / 4) * 2f);
                if (scenario == "bind")
                {
                    avatar.Root.position = new Vector3(x,0,dz);
                    for (int j = 0; j < 23; j++) avatar.Bones[j].SetPositionAndRotation(V(SoldierSkeleton.BindPositions[j]) + new Vector3(x,0,dz), Quaternion.identity);
                    avatar.Weapon.gameObject.SetActive(false); continue;
                }
                avatar.Weapon.gameObject.SetActive(true);
                var input = new SoldierMotionInput(new Float3(x,ground.Height(dz),dz), scenario == "turn" ? seconds : 0,
                    seconds * speed, speed, scenario == "crouch" ? 1 : 0,
                    scenario == "aim" ? .6f : 0,
                    scenario == "reload-down" ? -SoldierMotion.MaxAimPitchRadians : scenario == "reload-up" ? SoldierMotion.MaxAimPitchRadians : scenario == "aim" ? .3f : 0,
                    scenario == "reload" || scenario == "reload-down" || scenario == "reload-up" ? seconds % 1 : -1, reset);
                avatar.Root.position = V(input.Root);
                var pose = avatar.Motion.Evaluate(input, ground);
                for (int j = 0; j < 23; j++) avatar.Bones[j].SetPositionAndRotation(V(pose.WorldPositions[j]), Q(pose.WorldRotations[j]));
                avatar.Weapon.SetPositionAndRotation(V(pose.WeaponPosition),Q(pose.WeaponRotation));
                MaxGripError = Mathf.Max(MaxGripError,Mathf.Max(pose.LeftGripError,pose.RightGripError));
                MaxPlantSlide = Mathf.Max(MaxPlantSlide,pose.PlantSlide); MaxPlantTargetError = Mathf.Max(MaxPlantTargetError,pose.PlantTargetError);
                MinArmBodyClearance = Mathf.Min(MinArmBodyClearance,pose.ArmBodyClearanceMeters);
                MaxEyeSightLineError = Mathf.Max(MaxEyeSightLineError,pose.EyeSightLineErrorMeters);
                MaxStockError = Mathf.Max(MaxStockError,pose.RightStockErrorMeters);
            }
            LastEightRigMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            PoseSequence++;
        }
        public void Focus(string angle)
        {
            var center = avatars[0].Bones[1].position + new Vector3(0,.02f,0);
            if (avatars.Count == 8) center += new Vector3(2.4f,0,1);
            float distance = avatars.Count == 8 ? 10 : 3;
            Vector3 offset = angle == "front" ? new Vector3(0,.3f,distance) : angle == "side" ? new Vector3(distance,.3f,0) : new Vector3(distance*.8f,.5f,distance*.8f);
            ViewCamera.transform.position = center + offset; ViewCamera.transform.LookAt(center);
        }
        private Mesh Upload(MeshData data, bool skin)
        {
            var mesh = Own(new Mesh { name = "Verified generated soldier/weapon" });
            mesh.vertices = data.Positions.Select(V).ToArray(); mesh.normals = data.Normals.Select(V).ToArray();
            mesh.tangents = data.Tangents.Select(t => new Vector4(t.X,t.Y,t.Z,t.W)).ToArray(); mesh.uv = data.Uv.Select(t => new Vector2(t.X,t.Y)).ToArray();
            mesh.subMeshCount = data.Submeshes.Count;
            for (int i = 0; i < data.Submeshes.Count; i++)
            { var sub = data.Submeshes[i]; mesh.SetIndices(data.Indices.Skip((int)sub.FirstIndex).Take((int)sub.IndexCount).Select(v=>(int)v).ToArray(),MeshTopology.Triangles,i); }
            if (skin)
            {
                if (data.Joints == null || data.Weights == null) throw new InvalidDataException("Missing required soldier skin weights.");
                var retained = new SortedSet<int>();
                for (int i=0;i<data.Positions.Count;i++)
                {
                    var j=data.Joints[i];var w=data.Weights[i];
                    if(w.X>0)retained.Add(j.X);if(w.Y>0)retained.Add(j.Y);if(w.Z>0)retained.Add(j.Z);if(w.W>0)retained.Add(j.W);
                }
                int[] map=retained.ToArray();skinMaps.Add(map);
                if(map.Any(j=>j>=23)||map.Length>20)throw new InvalidDataException("Soldier visual palette exceeds bounded semantic mapping.");
                int Compact(int joint,float weight)=>weight==0?0:Array.IndexOf(map,joint);
                var weights = new BoneWeight[data.Positions.Count];
                for (int i = 0; i < weights.Length; i++)
                {
                    var j = data.Joints[i]; var w = data.Weights[i];
                    if (j.X >= 23 || j.Y >= 23 || j.Z >= 23 || j.W >= 23) throw new InvalidDataException("Invalid soldier bone mapping.");
                    weights[i] = new BoneWeight { boneIndex0=Compact(j.X,w.X),boneIndex1=Compact(j.Y,w.Y),boneIndex2=Compact(j.Z,w.Z),boneIndex3=Compact(j.W,w.W),weight0=w.X,weight1=w.Y,weight2=w.Z,weight3=w.W };
                }
                mesh.boneWeights = weights;
                mesh.bindposes = map.Select(j => Matrix4x4.TRS(V(SoldierSkeleton.BindPositions[j]),Quaternion.identity,Vector3.one).inverse).ToArray();
            }
            mesh.RecalculateBounds(); mesh.UploadMeshData(true); return mesh;
        }
        private static Vector3 V(Float3 v) => new Vector3(v.X,v.Y,v.Z);
        private static Quaternion Q(Quat q) => new Quaternion(q.X,q.Y,q.Z,q.W);
        private T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }
        private static void DestroyObject(UnityEngine.Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDestroy()
        {
            reflectionProbe?.Dispose(); reflectionProbe = null;
            sky?.Dispose(); sky = null;
            foreach (var value in owned) if (value != null) DestroyObject(value);
        }
    }
}
