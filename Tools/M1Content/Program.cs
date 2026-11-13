using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content;
using Shooter.Contracts;
using Shooter.Generation;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if(args.Length!=5)throw new ArgumentException("Expected repository, Artifacts cache, report, source commit, force(true/false).");
            string root=Path.GetFullPath(args[0]), cache=Path.GetFullPath(args[1]), report=Path.GetFullPath(args[2]);
            string artifacts=Path.Combine(root,"Artifacts")+Path.DirectorySeparatorChar;
            if(!cache.StartsWith(artifacts,StringComparison.OrdinalIgnoreCase)||!report.StartsWith(artifacts,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Outputs must remain under repository Artifacts.");
            var hash=ContentIdentity.Hash(File.ReadAllBytes(Path.Combine(root,"toolchain-lock.json")));
            var files=new CacheFiles(cache,hash);var nodes=new List<CompilationNode>();
            byte[] Source(string name)=>File.ReadAllBytes(Path.Combine(root,"UnityProject/Assets/Shooter/Generation/FeatureRecipes",name+".json"));
            foreach(string kind in new[]{"rifle","pistol"})
            {
                var recipe=WeaponRecipe.Parse(Source(kind));var valid=new ValidatedRecipe(recipe.Document,recipe.CanonicalSource,new WeaponRecipeValidator());
                foreach(string profile in new[]{"Low","gameplay"})nodes.Add(new CompilationNode(valid,"windows64",profile,hash,new WeaponAssetGenerator()));
            }
            foreach(string family in new[]{"metal","polymer","concrete","plaster","asphalt","soil","fabric"})
            {
                var recipe=MaterialRecipe.Parse(Source(family));nodes.Add(new CompilationNode(new ValidatedRecipe(recipe.Document,recipe.CanonicalSource,new MaterialRecipeValidator()),"windows64","Low",hash,new MaterialAssetGenerator(),new[]{family}));
            }
            var soldier=SoldierRecipe.Parse(Source("soldier"));nodes.Add(new CompilationNode(new ValidatedRecipe(soldier.Document,soldier.CanonicalSource,new SoldierRecipeValidator()),"windows64","Low",hash,new SoldierAssetGenerator(),new[]{"equipment","goggles","skin","uniform"}));
            long before=GC.GetTotalAllocatedBytes(true);var timer=Stopwatch.StartNew();
            var result=await new ContentCompiler(files).CompileAsync(nodes,bool.Parse(args[4]),CancellationToken.None);
            timer.Stop();long allocated=GC.GetTotalAllocatedBytes(true)-before;
            if(result.Error!=ErrorCode.None)throw new InvalidOperationException(result.Error+": "+result.Diagnostic);
            var proof=new {result="Verified",scope="M1 feasibility set12 requests; .NET tools CPU compile only, not Unity import, full map or release rendering",sourceCommit=args[3],
                toolchainHash=hash.ToString(),featureRegistrySha256=ContentIdentity.Hash(File.ReadAllBytes(Path.Combine(root,"feature-content-lock.json"))).ToString(),
                seed="460072030",profile="Low",force=bool.Parse(args[4]),elapsedMilliseconds=timer.Elapsed.TotalMilliseconds,
                managedAllocatedBytesAllThreads=allocated,processPeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64,
                allocationMethod="GC.GetTotalAllocatedBytes(precise:true) delta around CompileAsync; includes compiler/generation/verification plus async tool continuation",
                processPeakScope="Whole standalone tool process lifetime, includes recipe parsing/JIT; not compiler-only transient peak",
                content=result.Entries.Select(e=>new{recipe=e.Manifest.RecipeId,profile=e.Manifest.VisualProfile,id=e.Manifest.ContentId.ToString(),
                    manifestSha256=ContentIdentity.Hash(e.Manifest.Encode()).ToString(),sourceHash=e.Manifest.SourceHash.ToString(),e.Reused,bytes=e.Manifest.Artifacts.Sum(a=>a.Bytes)}).ToArray()};
            File.WriteAllText(report,JsonSerializer.Serialize(proof,new JsonSerializerOptions{WriteIndented=true})+"\n");
            Console.WriteLine("Verified12 M1 requests; "+timer.Elapsed.TotalMilliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"ms");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
