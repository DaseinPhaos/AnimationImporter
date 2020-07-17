using Luxko.Collections;

namespace Luxko.Geometry {
    public static class SkylinePacker {
        [System.Serializable]
        public struct Pos {
            public int x;
            public int y;
        }
        [System.Serializable]
        public struct Box {
            public int w;
            public int h;
        }
        [System.Serializable]
        public struct Output {
            public Pos pos;
            public int binIndex;
            public int boxIndex;
        }

        public class Sky {
            internal struct Skyline {
                public int x;
                public int h;
                public int w;
                public int rFit;
                public int lFit;
            }

            struct PackCandidate {
                public int localWaste;
                public int fitNum;

                public int skylineIndex;
                public int crossingCount;
                public int h;
                public bool spreadSatisfied;

                public int packIndex;
            }

            class CandidateComparer: System.Collections.Generic.IComparer<PackCandidate> {
                public CandidateComparer(Sky sky) {
                    this._sky = sky;
                }

                internal Sky _sky;
                public int Compare(PackCandidate x, PackCandidate y) {
                    if (x.spreadSatisfied != y.spreadSatisfied) {
                        return x.spreadSatisfied ? -1 : 1;
                    }
                    if (x.localWaste != y.localWaste) {
                        return x.localWaste - y.localWaste;
                    }
                    if (x.fitNum != y.fitNum) {
                        return y.fitNum - x.fitNum;
                    }
                    var bx = _sky.input[_sky._toPack[x.packIndex]];
                    var by = _sky.input[_sky._toPack[y.packIndex]];
                    if (bx.w != by.w) {
                        return by.w - bx.w;
                    }
                    // else if (x.wLeft != y.wLeft) {
                    //     return y.wLeft - x.wLeft;
                    // } 
                    if (x.h != y.h) {
                        return x.h - y.h;
                    } else {
                        return x.skylineIndex - y.skylineIndex; //?
                    }
                }
            }

            public int spreadFactor;
            public Box bin;
            public Box[] input;

            public int binIndex;

            public int packDump;
            public int hSpread;
            public int minWidth;
            public int minHeight;

            internal LStack<Skyline> skylines;
            internal LStack<int> _toPack;
            internal int lowestSky;
            internal int highestSky;
            LStack<Skyline> backBuffer;
            LStack<PackCandidate> packCandidates;
            CandidateComparer candComp;

            void UpdateFactors() {
                this.minWidth = this.bin.w;
                this.minHeight = this.bin.h;
                foreach (var pi in this._toPack) {
                    var box = this.input[pi];
                    if (box.w < this.minWidth) this.minWidth = box.w;
                    if (box.h < this.minHeight) this.minHeight = box.h;
                }
                this.hSpread = this.minWidth;
                this.packDump = this.minWidth;
            }

