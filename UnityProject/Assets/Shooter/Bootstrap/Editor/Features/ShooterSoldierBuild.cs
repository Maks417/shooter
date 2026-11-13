using System;
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

public static class ShooterSoldierBuild
{
    [Serializable] public sealed class Proof
    {
        public string result = "Verified", scope = "M1 helmeted soldier spike; no hit evaluator or performance/visual acceptance", toolchainHash, planSha256;
        public int[] triangles, vertices, activeBones;
        public int semanticJoints = 23;
        public double compileMilliseconds;
        public ShooterFeatureBuild.Item[] content;
        public string[] captures;
        public float maxGripError, maxPlantSlide, maxPlantTargetError;
        public string frameBaseline = SoldierSkeleton.Version;
        public ShooterBuild.FileProof[] sources;
    }
    public static void Compile() => Guard(() =>
    {
        ShooterBuild.CheckToolchain(); string root = CacheRoot();
        var hash = new ContentId(ShooterBuild.HashFile(ShooterBuild.LockPath)); var files = new CacheFiles(root,hash);
        var soldier = SoldierRecipe.Parse(File.ReadAllBytes(Path.Combine(Application.dataPath,"Shooter/Generation/FeatureRecipes/soldier.json")));
        var rifle = WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(Application.dataPath,"Shooter/Generation/FeatureRecipes/rifle.json")));
        var source = new ValidatedRecipe(soldier.Document,soldier.CanonicalSource,new SoldierRecipeValidator());
        var nodes = new[] { new CompilationNode(source,"windows64","Low",hash,new SoldierAssetGenerator(),new[]{"equipment","goggles","skin","uniform"}),
            new CompilationNode(new ValidatedRecipe(rifle.Document,rifle.CanonicalSource,new WeaponRecipeValidator()),"windows64","Low",hash,new WeaponAssetGenerator()) };
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = new ContentCompiler(files).CompileAsync(nodes,bool.Parse(ShooterBuild.Argument("-shooterForce","false")),CancellationToken.None).GetAwaiter().GetResult();
        watch.Stop();
        if (result.Error != ErrorCode.None) throw new InvalidOperationException(result.Diagnostic);
        var items = result.Entries.Select(e=>new ShooterFeatureBuild.Item { recipeId=e.Manifest.RecipeId,profile=e.Manifest.VisualProfile,contentId=e.Manifest.ContentId.ToString(),
            manifestSha256=ContentIdentity.Hash(e.Manifest.Encode()).ToString(),sourceHash=e.Manifest.SourceHash.ToString(),seed=e.Manifest.Seed.ToString(),
            vertices=e.Manifest.VertexCount,triangles=e.Manifest.TriangleCount,bytes=e.Manifest.Artifacts.Sum(a=>a.Bytes),reused=e.Reused }).ToArray();
        string Id(string recipe,string profile) => items.Single(i=>i.recipeId==recipe && i.profile==profile).contentId;
        byte[] plan = CanonicalJson.Encode(Encoding.ASCII.GetBytes("{\"schema\":\"shooter.soldier-gallery.v1\",\"toolchainHash\":\""+hash+"\",\"soldierVisual\":\""+Id("soldiers/baseline","Low")+"\",\"rifleVisual\":\""+Id("weapons/rifle","Low")+"\"}"));
        string target=Path.Combine(root,"soldier-gallery.json"),temporary=Path.Combine(root,".soldier-"+Guid.NewGuid().ToString("N")+".tmp");
        files.AssertSafePath(target);files.AssertSafePath(temporary);
        using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(plan,0,plan.Length);file.Flush(true);}
        if(File.Exists(target))File.Replace(temporary,target,null);else File.Move(temporary,target);
        var build = SoldierGeometry.Build(soldier);
        var sources=Directory.GetFiles(Path.Combine(Application.dataPath,"Shooter"),"*",SearchOption.AllDirectories)
            .Where(p=>p.EndsWith(".cs",StringComparison.Ordinal)||p.EndsWith(".shader",StringComparison.Ordinal)||p.Replace('\\','/').Contains("/FeatureRecipes/")&&p.EndsWith(".json",StringComparison.Ordinal))
            .OrderBy(p=>p,StringComparer.Ordinal).Select(p=>new ShooterBuild.FileProof{path="UnityProject/"+p.Substring(Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Length+1).Replace('\\','/'),sha256=ShooterBuild.HashFile(p)}).ToArray();
        ShooterBuild.WriteJson(ShooterBuild.Argument("-shooterReport"),new Proof{toolchainHash=hash.ToString(),planSha256=ContentIdentity.Hash(plan).ToString(),content=items,
            compileMilliseconds=watch.Elapsed.TotalMilliseconds,triangles=build.Lods.Select(m=>m.Indices.Count/3).ToArray(),vertices=build.Lods.Select(m=>m.Positions.Count).ToArray(),
            activeBones=build.Lods.Select(m=>m.Joints.SelectMany((j,i)=>new[]{(j.X,m.Weights[i].X),(j.Y,m.Weights[i].Y),(j.Z,m.Weights[i].Z),(j.W,m.Weights[i].W)}).Where(w=>w.Item2>0).Select(w=>w.Item1).Distinct().Count()).ToArray(),sources=sources});
    });
    public static void Capture() => Guard(() =>
    {
        ShooterBuild.CheckToolchain(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);QualitySettings.SetQualityLevel(0,true);
        string output=Path.GetFullPath(ShooterBuild.Argument("-shooterCaptureRoot")); RequireArtifacts(output);Directory.CreateDirectory(output);
        var view=SoldierGallery.Create(CacheRoot(),new ContentId(ShooterBuild.HashFile(ShooterBuild.LockPath))); view.Animate=false;
        try
        {
            var paths=new System.Collections.Generic.List<string>();
            void Shot(string label){view.BakeForCapture();string path=Path.Combine(output,label+".png");ShooterFeatureBuild.Render(view.ViewCamera,path);paths.Add(path);}
            foreach(string scenario in new[]{"bind","stand","crouch","aim","reload","walk","run","slope","steps","turn"})
            {
                // Evaluate a consecutive fixed60Hz path, so planted-foot state is exercised rather than rebuilt per image.
                for(int tick=0;tick<=36;tick++)view.PoseAt(tick/60f,scenario,tick==0);
                foreach(string angle in new[]{"front","side","three-quarter"}){view.Focus(angle);Shot(scenario+"-"+angle);}
            }
            for(int lod=1;lod<3;lod++){view.SetLod(lod);view.PoseAt(0,"stand",true);view.Focus("three-quarter");Shot("soldier-lod"+lod);}
            ShooterBuild.WriteJson(ShooterBuild.Argument("-shooterReport"),new Proof{toolchainHash=ShooterBuild.HashFile(ShooterBuild.LockPath),captures=paths.ToArray(),maxGripError=view.MaxGripError,maxPlantSlide=view.MaxPlantSlide,maxPlantTargetError=view.MaxPlantTargetError});
        }
        finally{UnityEngine.Object.DestroyImmediate(view.gameObject);}
    });
    private static string CacheRoot(){string root=Path.GetFullPath(ShooterBuild.Argument("-shooterCacheRoot"));RequireArtifacts(root);return root;}
    private static void RequireArtifacts(string path){if(!path.StartsWith(Path.Combine(ShooterBuild.RepositoryRoot,"Artifacts")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Output must remain under Artifacts.");}
    private static void Guard(Action body){try{body();}catch(Exception error){Debug.LogError("SHOOTER_FAILURE: "+error);EditorApplication.Exit(1);}}
}
