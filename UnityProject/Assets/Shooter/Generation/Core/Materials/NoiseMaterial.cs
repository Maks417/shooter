#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation.Noise;

namespace Shooter.Generation
{
    /// <summary>ADR-088: opt-in periodic materials. Legacy material.surface recipes stay byte-identical.</summary>
    public sealed class NoiseMaterialRecipe
    {
        public const string Generator = "material.noise", Version = "1.0.0", Schema = "shooter.recipe.material-noise.v1";
        public static readonly string[] Roles = { "concrete", "plaster", "rust", "painted-steel", "asphalt", "gravel", "corrugated-metal", "camo-fabric" };
        public RecipeDocument Document { get; }
        public string Role { get; }
        public int Resolution { get; }
        public int TileMm { get; }
        public NoiseMaterialRecipe(string role, int resolution, ulong seed, int tileMm = 2000)
        {
            if (Array.IndexOf(Roles, role) < 0) throw new ArgumentException("Unknown noise material role.", nameof(role));
            if (resolution != 128 && resolution != 256) throw new ArgumentOutOfRangeException(nameof(resolution));
            if (tileMm < 100 || tileMm > 8000) throw new ArgumentOutOfRangeException(nameof(tileMm));
            Role = role; Resolution = resolution; TileMm = tileMm;
            var parameters = Encoding.UTF8.GetBytes("{\"resolution\":" + resolution + ",\"role\":\"" + role + "\",\"tileMm\":" + tileMm + "}");
            Document = new RecipeDocument(new DefinitionId("materials/noise-" + role), Schema, Generator, Version,
                seed.ToString(System.Globalization.CultureInfo.InvariantCulture), parameters, Array.Empty<ContentId>());
        }
        public static NoiseMaterialRecipe FromDocument(RecipeDocument document)
        {
            if (document.SchemaId != Schema || document.GeneratorId != Generator || document.GeneratorVersion != Version || document.Dependencies.Count != 0)
                throw new FormatException("Unsupported noise material identity/dependencies.");
            var p = JsonValue.Parse(document.Parameters).Object;
            if (p.Count != 3) throw new FormatException("Noise material requires role, resolution and tileMm only.");
            var recipe = new NoiseMaterialRecipe(p["role"].String, p["resolution"].Number, document.SeedValue, p["tileMm"].Number);
            if (!document.Parameters.SequenceEqual(recipe.Document.Parameters)) throw new FormatException("Noise parameters must be canonical.");
            return recipe;
        }
    }

