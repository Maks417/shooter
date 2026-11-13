using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Shooter.Content;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests
{
    public sealed class SoldierSkinningTests
    {
        [UnityTest]
        public IEnumerator RealCacheLoadsSkinsAndRemapsFarPaletteWithoutAddingGameplayColliders()
        {
            string repository=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            var toolchain=ContentIdentity.Hash(File.ReadAllBytes(Path.Combine(repository,"toolchain-lock.json")));
            string cache=Path.Combine(repository,"Artifacts/unity-soldier-tests",Guid.NewGuid().ToString("N"));
            var soldier=SoldierRecipe.Parse(File.ReadAllBytes(Path.Combine(Application.dataPath,"Shooter/Generation/FeatureRecipes/soldier.json")));
            var rifle=WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(Application.dataPath,"Shooter/Generation/FeatureRecipes/rifle.json")));
            var nodes=new[]{new CompilationNode(new ValidatedRecipe(soldier.Document,soldier.CanonicalSource,new SoldierRecipeValidator()),"windows64","Low",toolchain,new SoldierAssetGenerator()),
                new CompilationNode(new ValidatedRecipe(rifle.Document,rifle.CanonicalSource,new WeaponRecipeValidator()),"windows64","Low",toolchain,new WeaponAssetGenerator())};
            var result=new ContentCompiler(new CacheFiles(cache,toolchain)).CompileAsync(nodes,false,CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(result.Error,Is.EqualTo(ErrorCode.None),result.Diagnostic);
            string id(string name)=>result.Entries.Single(e=>e.Manifest.RecipeId==name).Manifest.ContentId.ToString();
            File.WriteAllBytes(Path.Combine(cache,"soldier-gallery.json"),CanonicalJson.Encode(Encoding.ASCII.GetBytes("{\"schema\":\"shooter.soldier-gallery.v1\",\"toolchainHash\":\""+toolchain+"\",\"soldierVisual\":\""+id("soldiers/baseline")+"\",\"rifleVisual\":\""+id("weapons/rifle")+"\"}")));
            SoldierGallery view=null;var bind=new Mesh();var posed=new Mesh();
            try
            {
                view=SoldierGallery.Create(cache,toolchain);view.Animate=false;view.PoseAt(0,"bind",true);
                yield return null;
                var renderer=view.GetComponentsInChildren<SkinnedMeshRenderer>().Single();renderer.BakeMesh(bind);
                view.PoseAt(0,"crouch",true);yield return null;renderer.BakeMesh(posed);
                Assert.That(bind.vertexCount,Is.EqualTo(posed.vertexCount));
                var a=bind.vertices;var b=posed.vertices;float max=0;
                for(int i=0;i<a.Length;i++){max=Mathf.Max(max,Vector3.Distance(a[i],b[i]));Assert.That(float.IsNaN(b[i].x)||float.IsInfinity(b[i].x),Is.False);}
                Assert.That(max,Is.GreaterThan(.1f),"Actual engine skinning must deform the generated bind geometry.");
                view.SetLod(2);yield return null;
                Assert.That(renderer.bones.Length,Is.LessThanOrEqualTo(20));Assert.That(renderer.sharedMesh.bindposes.Length,Is.EqualTo(renderer.bones.Length));
                Assert.That(view.GetComponentsInChildren<Collider>().Length,Is.Zero);
            }
            finally { if(view!=null)UnityEngine.Object.DestroyImmediate(view.gameObject);UnityEngine.Object.DestroyImmediate(bind);UnityEngine.Object.DestroyImmediate(posed); }
            yield return null;
        }
    }
}
