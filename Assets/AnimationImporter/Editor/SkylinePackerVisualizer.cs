using UnityEngine;

namespace Luxko.Geometry.Tests {
    public class SkylinePackerVisualizer: MonoBehaviour {

        public Sprite[] spritesToPack = new Sprite[0];
        public SkylinePacker.Box[] boxesToPack = new SkylinePacker.Box[0];
        public SkylinePacker.Box bin;
        SkylinePacker.Output[] _packedResult = new SkylinePacker.Output[0];
        SkylinePacker.Sky _sky;
        public int skySpreadFactor = 1024;
        public int _packedCount = 0;

        [ContextMenu("PrePackSprites")]
        void PrePackSprites() {
            System.Array.Sort<Sprite>(this.spritesToPack, (a, b) => {
                var aw = (int)a.rect.width;
                var bw = (int)b.rect.width;
                if (aw == bw) {
                    return (int)(b.rect.height - a.rect.height);
                } else {
                    return bw - aw;
                }

                // if (a.rect.height == b.rect.height) {
                //     return (int)(b.rect.height - a.rect.height);
                // } else {
                //     return bw - aw;
                // }

                // return -(int)(a.rect.width * a.rect.height - b.rect.width * b.rect.height);
            });

            System.Array.Resize(ref this.boxesToPack, this.spritesToPack.Length);
            for (int i = 0; i < this.boxesToPack.Length; ++i) {
                this.boxesToPack[i].w = (int)this.spritesToPack[i].rect.width;
                this.boxesToPack[i].h = (int)this.spritesToPack[i].rect.height;
            }
            PreparePack();
        }

        [ContextMenu("PrePack")]
        void PreparePack() {

            System.Array.Resize(ref this._packedResult, this.boxesToPack.Length);
            this._sky = new SkylinePacker.Sky(bin, this.skySpreadFactor, this.boxesToPack);
            this._packedCount = 0;
        }

        [ContextMenu("Pack One")]
        void PackOne() {
            if (this._packedCount >= this._packedResult.Length) return;
            // if (this._packedCount + 1 >= this._packedResult.Length) {
            //     this._sky.hSpread = 0;
            // } else {
            //     this._sky.hSpread = this.boxesToPack[this._packedCount + 1].w;
            // }
            // this._sky.hSpread = this._sky.minWidth;
            // // this._sky.hSpread = this.bin.w;
            // // this._sky.hSpread = this.skySpreadFactor;
            // this._sky.packDump = this._sky.minWidth;
            _sky.PackNext(out this._packedResult[this._packedCount]);
            this._packedCount++;
        }

        [ContextMenu("Pack All")]
        void PackAll() {
            while (this._packedCount < this._packedResult.Length) {
                PackOne();
            }
        }

        void OnDrawGizmosSelected() {
            if (_packedResult == null || _packedResult.Length < _packedCount) return;

            Gizmos.color = Color.red;
            for (int i = 0; i < this._packedCount; ++i) {

                var packedPos = this._packedResult[i];
                var box = this.boxesToPack[packedPos.boxIndex];
                var hOffset = (this.bin.w * 1.25f) * packedPos.binIndex;
                var posLL = new Vector2(
                    packedPos.pos.x + hOffset,
                    packedPos.pos.y
                );
                var pllw = transform.TransformPoint(posLL);
                var plrw = pllw;
                plrw.x += box.w;
                var ptrw = plrw;
                ptrw.y += box.h;
                var ptlw = ptrw;
                ptlw.x -= box.w;
                Gizmos.DrawLine(pllw, plrw);
                Gizmos.DrawLine(ptrw, plrw);
                Gizmos.DrawLine(ptrw, ptlw);
                Gizmos.DrawLine(pllw, ptlw);
            }

            Gizmos.color = Color.green;
            if (_sky != null && _sky.skylines != null) {
                foreach (var skyline in _sky.skylines) {
                    var p = new Vector2(skyline.x + (this.bin.w * 1.25f) * this._sky.binIndex, skyline.h);
                    var pw = transform.TransformPoint(p);
                    var pt = pw;
                    pt.x += skyline.w;
                    Gizmos.DrawLine(pw, pt);
                }
            }
        }

    }
}