            void UpdateSkylines(PackCandidate candidate, Box box) {
                UnityEngine.Debug.Assert(candidate.skylineIndex >= 0);
                UnityEngine.Debug.Assert(candidate.skylineIndex < this.skylines.Count);
                this.backBuffer.EnsureCap(this.skylines.Capacity);
                this.backBuffer.Clear();
                if (candidate.crossingCount > 0) {
                    for (int i = 0; i < candidate.skylineIndex - 1; ++i) {
                        this.backBuffer.Add(this.skylines[i]);
                    }
                    var skyline = this.skylines[candidate.skylineIndex];
                    var oldX = skyline.x;
                    skyline.h += box.h;
                    skyline.w = box.w;
                    if (candidate.skylineIndex - 1 >= 0) {
                        if (this.skylines[candidate.skylineIndex - 1].h != skyline.h) {
                            this.backBuffer.Add(this.skylines[candidate.skylineIndex - 1]);
                        } else {
                            skyline.x = this.skylines[candidate.skylineIndex - 1].x;
                            skyline.w += oldX - skyline.x;
                        }
                    }

                    if (this.backBuffer.Count > 0) {
                        var last = this.backBuffer[this.backBuffer.Count - 1];
                    }
                    var crossedIndex = candidate.skylineIndex + candidate.crossingCount - 1;
                    var crossedLast = this.skylines[crossedIndex];
                    var crossedEnd = crossedLast.x + crossedLast.w;
                    var newEnd = oldX + box.w;
                    if (crossedEnd != newEnd) {
                        UnityEngine.Debug.Assert(crossedEnd > newEnd);
                        UnityEngine.Debug.Assert(crossedLast.h < skyline.h);

                        this.backBuffer.Add(skyline);
                        crossedLast.w = crossedLast.w - (newEnd - crossedLast.x);
                        crossedLast.x = newEnd;

                        this.backBuffer.Add(crossedLast);
                        if (crossedIndex + 1 < this.skylines.Count) {
                            this.backBuffer.Add(this.skylines[crossedIndex + 1]);
                        }
                    } else {
                        if (crossedIndex + 1 < this.skylines.Count) {
                            var next = this.skylines[crossedIndex + 1];
                            if (next.h == skyline.h) {
                                skyline.w += next.w;
                                this.backBuffer.Add(skyline);
                            } else {
                                this.backBuffer.Add(skyline);
                                this.backBuffer.Add(next);
                            }
                        } else {
                            this.backBuffer.Add(skyline);
                        }
                    }
                    for (int i = crossedIndex + 2; i < this.skylines.Count; ++i) {
                        this.backBuffer.Add(this.skylines[i]);
                    }

                } else {
                    UnityEngine.Debug.Assert(candidate.crossingCount < 0);
                    var oldIdx = candidate.skylineIndex + candidate.crossingCount; // + 1 - 1
                    for (int i = 0; i < oldIdx; ++i) {
                        this.backBuffer.Add(this.skylines[i]);
                    }

                    var skyline = this.skylines[candidate.skylineIndex];
                    var oldEnd = skyline.x + skyline.w;
                    skyline.h += box.h;
                    skyline.w = box.w;
                    skyline.x = oldEnd - skyline.w;
                    if (oldIdx >= 0) {
                        var old = this.skylines[oldIdx];
                        if (skyline.x == old.x + old.w && old.h == skyline.h) {
                            skyline.x = old.x;
                            skyline.w += old.w;
                        } else {
                            this.backBuffer.Add(old);
                        }
                    }
                    var crossedIdx = oldIdx + 1;
                    var crossed = this.skylines[crossedIdx];
                    if (skyline.x != crossed.x) {
                        crossed.w = skyline.x - crossed.x;
                        this.backBuffer.Add(crossed);
                    }
                    var nextIdx = candidate.skylineIndex + 1;
                    if (nextIdx < this.skylines.Count) {
                        var next = this.skylines[nextIdx];
                        if (next.h == skyline.h) {
                            skyline.w += next.w;
                            this.backBuffer.Add(skyline);
                        } else {
                            this.backBuffer.Add(skyline);
                            this.backBuffer.Add(next);
                        }
                    } else {
                        this.backBuffer.Add(skyline);
                    }

                    for (int i = nextIdx + 1; i < this.skylines.Count; ++i) {
                        this.backBuffer.Add(this.skylines[i]);
                    }
                }

                // min gap promotion
                this.skylines.Clear();
                for (int i = 0; i < this.backBuffer.Count; ++i) {
                    var line = this.backBuffer[i];
                    if (line.w >= this.packDump) {
                        this.skylines.Add(line);
                        continue;
                    }

                    int lh;
                    if (this.skylines.Count > 0) {
                        lh = this.skylines[this.skylines.Count - 1].h;
                    } else {
                        lh = bin.h;
                    }
                    int rh;
                    if (i + 1 < this.backBuffer.Count) {
                        rh = this.backBuffer[i + 1].h;
                    } else {
                        rh = bin.h;
                    }

                    if (lh < line.h || rh < line.h) {
                        this.skylines.Add(line);
                        continue;
                    }

                    if (lh < rh && this.skylines.Count > 0) {
                        this.skylines.Get(this.skylines.Count - 1).w += line.w;
                        continue;
                    }

                    if (i + 1 < this.backBuffer.Count) {
                        this.backBuffer.Get(i + 1).w += line.w;
                        this.backBuffer.Get(i + 1).x = line.x;
                        continue;
                    } else {
                        this.skylines.Add(line); // or should we just dump it
                    }

                }

                // var tmp = this.skylines;
                // this.skylines = this.backBuffer;
                // this.backBuffer = tmp;

                // fix lfit/rfit. 
                // TODO: optimize this shouldn't have been an O(n^2) op
                this.lowestSky = bin.h + 1;
                this.highestSky = -1;
                for (int i = 0; i < this.skylines.Count; ++i) {
                    var h = this.skylines.Get(i).h;
                    if (h > this.highestSky) this.highestSky = h;
                    if (h < this.lowestSky) this.lowestSky = h;
                    this.skylines.Get(i).rFit = this.skylines.Get(i).lFit = this.skylines.Get(i).w;
                    for (int j = i - 1; j >= 0; --j) {
                        if (this.skylines.Get(j).h > this.skylines.Get(i).h) {
                            break;
                        }
                        this.skylines.Get(i).lFit += this.skylines.Get(j).w;
                    }
                    for (int j = i + 1; j < this.skylines.Count; ++j) {
                        if (this.skylines.Get(j).h > this.skylines.Get(i).h) {
                            break;
                        }
                        this.skylines.Get(i).rFit += this.skylines.Get(j).w;
                    }
                }
            }

