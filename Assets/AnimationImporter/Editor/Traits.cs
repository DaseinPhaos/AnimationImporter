using System.Collections.Generic;

namespace Luxko.Traits {
    public interface ITo<T> {
        void To(out T ret);
    }

    public struct SelfTo<T>: ITo<T> {
        public T source;
        public void To(out T ret) { ret = source; }
        public static implicit operator SelfTo<T>(T source) {
            return new SelfTo<T> { source = source };
        }
    }

    public interface IRefSlice<T> {
        int CountOf();
        ref T Get(int at);
    }

    public interface IColle<T> {
        int CountOf(in T __);
    }

    public unsafe interface INativeColle<T>: IColle<T> where T : unmanaged {
        void GetPtr(int idx, out T* ptr);
    }

    public interface IReadOnlySlice<T>: IColle<T> {
        void Get(int idx, out T value);
    }

    public interface ISlice<T>: IReadOnlySlice<T> {
        void Set(int idx, T value);
    }

    public interface IExpandableBuffer<T> {
        void EnsureCap(int count, in T __);
    }

    public interface IAddableColle<T>: IColle<T> {
        void Add(T value);
    }
    public interface IStack<T>: IAddableColle<T> {
        void PopBack(int countToPop);
    }
    public interface IRemovableColle<T>: IColle<T> {
        void RemoveAt(int idx, in T __);
    }

    public struct ListTraits<T>: ISlice<T>, IAddableColle<T>, IRemovableColle<T>, Iiterable<T, EnumeratorIter<T, List<T>.Enumerator>> {
        public List<T> source;
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public int CountOf(in T __) { return source.Count; }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Set(int idx, T value) { source[idx] = value; }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Get(int idx, out T value) { value = source[idx]; }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Add(T value) { source.Add(value); }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void RemoveAt(int idx, in T __) { source.RemoveAt(idx); }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public EnumeratorIter<T, List<T>.Enumerator> GetIter(T __) {
            return source.GetEnumerator();
        }

        public static implicit operator ListTraits<T>(List<T> source) {
            return new ListTraits<T> { source = source };
        }
    }

    public struct ArrayTraits<T>: ISlice<T>, IRefSlice<T> {
        public T[] source;
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public int CountOf(in T __) { return source.Length; }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Set(int idx, T value) { source[idx] = value; }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Get(int idx, out T value) { value = source[idx]; }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public int CountOf() { return source.Length; }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public ref T Get(int idx) { return ref source[idx]; }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public static implicit operator ArrayTraits<T>(T[] source) {
            return new ArrayTraits<T> { source = source };
        }
    }

    public struct Slice<T, S>: ISlice<T>, Iiter<T>, Iiterable<T, Slice<T, S>>
    where S : ISlice<T> {
        public S source;
        public int offset;
        public int length;

        public int CountOf(in T __) { return length; }
        // TODO: oor checks?
        public void Set(int idx, T value) {
            source.Set(idx + offset, value);
        }
        public void Get(int idx, out T value) {
            source.Get(idx + offset, out value);
        }

        public bool MoveNext(T __) { offset += 1; length -= 1; return offset < length; }
        public void Get(out T current) { source.Get(offset, out current); }
        public Slice<T, S> GetIter(T __) { return new Slice<T, S> { source = source, offset = offset - 1, length = length + 1 }; }
    }

    public struct ReadOnlySlice<T, S>: IReadOnlySlice<T>, Iiter<T>, Iiterable<T, ReadOnlySlice<T, S>>
    where S : IReadOnlySlice<T> {
        public S source;
        public int offset;
        public int length;

        public int CountOf(in T __) { return length; }

        public void Get(int idx, out T value) {
            source.Get(idx + offset, out value);
        }

        public bool MoveNext(T __) { offset += 1; length -= 1; return offset < length; }
        public void Get(out T current) { source.Get(offset, out current); }
        public ReadOnlySlice<T, S> GetIter(T __) { return new ReadOnlySlice<T, S> { source = source, offset = offset - 1, length = length + 1 }; }
        public ReadOnlySlice(S source, int offset, int length) {
            this.source = source;
            this.offset = offset;
            this.length = length;
        }
    }

    public struct StringTraits: IReadOnlySlice<char>, Iiterable<char, EnumeratorIter<char, System.CharEnumerator>> {
        public string source;
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public int CountOf(in char __) { return source.Length; }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Get(int idx, out char value) { value = source[idx]; }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public EnumeratorIter<char, System.CharEnumerator> GetIter(char __) {
            return source.GetEnumerator();
        }

        public static implicit operator StringTraits(string str) {
            return new StringTraits { source = str };
        }
    }

    public interface Iiter<T> {
        bool MoveNext(T __);
        void Get(out T current);
    }

    public struct EnumeratorIter<T, E>: Iiter<T>
    where E : IEnumerator<T> {
        E e;

        [System.Runtime.CompilerServices.MethodImpl(256)]
        public bool MoveNext(T __) { return e.MoveNext(); }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Get(out T current) { current = e.Current; }

        public static implicit operator EnumeratorIter<T, E>(E e) {
            return new EnumeratorIter<T, E> { e = e };
        }
    }

    public interface Iiterable<T, I>
    where I : Iiter<T> {
        I GetIter(T __);
    }

