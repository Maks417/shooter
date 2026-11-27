#if UNITY_5_3_OR_NEWER
#nullable enable
using System;using System.Collections.Generic;using Shooter.Generation;using Shooter.Generation.Environment;using Shooter.Generation.Fx;using UnityEngine;using UnityEngine.Rendering;
namespace Shooter.Presentation.Fx
{
    /// <summary>
    /// Bounded code-built particle adaptation of the pure lifetime pool. Simulation is advanced from pool age. Fire,
    /// smoke and dust draw the ADR-126 licensed flipbooks; sparks, tracers, casings, debris, embers, the flash core,
    /// the muzzle star and the shockwave ring keep their generated sheets. A few map vehicles burn (ADR-126).
    /// </summary>
    public sealed class AnalyticFxPresenter:MonoBehaviour
    {
        public const int VisualLayer=11;
        private AnalyticFxPool? pool;private readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        private ParticleSystem[] systems=Array.Empty<ParticleSystem>();private ParticleSystemRenderer[] renderers=Array.Empty<ParticleSystemRenderer>();
        private readonly Material[] materials=new Material[5];private float[] age=Array.Empty<float>();private bool[] active=Array.Empty<bool>();private FxKind[] kind=Array.Empty<FxKind>();
        private bool bound;private int tier=-1;private Material? ringMaterial;
        // ADR-126 licensed sheets, indexed by FxArtRole.
        private FxArtSheet[] artSheets=Array.Empty<FxArtSheet>();private readonly Material[] artMaterials=new Material[(int)FxArtRole.Count];
        private static readonly List<ParticleSystemVertexStream> Streams=new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV,ParticleSystemVertexStream.UV2,ParticleSystemVertexStream.AnimBlend};
        private readonly List<GameObject> fires=new List<GameObject>();private Light? fireLight;private Vector3[] fireAt=Array.Empty<Vector3>();
        private static readonly Color FireLight=new Color(1f,.62f,.32f);
        // ADR-106: up to four short-lived point lights so muzzle flashes and explosions light their surroundings (Balanced/High).
        private readonly Light[] flashLights=new Light[4];private readonly int[] flashSlots=new int[4];
        private static readonly Color MuzzleLight=new Color(1f,.74f,.46f),BlastLight=new Color(1f,.8f,.52f);
        private AnimationCurve FadeSize=null!,SmokeSize=null!,RingSize=null!;
        // ADR-111 grenade blast curves.
        private AnimationCurve FlashSize=null!,FireballSize=null!,PuffSize=null!,EmberSize=null!,DustRingSize=null!,ColumnSize=null!;
        private Gradient FadeColor=null!,FlashColor=null!,FireColor=null!,SparkColor=null!,EmberColor=null!,ShockColor=null!,DustRingColor=null!,ColumnColor=null!,SheetFade=null!,PuffFade=null!;
        private static Gradient MakeFade(){var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});return g;}
        private static Gradient Ramp(Color[] colors,float[] at,float[] alpha,float[] alphaAt)
        {
            var c=new GradientColorKey[colors.Length];for(int k=0;k<c.Length;k++)c[k]=new GradientColorKey(colors[k],at[k]);
            var a=new GradientAlphaKey[alpha.Length];for(int k=0;k<a.Length;k++)a[k]=new GradientAlphaKey(alpha[k],alphaAt[k]);
            var g=new Gradient();g.SetKeys(c,a);return g;
        }
        private static AnimationCurve Curve(params float[] timeValue){var keys=new Keyframe[timeValue.Length/2];for(int k=0;k<keys.Length;k++)keys[k]=new Keyframe(timeValue[k*2],timeValue[k*2+1]);var curve=new AnimationCurve(keys);for(int k=0;k<keys.Length;k++)curve.SmoothTangents(k,0);return curve;}
        public static AnalyticFxPresenter Create(Transform parent,AnalyticFxPool pool)
        {
            var go=new GameObject("Procedural combat particles"){layer=VisualLayer};go.transform.SetParent(parent,false);var result=go.AddComponent<AnalyticFxPresenter>();result.Initialize(pool);return result;
        }
        private void Initialize(AnalyticFxPool pool)
        {
            FadeSize=AnimationCurve.Linear(0,1,1,.2f);SmokeSize=AnimationCurve.Linear(0,.3f,1,1.7f);RingSize=Curve(0,.1f,.35f,1.6f,1,2.4f);FadeColor=MakeFade();
            FlashSize=AnimationCurve.Linear(0,.7f,1,1.15f);FireballSize=Curve(0,1,.4f,1.6f,1,5.5f/3f);PuffSize=Curve(0,.45f,.5f,1.2f,1,1.5f);
            EmberSize=AnimationCurve.Linear(0,1,1,.3f);DustRingSize=Curve(0,.6f,.5f,1.6f,1,2.2f);ColumnSize=Curve(0,.4f,.25f,1f,1,2.2f);
            FlashColor=Ramp(new[]{Color.white,new Color(1f,.92f,.45f)},new[]{0f,1f},new[]{1f,0f},new[]{0f,1f});
            // Yellow -> orange -> deep red -> dark; multiplies the orange numeric flipbook.
            FireColor=Ramp(new[]{new Color(1f,.96f,.6f),new Color(1f,.58f,.16f),new Color(.78f,.13f,.04f),new Color(.12f,.07f,.05f)},new[]{0f,.22f,.6f,1f},new[]{1f,1f,0f},new[]{0f,.4f,1f});
            SparkColor=Ramp(new[]{new Color(1f,1f,.88f),new Color(1f,.7f,.22f),new Color(.95f,.28f,.05f)},new[]{0f,.45f,1f},new[]{1f,1f,0f},new[]{0f,.6f,1f});
            EmberColor=Ramp(new[]{new Color(1f,.75f,.32f),new Color(1f,.38f,.08f),new Color(.55f,.1f,.03f)},new[]{0f,.55f,1f},new[]{.95f,.85f,0f},new[]{0f,.7f,1f});
            ShockColor=Ramp(new[]{Color.white,new Color(1f,.75f,.45f)},new[]{0f,1f},new[]{1f,0f},new[]{0f,1f});
            DustRingColor=Ramp(new[]{new Color(.52f,.4f,.27f),new Color(.62f,.53f,.4f)},new[]{0f,1f},new[]{.8f,.55f,0f},new[]{0f,.45f,1f});
            // Dark grey -> grey rising column.
            ColumnColor=Ramp(new[]{new Color(.16f,.15f,.14f),new Color(.33f,.32f,.31f),new Color(.58f,.58f,.58f)},new[]{0f,.35f,1f},new[]{0f,.85f,.6f,0f},new[]{0f,.07f,.6f,1f});
            // ADR-126: the licensed explosion sheet carries its own colour and burns out into smoke; only fade the end.
            SheetFade=Ramp(new[]{Color.white,Color.white},new[]{0f,1f},new[]{1f,1f,0f},new[]{0f,.72f,1f});
            // Licensed smoke and dust puffs ease in so they do not pop at full size.
            PuffFade=Ramp(new[]{Color.white,Color.white},new[]{0f,1f},new[]{0f,1f,.8f,0f},new[]{0f,.12f,.55f,1f});
            this.pool=pool;int n=AnalyticFxPool.MaxInstances;systems=new ParticleSystem[n];renderers=new ParticleSystemRenderer[n];age=new float[n];active=new bool[n];kind=new FxKind[n];
            for(int i=0;i<n;i++)
            {
                var go=new GameObject("Pooled FX "+i){layer=VisualLayer};go.transform.SetParent(transform,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.playOnAwake=false;main.loop=false;main.maxParticles=24;main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=false;
                var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;var sheet=ps.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=8;sheet.numTilesY=8;sheet.cycleCount=1;sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));
                var limit=ps.limitVelocityOverLifetime;limit.enabled=false;limit.limit=new ParticleSystem.MinMaxCurve(1000f);
                var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                // ADR-126 frame blending: the shader lerps to the next flipbook frame by AnimBlend.
                renderer.SetActiveVertexStreams(Streams);
                systems[i]=ps;renderers[i]=renderer;ps.useAutoRandomSeed=false;ps.randomSeed=(uint)i+1;ps.Pause();
            }
            for(int l=0;l<flashLights.Length;l++)
            {
                var go=new GameObject("Flash light "+l);go.transform.SetParent(transform,false);var light=go.AddComponent<Light>();
                light.type=LightType.Point;light.shadows=LightShadows.None;light.color=MuzzleLight;light.renderMode=LightRenderMode.ForcePixel;light.enabled=false;
                flashLights[l]=light;
            }
        }
        public void BindVerifiedVisuals(PackagedVisualPayloads visuals){foreach(var step in BindVerifiedVisualSteps(visuals)) { }}
        public IEnumerable<int> BindVerifiedVisualSteps(PackagedVisualPayloads visuals)
        {
            ClearVisuals();ReleaseMaterials();var shader=Shader.Find("Shooter/CombatParticle")??throw new InvalidOperationException("Required combat particle shader missing.");
            for(int i=0;i<5;i++)
            {
                var data=MapVisualClosure.DecodeMaterial(visuals,FxFlipbook.Path((FlipbookKind)i),FxFlipbook.Fingerprint);
                var texture=new Texture2D(data.Width,data.Height,TextureFormat.RGBA32,false,true){name="Verified "+(FlipbookKind)i+" flipbook",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};owned.Add(texture);
                var bytes=AnalyticFxPool.FlipTextureRows(data.MipBytes[0],data.Width,data.Height);texture.LoadRawTextureData(bytes);texture.Apply(false,true);
                var material=new Material(shader){name="Numeric "+(FlipbookKind)i};owned.Add(material);material.SetTexture("_BaseMap",texture);bool additive=i>=2;material.SetFloat("_DstBlend",additive?(float)BlendMode.One:(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_Brightness",additive?5f:1f);material.SetFloat("_FrameBlend",1f);materials[i]=material;
                yield return 0;
            }
            ringMaterial=new Material(materials[2]){name="Numeric shockwave ring"};ringMaterial.SetFloat("_Ring",1);ringMaterial.SetFloat("_FrameBlend",0f);owned.Add(ringMaterial);
            // ADR-126: licensed sheets; a missing catalog or sheet throws (no generated fallback).
            var catalog=FxArtCatalog.Load();artSheets=catalog.Sheets;
            for(int r=0;r<artSheets.Length;r++)
            {
                var sheet=artSheets[r];var material=new Material(shader){name="Licensed "+sheet.Role};owned.Add(material);material.SetTexture("_BaseMap",catalog.Textures[r]);
                material.SetFloat("_DstBlend",sheet.Blend==FxArtBlend.Additive?(float)BlendMode.One:(float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_Brightness",sheet.Brightness);material.SetFloat("_Lit",sheet.Lit);material.SetFloat("_Emission",sheet.Emission);material.SetFloat("_FrameBlend",1f);
                artMaterials[r]=material;
            }
            yield return 0;
            bound=true;tier=-1;
        }
        public void Sync(bool suppressMuzzleFlash=false)
        {
            if(pool==null||!bound)return;
            int quality=QualitySettings.GetQualityLevel();
            if(tier!=quality)
            {
                tier=quality;foreach(var material in materials)material.SetFloat("_SoftParticles",quality>0?1:0);foreach(var material in artMaterials)material.SetFloat("_SoftParticles",quality>0?1:0);
                for(int f=0;f<fires.Count;f++)fires[f].SetActive(f<AmbientFireAnchors.Budget(quality));
            }
            for(int i=0;i<systems.Length;i++)
            {
                var fx=pool.Get(i);var ps=systems[i];
                // Staggered blast layers wait at negative age; ReducedFlash hides every bright additive layer.
                bool hide=!fx.Active||fx.Age<0f||suppressMuzzleFlash&&AnalyticFxPool.IsFlashing(fx.Kind);
                if(hide){if(active[i])ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);active[i]=false;continue;}
                bool born=!active[i]||fx.Age<age[i]||kind[i]!=fx.Kind;
                if(born)
                {
                    // Paused, not stopped: a paused system takes Emit everywhere (a stopped one ignores it outside play
                    // mode) and never advances on its own, so only the pool age below moves it.
                    ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);ps.Play(false);ps.Pause(false);
                    if(IsBlastLayer(fx))EmitBlast(i,fx,ps);else EmitCombat(i,fx,ps);
                }
                else if(fx.Age>age[i])ps.Simulate(fx.Age-age[i],false,false,false);
                active[i]=true;age[i]=fx.Age;kind[i]=fx.Kind;
            }
            SyncFlashLights();
            SyncFireLight(suppressMuzzleFlash);
        }
        private static bool IsBlastLayer(in FxInstance fx)=>AnalyticFxPool.IsBlast(fx.Kind)||fx.Variant==AnalyticFxPool.BlastVariant&&(fx.Kind==FxKind.SmokeProxy||fx.Kind==FxKind.Debris);
        /// <summary>Gunfire, surface impacts and ambient proxies (pre-ADR-111 look).</summary>
        private void EmitCombat(int i,in FxInstance fx,ParticleSystem ps)
        {
            bool concrete=fx.Variant==ImpactSurfaceFx.ConcreteVariant&&(fx.Kind==FxKind.Dust||fx.Kind==FxKind.Debris),dirt=fx.Variant==ImpactSurfaceFx.DirtVariant&&(fx.Kind==FxKind.Dust||fx.Kind==FxKind.Debris);
            float remaining=Mathf.Max(.02f,fx.Lifetime-fx.Age);
            // Generated sheets: muzzle star, spark streaks (sparks, tracers, casings, debris chips). ADR-126 licensed
            // sheets: muzzle fire ball, gun smoke and impact/footstep dust (tinted per surface).
            int texture=fx.Kind==FxKind.MuzzleFlash?3:4;
            var renderer=renderers[i];var role=FxArtRoles.For(fx.Kind,i);
            if(role is FxArtRole art)UseSheet(i,ps,renderer,art,remaining);else UseGenerated(ps,renderer,materials[texture]);
            renderer.renderMode=fx.Kind==FxKind.Tracer?ParticleSystemRenderMode.Stretch:ParticleSystemRenderMode.Billboard;
            renderer.velocityScale=.03f;renderer.lengthScale=2;
            // Smoke sorts behind the additive flash it spawns with so its alpha blend cannot hide it; the fire ball sits
            // between the smoke and the star.
            renderer.sortingFudge=fx.Kind==FxKind.SmokeProxy?2f:fx.Kind==FxKind.MuzzleFlash?-2f:fx.Kind==FxKind.MuzzleFire?-1f:0f;
            // Licensed puffs are denser than the old numeric ones, so fewer of them cover the same cloud.
            int count=fx.Kind==FxKind.SmokeProxy?(tier==0?2:3):fx.Kind==FxKind.Dust?(tier==0?3:5):fx.Kind==FxKind.ImpactSpark?(tier==0?4:12):fx.Kind==FxKind.Debris?(tier==0?3:7):1;
            var main=ps.main;main.maxParticles=24;main.gravityModifier=fx.Kind==FxKind.ShellCasing||fx.Kind==FxKind.Debris?.7f:dirt?.3f:0;
            var limit=ps.limitVelocityOverLifetime;limit.enabled=false;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,fx.Kind==FxKind.SmokeProxy||fx.Kind==FxKind.Dust?SmokeSize:FadeSize);
            var color=ps.colorOverLifetime;color.enabled=true;color.color=fx.Kind==FxKind.SmokeProxy||fx.Kind==FxKind.Dust?PuffFade:FadeColor;
            Vector3 normal=new Vector3(fx.Normal.X,fx.Normal.Y,fx.Normal.Z);
            // Surface tints over the grey licensed puff: concrete pale grey, dirt brown, footsteps faint tan.
            Color dust=concrete?new Color(.8f,.78f,.74f,.75f):dirt?new Color(.55f,.43f,.3f,.85f):new Color(.66f,.6f,.5f,.4f);
            for(int p=0;p<count;p++)
            {
                float turn=(p*2.399963f+i*.31f);Vector3 spread=new Vector3(Mathf.Cos(turn),.3f+(p%3)*.22f,Mathf.Sin(turn));
                var emit=new ParticleSystem.EmitParams{position=new Vector3(fx.Position.X,fx.Position.Y,fx.Position.Z),startLifetime=remaining,startColor=Color.white,rotation=turn};
                switch(fx.Kind)
                {
                    case FxKind.SmokeProxy:emit.velocity=Vector3.up*.25f+normal*(.35f+.15f*p)+spread*.06f;emit.startSize=.42f+.08f*p;emit.startColor=new Color(.86f,.86f,.86f,.3f);emit.rotation=Hash(i,p,3)*360f;break;
                    case FxKind.Dust:
                        emit.velocity=dirt?normal*1.1f+Vector3.up*1.4f+spread*.35f:normal*.6f+spread*.3f;emit.startSize=concrete?.6f:dirt?.7f:.55f;emit.startColor=dust;emit.rotation=Hash(i,p,3)*360f;break;
                    case FxKind.MuzzleFire:emit.velocity=normal*.6f;emit.startSize=MuzzleSize(fx.Variant)*.8f;emit.startColor=new Color(1f,.82f,.6f,1f);emit.rotation=Hash(i,p,3)*360f;break;
                    case FxKind.Tracer:emit.velocity=normal*70;emit.startSize=.18f;break;
                    case FxKind.MuzzleFlash:emit.velocity=normal*.5f+spread*1.7f;emit.startSize=MuzzleSize(fx.Variant);break;
                    default:
                        emit.velocity=normal*.5f+spread*1.7f;emit.startSize=concrete||dirt?.07f:.08f;
                        if(fx.Kind==FxKind.Debris&&(concrete||dirt))emit.startColor=new Color(.62f,.62f,.62f,1f);break;
                }
                ps.Emit(emit,1);
            }
        }
        /// <summary>
        /// ADR-111 grenade blast layers. Counts scale by tier (Low stays cheap: 40 sparks, 8 embers, 10 dust, 8 smoke).
        /// Particle randomness is a fixed hash of slot and index so a blast looks the same every replay.
        /// </summary>
        private void EmitBlast(int i,in FxInstance fx,ParticleSystem ps)
        {
            var renderer=renderers[i];var main=ps.main;var limit=ps.limitVelocityOverLifetime;var size=ps.sizeOverLifetime;var color=ps.colorOverLifetime;
            size.enabled=true;color.enabled=true;renderer.velocityScale=.03f;renderer.lengthScale=2;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            Vector3 at=new Vector3(fx.Position.X,fx.Position.Y,fx.Position.Z),n=new Vector3(fx.Normal.X,fx.Normal.Y,fx.Normal.Z);
            Vector3 side=Vector3.Cross(Mathf.Abs(n.y)<.95f?Vector3.up:Vector3.right,n).normalized,across=Vector3.Cross(n,side);
            float remaining=Mathf.Max(.02f,fx.Lifetime-fx.Age);
            int count=1;float gravity=0f,drag=0f,fudge=0f;Material? material=materials[2];AnimationCurve sizeCurve=FadeSize;Gradient gradient=FadeColor;
            switch(fx.Kind)
            {
                case FxKind.BlastFlash:sizeCurve=FlashSize;gradient=FlashColor;fudge=-3f;break;
                // ADR-126: licensed explosion sheet; it carries its own fire-to-smoke colour, so only its end fades.
                case FxKind.Fireball:sizeCurve=FireballSize;gradient=SheetFade;fudge=-1f;count=tier==0?1:2;break;
                case FxKind.FirePuff:sizeCurve=PuffSize;gradient=FireColor;fudge=-1f;count=tier==0?1:2;drag=3.2f;break;
                case FxKind.Sparks:material=materials[4];gradient=SparkColor;count=AnalyticFxPool.SparkCount(tier);gravity=1f;drag=.6f;
                    renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.velocityScale=.035f;renderer.lengthScale=1.2f;break;
                case FxKind.Embers:sizeCurve=EmberSize;gradient=EmberColor;count=AnalyticFxPool.EmberCount(tier);gravity=-.02f;drag=.9f;break;
                case FxKind.Shockwave:material=ringMaterial;sizeCurve=RingSize;gradient=ShockColor;renderer.renderMode=ParticleSystemRenderMode.HorizontalBillboard;break;
                case FxKind.DustRing:material=materials[1];sizeCurve=DustRingSize;gradient=DustRingColor;count=AnalyticFxPool.DustRingParticles(tier);drag=2.4f;fudge=2f;break;
                case FxKind.Debris:material=materials[4];count=tier==0?3:7;gravity=.9f;break;
                case FxKind.SmokeProxy:material=materials[0];sizeCurve=ColumnSize;gradient=ColumnColor;count=AnalyticFxPool.BlastSmokeParticles(tier);drag=.35f;fudge=3f;break;
            }
            var role=FxArtRoles.For(fx.Kind,i);
            if(role is FxArtRole art)UseSheet(i,ps,renderer,art,remaining);else UseGenerated(ps,renderer,material!);
            renderer.sortingFudge=fudge;
            main.maxParticles=Mathf.Max(24,count+4);main.gravityModifier=gravity;
            limit.enabled=drag>0f;if(drag>0f)limit.drag=new ParticleSystem.MinMaxCurve(drag);
            size.size=new ParticleSystem.MinMaxCurve(1,sizeCurve);color.color=gradient;
            for(int p=0;p<count;p++)
            {
                float r0=Hash(i,p,0),r1=Hash(i,p,1),r2=Hash(i,p,2),r3=Hash(i,p,3),turn=p*2.399963f+i*.31f;
                Vector3 ring=side*Mathf.Cos(turn)+across*Mathf.Sin(turn);
                var emit=new ParticleSystem.EmitParams{position=at,velocity=Vector3.zero,startLifetime=remaining,startColor=Color.white,rotation=r3*360f,startSize=1f};
                switch(fx.Kind)
                {
                    case FxKind.BlastFlash:emit.startSize=6f;break;
                    case FxKind.Fireball:emit.startSize=3.4f*(p==0?1f:.8f);emit.velocity=n*(.4f+.5f*p);emit.position=at+n*.35f+ring*(.35f*p);break;
                    case FxKind.FirePuff:emit.startSize=1.2f+.5f*r0;emit.velocity=n*(3f+2.2f*r1)+ring*.4f;emit.position=at+n*.15f*p;break;
                    case FxKind.Sparks:
                    {
                        // Hemisphere around the surface normal, biased upward so streaks arc and fall.
                        Vector3 dir=(n*(.35f+r0)+ring*(.4f+.9f*r1)+Vector3.up*.3f).normalized;
                        emit.velocity=dir*(7f+10f*r2);emit.startSize=.05f+.04f*r3;
                        emit.startLifetime=Mathf.Max(.02f,AnalyticFxPool.SparkMinLifetime+(AnalyticFxPool.SparksLifetime-AnalyticFxPool.SparkMinLifetime)*r1-fx.Age);break;
                    }
                    case FxKind.Embers:
                        emit.position=at+ring*(.3f*r0)+n*.2f;emit.velocity=Vector3.up*(1f+1.6f*r1)+ring*(.6f+1.2f*r2);emit.startSize=.06f+.06f*r3;
                        emit.startLifetime=Mathf.Max(.02f,remaining*(.65f+.35f*r0));break;
                    case FxKind.Shockwave:emit.startSize=5f;emit.startColor=new Color(1f,.9f,.72f,1f);break;
                    case FxKind.DustRing:
                        emit.position=at+ring*.4f+n*.15f;emit.velocity=ring*(6f+2.5f*r0)+n*(.25f+.4f*r1);emit.startSize=1.3f+.6f*r2;
                        emit.startLifetime=Mathf.Max(.02f,remaining*(.8f+.2f*r3));break;
                    case FxKind.Debris:
                        emit.velocity=n*(2.5f+2f*r0)+ring*(2.5f+3.5f*r1)+Vector3.up*1.2f;emit.startSize=.06f+.05f*r2;emit.startColor=new Color(.38f,.32f,.27f,1f);break;
                    case FxKind.SmokeProxy:
                        // Rising column: stacked puffs drift up and widen; later puffs start higher.
                        emit.position=at+ring*(.5f*r0)+Vector3.up*(.2f+1.3f*p/Mathf.Max(1,count-1));
                        emit.velocity=Vector3.up*(.7f+.9f*r1)+ring*(.25f*r2);emit.startSize=1.6f+.8f*r3;
                        emit.startLifetime=Mathf.Max(.02f,remaining*(.75f+.25f*r1));break;
                }
                ps.Emit(emit,1);
            }
        }
        /// <summary>ADR-126 licensed sheet: material, grid and playback (looping sheets start on a random frame).</summary>
        private void UseSheet(int slot,ParticleSystem ps,ParticleSystemRenderer renderer,FxArtRole role,float lifetime)
        {
            var sheet=artSheets[(int)role];renderer.sharedMaterial=artMaterials[(int)role];
            var anim=ps.textureSheetAnimation;anim.numTilesX=sheet.Columns;anim.numTilesY=sheet.Rows;
            var (cycles,end)=FxArtRoles.Playback(sheet,lifetime);anim.cycleCount=cycles;anim.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,end));
            anim.startFrame=FxArtRoles.Loops(role)?new ParticleSystem.MinMaxCurve(0f,.999f):new ParticleSystem.MinMaxCurve(0f);
        }
        /// <summary>Generated 8x8 numeric sheets play once over the particle life.</summary>
        private static void UseGenerated(ParticleSystem ps,ParticleSystemRenderer renderer,Material material)
        {
            renderer.sharedMaterial=material;var anim=ps.textureSheetAnimation;anim.numTilesX=8;anim.numTilesY=8;anim.cycleCount=1;
            anim.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));anim.startFrame=new ParticleSystem.MinMaxCurve(0f);
        }
        private static float Hash(int slot,int index,int lane){unchecked{uint h=(uint)(slot*73856093)^(uint)(index*19349663)^(uint)(lane*83492791);h^=h>>13;h*=0x5bd1e995;h^=h>>15;return (h&0xFFFF)/65535f;}}
        /// <summary>Lights the youngest flashes: muzzle 60 ms over 5 m; grenade blasts 16 over 22 m, flickering for 0.45 s. Low has none.</summary>
        private void SyncFlashLights()
        {
            int used=0;
            if(tier>0&&pool!=null)
                for(int i=0;i<systems.Length;i++)
                {
                    if(!active[i]||kind[i]!=FxKind.MuzzleFlash&&kind[i]!=FxKind.Fireball)continue;
                    float window=kind[i]==FxKind.Fireball?AnalyticFxPool.BlastLightSeconds:.06f;if(age[i]>=window)continue;
                    if(used<flashSlots.Length){flashSlots[used++]=i;continue;}
                    int oldest=0;for(int k=1;k<used;k++)if(age[flashSlots[k]]>age[flashSlots[oldest]])oldest=k;
                    if(age[i]<age[flashSlots[oldest]])flashSlots[oldest]=i;
                }
            for(int l=0;l<flashLights.Length;l++)
            {
                var light=flashLights[l];if(light==null)continue;
                if(l>=used){if(light.enabled)light.enabled=false;continue;}
                int i=flashSlots[l];var fx=pool!.Get(i);bool fire=kind[i]==FxKind.Fireball;
                light.transform.position=new Vector3(fx.Position.X,fx.Position.Y,fx.Position.Z);
                if(fire){light.range=AnalyticFxPool.BlastLightRange;light.intensity=AnalyticFxPool.BlastLightAt(age[i]);light.color=BlastLight;}
                else{float t=Mathf.Clamp01(age[i]/.06f),fade=(1-t)*(1-t);light.range=5f;light.intensity=3.2f*fade;light.color=MuzzleLight;}
                light.enabled=true;
            }
        }
        /// <summary>Variants 0..2 are the legacy rifle/pistol/carbine flashes; 10+ carry the ADR-102 size in hundredths.</summary>
        private static float MuzzleSize(byte variant) => variant >= 10 ? .65f * variant / 100f : variant == 1 ? .42f : variant == 2 ? .54f : .65f;
        /// <summary>
        /// ADR-126 burning wrecks: a flame column on the roof of a few map vehicles and a dark plume rising from above
        /// it, picked per map by <see cref="AmbientFireAnchors"/>. Cosmetic only: no damage, collision, navigation or
        /// bot sight change. Low shows the first two fires and no fire light.
        /// </summary>
        public void BindAmbientFires(string mapId,IReadOnlyList<AmbientFireSite> sites)
        {
            if(!bound)throw new InvalidOperationException("Bind the verified effect visuals before the ambient fires.");
            RemoveFires();
            var anchors=AmbientFireAnchors.Pick(mapId,sites,AmbientFireAnchors.MaxFires);
            int quality=QualitySettings.GetQualityLevel();fireAt=new Vector3[anchors.Length];
            for(int f=0;f<anchors.Length;f++)
            {
                var a=anchors[f];var at=new Vector3(a.Base.X,a.Base.Y,a.Base.Z);fireAt[f]=at+Vector3.up*a.Scale;
                var root=new GameObject("Burning wreck "+a.Site){layer=VisualLayer};root.transform.SetParent(transform,false);root.transform.position=at;
                var flame=Looping(root.transform,"Flame",FxArtRole.Flame,1.1f,3.5f,f*2+1,new Vector3(0,a.Scale*1.25f,0));
                {
                    var main=flame.main;main.startSize3D=true;main.startSizeX=a.Scale*1.6f;main.startSizeY=a.Scale*3.2f;main.startSizeZ=1f;
                    var shape=flame.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(a.Scale*.35f,0f,a.Scale*.35f);
                    var velocity=flame.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=new ParticleSystem.MinMaxCurve(0f);velocity.y=new ParticleSystem.MinMaxCurve(.35f);velocity.z=new ParticleSystem.MinMaxCurve(0f);
                    var color=flame.colorOverLifetime;color.enabled=true;color.color=Ramp(new[]{Color.white,Color.white},new[]{0f,1f},new[]{0f,1f,1f,0f},new[]{0f,.15f,.7f,1f});
                    var renderer=flame.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.VerticalBillboard;renderer.flip=new Vector3(.5f,0f,0f);renderer.sortingFudge=-1f;
                }
                var plume=Looping(root.transform,"Smoke plume",FxArtRole.SmokePlume,7f,1.6f,f*2+2,new Vector3(0,a.Scale*3f,0));
                {
                    var main=plume.main;main.startSize3D=true;main.startSizeX=a.Scale*1.6f;main.startSizeY=a.Scale*3.2f;main.startSizeZ=1f;main.startColor=new Color(.2f,.19f,.18f,1f);
                    var shape=plume.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(a.Scale*.4f,0f,a.Scale*.4f);
                    // Rises and drifts with one shared light wind so every plume on a map leans the same way.
                    var velocity=plume.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=new ParticleSystem.MinMaxCurve(.35f);velocity.y=new ParticleSystem.MinMaxCurve(1.1f);velocity.z=new ParticleSystem.MinMaxCurve(.15f);
                    var size=plume.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,Curve(0,.6f,.5f,1.5f,1,2.2f));
                    var color=plume.colorOverLifetime;color.enabled=true;color.color=Ramp(new[]{Color.white,Color.white},new[]{0f,1f},new[]{0f,.8f,.55f,0f},new[]{0f,.12f,.6f,1f});
                    plume.GetComponent<ParticleSystemRenderer>().sortingFudge=2f;
                }
                flame.Play(true);plume.Play(true);
                root.SetActive(f<AmbientFireAnchors.Budget(quality));fires.Add(root);
            }
            if(fireLight==null)
            {
                var go=new GameObject("Wreck fire light");go.transform.SetParent(transform,false);fireLight=go.AddComponent<Light>();
                fireLight.type=LightType.Point;fireLight.shadows=LightShadows.None;fireLight.color=FireLight;fireLight.renderMode=LightRenderMode.ForcePixel;fireLight.range=9f;fireLight.enabled=false;
            }
        }
        /// <summary>Looping licensed-sheet emitter (prewarmed so a fire is already burning when the match starts).</summary>
        private ParticleSystem Looping(Transform parent,string name,FxArtRole role,float lifetime,float rate,int seed,Vector3 local)
        {
            var go=new GameObject(name){layer=VisualLayer};go.transform.SetParent(parent,false);go.transform.localPosition=local;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(seed*7919+13);
            var main=ps.main;main.playOnAwake=false;main.loop=true;main.prewarm=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=lifetime;main.startSpeed=0f;
            main.maxParticles=Mathf.CeilToInt(rate*lifetime)+4;
            var emission=ps.emission;emission.enabled=true;emission.rateOverTime=rate;
            var sheet=artSheets[(int)role];var anim=ps.textureSheetAnimation;anim.enabled=true;anim.numTilesX=sheet.Columns;anim.numTilesY=sheet.Rows;
            var (cycles,end)=FxArtRoles.Playback(sheet,lifetime);anim.cycleCount=cycles;anim.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,end));anim.startFrame=new ParticleSystem.MinMaxCurve(0f,.999f);
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=artMaterials[(int)role];renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.SetActiveVertexStreams(Streams);
            return ps;
        }
        /// <summary>One flickering light on the nearest burning wreck within 40 m (Balanced/High); steady under ReducedFlash.</summary>
        private void SyncFireLight(bool steady)
        {
            if(fireLight==null)return;
            var view=Camera.main;int nearest=-1;float best=40f*40f;
            if(tier>0&&view!=null)
                for(int f=0;f<fires.Count&&f<fireAt.Length;f++)
                {
                    if(!fires[f].activeSelf)continue;float d=(fireAt[f]-view.transform.position).sqrMagnitude;
                    if(d<best){best=d;nearest=f;}
                }
            if(nearest<0){if(fireLight.enabled)fireLight.enabled=false;return;}
            fireLight.transform.position=fireAt[nearest];
            fireLight.intensity=steady?2.2f:2.4f*(.8f+.2f*Mathf.PerlinNoise(Time.time*6f,nearest*3.1f));
            fireLight.enabled=true;
        }
        private void RemoveFires(){foreach(var root in fires)if(root!=null){if(Application.isPlaying)Destroy(root);else DestroyImmediate(root);}fires.Clear();fireAt=Array.Empty<Vector3>();if(fireLight!=null)fireLight.enabled=false;}
        /// <summary>Burning wrecks built for the bound map (Low shows only the first <see cref="AmbientFireAnchors.MaxFiresLow"/>).</summary>
        public int AmbientFireCount=>fires.Count;
        public void ClearVisuals(){for(int i=0;i<systems.Length;i++){if(systems[i]!=null)systems[i].Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);active[i]=false;}}
        private void ReleaseMaterials(){RemoveFires();foreach(var value in owned)if(value!=null){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}owned.Clear();bound=false;}
        private void OnDestroy(){ClearVisuals();ReleaseMaterials();}
    }
}
#endif
