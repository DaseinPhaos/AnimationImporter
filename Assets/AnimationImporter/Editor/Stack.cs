using System;
using System.Collections;
using System.Collections.Generic;
using Luxko.Traits;

namespace Luxko.Collections {

    public class LStack<T>: IExpandableBuffer<T>, IRefSlice<T>, ISlice<T>, IStack<T> {
        public LStack() : this(8) { }
        public LStack(int cap) {
            _buffer = new T[cap];
            _tail = 0;
        }
        public int Count {
            [System.Runtime.CompilerServices.MethodImpl(256)]
            get { return _tail; }
        }
        public int Capacity {
            [System.Runtime.CompilerServices.MethodImpl(256)]
            get { return _buffer.Length; }
            [System.Runtime.CompilerServices.MethodImpl(256)]
            private set {
                if (value <= Count) return;
                Array.Resize(ref _buffer, value);
            }
        }
        public T[] _buffer;
        public int _tail;
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void EnsureCap(int newCap) {
            if (_buffer.Length <= newCap) {
                Array.Resize(ref _buffer, newCap);
            }
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Push(in T v) {
            if (_tail == _buffer.Length) Array.Resize(ref _buffer, _tail * 2);
            _buffer[_tail] = v;
            _tail++;
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public bool Pop() {
            if (_tail == 0) return false;
            _tail--;
            _buffer[_tail] = default(T);
            return true;
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public ref T Top() {
            return ref _buffer[_tail - 1];
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Clear() {
            _tail = 0;
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void SwapRemoveAt(int idx) {
            this._buffer[idx] = this._buffer[this._tail - 1];
            this._buffer[this._tail - 1] = default;
            _tail--;
        }

        public T this[int index] {
            get {
                if (index >= Count) throw new ArgumentOutOfRangeException("index");
                return _buffer[index];
            }
            set {
                if (index >= Count) throw new ArgumentOutOfRangeException("index");
                _buffer[index] = value;
            }
        }

        public struct Enumerator: IEnumerator<T> {
            public LStack<T> source;
            public int current;

            public Enumerator GetEnumerator() { return this; } //?
            [System.Runtime.CompilerServices.MethodImpl(256)]
            public bool MoveNext() {
                current += 1;
                return current < source.Count;
            }

            public ref T Current {
                [System.Runtime.CompilerServices.MethodImpl(256)]
                get { return ref source._buffer[current]; }
            }
            public void Dispose() { source = null; }
            public void Reset() { current = -1; }
            T IEnumerator<T>.Current { get { return this.Current; } }
            object IEnumerator.Current { get { return this.Current; } }
        }

        public Enumerator GetEnumerator() {
            return new Enumerator { source = this, current = -1 };
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Add(T value) {
            this.Push(value);
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public int CountOf(in T __) {
            return this.Count;
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void EnsureCap(int count, in T __) {
            this.EnsureCap(count);
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public int CountOf() {
            return this.Count;
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public ref T Get(int at) {
            return ref this._buffer[at];
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Set(int idx, T value) {
            this[idx] = value;
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Get(int idx, out T value) {
            value = this[idx];
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void PopBack(int countToPop) {
            this._tail -= countToPop;
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Sort(System.Collections.Generic.IComparer<T> c) {
            System.Array.Sort(this._buffer, 0, this._tail, c);
        }
    }

    public unsafe struct PtrStack<T>: IStack<T>, ISlice<T>, IRefSlice<T>
    where T : unmanaged {
        public T* basePtr;
        public int count;
        // ?public int cap;

        public PtrStack(void* ptr) {
            this.basePtr = (T*)ptr;
            this.count = 0;
        }

        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Add(T value) {
            *(basePtr + count) = value;
            count += 1;
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public int CountOf(in T __) {
            return count;
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public int CountOf() {
            return count;
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public ref T Get(int at) {
            return ref *(basePtr + at);
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Get(int idx, out T value) {
            value = this.Get(idx);
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void Set(int idx, T value) {
            this.Get(idx) = value;
        }
        [System.Runtime.CompilerServices.MethodImpl(256)]
        public void PopBack(int countToPop) {
            count -= countToPop;
        }

    }

}
