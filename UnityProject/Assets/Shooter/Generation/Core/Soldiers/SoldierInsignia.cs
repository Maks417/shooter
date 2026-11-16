#nullable enable
using System;using Shooter.Contracts;using Shooter.Generation.Icons;
namespace Shooter.Generation
{
    /// <summary>Numeric library emblem rasterised into a patch plane; no imported insignia or sprite.</summary>
    public static class SoldierInsignia
    {
        public static MaterialData Bake(byte team)
        {
            var recipe=IconLibrary.All[0];
            foreach(var icon in IconLibrary.All)if(icon.Name==(team==1?IconLibrary.EmblemAlly:IconLibrary.EmblemEnemy))recipe=icon;
            const int n=64;var pixels=new byte[n*n*4];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                bool border=x<3||x>=n-3||y<3||y>=n-3;
                // Original emblem shape distances from the shared numeric icon recipe.
                double d=recipe.Distance((x+.5)*1000/n,(y+.5)*1000/n);double ink=Math.Max(0,Math.Min(1,.5-d/20));
                int p=(y*n+x)*4;
                pixels[p]=(byte)(border?75:team==1?35+ink*100:80+ink*120);
                pixels[p+1]=(byte)(border?75:team==1?65+ink*130:48+ink*95);
                pixels[p+2]=(byte)(border?62:team==1?70+ink*130:30+ink*65);pixels[p+3]=255;
            }
            return NumericPlanes.Rgba(n,n,pixels,"rgba8-srgb");
        }
    }
}
