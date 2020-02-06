using UnityEngine;

namespace Blingame.Importers
{
    public static class SpritePacker
    {
        public struct SpriteInfo
        {
            public string name;
            public Texture2D tex;
            public RectInt frame;
            public Vector2 pivotN;

            public SpriteInfo Trim(Color trimAgainst, int marginX, int marginY)
            {
                var top = frame.y;
                var right = frame.x;
                var btm = frame.y + frame.height;
                var left = frame.x + frame.width;

                for (int y = frame.y; y < frame.y + frame.height; ++y)
                {
                    for (int x = frame.x; x < frame.x + frame.width; ++x)
                    {
                        var color = tex.GetPixel(x, y);
                        if (color.a == 0) color = new Color();
                        if (color != trimAgainst)
                        {
                            if (top <= y) top = y + 1;
                            if (right <= x) right = x + 1;
                            if (btm > y) btm = y;
                            if (left > x) left = x;
                        }
                    }
                }

                top = Mathf.Min(frame.y + frame.height, top + marginY);
                right = Mathf.Min(frame.x + frame.width, right + marginX);
                btm = Mathf.Max(frame.y, btm - marginY);
                left = Mathf.Max(frame.x, left - marginX);

                var pivotT = new Vector2(frame.x + pivotN.x * frame.width, frame.y + pivotN.y * frame.height);

                var ret = new SpriteInfo
                {
                    tex = this.tex,
                    frame = new RectInt(left, btm, right - left, top - btm),
                    name = this.name,
                };
                ret.pivotN = new Vector2((pivotT.x - ret.frame.x) / ret.frame.width, (pivotT.y - ret.frame.y) / ret.frame.height);
                return ret;
            }

            public bool TryCopyTo(ref SpriteInfo target)
            {
                target.name = this.name;
                target.frame.width = this.frame.width;
                target.frame.height = this.frame.height;
                target.pivotN = this.pivotN;
                if (target.tex.width < frame.width + target.frame.x) return false;
                if (target.tex.height < frame.height + target.frame.y) return false;

                for (int y = frame.y; y < frame.y + frame.height; ++y)
                {
                    for (int x = frame.x; x < frame.x + frame.width; ++x)
                    {
                        var color = tex.GetPixel(x, y);
                        target.tex.SetPixel(x - frame.x + target.frame.x, y - frame.y + target.frame.y, color);
                    }
                }
                return true;
            }
        }
    }
}