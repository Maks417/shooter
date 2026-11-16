#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Generation.Noise;
namespace Shooter.Generation.Environment
{
    /// <summary>Integer-authored map grade. Lookup layout: x = blue * 32 + red, y = green.</summary>
    public sealed class MapGrade
    {
        public readonly int Red,Green,Blue,LiftRed,LiftGreen,LiftBlue,Contrast,Saturation;
        public MapGrade(int red=1000,int green=1000,int blue=1000,int liftRed=0,int liftGreen=0,int liftBlue=0,int contrast=1000,int saturation=1000)
        {
            if(red<700||red>1400||green<700||green>1400||blue<700||blue>1400||Math.Abs(liftRed)>100||Math.Abs(liftGreen)>100||Math.Abs(liftBlue)>100||contrast<800||contrast>1200||saturation<800||saturation>1200)throw new ArgumentOutOfRangeException(nameof(red));
            Red=red;Green=green;Blue=blue;LiftRed=liftRed;LiftGreen=liftGreen;LiftBlue=liftBlue;Contrast=contrast;Saturation=saturation;
        }
    }
    public static class AtmosphereBaker
    {
        public const int CubeSize=64,LutSize=32;
        private static readonly System.Collections.Generic.Dictionary<string,MaterialData> skyCache=new System.Collections.Generic.Dictionary<string,MaterialData>(StringComparer.Ordinal);
        public static MaterialData Grade(MapGrade g)
        {
            var bytes=new byte[LutSize*LutSize*LutSize*4];
            for(int b=0;b<LutSize;b++)for(int y=0;y<LutSize;y++)for(int r=0;r<LutSize;r++)
            {
                double red=r/31.0,green=y/31.0,blue=b/31.0;
                double luma=red*.2126+green*.7152+blue*.0722;
                red=luma+(red-luma)*g.Saturation/1000.0;green=luma+(green-luma)*g.Saturation/1000.0;blue=luma+(blue-luma)*g.Saturation/1000.0;
                int p=(y*LutSize*LutSize+b*LutSize+r)*4;
                bytes[p]=Byte(((red-.5)*g.Contrast/1000.0+.5)*g.Red/1000.0+g.LiftRed/1000.0);
                bytes[p+1]=Byte(((green-.5)*g.Contrast/1000.0+.5)*g.Green/1000.0+g.LiftGreen/1000.0);
                bytes[p+2]=Byte(((blue-.5)*g.Contrast/1000.0+.5)*g.Blue/1000.0+g.LiftBlue/1000.0);bytes[p+3]=255;
            }
            return NumericPlanes.Rgba(1024,32,bytes);
        }
        /// <summary>Six faces in a horizontal strip, following Unity CubemapFace order; computed only once.</summary>
        public static MaterialData Sky(string preset)
        {
            lock(skyCache)if(skyCache.TryGetValue(preset,out var cached))return cached;
            int n=CubeSize;var bytes=new byte[n*n*6*4];
            var cloud=NoiseField.Create(NoiseBasis.DomainWarpedFbm,NamedRandomStream.Derive(460072030,"sky.atmosphere",preset),3,4);
            bool overcast=preset=="dusty-yard",golden=preset=="petroleum";
            double[] zenith=overcast?new[]{.29,.34,.39}:golden?new[]{.20,.27,.36}:new[]{.19,.33,.51};
            double[] horizon=overcast?new[]{.59,.63,.65}:golden?new[]{.76,.48,.24}:new[]{.70,.74,.75};
            double sx=golden?-.75:-.42,sy=golden?.20:.66,sz=-.62,len=Math.Sqrt(sx*sx+sy*sy+sz*sz);sx/=len;sy/=len;sz/=len;
            for(int face=0;face<6;face++)for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                double u=2*(x+.5)/n-1,v=2*(y+.5)/n-1,dx,dy,dz;
                switch(face){case 0:dx=1;dy=-v;dz=-u;break;case 1:dx=-1;dy=-v;dz=u;break;case 2:dx=u;dy=1;dz=v;break;case 3:dx=u;dy=-1;dz=-v;break;case 4:dx=u;dy=-v;dz=1;break;default:dx=-u;dy=-v;dz=-1;break;}
                len=Math.Sqrt(dx*dx+dy*dy+dz*dz);dx/=len;dy/=len;dz/=len;
                double t=Math.Max(0,dy),coverage=Math.Max(0,Math.Min(1,(cloud.Raw3D(dx*3,dy*4,dz*3)+(overcast?.55:.12))*1.8));
                coverage*=Math.Max(0,Math.Min(1,dy*5));
                double sun=Math.Max(0,dx*sx+dy*sy+dz*sz);sun*=sun;sun*=sun;sun*=sun;sun*=sun;sun*=sun;sun*=sun;sun*=sun;
                int p=(y*n*6+face*n+x)*4;
                for(int c=0;c<3;c++)
                {
                    double baseColor=horizon[c]+(zenith[c]-horizon[c])*t;
                    if(dy<0)baseColor=horizon[c]+((golden?.19:.15)-horizon[c])*Math.Min(1,-dy*2);
                    double cloudColor=(overcast?.67:.83)-coverage*.08;
                    bytes[p+c]=Byte(baseColor*(1-coverage*.7)+cloudColor*coverage*.7+sun*(overcast?.06:.7)*(c==0?1:c==1?.85:.62));
                }
                bytes[p+3]=255;
            }
            var result=NumericPlanes.Rgba(n*6,n,bytes);lock(skyCache)skyCache[preset]=result;return result;
        }
        private static byte Byte(double value)=>(byte)(Math.Max(0,Math.Min(1,value))*255+.5);
    }
}