    public sealed class NoiseMaterialValidator : IRecipeValidator
    {
        public ValidationReport Validate(RecipeDocument recipe)
        {
            try { NoiseMaterialRecipe.FromDocument(recipe); return new ValidationReport(true, Array.Empty<Diagnostic>()); }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is KeyNotFoundException)
            { return new ValidationReport(false, new[] { new Diagnostic(ErrorCode.InvalidInput, "recipe", "Invalid noise material recipe.") }); }
        }
    }
    public sealed class NoiseMaterialGenerator : IAssetGenerator
    {
        public GenerationResult Generate(GenerationRequest request, CancellationToken cancel)
        {
            try
            {
                cancel.ThrowIfCancellationRequested();
                if(request==null || request.VisualProfile!="Low"&&request.VisualProfile!="Balanced"&&request.VisualProfile!="High")return Fail(ErrorCode.InvalidInput);
                var bake = NoiseMaterialBaker.Bake(NoiseMaterialRecipe.FromDocument(request.Recipe), cancel);
                var fp = GeneratorFingerprint.Compute(NoiseMaterialRecipe.Generator, NoiseMaterialRecipe.Version);
                GeneratedArtifact Plane(MaterialData plane, string name)
                {
                    var bytes = ArtifactCodec.Encode(ContentKind.Material, GeometryCodec.EncodeMaterial(plane), fp, request.ToolchainHash);
                    return new GeneratedArtifact(ContentKind.Material, name, 1, bytes, ContentIdentity.Hash(bytes));
                }
                return new GenerationResult(ErrorCode.None, new[] { Plane(bake.Albedo,"albedo"), Plane(bake.Normal,"normal"), Plane(bake.Mask,"mask") },
                    new Aabb(new Float3(0,0,0),new Float3(0,0,0)), Array.Empty<Diagnostic>());
            }
            catch (OperationCanceledException) { return Fail(ErrorCode.Cancelled); }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is KeyNotFoundException) { return Fail(ErrorCode.InvalidInput); }
        }
        private static GenerationResult Fail(ErrorCode code) => new GenerationResult(code, Array.Empty<GeneratedArtifact>(),
            new Aabb(new Float3(0,0,0),new Float3(0,0,0)), new[] { new Diagnostic(code,"recipe","Noise material generation failed.") });
    }

    public static class NoiseMaterialBaker
    {
        public static MaterialBake Bake(NoiseMaterialRecipe recipe, CancellationToken cancel = default)
        {
            if(recipe==null)throw new ArgumentNullException(nameof(recipe));
            int n = recipe.Resolution;
            var albedo = new byte[n*n*4]; var normal = new byte[albedo.Length]; var mask = new byte[albedo.Length];
            var height = new double[n*n];
            NoiseField Field(string key, NoiseBasis basis, int period, int octaves) => NoiseField.Create(basis,
                NamedRandomStream.Derive(recipe.Document.SeedValue, NoiseMaterialRecipe.Generator + "/" + recipe.Role, key), period, octaves);
            var broad = Field("staining", NoiseBasis.DomainWarpedFbm, 3, 3);
            var grain = Field("aggregate", NoiseBasis.Gradient, 48, 2);
            var cells = Field("cracks", NoiseBasis.Cellular, 12, 1);
            var warp = Field("oxide", NoiseBasis.DomainWarpedFbm, 8, 3);
            for (int y=0;y<n;y++)
            {
                cancel.ThrowIfCancellationRequested();
                for (int x=0;x<n;x++)
                {
                    double u=(x+.5)/n,v=(y+.5)/n;
                    double b=broad.SamplePeriodic(u,v), g=grain.SamplePeriodic(u,v), c=cells.SamplePeriodic(u,v);
                    double streak=(warp.SamplePeriodic(u,.16)*.8+b*.2), crack=Clamp((.28-c)*9), chips=Clamp((g+b*.3-.73)*8);
                    double r=.32, green=.33, blue=.31, rough=.85, metal=0, h=g*.001;
                    switch(recipe.Role)
                    {
                        case "concrete":
                            r=.36;green=.37;blue=.34; h=g*.002-crack*.0012;
                            b=.7+b*.35-crack*.36-streak*.08; break;
                        case "plaster":
                            r=.60;green=.57;blue=.49;h=g*.0008-chips*.0004;
                            b=.78+b*.25-chips*.14;break;
                        case "rust":
                            r=.29;green=.10;blue=.035;rough=.95;h=g*.0012;
                            b=.56+b*.45+streak*.27;break;
                        case "painted-steel":
                            r=.085;green=.16;blue=.19;metal=chips*.8;rough=.62;h=chips*.0003;
                            r=r*(1-chips)+.22*chips;green=green*(1-chips)+.12*chips;blue=blue*(1-chips)+.065*chips;
                            b=.75+b*.4-streak*.14;break;
                        case "asphalt":
                            r=.035;green=.038;blue=.04;rough=.94;h=g*.003-crack*.001;
                            b=.6+g*.8-crack*.5;break;
                        case "gravel":
                            r=.14;green=.095;blue=.055;rough=.97;h=(1-c)*.006;
                            b=.55+g*.65+c*.22;break;
                        case "corrugated-metal":
                            r=.16;green=.19;blue=.20;metal=.65;rough=.72;
                            double ridge=1-Math.Abs(2*(u*16-Math.Floor(u*16))-1);
                            h=ridge*.005; b=.68+b*.28-streak*.27;break;
                        case "camo-fabric":
                            bool dark=b<.44, light=b>.58;
                            r=dark?.035:light?.22:.095;green=dark?.045:light?.20:.12;blue=dark?.025:light?.12:.055;
                            rough=.94;h=((x%2)+(y%2))*.00008;b=.88+g*.2;break;
                    }
                    int p=(y*n+x)*4; height[y*n+x]=h;
                    albedo[p]=Byte(r*b);albedo[p+1]=Byte(green*b);albedo[p+2]=Byte(blue*b);albedo[p+3]=255;
                    mask[p]=Byte(metal);mask[p+1]=Byte(1-crack*.15);mask[p+2]=Byte(rough);mask[p+3]=255;
                }
            }
            double spacing=2*recipe.TileMm/1000.0/n;
            for(int y=0;y<n;y++) for(int x=0;x<n;x++)
            {
                double du=(height[y*n+(x+1)%n]-height[y*n+(x+n-1)%n])/spacing;
                double dv=(height[((y+1)%n)*n+x]-height[((y+n-1)%n)*n+x])/spacing;
                double len=Math.Sqrt(du*du+dv*dv+1); int p=(y*n+x)*4;
                normal[p]=Byte(.5-du/len*.5);normal[p+1]=Byte(.5-dv/len*.5);normal[p+2]=Byte(.5+.5/len);normal[p+3]=255;
            }
            return new MaterialBake(SrgbPlane(MaterialBaker.BuildPlane(n,albedo,"mask",cancel)),MaterialBaker.BuildPlane(n,normal,"normal",cancel),MaterialBaker.BuildPlane(n,mask,"mask",cancel));
        }
        private static MaterialData SrgbPlane(MaterialData linear)
        {
            var mips=new List<byte[]>();
            foreach(var mip in linear.MipBytes)
            {
                var data=new byte[mip.Count];
                for(int i=0;i<data.Length;i++)
                {
                    if(i%4==3){data[i]=255;continue;}
                    double v=mip[i]/255.0;
                    // Fixed square-root approximation: IEEE arithmetic only in hashed bytes.
                    data[i]=Byte(v<=.0031308?v*12.92:1.13005*Math.Sqrt(v)-.13448*v+.00443);
                }
                mips.Add(data);
            }
            return new MaterialData(linear.Width,linear.Height,"rgba8-srgb",mips);
        }
        private static double Clamp(double v) => Math.Max(0,Math.Min(1,v));
        private static byte Byte(double v) => (byte)(Clamp(v)*255+.5);
    }
}
