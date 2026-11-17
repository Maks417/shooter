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
        private AnimationCurve FadeSize=null!;
        private AnimationCurve SmokeSize=null!;
        private AnimationCurve RingSize=null!;
        private Gradient FadeColor=null!;
        private static Gradient MakeFade(){var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});return g;}
        public static AnalyticFxPresenter Create(Transform parent,AnalyticFxPool pool)
        {
            var go=new GameObject("Procedural combat particles"){layer=VisualLayer};go.transform.SetParent(parent,false);var result=go.AddComponent<AnalyticFxPresenter>();result.Initialize(pool);return result;
        }
        private void Initialize(AnalyticFxPool pool)
        {
            FadeSize=AnimationCurve.Linear(0,1,1,.2f);SmokeSize=AnimationCurve.Linear(0,.3f,1,1.7f);RingSize=AnimationCurve.Linear(0,.1f,1,1.8f);FadeColor=MakeFade();
            this.pool=pool;int n=AnalyticFxPool.MaxInstances;systems=new ParticleSystem[n];renderers=new ParticleSystemRenderer[n];age=new float[n];active=new bool[n];kind=new FxKind[n];
            for(int i=0;i<n;i++)
            {
                var go=new GameObject("Pooled FX "+i){layer=VisualLayer};go.transform.SetParent(transform,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.playOnAwake=false;main.loop=false;main.maxParticles=24;main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=false;
                var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;var sheet=ps.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=8;sheet.numTilesY=8;sheet.cycleCount=1;sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));
                var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                systems[i]=ps;renderers[i]=renderer;ps.useAutoRandomSeed=false;ps.randomSeed=(uint)i+1;ps.Pause();
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
                var fx=pool.Get(i);var ps=systems[i];bool hide=!fx.Active||suppressMuzzleFlash&&(fx.Kind==FxKind.MuzzleFlash||fx.Kind==FxKind.Tracer||fx.Kind==FxKind.Fireball);
                if(hide){if(active[i])ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);active[i]=false;continue;}
                bool born=!active[i]||fx.Age<age[i]||kind[i]!=fx.Kind;
                if(born)
                {
                    ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    bool concrete=fx.Variant==ImpactSurfaceFx.ConcreteVariant&&(fx.Kind==FxKind.Dust||fx.Kind==FxKind.Debris),dirt=fx.Variant==ImpactSurfaceFx.DirtVariant&&(fx.Kind==FxKind.Dust||fx.Kind==FxKind.Debris);
                    // Surface impacts: concrete reads as grey smoke-sheet dust and chips, dirt as brown kick-up and clods.
                    int texture=concrete?0:dirt?1:fx.Kind==FxKind.MuzzleFlash?3:fx.Kind==FxKind.SmokeProxy?0:fx.Kind==FxKind.Dust?1:fx.Kind==FxKind.Fireball?2:4;
                    renderers[i].sharedMaterial=fx.Kind==FxKind.Shockwave?ringMaterial:materials[texture];
                    renderers[i].renderMode=fx.Kind==FxKind.Shockwave?ParticleSystemRenderMode.HorizontalBillboard:fx.Kind==FxKind.Tracer?ParticleSystemRenderMode.Stretch:ParticleSystemRenderMode.Billboard;
                    renderers[i].velocityScale=.03f;renderers[i].lengthScale=2;
                    // Smoke sorts behind the additive flash it spawns with so its alpha blend cannot hide it.
                    renderers[i].sortingFudge=fx.Kind==FxKind.SmokeProxy?2f:fx.Kind==FxKind.MuzzleFlash?-2f:0f;
                    int count=fx.Kind==FxKind.SmokeProxy||fx.Kind==FxKind.Dust?(tier==0?3:8):fx.Kind==FxKind.ImpactSpark?(tier==0?4:12):fx.Kind==FxKind.Debris?(tier==0?3:7):1;
                    var main=ps.main;main.gravityModifier=fx.Kind==FxKind.ShellCasing||fx.Kind==FxKind.Debris?.7f:dirt?.3f:0;
                    var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,fx.Kind==FxKind.Shockwave?RingSize:fx.Kind==FxKind.SmokeProxy?SmokeSize:FadeSize);
                    var color=ps.colorOverLifetime;color.enabled=true;color.color=FadeColor;
                    Vector3 normal=new Vector3(fx.Normal.X,fx.Normal.Y,fx.Normal.Z);
                    for(int p=0;p<count;p++)
                    {
                        float turn=(p*2.399963f+i*.31f);Vector3 spread=new Vector3(Mathf.Cos(turn),.3f+(p%3)*.22f,Mathf.Sin(turn));
                        var emit=new ParticleSystem.EmitParams{position=new Vector3(fx.Position.X,fx.Position.Y,fx.Position.Z),velocity=fx.Kind==FxKind.SmokeProxy?Vector3.up*.25f:fx.Kind==FxKind.Tracer?normal*70:dirt&&fx.Kind==FxKind.Dust?normal*1.1f+Vector3.up*1.4f+spread*.35f:normal*.5f+spread*(fx.Kind==FxKind.Dust?.25f:1.7f),startLifetime=Mathf.Max(.02f,fx.Lifetime-fx.Age),startSize=fx.Kind==FxKind.Fireball?3f:fx.Kind==FxKind.Shockwave?4f:fx.Kind==FxKind.SmokeProxy?1.1f:fx.Kind==FxKind.Dust?(concrete?.38f:dirt?.7f:.55f):fx.Kind==FxKind.MuzzleFlash?(fx.Variant==1?.42f:fx.Variant==2?.54f:.65f):fx.Kind==FxKind.Tracer?.18f:concrete||dirt?.07f:.08f,startColor=fx.Kind==FxKind.Debris&&(concrete||dirt)?new Color(.62f,.62f,.62f,1f):Color.white,rotation=turn};ps.Emit(emit,1);
                    }
                }
                else if(fx.Age>age[i])ps.Simulate(fx.Age-age[i],false,false,false);
                active[i]=true;age[i]=fx.Age;kind[i]=fx.Kind;
            }
        }
        public void ClearVisuals(){for(int i=0;i<systems.Length;i++){if(systems[i]!=null)systems[i].Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);active[i]=false;}}
        private void ReleaseMaterials(){foreach(var value in owned)if(value!=null){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}owned.Clear();bound=false;}
        private void OnDestroy(){ClearVisuals();ReleaseMaterials();}
    }
}
#endif
