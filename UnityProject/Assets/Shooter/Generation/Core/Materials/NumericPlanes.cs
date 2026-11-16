#nullable enable
using System;using System.Collections.Generic;using Shooter.Contracts;
namespace Shooter.Generation
{
    /// <summary>Complete rectangular RGBA mip chain for numeric atlases, cubes and lookups; no implicit images.</summary>
    public static class NumericPlanes
    {
        public static MaterialData Rgba(int width,int height,byte[] pixels,string format="rgba8-linear")
        {
            if(width<1||height<1||width>4096||height>4096||pixels.Length!=width*height*4)throw new ArgumentException("Invalid numeric plane.");
            var mips=new List<byte[]>{pixels};int w=width,h=height;
            while(w>1||h>1)
            {
                var prior=mips[mips.Count-1];int nw=Math.Max(1,w/2),nh=Math.Max(1,h/2);var next=new byte[nw*nh*4];
                for(int y=0;y<nh;y++)for(int x=0;x<nw;x++)for(int c=0;c<4;c++)
                {int sum=0;for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)sum+=prior[(Math.Min(h-1,y*2+dy)*w+Math.Min(w-1,x*2+dx))*4+c];next[(y*nw+x)*4+c]=(byte)((sum+2)/4);}
                mips.Add(next);w=nw;h=nh;
            }
            return new MaterialData((ushort)width,(ushort)height,format,mips);
        }
    }
}