            public Sky(Box bin, int spreadFactor, Box[] input) {
                this.skylines = new LStack<Skyline>();
                this.backBuffer = new LStack<Skyline>();
                this.skylines.Add(new Skyline {
                    x = 0,
                    h = 0,
                    w = bin.w,
                    rFit = bin.w,
                    lFit = bin.w,
                });
                this.binIndex = 0;
                this.bin = bin;
                this.input = input;
                this._toPack = new LStack<int>(this.input.Length);
                for (int i = 0; i < this.input.Length; ++i) {
                    _toPack.Add(i);
                }

                this.packCandidates = new LStack<PackCandidate>();
                this.candComp = new CandidateComparer(this);
                this.spreadFactor = spreadFactor;
                this.lowestSky = this.highestSky = 0;
            }

            void PickCandidate(ref PackCandidate pc) {
                var box = this.input[this._toPack[pc.packIndex]];

                for (
                    pc.skylineIndex = 0;
                    pc.skylineIndex < this.skylines.Count;
                    ++pc.skylineIndex
                ) {
                    var skyline = this.skylines[pc.skylineIndex];
                    if (box.h + skyline.h > bin.h) {
                        continue;
                    }
                    // if (box.h + skyline.h - this.lowestSky > this.spreadFactor) {
                    //     continue;
                    // }
                    pc.spreadSatisfied = (box.h + skyline.h - this.lowestSky) <= this.spreadFactor;
                    pc.h = skyline.h;

                    if (skyline.rFit >= box.w) {
                        pc.crossingCount = 1;
                        pc.fitNum = 0;
                        if (box.w == skyline.w) {
                            pc.fitNum += 1;
                        }
                        if (box.h + skyline.h == bin.h) {
                            pc.fitNum += 1;
                        }
                        if (pc.skylineIndex > 0 && box.h == (this.skylines[pc.skylineIndex - 1].h - skyline.h)) {
                            pc.fitNum += 1;
                        }

                        pc.localWaste = 0;
                        for (
                            var widthLeft = box.w - skyline.w;
                            widthLeft > 0;
                            pc.crossingCount++
                        ) {
                            var nsi = pc.crossingCount + pc.skylineIndex;
                            UnityEngine.Debug.Assert(nsi < this.skylines.Count);
                            var ns = this.skylines[nsi];
                            UnityEngine.Debug.Assert(skyline.h > ns.h);
                            pc.localWaste += (ns.w) * (skyline.h - ns.h);
                            widthLeft -= ns.w;
                        }

                        var mh = pc.h + box.h;
                        // left
                        for (int li = pc.skylineIndex - 1, lw = 0, ml = 0; li >= 0; li--) {
                            var left = skylines[li];
                            if (left.h > mh) {
                                if (ml < this.hSpread) {
                                    // if (true) {
                                    pc.localWaste += lw;
                                }
                                break;
                            }
                            ml += left.w;
                            lw += (mh - left.h) * left.w;
                        }


                        { // right
                            var crossIdx = pc.crossingCount + pc.skylineIndex - 1;
                            var cross = this.skylines[crossIdx];
                            var mr = (cross.w + cross.x - skyline.x - box.w);
                            UnityEngine.Debug.Assert(mr >= 0);
                            var rw = (mh - cross.h) * mr;
                            for (int ri = crossIdx + 1; ri < this.skylines.Count; ++ri) {
                                var right = skylines[ri];
                                if (right.h > mh) {
                                    if (mr < this.hSpread) {
                                        pc.localWaste += rw;
                                    }
                                    break;
                                }
                                mr += right.w;
                                rw += (mh - right.h) * right.w;
                            }
                        }

                        // upper
                        if (bin.h - mh < this.minHeight) {
                            pc.localWaste += (bin.h - mh) * box.w;
                        }
                        packCandidates.Add(pc);
                    }
                    if (skyline.lFit >= box.w) {
                        pc.crossingCount = -1;
                        pc.fitNum = 0;
                        if (box.w == skyline.w) {
                            pc.fitNum += 1;
                        }
                        if (box.h + skyline.h == bin.h) {
                            pc.fitNum += 1;
                        }
                        if (pc.skylineIndex < (this.skylines.Count - 1) && box.h == (this.skylines[pc.skylineIndex + 1].h - skyline.h)) {
                            pc.fitNum += 1;
                        }

                        pc.localWaste = 0;
                        var widthLeft = box.w - skyline.w;

                        while (widthLeft > 0) {
                            var nsi = pc.crossingCount + pc.skylineIndex;
                            UnityEngine.Debug.Assert(nsi >= 0);
                            var ns = this.skylines[nsi];
                            UnityEngine.Debug.Assert(skyline.h > ns.h);
                            pc.localWaste += (ns.w) * (skyline.h - ns.h);
                            widthLeft -= ns.w;
                            pc.crossingCount--;
                        }

                        var mh = pc.h + box.h;
                        // right
                        for (int ri = pc.skylineIndex + 1, rw = 0, mr = 0; ri < skylines.Count; ++ri) {
                            var right = skylines[ri];
                            if (right.h > mh) {
                                if (mr < this.hSpread) {
                                    pc.localWaste += rw;
                                }
                                break;
                            }
                            mr += right.w;
                            rw += (mh - right.h) * right.w;
                        }

                        {// left
                            var crossIdx = pc.crossingCount + pc.skylineIndex + 1;
                            var cross = this.skylines[crossIdx];
                            var ml = (skyline.x + skyline.w - box.w - cross.x);
                            UnityEngine.Debug.Assert(ml >= 0);
                            var lw = (mh - cross.h) * ml;
                            for (int li = crossIdx - 1; li >= 0; --li) {
                                var left = skylines[li];
                                if (left.h > mh) {
                                    if (ml < this.hSpread) {
                                        pc.localWaste += lw;
                                    }
                                    break;
                                }
                                ml += left.w;
                                lw += (mh - left.h) * left.w;
                            }
                        }
                        // upper
                        if (bin.h - mh < this.minHeight) {
                            pc.localWaste += (bin.h - mh) * box.w;
                        }
                        packCandidates.Add(pc);
                    }
                }
            }

