#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Shooter.Contracts;

namespace Shooter.Generation
{
    public sealed class SoldierAssetGenerator : IAssetGenerator
    {
        public GenerationResult Generate(GenerationRequest request, CancellationToken cancel)
        {
            if(cancel.IsCancellationRequested)return Fail(ErrorCode.Cancelled,"Soldier generation cancelled.");
            if(request==null)return Fail(ErrorCode.InvalidInput,"Soldier generation request is required.");
            if(request.Recipe.SchemaId!=SoldierRecipe.Schema || request.Recipe.GeneratorId!=SoldierRecipe.GeneratorId || request.Recipe.GeneratorVersion!=SoldierRecipe.GeneratorVersion)
                return Fail(ErrorCode.UnsupportedVersion,"Unsupported soldier schema/generator version.");
            if(request.VisualProfile!="Low" && request.VisualProfile!="Balanced" && request.VisualProfile!="High" && request.VisualProfile!="gameplay")
                return Fail(ErrorCode.InvalidInput,"Unknown soldier profile.");
            try{SoldierRecipe.Validate(request.Recipe);}
            catch(Exception error)when(error is ArgumentException || error is FormatException){return Fail(ErrorCode.InvalidInput,error.Message);}
            var artifacts=new List<GeneratedArtifact>();ContentId fingerprint=GeneratorFingerprint.Compute(SoldierRecipe.GeneratorId,SoldierRecipe.GeneratorVersion);
            void Add(ContentKind kind,string variant,byte[] body)
            {
                byte[] bytes=ArtifactCodec.Encode(kind,body,fingerprint,request.ToolchainHash);
                artifacts.Add(new GeneratedArtifact(kind,variant,1,bytes,ContentIdentity.Hash(bytes)));
            }
            Add(ContentKind.Skeleton,"main",GeometryCodec.EncodeSkeleton(SoldierSkeleton.Definition));
            if(request.VisualProfile!="gameplay")
            {
                var source=Encoding.UTF8.GetBytes("{\"schemaId\":\""+SoldierRecipe.Schema+"\",\"recipeId\":\""+request.Recipe.RecipeId+"\",\"generatorId\":\""+SoldierRecipe.GeneratorId+"\",\"generatorVersion\":\""+SoldierRecipe.GeneratorVersion+"\",\"seed\":\""+request.Recipe.Seed+"\",\"parameters\":"+SoldierRecipe.ParametersJson+",\"dependencies\":[]}");
                var build=SoldierGeometry.Build(SoldierRecipe.Parse(source));
                for(int lod=0;lod<build.Lods.Count;lod++)
                {if(cancel.IsCancellationRequested)return Fail(ErrorCode.Cancelled,"Soldier generation cancelled.");Add(ContentKind.Mesh,"lod"+lod,GeometryCodec.EncodeMesh(build.Lods[lod]));}
            }
            if(cancel.IsCancellationRequested)return Fail(ErrorCode.Cancelled,"Soldier generation cancelled.");
            return new GenerationResult(ErrorCode.None,artifacts,SoldierGeometry.BindBounds,Array.Empty<Diagnostic>());
        }
        private static GenerationResult Fail(ErrorCode code,string message)=>new GenerationResult(code,Array.Empty<GeneratedArtifact>(),new Aabb(new Float3(0,0,0),new Float3(0,0,0)),new[]{new Diagnostic(code,"recipe",message)});
    }
}
