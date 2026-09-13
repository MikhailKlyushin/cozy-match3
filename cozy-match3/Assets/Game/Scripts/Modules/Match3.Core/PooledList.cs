using System;
using System.Collections;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Reusable growable buffer. Exists for two reasons <see cref="List{T}"/> cannot cover:
    /// <c>ref</c> element access and a struct enumerator, so foreach does not allocate (§14).
    /// There is deliberately no static pool — owners keep an instance and call
    /// <see cref="Clear"/>, so a buffer never outlives the level attempt.
    /// </summary>
    public sealed class PooledList<T> : IReadOnlyList<T>
    {
        private const int DefaultCapacity = 16;

        private T[] _items;
        private int _count;

        public PooledList()
            : this(DefaultCapacity)
        {
        }

        public PooledList(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _items = capacity == 0 ? Array.Empty<T>() : new T[capacity];
        }

        public int Count => _count;

        public int Capacity => _items.Length;

        /// <summary>Backing array, for <see cref="Array.Sort{T}(T[],int,int,IComparer{T})"/> without copies.</summary>
        public T[] Buffer => _items;

        public ref T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return ref _items[index];
            }
        }

        T IReadOnlyList<T>.this[int index] => this[index];

        public void Add(in T item)
        {
            if (_count == _items.Length)
            {
                Grow(_count + 1);
            }

            _items[_count++] = item;
        }

        public void AddRange(PooledList<T> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            EnsureCapacity(_count + source._count);
            Array.Copy(source._items, 0, _items, _count, source._count);
            _count += source._count;
        }

        /// <summary>Order-preserving: element order is frequently part of a GDD contract.</summary>
        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            _count--;
            if (index < _count)
            {
                Array.Copy(_items, index + 1, _items, index, _count - index);
            }

            _items[_count] = default;
        }

        /// <summary>Resets length, keeps capacity.</summary>
        public void Clear()
        {
            if (!typeof(T).IsValueType)
            {
                Array.Clear(_items, 0, _count);
            }

            _count = 0;
        }

        public void EnsureCapacity(int capacity)
        {
            if (capacity > _items.Length)
            {
                Grow(capacity);
            }
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        private void Grow(int required)
        {
            int capacity = _items.Length == 0 ? DefaultCapacity : _items.Length * 2;
            if (capacity < required)
            {
                capacity = required;
            }

            Array.Resize(ref _items, capacity);
        }

        public struct Enumerator : IEnumerator<T>
        {
            private readonly PooledList<T> _list;
            private int _index;

            internal Enumerator(PooledList<T> list)
            {
                _list = list;
                _index = -1;
            }

            public T Current => _list._items[_index];

            object IEnumerator.Current => Current;

            public bool MoveNext() => ++_index < _list._count;

            public void Reset() => _index = -1;

            public void Dispose()
            {
            }
        }
    }
}
