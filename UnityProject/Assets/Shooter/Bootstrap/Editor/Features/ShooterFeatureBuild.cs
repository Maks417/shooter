using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Shooter.Content;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using EntityId = Shooter.Contracts.EntityId;

public static class ShooterFeatureBuild
{
    [Serializable] public sealed class Item { public string recipeId, profile, contentId, manifestSha256, sourceHash, seed; public int vertices, triangles, bytes; public bool reused; }
    [Serializable] public sealed class Socket { public string id; public float[] position, rotation; }
    [Serializable] public sealed class Weapon { public string kind; public int[] lodTriangles; public Socket[] sockets; }
    [Serializable] public sealed class CompileProof
    {
        public string result = "Verified", scope = "M1 P03/P04 diagnostic content; no soldier, combat or hardware gate", profile, toolchainHash, galleryPlanSha256;
        public Item[] content; public Weapon[] weapons; public double elapsedMilliseconds;
        public ShooterBuild.FileProof[] sources;
    }
    [Serializable] public sealed class CaptureProof
    {
        public string result = "Verified", workload = "M1-content-gallery-v1", scope = "Editor visual diagnostic only; not B01-materials-v1 performance acceptance";
        public string[] captures; public long textureBytes, meshBytes; public int trianglesAllLods; public ulong shaderVariants;
        public bool compressedTextures = false, shaderVariantCountAvailable = false;
        public int declaredKeywordCombinations = 25;
    }
    public static void Compile() => Guard(() =>
    {
        ShooterBuild.CheckToolchain(); string root = CacheRoot(), profile = ShooterBuild.Argument("-shooterProfile", "Low");
        if (profile != "Low" && profile != "Balanced" && profile != "High") throw new ArgumentException("Unknown profile.");
        bool force = bool.Parse(ShooterBuild.Argument("-shooterForce", "false"));
        var hash = new ContentId(ShooterBuild.HashFile(ShooterBuild.LockPath)); var files = new CacheFiles(root, hash);
        var nodes = new List<CompilationNode>(); var weaponRecipes = new List<WeaponRecipe>();
        foreach (string kind in new[] { "rifle", "pistol" })
        {
            var recipe = WeaponRecipe.Parse(Source(kind)); weaponRecipes.Add(recipe);
            var validated = new ValidatedRecipe(recipe.Document, recipe.CanonicalSource, new WeaponRecipeValidator());
            nodes.Add(new CompilationNode(validated, "windows64", profile, hash, new WeaponAssetGenerator()));
            nodes.Add(new CompilationNode(validated, "windows64", "gameplay", hash, new WeaponAssetGenerator()));
        }
        var materialRecipes = new List<MaterialRecipe>();
        foreach (string family in GeneratedGallery.Families)
        {
            var recipe = MaterialRecipe.Parse(Source(family)); materialRecipes.Add(recipe);
            nodes.Add(new CompilationNode(new ValidatedRecipe(recipe.Document, recipe.CanonicalSource, new MaterialRecipeValidator()), "windows64", profile, hash, new MaterialAssetGenerator(), new[] { family }));
        }
        var timer = Stopwatch.StartNew();
        var result = new ContentCompiler(files).CompileAsync(nodes, force, CancellationToken.None).GetAwaiter().GetResult();
        if (result.Error != ErrorCode.None) throw new InvalidOperationException(result.Error + ": " + result.Diagnostic);
        Item[] items = result.Entries.Select(e => new Item { recipeId = e.Manifest.RecipeId, profile = e.Manifest.VisualProfile, contentId = e.Manifest.ContentId.ToString(), manifestSha256 = ContentIdentity.Hash(e.Manifest.Encode()).ToString(), sourceHash = e.Manifest.SourceHash.ToString(), seed = e.Manifest.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture), vertices = e.Manifest.VertexCount, triangles = e.Manifest.TriangleCount, bytes = e.Manifest.Artifacts.Sum(a => a.Bytes), reused = e.Reused }).ToArray();
        string Id(string recipe, string tier) => items.Single(e => e.recipeId == recipe && e.profile == tier).contentId;
        string materials = string.Join(",", materialRecipes.Select(r => "{\"family\":\"" + r.Family + "\",\"tileMm\":" + r.TileMm + ",\"contentId\":\"" + Id(r.Document.RecipeId.ToString(), profile) + "\"}"));
        string plan = "{\"schema\":\"shooter.gallery.v1\",\"toolchainHash\":\"" + hash + "\",\"profile\":\"" + profile + "\",\"materials\":[" + materials + "],\"rifleVisual\":\"" + Id("weapons/rifle", profile) + "\",\"pistolVisual\":\"" + Id("weapons/pistol", profile) + "\",\"rifleGameplay\":\"" + Id("weapons/rifle", "gameplay") + "\",\"pistolGameplay\":\"" + Id("weapons/pistol", "gameplay") + "\"}";
        byte[] planBytes = CanonicalJson.Encode(Encoding.UTF8.GetBytes(plan));
        string target = Path.Combine(root, "gallery.json"), temporary = Path.Combine(root, ".gallery-" + Guid.NewGuid().ToString("N") + ".tmp");
        files.AssertSafePath(temporary); files.AssertSafePath(target);
        using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(planBytes, 0, planBytes.Length); file.Flush(true); }
        if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
        timer.Stop();
        var weapons = weaponRecipes.Select(r => new Weapon { kind = r.Kind, lodTriangles = WeaponGeometry.Build(r).Lods.Select(m => m.Indices.Count / 3).ToArray(), sockets = WeaponGeometry.Sockets(r).Select(s => new Socket { id = s.Id, position = new[] { s.Position.X, s.Position.Y, s.Position.Z }, rotation = new[] { s.Rotation.X, s.Rotation.Y, s.Rotation.Z, s.Rotation.W } }).ToArray() }).ToArray();
        var sources = Directory.GetFiles(Path.Combine(Application.dataPath, "Shooter"), "*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".cs", StringComparison.Ordinal) || p.EndsWith(".shader", StringComparison.Ordinal) || p.Replace('\\', '/').Contains("/FeatureRecipes/") && p.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => new ShooterBuild.FileProof { path = "UnityProject/" + p.Substring(Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Length + 1).Replace('\\', '/'), sha256 = ShooterBuild.HashFile(p) }).ToArray();
        ShooterBuild.WriteJson(ShooterBuild.Argument("-shooterReport"), new CompileProof { profile = profile, toolchainHash = hash.ToString(), content = items, weapons = weapons, elapsedMilliseconds = timer.Elapsed.TotalMilliseconds, galleryPlanSha256 = ContentIdentity.Hash(planBytes).ToString(), sources = sources });
        Debug.Log("SHOOTER_FEATURE_CONTENT_VERIFIED content=" + items.Length);
    });
    public static void Capture() => Guard(() =>
    {
        ShooterBuild.CheckToolchain(); string root = CacheRoot(), output = Path.GetFullPath(ShooterBuild.Argument("-shooterCaptureRoot"));
        RequireArtifacts(output); Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        QualitySettings.SetQualityLevel(0, true);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shooter/Shaders/ProceduralSurface.shader");
        if (shader == null || ShaderUtil.GetShaderMessages(shader).Any(m => m.severity.ToString() == "Error")) throw new InvalidOperationException("Procedural shader compile errors.");
        var gallery = GeneratedGallery.Create(root, new ContentId(ShooterBuild.HashFile(ShooterBuild.LockPath)), shader);
        try
        {
            var paths = new List<string>();
            void Shot(string name)
            {
                string file = Path.Combine(output, name + ".png"); Render(gallery.ViewCamera, file); paths.Add(file);
            }
            Shot("gallery");
            for (int material = 0; material < 7; material++) { gallery.FocusMaterial(material); Shot("material-" + GeneratedGallery.Families[material]); }
            for (int weapon = 0; weapon < 2; weapon++)
            {
                string kind = weapon == 0 ? "rifle" : "pistol";
                foreach (string angle in new[] { "front", "side", "three-quarter", "ads" }) { gallery.FocusWeapon(weapon, 0, angle); Shot(kind + "-" + angle); }
                for (int lod = 1; lod < 4; lod++) { gallery.FocusWeapon(weapon, lod, "side"); Shot(kind + "-lod" + lod); }
            }
            if (ShaderUtil.GetShaderMessages(shader).Any(m => m.severity.ToString() == "Error")) throw new InvalidOperationException("Procedural shader failed during rendering.");
            ShooterBuild.WriteJson(ShooterBuild.Argument("-shooterReport"), new CaptureProof { captures = paths.ToArray(), textureBytes = gallery.TextureBytes, meshBytes = gallery.MeshBytes, trianglesAllLods = gallery.Triangles, shaderVariants = 0 });
        }
        finally { UnityEngine.Object.DestroyImmediate(gallery.gameObject); }
    });
    // Offline source visual diagnostic with the same URP material adapter as the product.
    // Captures are not first-person gameplay, packaged-player or performance acceptance.
    public static void CaptureArsenal() => Guard(() =>
    {
        ShooterBuild.CheckToolchain();
        string output = Path.GetFullPath(ShooterBuild.Argument("-shooterCaptureRoot"));
        RequireArtifacts(output); Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        QualitySettings.SetQualityLevel(0, true);
        var owned = new List<UnityEngine.Object>();
        try
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Required URP Lit unavailable.");
            Material Surface(int family)
            {
                var bake = Shooter.Generation.Environment.FirstPersonSurfaceBaker.Bake(family);
                return Shooter.Presentation.Rendering.ProductSurfaceMaterial.Create(shader, bake.Albedo, bake.Normal, bake.Mask,
                    "Arsenal source finish " + family, 10, value => owned.Add(value));
            }
            // ADR-102: seven-slot palette (wood and gold tints) so the named guns render with their furniture.
            // ADR-111: knife and grenade finishes in slots 7-10.
            var palette = Shooter.Presentation.Rendering.ProductSurfaceMaterial.WeaponFinishes(Surface(0), Surface(1), Surface(4), Surface(5), Surface(6),
                Surface(7), Surface(8), Surface(9), value => owned.Add(value));
            var cameraObject = new GameObject("Arsenal capture camera"); owned.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f, .09f, .11f);
            camera.nearClipPlane = .005f; camera.farClipPlane = 10; camera.fieldOfView = 32;
            var lightObject = new GameObject("Arsenal key"); owned.Add(lightObject);
            var key = lightObject.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 2.0f;
            key.transform.rotation = Quaternion.Euler(35, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.32f, .36f, .42f);
            var paths = new List<string>();
            string[] arsenal = { "ak47", "ak102", "m16", "mk14", "mtac338", "g17", "golden-eagle", "m4a1", "m24", "mk16", "m870" };
            foreach (string kind in new[] { "rifle", "pistol", "knife", "grenade" }.Concat(arsenal))
            {
                var root = new GameObject(kind); owned.Add(root);
                var parts = new List<MeshData>();
                if (kind == "rifle" || kind == "pistol" || arsenal.Contains(kind))
                {
                    var build = WeaponGeometry.Build(WeaponRecipe.Parse(Source(kind)));
                    parts.Add(build.Lods[0]); parts.Add(build.Magazine); parts.Add(build.CyclePart);
                }
                else parts.Add(kind == "knife" ? LoadoutPropGeometry.BuildKnife(0) : LoadoutPropGeometry.BuildGrenade(0));
                foreach (var data in parts)
                {
                    var mesh = new Mesh(); owned.Add(mesh);
                    mesh.vertices = data.Positions.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
                    mesh.normals = data.Normals.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
                    mesh.tangents = data.Tangents.Select(v => new Vector4(v.X, v.Y, v.Z, v.W)).ToArray();
                    mesh.uv = data.Uv.Select(v => new Vector2(v.X, v.Y)).ToArray();
                    mesh.subMeshCount = data.Submeshes.Count;
                    for (int i = 0; i < data.Submeshes.Count; i++)
                    {
                        var sub = data.Submeshes[i];
                        mesh.SetIndices(data.Indices.Skip((int)sub.FirstIndex).Take((int)sub.IndexCount).Select(v => (int)v).ToArray(), MeshTopology.Triangles, i);
                    }
                    mesh.RecalculateBounds();
                    var child = new GameObject("Source part"); child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    child.AddComponent<MeshRenderer>().sharedMaterials = data.Submeshes.Select(sub => palette[sub.MaterialSlot]).ToArray();
                }
                var renderers = root.GetComponentsInChildren<MeshRenderer>(); var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float distance = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) * 2.2f;
                foreach (string angle in new[] { "side", "three-quarter" })
                {
                    camera.transform.position = bounds.center + (angle == "side" ? Vector3.right : new Vector3(.8f, .35f, -.7f).normalized) * distance;
                    camera.transform.LookAt(bounds.center);
                    string path = Path.Combine(output, kind + "-" + angle + ".png"); Render(camera, path); paths.Add(path);
                }
                root.SetActive(false);
            }
            File.WriteAllText(Path.Combine(output, "scope.txt"), "Offline source LOD0 arsenal diagnostic, URP Lit, 1280x720. Not gameplay/visual acceptance. geometry.weapon " + WeaponRecipe.GeneratorVersion + " source " + ShooterBuild.HashFile(Path.Combine(Application.dataPath, "Shooter/Generation/Core/Weapons/WeaponGeometry.cs")));
            Debug.Log("SHOOTER_ARSENAL_CAPTURE files=" + paths.Count);
        }
        finally { foreach (var value in owned) if (value != null) UnityEngine.Object.DestroyImmediate(value); }
    });

    // ADR-106 offline source diagnostic: the product soldier (soldier.loft 1.0.3 and 1.1.0) in bind pose with flat
    // stand-in colours. Not packaged-player, skinning, material or P05 visual acceptance.
    public static void CaptureSoldierKit() => Guard(() =>
    {
        ShooterBuild.CheckToolchain();
        string output = Path.GetFullPath(ShooterBuild.Argument("-shooterCaptureRoot"));
        RequireArtifacts(output); Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        QualitySettings.SetQualityLevel(0, true); // same as the other captures, so ProjectSettings are not rewritten
        var owned = new List<UnityEngine.Object>();
        try
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Required URP Lit unavailable.");
            Material Flat(Color color, float smoothness)
            { var m = new Material(shader); m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smoothness); owned.Add(m); return m; }
            var palette = new[] { Flat(new Color(.30f, .33f, .22f), .15f), Flat(new Color(.36f, .31f, .23f), .25f), Flat(new Color(.62f, .48f, .38f), .3f),
                Flat(new Color(.08f, .09f, .10f), .85f), Flat(new Color(.55f, .12f, .10f), .2f), Flat(new Color(.20f, .25f, .45f), .2f) };
            var cameraObject = new GameObject("Soldier capture camera"); owned.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .20f, .25f);
            camera.nearClipPlane = .01f; camera.farClipPlane = 20; camera.fieldOfView = 30;
            var lightObject = new GameObject("Soldier key"); owned.Add(lightObject);
            var key = lightObject.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.8f; key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(40, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.42f, .46f, .52f);
            RenderSettings.ambientEquatorColor = new Color(.30f, .30f, .30f); RenderSettings.ambientGroundColor = new Color(.18f, .16f, .14f);
            var paths = new List<string>();
            foreach (string version in new[] { SoldierRecipe.GeneratorVersion, SoldierRecipe.ProfileVersion })
            {
                var recipe = SoldierRecipe.Parse(Encoding.ASCII.GetBytes("{\"schemaId\":\"shooter.soldier.v1\",\"recipeId\":\"soldiers/baseline\",\"generatorId\":\"soldier.loft\",\"generatorVersion\":\"" +
                    version + "\",\"seed\":\"460072030\",\"parameters\":" + SoldierRecipe.ParametersJson + ",\"dependencies\":[]}"));
                var build = SoldierGeometry.Build(recipe);
                for (int lod = 0; lod < 3; lod++)
                {
                    var data = build.Lods[lod];
                    var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; owned.Add(mesh);
                    mesh.vertices = data.Positions.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
                    mesh.normals = data.Normals.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
                    mesh.tangents = data.Tangents.Select(v => new Vector4(v.X, v.Y, v.Z, v.W)).ToArray();
                    mesh.uv = data.Uv.Select(v => new Vector2(v.X, v.Y)).ToArray();
                    mesh.subMeshCount = data.Submeshes.Count;
                    for (int i = 0; i < data.Submeshes.Count; i++)
                    {
                        var sub = data.Submeshes[i];
                        mesh.SetIndices(data.Indices.Skip((int)sub.FirstIndex).Take((int)sub.IndexCount).Select(v => (int)v).ToArray(), MeshTopology.Triangles, i);
                    }
                    mesh.RecalculateBounds();
                    var root = new GameObject("Soldier " + version + " lod" + lod); owned.Add(root);
                    root.AddComponent<MeshFilter>().sharedMesh = mesh;
                    root.AddComponent<MeshRenderer>().sharedMaterials = data.Submeshes.Select(sub => palette[Math.Min(5, (int)sub.MaterialSlot)]).ToArray();
                    var shots = lod == 0 ? new[] { "front", "three-quarter", "back", "head" } : new[] { "three-quarter" };
                    foreach (string angle in shots)
                    {
                        Vector3 target = angle == "head" ? new Vector3(0, 1.62f, 0) : new Vector3(0, .95f, 0);
                        Vector3 direction = angle == "front" ? Vector3.forward : angle == "back" ? Vector3.back : new Vector3(.75f, .25f, .85f).normalized;
                        camera.transform.position = target + direction * (angle == "head" ? 1.0f : 4.4f);
                        camera.transform.LookAt(target);
                        string path = Path.Combine(output, "soldier-" + version + "-lod" + lod + "-" + angle + ".png"); Render(camera, path); paths.Add(path);
                    }
                    root.SetActive(false);
                }
            }
            Debug.Log("SHOOTER_SOLDIER_KIT_CAPTURE files=" + paths.Count);
        }
        finally { foreach (var value in owned) if (value != null) UnityEngine.Object.DestroyImmediate(value); }
    });

    // Editor-only data fixture: no authority/fake adapter can ship from this assembly.
    private sealed class HandCaptureLifecycle : Shooter.Game.Contracts.IActorLifecycle, Shooter.Game.Session.IActorStateReader
    {
        public Shooter.Game.Contracts.ActorLifecycleState State;
        public int ActiveActorCount=>1;
        public EntityId SpawnLife(Shooter.Game.Contracts.ParticipantId p,Shooter.Game.Contracts.LifeId l,Float3 f,float y,byte t)=>default;
        public void DespawnLife(EntityId e) { }
        public void ResetLife(EntityId e,Float3 f,float y) { }
        public bool TryGetActorState(EntityId e,out Shooter.Game.Contracts.ActorLifecycleState state) { state=State;return e.Equals(State.EntityId); }
    }
    public static void CaptureHandling() => Guard(() =>
    {
        ShooterBuild.CheckToolchain();
        string output=Path.GetFullPath(ShooterBuild.Argument("-shooterCaptureRoot")); RequireArtifacts(output); Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); QualitySettings.SetQualityLevel(0,true);
        var root=new GameObject("Handling source diagnostic");
        GeneratedSky sky=null;
        try
        {
            var camera=new GameObject("Diagnostic camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform,false);
            // The product camera: default hip FOV 80 (vertical), near plane 0.05 (ProductSessionView).
            camera.fieldOfView=80; camera.nearClipPlane=.05f; camera.farClipPlane=10;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.16f,.20f,.25f);
            var key=new GameObject("Diagnostic sun").AddComponent<Light>(); key.transform.SetParent(root.transform,false);
            key.type=LightType.Directional;key.intensity=1.5f;key.color=new Color(1,.94f,.84f);key.transform.rotation=Quaternion.Euler(44,-32,0);
            sky=GeneratedSky.Apply(root.transform,false);
            var hash=new ContentId(ShooterBuild.HashFile(ShooterBuild.LockPath));
            var payloads=Shooter.Generation.Environment.PackagedVisualPayloads.FromBuild(Shooter.Generation.Environment.MapPackageBuilder.Build("training-compound","low",hash));
            var presentation=Shooter.Presentation.Rig.ProductActorPresentation.Create(root.transform,camera); presentation.BindPackagedVisuals(payloads);
            var lifecycle=new HandCaptureLifecycle();
            for(byte slot=0;slot<4;slot++)
            {
                var id=new EntityId((ulong)slot+1);
                lifecycle.State=new Shooter.Game.Contracts.ActorLifecycleState(id,1,true,100,default,0,0,slot,30,90);
                var snapshot=new Shooter.Game.Contracts.MatchViewSnapshot(1,10,10,53990,Shooter.Game.Contracts.GameMatchPhase.Playing,
                    Shooter.Game.Contracts.MatchOutcome.None,Shooter.Game.Contracts.FinishReason.None,0,0,
                    new[]{new Shooter.Game.Contracts.ParticipantState(new Shooter.Game.Contracts.ParticipantId(1),"Diagnostic",1,true,id,new Shooter.Game.Contracts.LifeId((uint)slot+1),true,0,0,0)},0);
                presentation.Sync(snapshot,lifecycle,null,0,id,true,0,Array.Empty<CombatEvent>(),10);
                string name=new[]{"rifle","pistol","knife","grenade"}[slot];
                RenderHandPose(camera,Path.Combine(output,name+"-ready.png"));
                // ADR-129 review: the same pose from beside and below the view, to judge arm placement and grips.
                RenderHandPose(camera,Path.Combine(output,name+"-ready-side.png"),new Vector3(-.75f,-.05f,.45f));
                RenderHandPose(camera,Path.Combine(output,name+"-ready-right.png"),new Vector3(.75f,-.15f,.45f));
                if(slot<2)RenderHandPose(camera,Path.Combine(output,name+"-grip.png"),new Vector3(.42f,-.2f,.32f),new Vector3(.06f,-.2f,.34f));
                if(slot>=2)
                {
                    // ADR-111: one-handed knife/grenade rest after the aim blend and idle layers settle (no aim held).
                    for(int settle=0;settle<8;settle++) presentation.Sync(snapshot,lifecycle,null,0,id,false,.1f,Array.Empty<CombatEvent>(),(uint)(11+settle));
                    RenderHandPose(camera,Path.Combine(output,name+"-hold.png"));
                }
                if(slot<2)
                {
                    var reload=new CombatEvent(new EventId(1,10,id,1),CombatEventKind.ReloadStarted,id,default,new ActionPayload(slot,10,110));
                    presentation.Sync(snapshot,lifecycle,null,0,id,true,0,new[]{reload},24); RenderHandPose(camera,Path.Combine(output,name+"-reach.png"));
                    presentation.Sync(snapshot,lifecycle,null,0,id,true,0,Array.Empty<CombatEvent>(),52); RenderHandPose(camera,Path.Combine(output,name+"-extract.png"));
                    presentation.Sync(snapshot,lifecycle,null,0,id,true,0,Array.Empty<CombatEvent>(),86); RenderHandPose(camera,Path.Combine(output,name+"-seat.png"));
                    presentation.Sync(snapshot,lifecycle,null,0,id,true,0,Array.Empty<CombatEvent>(),110);
                    // ADR-099 empty reload: the fresh magazine on its way up, then the bolt-catch press / slide release.
                    lifecycle.State=new Shooter.Game.Contracts.ActorLifecycleState(id,1,true,100,default,0,0,slot,0,90);
                    var empty=new CombatEvent(new EventId(1,120,id,2),CombatEventKind.ReloadStarted,id,default,new ActionPayload(slot,120,220));
                    presentation.Sync(snapshot,lifecycle,null,0,id,true,0,new[]{empty},176); RenderHandPose(camera,Path.Combine(output,name+"-fresh.png"));
                    presentation.Sync(snapshot,lifecycle,null,0,id,true,0,Array.Empty<CombatEvent>(),199); RenderHandPose(camera,Path.Combine(output,name+"-release.png"));
                    presentation.Sync(snapshot,lifecycle,null,0,id,true,0,Array.Empty<CombatEvent>(),220);
                    // Sprint carry (FirstPersonLocomotionPose), settled, from the view and from the right.
                    lifecycle.State=new Shooter.Game.Contracts.ActorLifecycleState(id,1,true,100,default,0,0,slot,30,90);
                    Func<EntityId,Shooter.Game.Session.LocalMovementView?> sprinting=_=>new Shooter.Game.Session.LocalMovementView(Stance.Standing,1.6f,false,1f,6f,2f,true);
                    for(int step=0;step<12;step++) presentation.Sync(snapshot,lifecycle,null,0,id,false,.05f,Array.Empty<CombatEvent>(),(uint)(260+step),sprinting);
                    RenderHandPose(camera,Path.Combine(output,name+"-sprint.png"));
                    RenderHandPose(camera,Path.Combine(output,name+"-sprint-right.png"),new Vector3(.75f,-.15f,.45f));
                    for(int step=0;step<12;step++) presentation.Sync(snapshot,lifecycle,null,0,id,false,.05f,Array.Empty<CombatEvent>(),(uint)(280+step));
                }
                else if(slot==2)
                {
                    var swing=new CombatEvent(new EventId(1,20,id,1),CombatEventKind.ShotAccepted,id,default,
                        new ShotPayload(new DefinitionId("weapons/knife"),1,default,new Float3(0,0,1),1,1));
                    presentation.Sync(snapshot,lifecycle,null,0,id,false,0,new[]{swing},20);
                    presentation.Sync(snapshot,lifecycle,null,0,id,false,.12f,Array.Empty<CombatEvent>(),27); RenderHandPose(camera,Path.Combine(output,"knife-slash.png"));
                }
                else
                {
                    lifecycle.State=new Shooter.Game.Contracts.ActorLifecycleState(id,1,true,100,default,0,0,slot,1,0,WeaponAction.Cooking);
                    presentation.Sync(snapshot,lifecycle,null,0,id,false,.14f,Array.Empty<CombatEvent>(),20); RenderHandPose(camera,Path.Combine(output,"grenade-pin.png"));
                    presentation.Sync(snapshot,lifecycle,null,0,id,false,.5f,Array.Empty<CombatEvent>(),50);
                    lifecycle.State=new Shooter.Game.Contracts.ActorLifecycleState(id,1,true,100,default,0,0,0,30,90,WeaponAction.Equipping);
                    var spawn=new CombatEvent(new EventId(1,60,id,1),CombatEventKind.ProjectileSpawned,id,default,
                        new ProjectilePayload(new EntityId(400),new Float3(0,1,0),new Float3(0,2,10),new DefinitionId("weapons/grenade")));
                    presentation.Sync(snapshot,lifecycle,null,0,id,false,0,new[]{spawn},60);
                    presentation.Sync(snapshot,lifecycle,null,0,id,false,.06f,Array.Empty<CombatEvent>(),64); RenderHandPose(camera,Path.Combine(output,"grenade-throw.png"));
                }
            }
            // The loadout sidearms (the legacy slot-1 pistol above is not what players carry).
            foreach(string sidearm in new[]{"g17","golden-eagle"})
            {
                var id=new EntityId(10+(ulong)sidearm.Length);
                lifecycle.State=new Shooter.Game.Contracts.ActorLifecycleState(id,1,true,100,default,0,0,1,7,35,WeaponAction.Ready,new DefinitionId("weapons/"+sidearm));
                var snapshot=new Shooter.Game.Contracts.MatchViewSnapshot(1,10,10,53990,Shooter.Game.Contracts.GameMatchPhase.Playing,
                    Shooter.Game.Contracts.MatchOutcome.None,Shooter.Game.Contracts.FinishReason.None,0,0,
                    new[]{new Shooter.Game.Contracts.ParticipantState(new Shooter.Game.Contracts.ParticipantId(1),"Diagnostic",1,true,id,new Shooter.Game.Contracts.LifeId(20+(uint)sidearm.Length),true,0,0,0)},0);
                for(int settle=0;settle<10;settle++) presentation.Sync(snapshot,lifecycle,null,0,id,false,.1f,Array.Empty<CombatEvent>(),(uint)(400+settle));
                RenderHandPose(camera,Path.Combine(output,sidearm+"-ready.png"));
                RenderHandPose(camera,Path.Combine(output,sidearm+"-ready-right.png"),new Vector3(.75f,-.15f,.45f));
                foreach(var r in camera.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy))
                {
                    var b=r.bounds; var c=camera.transform.InverseTransformPoint(b.center);
                    Debug.Log($"SHOOTER_FP_BOUNDS {sidearm} {r.name} centre=({c.x:F3},{c.y:F3},{c.z:F3}) size={b.size.magnitude:F3}");
                }
                var reload=new CombatEvent(new EventId(1,410,id,1),CombatEventKind.ReloadStarted,id,default,new ActionPayload(1,410,510));
                presentation.Sync(snapshot,lifecycle,null,0,id,false,.016f,new[]{reload},411);
                foreach(int beat in new[]{10,20,30,45,58,75,90})
                {
                    presentation.Sync(snapshot,lifecycle,null,0,id,false,.016f,Array.Empty<CombatEvent>(),(uint)(410+beat));
                    RenderHandPose(camera,Path.Combine(output,$"{sidearm}-reload-{beat:00}.png"));
                }
                presentation.Sync(snapshot,lifecycle,null,0,id,false,.016f,Array.Empty<CombatEvent>(),520);
                // Empty reload: the overhand slide rack after the seat.
                lifecycle.State=new Shooter.Game.Contracts.ActorLifecycleState(id,1,true,100,default,0,0,1,0,35,WeaponAction.Ready,new DefinitionId("weapons/"+sidearm));
                var empty=new CombatEvent(new EventId(1,530,id,2),CombatEventKind.ReloadStarted,id,default,new ActionPayload(1,530,630));
                presentation.Sync(snapshot,lifecycle,null,0,id,false,.016f,new[]{empty},531);
                foreach(int beat in new[]{72,76,80})
                {
                    presentation.Sync(snapshot,lifecycle,null,0,id,false,.016f,Array.Empty<CombatEvent>(),(uint)(530+beat));
                    RenderHandPose(camera,Path.Combine(output,$"{sidearm}-rack-{beat:00}.png"));
                }
                presentation.Sync(snapshot,lifecycle,null,0,id,false,.016f,Array.Empty<CombatEvent>(),640);
            }
            File.WriteAllText(Path.Combine(output,"scope.txt"),"Editor packaged Low visual diagnostic; fixed camera and copied authority/action fixtures, no gameplay acceptance. fp.hand.v2 / geometry.weapon " + WeaponRecipe.GeneratorVersion + " / ADR-099 choreography");
            Debug.Log("SHOOTER_HANDLING_CAPTURE files=29");
        }
        finally { sky?.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
    });

    private static void RenderHandPose(Camera camera,string path,Vector3? sideOffset=null,Vector3? sideFocus=null)
    {
        // Synchronous Editor render requests can retain skin matrices from the previous pose.
        // Bake the actual current bones, as the soldier diagnostic already does.
        // Only what is drawn: hidden gloves (ADR-129 licensed arms) and a hidden support arm stay hidden.
        var sources=camera.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled).ToArray();
        var proxies=new List<GameObject>();var meshes=new List<Mesh>();
        try
        {
            foreach(var source in sources)
            {
                // BakeMesh without scale is in the renderer's rotated, unscaled frame: imported bodies carry FBX scale 100.
                var mesh=new Mesh();source.BakeMesh(mesh);meshes.Add(mesh);
                var proxy=new GameObject("Baked diagnostic hand");proxy.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);proxies.Add(proxy);
                proxy.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=proxy.AddComponent<MeshRenderer>();renderer.sharedMaterials=source.sharedMaterials;renderer.shadowCastingMode=source.shadowCastingMode;
                source.enabled=false;
            }
            if(sideOffset.HasValue)
            {
                // A second camera beside the view, looking at the view model; the view camera is drawn as nothing.
                var side=new GameObject("Side review camera").AddComponent<Camera>();
                try
                {
                    side.CopyFrom(camera);side.nearClipPlane=.01f;side.fieldOfView=45;
                    var focus=camera.transform.TransformPoint(sideFocus??new Vector3(.05f,-.15f,.3f));
                    side.transform.position=camera.transform.TransformPoint(sideOffset.Value);side.transform.LookAt(focus,camera.transform.up);
                    Render(side,path);
                }
                finally{UnityEngine.Object.DestroyImmediate(side.gameObject);}
            }
            else Render(camera,path);
        }
        finally
        {
            foreach(var source in sources)source.enabled=true;
            foreach(var proxy in proxies)UnityEngine.Object.DestroyImmediate(proxy);
            foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    internal static void Render(Camera camera, string path)
    {
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create();
        var prior = RenderTexture.active; Texture2D pixels = null;
        try
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            RenderPipeline.SubmitRenderRequest(camera, request); RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target; pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            var colors = pixels.GetPixels32(); int pink = 0;
            foreach (var c in colors) if (c.r > 220 && c.b > 220 && c.g < 40) pink++;
            if (pink > colors.Length / 100) throw new InvalidOperationException("Magenta shader error detected in capture.");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = prior; if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }
    private static byte[] Source(string name) => File.ReadAllBytes(Path.Combine(Application.dataPath, "Shooter/Generation/FeatureRecipes/" + name + ".json"));
    private static string CacheRoot() { string path = Path.GetFullPath(ShooterBuild.Argument("-shooterCacheRoot")); RequireArtifacts(path); return path; }
    private static void RequireArtifacts(string path)
    { if (!path.StartsWith(Path.Combine(ShooterBuild.RepositoryRoot, "Artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output must stay under repository Artifacts."); }
    private static void Guard(Action body) { try { body(); } catch (Exception error) { Debug.LogError("SHOOTER_FAILURE: " + error); EditorApplication.Exit(1); } }
}