            public void PackNext(out Output output) {
                UpdateFactors();
                var pc = default(PackCandidate);
                for (int i = 0; i < this._toPack.Count; ++i) {
                    pc.packIndex = i;
                    PickCandidate(ref pc);
                }

                if (packCandidates.Count <= 0) {
                    this.binIndex++;
                    this.skylines.Clear();
                    this.skylines.Add(new Skyline {
                        x = 0,
                        h = 0,
                        w = bin.w,
                        rFit = bin.w,
                        lFit = bin.w,
                    });

                    for (int i = 0; i < this._toPack.Count; ++i) {
                        pc.packIndex = i;
                        PickCandidate(ref pc);
                    }
                    if (packCandidates.Count <= 0) {
                        throw new System.ArgumentOutOfRangeException("oor");
                    }
                }
                packCandidates.Sort(candComp);
                pc = packCandidates[0];
                packCandidates.Clear();

                {
                    output.boxIndex = this._toPack[pc.packIndex];
                    var box = this.input[output.boxIndex];
                    this._toPack.SwapRemoveAt(pc.packIndex);
                    output.binIndex = binIndex;
                    var skyline = skylines[pc.skylineIndex];
                    output.pos.y = skyline.h;
                    if (pc.crossingCount > 0) {
                        output.pos.x = skyline.x;
                    } else {
                        UnityEngine.Debug.Assert(pc.crossingCount != 0);
                        output.pos.x = skyline.x + skyline.w - box.w;
                    }

                    UpdateSkylines(pc, box);
                }
            }
        }

        public static void Pack(Box bin, Box[] input, Output[] output, int spreadFactor) {
            var sky = new Sky(bin, spreadFactor, input);

            // ???? sort input?

            for (int i = 0; i < input.Length; ++i) {
                sky.PackNext(out output[i]);
            }
        }
    }
}
