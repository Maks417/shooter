#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
namespace Shooter.Generation.Environment
{
    public readonly struct ArenaDecal
    {
        public readonly Float3 Position; public readonly float Width, Height, Yaw; public readonly int Tile;
        public ArenaDecal(Float3 position,float width,float height,float yaw,int tile)
        { Position=position;Width=width;Height=height;Yaw=yaw;Tile=tile; }
    }
    public static class ProceduralDecals
    {
        public const string AtlasPath="material-decal-atlas.bin", PlacementsPath="visual-decals.json";
        public const int CellSize=64, Columns=4, Rows=2;
        public static ContentId Fingerprint => GeneratorFingerprint.Compute("visual.decal","1.0.0");
        /// <summary>Original analytic paint/stain cells. Transparent gutters prevent neighboring cells bleeding.</summary>
        public static MaterialData Bake()
        {
            int w=CellSize*Columns,h=CellSize*Rows;var data=new byte[w*h*4];
            for(int tile=0;tile<8;tile++) for(int y=2;y<CellSize-2;y++) for(int x=2;x<CellSize-2;x++)
            {
                double u=(x+.5)/CellSize,v=(y+.5)/CellSize,dx=u-.5,dy=v-.5, r2=dx*dx+dy*dy;
                double a=0; byte red=180,green=170,blue=136;
                switch(tile)
                {
                    case 0: a=((x+y)/9)%2==0?.82:.75; if(((x+y)/9)%2!=0){red=22;green=24;blue=23;}else{red=205;green=155;blue=35;} break;
                    case 1: a=Math.Max(0,1-r2*5)*.65*(.82+.18*((x*13+y*29)%17)/17.0);red=15;green=14;blue=11;break;
                    case 2: a=Math.Max(0,1-r2*4.7)*.8;red=18;green=16;blue=14;break;
                    case 3: a=r2<.024?.95:r2<.065?.65:r2<.12&& (Math.Abs(dx)<.018||Math.Abs(dy)<.018||Math.Abs(dx-dy)<.016)?.7:0;red=28;green=26;blue=24;break;
                    case 4: a=(Math.Abs(dy+.23)<.035 && Math.Abs(dx)<.21 || Math.Abs(Math.Abs(dx)-(.12-dy*.17))<.035&&dy<.28&&dy>-.28 || Math.Abs(dy)<.025&&Math.Abs(dx)<.16)?.9:0;break;
                    case 5: a=(Math.Abs(Math.Abs(dx)-.17)<.035&&Math.Abs(dy)<.28 || Math.Abs(Math.Abs(dy)-.28)<.035&&Math.Abs(dx)<.18 || Math.Abs(dy)<.025&&Math.Abs(dx)<.17)?.9:0;break;
                    case 6: a=(Math.Abs(dy-Math.Abs(dx)*.65+.12)<.04||Math.Abs(dy-Math.Abs(dx)*.65-.12)<.04)&&Math.Abs(dx)<.3?.9:0;break;
                    case 7: a=Math.Abs(dx)<.025&&Math.Abs(dy)<.3 || Math.Abs(dy+.15+Math.Abs(dx))<.04&&Math.Abs(dx)<.18?.9:0;break;
                }
                // Deterministic chipped edges in all painted marks, no imported alpha.
                if(tile>=4 && (x*17+y*31)%43==0)a*=.4;
                int p=((tile/Columns*CellSize+y)*w+tile%Columns*CellSize+x)*4;
                data[p]=red;data[p+1]=green;data[p+2]=blue;data[p+3]=(byte)(Math.Max(0,Math.Min(1,a))*255+.5);
            }
            return NumericPlanes.Rgba(w,h,data);
        }
        public static byte[] EncodePlacements(IReadOnlyList<ArenaDecal> decals)
        {
            var list=new List<object?>();foreach(var d in decals) list.Add(new Dictionary<string,object?>{
                ["position"]=new object?[]{d.Position.X,d.Position.Y,d.Position.Z},["width"]=d.Width,["height"]=d.Height,["yaw"]=d.Yaw,["tile"]=d.Tile});
            return MapDocumentJson.EncodeObject(new Dictionary<string,object?>{["schema"]="shooter.visual-decals.v1",["decals"]=list});
        }
    }
}
