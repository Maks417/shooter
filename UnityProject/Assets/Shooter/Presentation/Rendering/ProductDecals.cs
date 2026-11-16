#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Shooter.Presentation.Rendering
{
    /// <summary>Verified numeric atlas. Static layout paint and a bounded cosmetic impact-mark pool; no physics.</summary>
    public sealed class ProductDecals : MonoBehaviour
    {
        public const int ImpactCapacity=64;
        private sealed class Slot { public GameObject Root=null!;public MeshRenderer Mesh=null!;public DecalProjector Projector=null!;public float Until;public bool Dynamic; }
        private readonly List<Slot> slots=new List<Slot>();
        private readonly Slot?[] impacts=new Slot?[ImpactCapacity];
        private int cursor,tier=-1;private Texture2D? atlas;private Mesh? quad;
        private readonly Material[] materials=new Material[8];
        private MaterialPropertyBlock block=null!;
        public static ProductDecals Create(Transform parent,PackagedVisualPayloads visuals)
        {
            var go=new GameObject("Verified procedural decals"){layer=11};go.transform.SetParent(parent,false);
            var component=go.AddComponent<ProductDecals>();
            try{component.Initialize(visuals);return component;}
            catch{component.Release();DestroyOwned(go);throw;}
        }
        private void Initialize(PackagedVisualPayloads visuals)
        {
            block=new MaterialPropertyBlock();
            var data=MapVisualClosure.DecodeMaterial(visuals,ProceduralDecals.AtlasPath,ProceduralDecals.Fingerprint);
            atlas=new Texture2D(data.Width,data.Height,TextureFormat.RGBA32,false,true){name="Numeric decal atlas",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new byte[data.MipBytes[0].Count];for(int i=0;i<pixels.Length;i++)pixels[i]=data.MipBytes[0][i];atlas.LoadRawTextureData(pixels);atlas.Apply(false,true);
            var shader=Shader.Find("Shooter/ProceduralDecal")??throw new InvalidOperationException("Required procedural decal shader missing.");
            for(int i=0;i<materials.Length;i++)
            {
                materials[i]=new Material(shader){name="Numeric decal "+i,enableInstancing=true};materials[i].SetTexture("_BaseMap",atlas);
                materials[i].SetVector("_AtlasRect",new Vector4(i%4*.25f,i/4*.5f,.25f,.5f));
            }
            quad=new Mesh{name="Numeric decal quad"};
            quad.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
            quad.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};quad.triangles=new[]{0,1,2,0,2,3};quad.RecalculateNormals();quad.tangents=new[]{new Vector4(1,0,0,1),new Vector4(1,0,0,1),new Vector4(1,0,0,1),new Vector4(1,0,0,1)};quad.RecalculateBounds();
            var root=MapJsonValue.Parse(visuals.Bytes(ProceduralDecals.PlacementsPath)).Object;
            if(root["schema"].String!="shooter.visual-decals.v1"||root["decals"].Array.Count>256)throw new FormatException("Invalid verified decal placements.");
            foreach(var entry in root["decals"].Array)
            {
                var v=entry.Object;var xyz=v["position"].Array;
                var position=new Vector3((float)xyz[0].Number,(float)xyz[1].Number,(float)xyz[2].Number);
                CreateSlot(position,Vector3.up,(float)v["width"].Number,(float)v["height"].Number,(float)v["yaw"].Number,v["tile"].Int32,false);
            }
            ApplyTier();
        }
        private Slot CreateSlot(Vector3 position,Vector3 normal,float width,float height,float yaw,int tile,bool dynamic)
        {
            if(tile<0||tile>=materials.Length||width<=0||height<=0)throw new FormatException("Invalid decal tile/size.");
            var root=new GameObject(dynamic?"Impact mark":"Layout paint"){layer=11};root.transform.SetParent(transform,false);
            root.transform.SetPositionAndRotation(position+normal*.008f,Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.Euler(0,yaw,0));
            var meshGo=new GameObject("Low quad"){layer=11};meshGo.transform.SetParent(root.transform,false);meshGo.transform.localScale=new Vector3(width,1,height);
            meshGo.AddComponent<MeshFilter>().sharedMesh=quad;var renderer=meshGo.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[tile];renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var projectGo=new GameObject("URP projection"){layer=11};projectGo.transform.SetParent(root.transform,false);
            projectGo.transform.localRotation=Quaternion.Euler(90,0,0);
            var projector=projectGo.AddComponent<DecalProjector>();projector.material=materials[tile];projector.size=new Vector3(width,height,.35f);projector.drawDistance=80;
            var slot=new Slot{Root=root,Mesh=renderer,Projector=projector,Dynamic=dynamic};slots.Add(slot);SetTier(slot);return slot;
        }
        public void Impact(Float3 position,Float3 normal,bool scorch=false)
        {
            var slot=impacts[cursor];
            if(slot==null)
            {
                slot=CreateSlot(Vector3.zero,Vector3.up,scorch?2f:.22f,scorch?2f:.22f,0,scorch?2:3,true);impacts[cursor]=slot;
            }
            var n=new Vector3(normal.X,normal.Y,normal.Z).normalized;if(n.sqrMagnitude<.1f)n=Vector3.up;
            slot.Root.transform.SetPositionAndRotation(new Vector3(position.X,position.Y,position.Z)+n*.012f,Quaternion.FromToRotation(Vector3.up,n));
            float size=scorch?2f:.22f;slot.Mesh.transform.localScale=new Vector3(size,1,size);slot.Projector.size=new Vector3(size,size,.35f);
            slot.Mesh.sharedMaterial=materials[scorch?2:3];slot.Projector.material=materials[scorch?2:3];
            slot.Until=Time.unscaledTime+25;slot.Root.SetActive(true);SetTier(slot);cursor=(cursor+1)%ImpactCapacity;
        }
        private void Update()
        {
            if(tier!=QualitySettings.GetQualityLevel())ApplyTier();
            foreach(var slot in impacts)
            {
                if(slot==null||!slot.Root.activeSelf)continue;
                float left=slot.Until-Time.unscaledTime;if(left<=0){slot.Root.SetActive(false);continue;}
                float alpha=Mathf.Clamp01(left/3);slot.Projector.fadeFactor=alpha;block.SetFloat("_Opacity",alpha);slot.Mesh.SetPropertyBlock(block);
            }
        }
        private void ApplyTier(){tier=QualitySettings.GetQualityLevel();foreach(var slot in slots)SetTier(slot);}
        private void SetTier(Slot slot){slot.Mesh.enabled=QualitySettings.GetQualityLevel()==0;slot.Projector.enabled=QualitySettings.GetQualityLevel()>0;}
        private void OnDestroy()=>Release();
        private void Release(){foreach(var m in materials)DestroyOwned(m);DestroyOwned(atlas);DestroyOwned(quad);slots.Clear();}
        private static void DestroyOwned(UnityEngine.Object? value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