    public struct DictTraits<K, V>: Iiterable<KeyValuePair<K, V>, EnumeratorIter<KeyValuePair<K, V>, Dictionary<K, V>.Enumerator>> {
        Dictionary<K, V> l;
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public EnumeratorIter<KeyValuePair<K, V>, Dictionary<K, V>.Enumerator> GetIter(KeyValuePair<K, V> __) {
            return l.GetEnumerator();
        }

        public static implicit operator DictTraits<K, V>(Dictionary<K, V> l) {
            return new DictTraits<K, V> { l = l };
        }
    }

    public struct ComposedRos<S0, S1, T>: IReadOnlySlice<T>
    where S0 : IReadOnlySlice<T>
    where S1 : IReadOnlySlice<T> {
        public S0 s0;
        public S1 s1;
        public int CountOf(in T __) { return s0.CountOf(__) + s1.CountOf(__); }

        public void Get(int idx, out T value) {
            var s0Count = s0.CountOf(default(T));
            if (idx < s0Count) {
                s0.Get(idx, out value);
            } else {
                s1.Get(idx - s0Count, out value);
            }
        }
    }


    public static class SystemInterfaceTraitsHelpers {
        // helper methods for type deduction zzz
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public static ListTraits<T> GetTraits<T>(this List<T> list) {
            return list;
        }

        [System.Runtime.CompilerServices.MethodImpl(256)]
        public static ArrayTraits<T> GetTraits<T>(this T[] array) {
            return array;
        }

        [System.Runtime.CompilerServices.MethodImpl(256)]
        public static StringTraits GetTraits(this string s) {
            return s;
        }

        [System.Runtime.CompilerServices.MethodImpl(256)]
        public static DictTraits<K, V> GetTraits<K, V>(this Dictionary<K, V> dict) {
            return dict;
        }

        [System.Runtime.CompilerServices.MethodImpl(256)]
        public static EnumeratorIter<T, E> ToIter<T, E>(this E iter, T __)
        where E : IEnumerator<T> {
            return iter;
        }

        [System.Runtime.CompilerServices.MethodImpl(256)]
        public static ComposedRos<S0, S1, char> Compose<S0, S1>(this S0 s0, S1 s1)
        where S0 : IReadOnlySlice<char>
        where S1 : IReadOnlySlice<char> {
            return new ComposedRos<S0, S1, char> {
                s0 = s0,
                s1 = s1,
            };
        }

        [System.Runtime.CompilerServices.MethodImpl(256)]
        public static void CopyTo<Sfrom, Sto, T>(this Sfrom from, int fromOffset, int len, Sto to, int toOffset, T t) // t is there to help type deduction
        where Sfrom : IReadOnlySlice<T>
        where Sto : ISlice<T> {
            for (int i = 0; i < len; ++i) {
                from.Get(fromOffset + i, out t);
                to.Set(toOffset + i, t);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(256)]
        public static void SelfCopy<S, T>(this S slice, int fromOffset, int len, int toOffset)
                where S : ISlice<T> {
            T t;
            if (fromOffset <= toOffset && fromOffset + len > toOffset) {
                for (int i = len; i >= 0; --i) {
                    slice.Get(fromOffset + i, out t);
                    slice.Set(toOffset + i, t);
                }
            } else {
                for (int i = 0; i < len; ++i) {
                    slice.Get(fromOffset + i, out t);
                    slice.Set(toOffset + i, t);
                }
            }
        }

        public static int BiSearch<T, S>(this S slice, T value, int from, int length)
        where T : System.IComparable<T>
        where S : IReadOnlySlice<T> {
            var lb = from;
            var hb = from + length - 1;
            while (lb <= hb) {
                var mid = lb + ((hb - lb) >> 1);
                // TODO: null checks?
                T midV;
                slice.Get(mid, out midV);
                var com = value.CompareTo(midV);
                if (com == 0) return mid;
                else if (com < 0) hb = mid - 1;
                else lb = mid + 1;
            }
            return ~lb;
        }

        public static void InsertAt<T, S>(this S slice, int at, T value)
        where S : IAddableColle<T>, ISlice<T> {
            var tmp = default(T);
            var count = slice.CountOf(tmp);
            slice.Add(tmp);
            for (int i = count - 1; i >= at; --i) {
                slice.Get(i, out tmp);
                slice.Set(i + 1, tmp);
            }
            slice.Set(at, value);
        }

        [System.Runtime.CompilerServices.MethodImpl(256)]
        public static string GenerateString<T>(this T chars, int from, int len)
        where T : INativeColle<char> {
            unsafe {
                chars.GetPtr(from, out var fromPtr);
                return new string(fromPtr, 0, len);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(256)]
        public static string GenerateString<T>(this T chars)
        where T : INativeColle<char>, IReadOnlySlice<char> {
            unsafe {
                chars.GetPtr(0, out var fromPtr);
                return new string(fromPtr, 0, chars.CountOf(default));
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(256)]
        public static bool ByteWiseEquals<B0, B1>(ref B0 lhs, ref B1 rhs) // stupid C#8!
        where B0 : Luxko.Traits.IReadOnlySlice<byte>
        where B1 : Luxko.Traits.IReadOnlySlice<byte> {
            var len = lhs.CountOf(default);
            if (len != rhs.CountOf(default)) return false;
            for (int i = 0; i < len; ++i) {
                lhs.Get(i, out var l);
                rhs.Get(i, out var r);
                if (l != r) return false;
            }
            return true;
        }
    }
}
