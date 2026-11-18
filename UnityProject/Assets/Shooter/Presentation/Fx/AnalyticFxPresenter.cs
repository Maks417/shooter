#if UNITY_5_3_OR_NEWER
#nullable enable
using System;using System.Collections.Generic;using Shooter.Generation;using Shooter.Generation.Environment;using Shooter.Generation.Fx;using UnityEngine;using UnityEngine.Rendering;
namespace Shooter.Presentation.Fx
{
    /// <summary>Bounded code-only particle adaptation of the pure lifetime pool. Simulation is advanced from pool age.</summary>
    public sealed class AnalyticFxPresenter:MonoBehaviour
    {
        public const int VisualLayer=11;
        private AnalyticFxPool? pool;private readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        private ParticleSystem[] systems=Array.Empty<ParticleSystem>();private ParticleSystemRenderer[] renderers=Array.Empty<ParticleSystemRenderer>();
        private readonly Material[] materials=new Material[5];private float[] age=Array.Empty<float>();private bool[] active=Array.Empty<bool>();private FxKind[] kind=Array.Empty<FxKind>();
        private bool bound;private int tier=-1;private Material? ringMaterial;
        // ADR-106: up to four short-lived point lights so muzzle flashes and explosions light their surroundings (Balanced/High).
        private readonly Light[] flashLights=new Light[4];private readonly int[] flashSlots=new int[4];
        private static readonly Color MuzzleLight=new Color(1f,.74f,.46f),BlastLight=new Color(1f,.8f,.52f);
        private AnimationCurve FadeSize=null!,SmokeSize=null!,RingSize=null!;
        // ADR-111 grenade blast curves.
        private AnimationCurve FlashSize=null!,FireballSize=null!,PuffSize=null!,EmberSize=null!,DustRingSize=null!,ColumnSize=null!;
        private Gradient FadeColor=null!,FlashColor=null!,FireColor=null!,SparkColor=null!,EmberColor=null!,ShockColor=null!,DustRingColor=null!,ColumnColor=null!;
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
            this.pool=pool;int n=AnalyticFxPool.MaxInstances;systems=new ParticleSystem[n];renderers=new ParticleSystemRenderer[n];age=new float[n];active=new bool[n];kind=new FxKind[n];
            for(int i=0;i<n;i++)
            {
                var go=new GameObject("Pooled FX "+i){layer=VisualLayer};go.transform.SetParent(transform,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.playOnAwake=false;main.loop=false;main.maxParticles=24;main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=false;
                var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;var sheet=ps.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=8;sheet.numTilesY=8;sheet.cycleCount=1;sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));
                var limit=ps.limitVelocityOverLifetime;limit.enabled=false;limit.limit=new ParticleSystem.MinMaxCurve(1000f);
                var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
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
                var material=new Material(shader){name="Numeric "+(FlipbookKind)i};owned.Add(material);material.SetTexture("_BaseMap",texture);bool additive=i>=2;material.SetFloat("_DstBlend",additive?(float)BlendMode.One:(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_Brightness",additive?5f:1f);materials[i]=material;
                yield return 0;
            }
            ringMaterial=new Material(materials[2]){name="Numeric shockwave ring"};ringMaterial.SetFloat("_Ring",1);owned.Add(ringMaterial);
            bound=true;tier=-1;
        }
        public void Sync(bool suppressMuzzleFlash=false)
        {
            if(pool==null||!bound)return;
            int quality=QualitySettings.GetQualityLevel();
            if(tier!=quality){tier=quality;foreach(var material in materials)material.SetFloat("_SoftParticles",quality>0?1:0);}
            for(int i=0;i<systems.Length;i++)
            {
                var fx=pool.Get(i);var ps=systems[i];
                // Staggered blast layers wait at negative age; ReducedFlash hides every bright additive layer.
                bool hide=!fx.Active||fx.Age<0f||suppressMuzzleFlash&&AnalyticFxPool.IsFlashing(fx.Kind);
                if(hide){if(active[i])ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);active[i]=false;continue;}
                bool born=!active[i]||fx.Age<age[i]||kind[i]!=fx.Kind;
                if(born)
                {
                    ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    if(IsBlastLayer(fx))EmitBlast(i,fx,ps);else EmitCombat(i,fx,ps);
                }
                else if(fx.Age>age[i])ps.Simulate(fx.Age-age[i],false,false,false);
                active[i]=true;age[i]=fx.Age;kind[i]=fx.Kind;
            }
            SyncFlashLights();
        }
        private static bool IsBlastLayer(in FxInstance fx)=>AnalyticFxPool.IsBlast(fx.Kind)||fx.Variant==AnalyticFxPool.BlastVariant&&(fx.Kind==FxKind.SmokeProxy||fx.Kind==FxKind.Debris);
        /// <summary>Gunfire, surface impacts and ambient proxies (pre-ADR-111 look).</summary>
        private void EmitCombat(int i,in FxInstance fx,ParticleSystem ps)
        {
            bool concrete=fx.Variant==ImpactSurfaceFx.ConcreteVariant&&(fx.Kind==FxKind.Dust||fx.Kind==FxKind.Debris),dirt=fx.Variant==ImpactSurfaceFx.DirtVariant&&(fx.Kind==FxKind.Dust||fx.Kind==FxKind.Debris);
            // Surface impacts: concrete reads as grey smoke-sheet dust and chips, dirt as brown kick-up and clods.
            int texture=concrete?0:dirt?1:fx.Kind==FxKind.MuzzleFlash?3:fx.Kind==FxKind.SmokeProxy?0:fx.Kind==FxKind.Dust?1:4;
            var renderer=renderers[i];renderer.sharedMaterial=materials[texture];
            renderer.renderMode=fx.Kind==FxKind.Tracer?ParticleSystemRenderMode.Stretch:ParticleSystemRenderMode.Billboard;
            renderer.velocityScale=.03f;renderer.lengthScale=2;
            // Smoke sorts behind the additive flash it spawns with so its alpha blend cannot hide it.
            renderer.sortingFudge=fx.Kind==FxKind.SmokeProxy?2f:fx.Kind==FxKind.MuzzleFlash?-2f:0f;
            int count=fx.Kind==FxKind.SmokeProxy||fx.Kind==FxKind.Dust?(tier==0?3:8):fx.Kind==FxKind.ImpactSpark?(tier==0?4:12):fx.Kind==FxKind.Debris?(tier==0?3:7):1;
            var main=ps.main;main.maxParticles=24;main.gravityModifier=fx.Kind==FxKind.ShellCasing||fx.Kind==FxKind.Debris?.7f:dirt?.3f:0;
            var limit=ps.limitVelocityOverLifetime;limit.enabled=false;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,fx.Kind==FxKind.SmokeProxy?SmokeSize:FadeSize);
            var color=ps.colorOverLifetime;color.enabled=true;color.color=FadeColor;
            Vector3 normal=new Vector3(fx.Normal.X,fx.Normal.Y,fx.Normal.Z);
            for(int p=0;p<count;p++)
            {
                float turn=(p*2.399963f+i*.31f);Vector3 spread=new Vector3(Mathf.Cos(turn),.3f+(p%3)*.22f,Mathf.Sin(turn));
                var emit=new ParticleSystem.EmitParams{position=new Vector3(fx.Position.X,fx.Position.Y,fx.Position.Z),velocity=fx.Kind==FxKind.SmokeProxy?Vector3.up*.25f:fx.Kind==FxKind.Tracer?normal*70:dirt&&fx.Kind==FxKind.Dust?normal*1.1f+Vector3.up*1.4f+spread*.35f:normal*.5f+spread*(fx.Kind==FxKind.Dust?.25f:1.7f),startLifetime=Mathf.Max(.02f,fx.Lifetime-fx.Age),startSize=fx.Kind==FxKind.SmokeProxy?1.1f:fx.Kind==FxKind.Dust?(concrete?.38f:dirt?.7f:.55f):fx.Kind==FxKind.MuzzleFlash?MuzzleSize(fx.Variant):fx.Kind==FxKind.Tracer?.18f:concrete||dirt?.07f:.08f,startColor=fx.Kind==FxKind.Debris&&(concrete||dirt)?new Color(.62f,.62f,.62f,1f):Color.white,rotation=turn};ps.Emit(emit,1);
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
                case FxKind.Fireball:sizeCurve=FireballSize;gradient=FireColor;fudge=-1f;count=tier==0?1:2;break;
                case FxKind.FirePuff:sizeCurve=PuffSize;gradient=FireColor;fudge=-1f;count=tier==0?1:2;drag=3.2f;break;
                case FxKind.Sparks:material=materials[4];gradient=SparkColor;count=AnalyticFxPool.SparkCount(tier);gravity=1f;drag=.6f;
                    renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.velocityScale=.035f;renderer.lengthScale=1.2f;break;
                case FxKind.Embers:sizeCurve=EmberSize;gradient=EmberColor;count=AnalyticFxPool.EmberCount(tier);gravity=-.02f;drag=.9f;break;
                case FxKind.Shockwave:material=ringMaterial;sizeCurve=RingSize;gradient=ShockColor;renderer.renderMode=ParticleSystemRenderMode.HorizontalBillboard;break;
                case FxKind.DustRing:material=materials[1];sizeCurve=DustRingSize;gradient=DustRingColor;count=AnalyticFxPool.DustRingParticles(tier);drag=2.4f;fudge=2f;break;
                case FxKind.Debris:material=materials[4];count=tier==0?3:7;gravity=.9f;break;
                case FxKind.SmokeProxy:material=materials[0];sizeCurve=ColumnSize;gradient=ColumnColor;count=AnalyticFxPool.BlastSmokeParticles(tier);drag=.35f;fudge=3f;break;
            }
            renderer.sharedMaterial=material;renderer.sortingFudge=fudge;
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
                    case FxKind.Fireball:emit.startSize=3f*(p==0?1f:.8f);emit.velocity=n*(.4f+.5f*p);break;
                    case FxKind.FirePuff:emit.startSize=1.5f+.5f*r0;emit.velocity=n*(3f+2.2f*r1)+ring*.4f;emit.position=at+n*.15f*p;break;
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
        public void ClearVisuals(){for(int i=0;i<systems.Length;i++){if(systems[i]!=null)systems[i].Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);active[i]=false;}}
        private void ReleaseMaterials(){foreach(var value in owned)if(value!=null){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}owned.Clear();bound=false;}
        private void OnDestroy(){ClearVisuals();ReleaseMaterials();}
    }
}
#endif
