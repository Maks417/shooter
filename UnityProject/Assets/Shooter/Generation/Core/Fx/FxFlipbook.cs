#nullable enable
using System;using System.Collections.Generic;using System.Threading;using Shooter.Contracts;using Shooter.Generation.Noise;
namespace Shooter.Generation.Fx
{
    public enum FlipbookKind { Smoke, Dust, Fireball, Muzzle, Spark }
    public static class FxFlipbook
    {
        public const string Generator="fx.flipbook",Version="1.0.0",Schema="shooter.recipe.fx-flipbook.v1";
        public const int Tiles=8;
        public static ContentId Fingerprint=>GeneratorFingerprint.Compute(Generator,Version);
        public static string Path(FlipbookKind kind)=>"material-fx-"+kind.ToString().ToLowerInvariant()+".bin";
        private static readonly Dictionary<string,MaterialData> cache=new Dictionary<string,MaterialData>(StringComparer.Ordinal);
        public static MaterialData Bake(FlipbookKind kind,int cell=32,ulong seed=460072030,CancellationToken cancel=default)
        {
            if(!Enum.IsDefined(typeof(FlipbookKind),kind)||cell!=32&&cell!=64)throw new ArgumentOutOfRangeException(nameof(kind));
            cancel.ThrowIfCancellationRequested();
            string key=kind+":"+cell+":"+seed;
            lock(cache)if(cache.TryGetValue(key,out var cached))return cached;
            var cloud=NoiseField.Create(NoiseBasis.Gradient,NamedRandomStream.Derive(seed,Generator,kind+"/volume"),4,3);
            var curl=NoiseField.Create(NoiseBasis.Gradient,NamedRandomStream.Derive(seed,Generator,kind+"/curl"),3,2);
            int n=cell*Tiles;var bytes=new byte[n*n*4];
            for(int frame=0;frame<64;frame++)
            {
                cancel.ThrowIfCancellationRequested();double t=frame/63.0;
                for(int y=1;y<cell-1;y++)for(int x=1;x<cell-1;x++)
                {
                    double u=2*(x+.5)/cell-1,v=2*(y+.5)/cell-1;
                    double radius=.23+.59*t, dx=u/radius,dy=(v+t*.18)/radius;
                    double density=0;
                    // Curl in the projected plane is the rotated gradient of a 3D scalar potential.
                    double cx=(curl.Raw3D(u,v+.03,t*2)-curl.Raw3D(u,v-.03,t*2))/.06;
                    double cy=-(curl.Raw3D(u+.03,v,t*2)-curl.Raw3D(u-.03,v,t*2))/.06;
                    for(int z=0;z<4;z++)
                    {
                        double dz=(z+.5)/2-1;
                        double sphere=Math.Max(0,1-dx*dx-dy*dy-dz*dz);
                        double noise=cloud.Raw3D(dx*2+cx*t*.10,dy*2+cy*t*.10,dz*2+t*2);
                        density+=sphere*Math.Max(0,.58+noise*.6)*.25;
                    }
                    double alpha=density*(1-t)*(kind==FlipbookKind.Dust?1.3:1.8),red=.56,green=.58,blue=.60;
                    if(kind==FlipbookKind.Dust){red=.44;green=.34;blue=.22;}
                    if(kind==FlipbookKind.Fireball){red=1;green=.35+.45*(1-t);blue=.06+.22*(1-t);alpha*=1.8;}
                    if(kind==FlipbookKind.Muzzle)
                    {
                        double radial=u*u+v*v;
                        double star=Math.Max(0,1-radial*1.5)*Math.Max(Math.Max(0,1-Math.Abs(u)*9),Math.Max(0,1-Math.Abs(v)*9));
                        alpha=star*(1-t)*(1-t);red=1;green=.76;blue=.28;
                    }
                    if(kind==FlipbookKind.Spark)
                    {alpha=Math.Max(0,1-Math.Abs(u)*14)*Math.Max(0,1-Math.Abs(v)*1.6)*(1-t);red=1;green=.76;blue=.32;}
                    int p=((frame/8*cell+y)*n+frame%8*cell+x)*4;
                    bytes[p]=Byte(red);bytes[p+1]=Byte(green);bytes[p+2]=Byte(blue);bytes[p+3]=Byte(alpha);
                }
            }
            var result=NumericPlanes.Rgba(n,n,bytes);lock(cache)cache[key]=result;return result;
        }
        private static byte Byte(double value)=>(byte)(Math.Max(0,Math.Min(1,value))*255+.5);
    }
    public sealed class FxFlipbookGenerator:IAssetGenerator
    {
        public GenerationResult Generate(GenerationRequest request,CancellationToken cancel)
        {
            GenerationResult Failure(ErrorCode code)=>new GenerationResult(code,Array.Empty<GeneratedArtifact>(),new Aabb(new Float3(0,0,0),new Float3(0,0,0)),new[]{new Diagnostic(code,"recipe","Invalid or cancelled FX recipe.")});
            try
            {
                cancel.ThrowIfCancellationRequested();
                if(request==null)return Failure(ErrorCode.InvalidInput);
                var recipe=request.Recipe;
                if(recipe.GeneratorId!=FxFlipbook.Generator||recipe.GeneratorVersion!=FxFlipbook.Version||recipe.SchemaId!=FxFlipbook.Schema)return Failure(ErrorCode.UnsupportedVersion);
                if(recipe.Dependencies.Count!=0||request.VisualProfile!="Low"&&request.VisualProfile!="Balanced"&&request.VisualProfile!="High")return Failure(ErrorCode.InvalidInput);
                var parameters=JsonValue.Parse(recipe.Parameters).Object;
                if(parameters.Count!=2||!Enum.TryParse(parameters["kind"].String,false,out FlipbookKind kind))return Failure(ErrorCode.InvalidInput);
                var plane=FxFlipbook.Bake(kind,parameters["cell"].Number,recipe.SeedValue,cancel);
                var bytes=ArtifactCodec.Encode(ContentKind.Material,GeometryCodec.EncodeMaterial(plane),FxFlipbook.Fingerprint,request.ToolchainHash);
                return new GenerationResult(ErrorCode.None,new[]{new GeneratedArtifact(ContentKind.Material,"atlas",1,bytes,ContentIdentity.Hash(bytes))},new Aabb(new Float3(0,0,0),new Float3(0,0,0)),Array.Empty<Diagnostic>());
            }
            catch(OperationCanceledException){return Failure(ErrorCode.Cancelled);}
            catch(Exception ex)when(ex is ArgumentException||ex is FormatException||ex is KeyNotFoundException){return Failure(ErrorCode.InvalidInput);}
        }
    }

}
