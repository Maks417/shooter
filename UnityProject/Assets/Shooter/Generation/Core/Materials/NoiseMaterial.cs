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
        /// <summary>ADR-106 structured surfaces in metres with Toksvig mask mips; 256/512 px over a 4 m tile.</summary>
        public const string StructuredVersion = "1.1.0";
        /// <summary>ADR-116 realistic colour over the 1.1.0 relief; the version every map surface bakes.</summary>
        public const string RealisticVersion = "1.2.0";
        static bool Known(string version) => version == Version || version == StructuredVersion || version == RealisticVersion;
        public static readonly string[] Roles = { "concrete", "plaster", "rust", "painted-steel", "asphalt", "gravel", "corrugated-metal", "camo-fabric" };
        public RecipeDocument Document { get; }
        public string Role { get; }
        public int Resolution { get; }
        public int TileMm { get; }
        public string GeneratorVersion => Document.GeneratorVersion;
        public NoiseMaterialRecipe(string role, int resolution, ulong seed, int tileMm = 2000, string version = Version)
        {
            if (Array.IndexOf(Roles, role) < 0) throw new ArgumentException("Unknown noise material role.", nameof(role));
            if (!Known(version)) throw new ArgumentException("Unknown noise material version.", nameof(version));
            if (version == Version ? resolution != 128 && resolution != 256 : resolution != 256 && resolution != 512) throw new ArgumentOutOfRangeException(nameof(resolution));
            if (tileMm < 100 || tileMm > 8000) throw new ArgumentOutOfRangeException(nameof(tileMm));
            Role = role; Resolution = resolution; TileMm = tileMm;
            var parameters = Encoding.UTF8.GetBytes("{\"resolution\":" + resolution + ",\"role\":\"" + role + "\",\"tileMm\":" + tileMm + "}");
            Document = new RecipeDocument(new DefinitionId("materials/noise-" + role), Schema, Generator, version,
                seed.ToString(System.Globalization.CultureInfo.InvariantCulture), parameters, Array.Empty<ContentId>());
        }
        public static NoiseMaterialRecipe FromDocument(RecipeDocument document)
        {
            if (document.SchemaId != Schema || document.GeneratorId != Generator || !Known(document.GeneratorVersion) || document.Dependencies.Count != 0)
                throw new FormatException("Unsupported noise material identity/dependencies.");
            var p = JsonValue.Parse(document.Parameters).Object;
            if (p.Count != 3) throw new FormatException("Noise material requires role, resolution and tileMm only.");
            var recipe = new NoiseMaterialRecipe(p["role"].String, p["resolution"].Number, document.SeedValue, p["tileMm"].Number, document.GeneratorVersion);
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
                var fp = GeneratorFingerprint.Compute(NoiseMaterialRecipe.Generator, request.Recipe.GeneratorVersion);
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
            if(recipe.GeneratorVersion!=NoiseMaterialRecipe.Version)return BakeStructured(recipe,cancel);
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
        /// <summary>
        /// ADR-106 material.noise 1.1.0: structured surfaces sized in metres over a 4 m tile. Every noise feature spans at
        /// least six texels (no near-Nyquist grain), normal relief is gentler, concrete gains panel joints, form-tie holes
        /// and sparse cracks, and the mask mips carry Toksvig roughness so averaged-out bumps turn into broader highlights
        /// instead of glitter.
        /// </summary>
        private static MaterialBake BakeStructured(NoiseMaterialRecipe recipe, CancellationToken cancel)
        {
            int n = recipe.Resolution;
            double tile = recipe.TileMm / 1000.0;
            var albedo = new byte[n * n * 4]; var mask = new byte[albedo.Length];
            var height = new double[n * n];
            NoiseField Field(string key, NoiseBasis basis, double featureMeters, int octaves)
            {
                // Cells per tile from the requested feature size, never finer than six texels per cell.
                int period = Math.Max(1, Math.Min((int)Math.Round(tile / featureMeters), n / 6));
                return NoiseField.Create(basis, NamedRandomStream.Derive(recipe.Document.SeedValue, NoiseMaterialRecipe.Generator + "/1.1/" + recipe.Role, key), period, octaves);
            }
            var broad = Field("staining", NoiseBasis.DomainWarpedFbm, 1.4, 3);
            var macro = Field("macro", NoiseBasis.Gradient, .6, 2);
            var grain = Field("aggregate", NoiseBasis.Gradient, .045, 2);
            var cells = Field("cracks", NoiseBasis.Cellular, .45, 1);
            var crackMask = Field("crack-zones", NoiseBasis.Gradient, 1.3, 1);
            var stones = Field("stones", NoiseBasis.Cellular, .05, 1);
            var warp = Field("oxide", NoiseBasis.DomainWarpedFbm, .5, 3);
            // ADR-116 material.noise 1.2.0 adds colour fields only; the 1.1.0 fields, keys and bytes are untouched.
            bool realistic = recipe.GeneratorVersion == NoiseMaterialRecipe.RealisticVersion;
            var hueField = realistic ? Field("hue", NoiseBasis.DomainWarpedFbm, .9, 3) : null;
            var spallField = realistic ? Field("spall", NoiseBasis.Gradient, .7, 2) : null;
            var dampField = realistic ? Field("damp", NoiseBasis.Gradient, 1.6, 2) : null;
            var stoneHueField = realistic ? Field("stone-hue", NoiseBasis.Gradient, .05, 1) : null;
            ulong panelSeed = NamedRandomStream.Derive(recipe.Document.SeedValue, NoiseMaterialRecipe.Generator + "/1.2/" + recipe.Role, "panels").InitState;
            for (int y = 0; y < n; y++)
            {
                cancel.ThrowIfCancellationRequested();
                for (int x = 0; x < n; x++)
                {
                    double u = (x + .5) / n, v = (y + .5) / n, mx = u * tile, my = v * tile;
                    double b = broad.SamplePeriodic(u, v), m = macro.SamplePeriodic(u, v), g = grain.SamplePeriodic(u, v), c = cells.SamplePeriodic(u, v);
                    double zones = Clamp((crackMask.SamplePeriodic(u, v) - .55) * 5);
                    double crack = Clamp((.07 - c) * 22) * zones;
                    double streak = warp.SamplePeriodic(u, .12) * .8 + b * .2;
                    double r = .32, green = .33, blue = .31, rough = .85, metal = 0, h = 0, occlusion = 1, tone = 1;
                    switch (recipe.Role)
                    {
                        case "concrete":
                        {
                            // Cast panels every 2 m with recessed joints, form-tie holes on a 0.6 m grid, stains and sparse cracks.
                            double jx = Math.Abs(mx - Math.Round(mx / 2) * 2), jy = Math.Abs(my - Math.Round(my / 2) * 2);
                            double joint = Clamp(1 - Math.Min(jx, jy) / .009);
                            double tx = mx - (Math.Floor(mx / .6) * .6 + .3), ty = my - (Math.Floor(my / .6) * .6 + .3);
                            double tie = Clamp(1 - Math.Sqrt(tx * tx + ty * ty) / .011);
                            r = .37; green = .375; blue = .35; rough = .88;
                            h = g * .0007 + m * .0012 - crack * .0008 - joint * .0025 - tie * .002;
                            tone = .78 + b * .26 + m * .06 - crack * .28 - streak * .07 - joint * .22 - tie * .35;
                            occlusion = 1 - joint * .45 - tie * .5 - crack * .2; rough += joint * .08;
                            break;
                        }
                        case "plaster":
                            r = .60; green = .57; blue = .49; rough = .9;
                            h = g * .0004 + m * .0008; tone = .84 + b * .2 + m * .05 - streak * .05; break;
                        case "rust":
                            r = .29; green = .10; blue = .035; rough = .95;
                            h = g * .0008 + m * .0006; tone = .58 + b * .4 + streak * .25; break;
                        case "painted-steel":
                        {
                            double chips = Clamp((g * .6 + b * .5 - .74) * 7);
                            r = .085 * (1 - chips) + .22 * chips; green = .16 * (1 - chips) + .12 * chips; blue = .19 * (1 - chips) + .065 * chips;
                            metal = chips * .8; rough = .58 + chips * .2; h = chips * .00025 + m * .0003;
                            tone = .78 + b * .32 - streak * .16; break;
                        }
                        case "asphalt":
                        {
                            double s = stones.SamplePeriodic(u, v);
                            r = .040; green = .042; blue = .045; rough = .93;
                            h = (1 - s) * .0012 + m * .0008 - crack * .0012;
                            tone = .62 + (1 - s) * .35 + b * .25 - crack * .45; occlusion = 1 - crack * .3; break;
                        }
                        case "gravel":
                        {
                            double s = stones.SamplePeriodic(u, v);
                            r = .14; green = .098; blue = .058; rough = .96;
                            h = (1 - s) * .004 + m * .0015; tone = .55 + (1 - s) * .45 + b * .2; occlusion = .75 + .25 * (1 - s); break;
                        }
                        case "corrugated-metal":
                        {
                            double ridge = .5 - .5 * Math.Cos(2 * Math.PI * mx / .125);
                            r = .16; green = .19; blue = .20; metal = .65; rough = .66 + streak * .15;
                            h = ridge * .006 + m * .0003; tone = .70 + b * .25 - streak * .28; occlusion = .8 + .2 * ridge; break;
                        }
                        case "camo-fabric":
                        {
                            bool dark = b < .44, light = b > .58;
                            r = dark ? .035 : light ? .22 : .095; green = dark ? .045 : light ? .20 : .12; blue = dark ? .025 : light ? .12 : .055;
                            rough = .94; h = g * .0002; tone = .9 + g * .15; break;
                        }
                    }
                    if (realistic)
                        Realistic(recipe.Role, mx, my, b, streak, stones.SamplePeriodic(u, v), hueField!.SamplePeriodic(u, v), spallField!.SamplePeriodic(u, v),
                            dampField!.SamplePeriodic(u, v), stoneHueField!.SamplePeriodic(u, v), panelSeed, ref r, ref green, ref blue, ref tone, ref metal, ref rough, ref occlusion);
                    // Every role keeps spatial roughness/occlusion variation (wear, staining, cavities).
                    rough = Clamp(rough + (b - .5) * .10 + (g - .5) * .04 - streak * .03);
                    occlusion = Clamp(occlusion - (1 - m) * .06);
                    int p = (y * n + x) * 4; height[y * n + x] = h;
                    albedo[p] = Byte(r * tone); albedo[p + 1] = Byte(green * tone); albedo[p + 2] = Byte(blue * tone); albedo[p + 3] = 255;
                    mask[p] = Byte(metal); mask[p + 1] = Byte(occlusion); mask[p + 2] = Byte(rough); mask[p + 3] = 255;
                }
            }
            var normal = new byte[albedo.Length];
            var unit = new double[n * n * 3];
            double spacing = 2 * tile / n;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                double du = (height[y * n + (x + 1) % n] - height[y * n + (x + n - 1) % n]) / spacing;
                double dv = (height[((y + 1) % n) * n + x] - height[((y + n - 1) % n) * n + x]) / spacing;
                double len = Math.Sqrt(du * du + dv * dv + 1); int p = (y * n + x) * 4, q = (y * n + x) * 3;
                unit[q] = -du / len; unit[q + 1] = -dv / len; unit[q + 2] = 1 / len;
                normal[p] = Byte(.5 - du / len * .5); normal[p + 1] = Byte(.5 - dv / len * .5); normal[p + 2] = Byte(.5 + .5 / len); normal[p + 3] = 255;
            }
            var maskPlane = MaterialBaker.BuildPlane(n, mask, "mask", cancel);
            return new MaterialBake(SrgbPlane(MaterialBaker.BuildPlane(n, albedo, "mask", cancel)), MaterialBaker.BuildPlane(n, normal, "normal", cancel),
                ToksvigRoughness(maskPlane, unit, n));
        }

        /// <summary>
        /// ADR-116 material.noise 1.2.0: realistic linear albedo per role over the unchanged 1.1.0 relief. Concrete is a warm
        /// grey with per-panel pour variation, rain efflorescence and damp darkening; plaster carries ochre wash and brick
        /// spalls; painted steel is chipped through to red-oxide primer with rust runs; corrugated sheet is galvanized with
        /// white-rust mottling and rust in the troughs; asphalt gains pale aggregate and tar patches; gravel mixes warm and
        /// cool stones. Every feature keeps the 1.1.0 six-texel minimum.
        /// </summary>
        private static void Realistic(string role, double mx, double my, double b, double streak, double stone, double hue, double spall,
            double damp, double stoneHue, ulong panelSeed, ref double r, ref double green, ref double blue, ref double tone,
            ref double metal, ref double rough, ref double occlusion)
        {
            void Mix(double tr, double tg, double tb, double t, ref double cr, ref double cg, ref double cb)
            { t = Clamp(t); cr += (tr - cr) * t; cg += (tg - cg) * t; cb += (tb - cb) * t; }
            switch (role)
            {
                case "concrete":
                {
                    double panel = PanelJitter(panelSeed, (int)Math.Floor(mx / 2), (int)Math.Floor(my / 2));
                    r = .50 + panel * .03 + (hue - .5) * .05; green = .475 + panel * .025 + (hue - .5) * .03; blue = .43 + panel * .015;
                    double wet = Clamp((damp - .58) * 4);
                    Mix(.36, .35, .33, wet * .6, ref r, ref green, ref blue);
                    tone *= 1 + Clamp((streak - .6) * 3) * .10 - wet * .08;
                    rough = Math.Max(.55, rough - wet * .12);
                    break;
                }
                case "plaster":
                {
                    r = .72; green = .66; blue = .54;
                    Mix(.74, .58, .36, (hue - .58) * 3.5, ref r, ref green, ref blue);
                    double brick = Clamp((spall - .70) * 9) * Clamp((damp - .35) * 3);
                    Mix(.46, .22, .14, brick, ref r, ref green, ref blue);
                    Mix(.50, .47, .40, Clamp((damp - .62) * 4) * .5, ref r, ref green, ref blue);
                    occlusion -= brick * .25; rough += brick * .04;
                    break;
                }
                case "painted-steel":
                {
                    r = .17 + (hue - .5) * .04; green = .29; blue = .27;
                    double chips = Clamp((spall + stoneHue * .35 - .86) * 8);
                    double runs = Clamp((streak - .62) * 4) * .7;
                    Mix(.38, .15, .08, chips, ref r, ref green, ref blue);
                    Mix(.30, .14, .06, runs, ref r, ref green, ref blue);
                    metal = chips * .3; rough = .52 + chips * .3 + runs * .2;
                    tone = .86 + b * .2 - streak * .08;
                    break;
                }
                case "corrugated-metal":
                {
                    double ridge = .5 - .5 * Math.Cos(2 * Math.PI * mx / .125);
                    r = .42; green = .435; blue = .44;
                    Mix(.58, .59, .57, Clamp((hue - .62) * 4) * .7, ref r, ref green, ref blue);
                    double rust = Clamp((streak - .55) * 3) * (.4 + .6 * (1 - ridge)) * Clamp((damp - .3) * 2);
                    Mix(.34, .14, .05, rust, ref r, ref green, ref blue);
                    metal = .75 * (1 - rust); rough = .48 + rust * .4 + streak * .1;
                    tone = .80 + b * .2 - rust * .1;
                    break;
                }
                case "asphalt":
                {
                    r = .075 + (1 - stone) * .03; green = .074 + (1 - stone) * .028; blue = .078 + (1 - stone) * .026;
                    double tar = Clamp((hue - .68) * 5);
                    tone = (.80 + b * .25) * (1 - tar * .4);
                    rough -= tar * .2;
                    break;
                }
                case "gravel":
                {
                    r = .30; green = .26; blue = .20;
                    if (stoneHue > .55) Mix(.36, .27, .18, (stoneHue - .55) * 4, ref r, ref green, ref blue);
                    else Mix(.25, .25, .24, (.55 - stoneHue) * 4, ref r, ref green, ref blue);
                    tone = .60 + (1 - stone) * .45 + b * .15;
                    break;
                }
            }
        }

        /// <summary>Deterministic per-panel jitter in [-1, 1] (splitmix64 of the seed and panel cell).</summary>
        private static double PanelJitter(ulong seed, int px, int py)
        {
            ulong z = seed ^ ((ulong)(uint)px * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)py * 0xC2B2AE3D27D4EB4FUL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; z ^= z >> 31;
            return (z >> 11) * (1.0 / (1UL << 53)) * 2 - 1;
        }

        /// <summary>
        /// Specular anti-aliasing: each mask mip's roughness grows with the spread of the base normals it averages
        /// (Toksvig: alpha^2 + 2(1 - |mean n|) / |mean n|), so distant or grazing bumps cannot sparkle.
        /// </summary>
        private static MaterialData ToksvigRoughness(MaterialData mask, double[] unit, int n)
        {
            var mips = new List<byte[]>();
            for (int level = 0; level < mask.MipBytes.Count; level++)
            {
                var data = new byte[mask.MipBytes[level].Count]; for (int i = 0; i < data.Length; i++) data[i] = mask.MipBytes[level][i];
                if (level > 0)
                {
                    int size = Math.Max(1, n >> level), block = n / size;
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    {
                        double sx = 0, sy = 0, sz = 0;
                        for (int dy = 0; dy < block; dy++) for (int dx = 0; dx < block; dx++)
                        { int q = ((y * block + dy) * n + x * block + dx) * 3; sx += unit[q]; sy += unit[q + 1]; sz += unit[q + 2]; }
                        double count = block * (double)block, length = Math.Sqrt(sx * sx + sy * sy + sz * sz) / count;
                        int p = (y * size + x) * 4 + 2;
                        double rough = data[p] / 255.0, alpha = rough * rough;
                        double spread = 2 * (1 - length) / Math.Max(length, .05);
                        data[p] = Byte(Math.Pow(Math.Min(1, alpha * alpha + spread), .25));
                    }
                }
                mips.Add(data);
            }
            return new MaterialData(mask.Width, mask.Height, mask.ChannelFormat, mips);
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
