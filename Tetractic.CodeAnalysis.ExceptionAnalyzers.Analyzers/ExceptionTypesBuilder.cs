// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal sealed class ExceptionTypesBuilder
    {
        private static readonly ObjectPool<ExceptionTypesBuilder> _pool = new ObjectPool(Environment.ProcessorCount * 16, 64);

        private INamedTypeSymbol[] _items;

        private int _count;

        public ExceptionTypesBuilder()
        {
            _items = Array.Empty<INamedTypeSymbol>();
        }

        public int Count => _count;

        public int Capacity => _items.Length;

        /// <exception cref="IndexOutOfRangeException" accessor="get"/>
        /// <exception cref="IndexOutOfRangeException" accessor="set"/>
        public INamedTypeSymbol this[int index]
        {
            get
            {
                if ((uint)index >= _count)
                    throw new IndexOutOfRangeException();

                return _items[index];
            }
            set
            {
                if ((uint)index >= _count)
                    throw new IndexOutOfRangeException();

                _items[index] = value;
            }
        }

        public static ExceptionTypesBuilder Allocate()
        {
            return _pool.Take();
        }

        public void Free()
        {
            _pool.Return(this);
        }

        public void Add(INamedTypeSymbol item)
        {
            if (item.TypeKind == TypeKind.Error)
                return;

            for (int i = 0; i < _count; ++i)
                if (SymbolEqualityComparer.Default.Equals(_items[i], item))
                    return;

            if (_count == _items.Length)
            {
                int newCapacity = _items.Length == 0 ? 4 : _items.Length * 2;
                if (newCapacity < _items.Length)
#pragma warning disable Ex0100 // Member may throw undocumented exception
                    throw new OverflowException();
#pragma warning restore Ex0100 // Member may throw undocumented exception
                Array.Resize(ref _items, newCapacity);
            }

            _items[_count] = item;
            _count += 1;
        }

        public void Clear()
        {
            Array.Clear(_items, 0, _count);
            _count = 0;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        public void Reverse(int index)
        {
            for (int low = index, high = _count - 1; low < high; low += 1, high -= 1)
            {
                var temp = _items[low];
                _items[low] = _items[high];
                _items[high] = temp;
            }
        }

        public ImmutableArray<INamedTypeSymbol> ToImmutable()
        {
            return ImmutableArray.Create(_items, 0, _count);
        }

        public struct Enumerator
        {
            private readonly ExceptionTypesBuilder _collection;

            private int _index;

            internal Enumerator(ExceptionTypesBuilder collection)
            {
                _collection = collection;
                _index = -1;
            }

            public INamedTypeSymbol Current => _collection[_index];

            public bool MoveNext()
            {
                if (_index + 1 == _collection.Count)
                    return false;

                _index += 1;
                return true;
            }
        }

        private sealed class ObjectPool : ObjectPool<ExceptionTypesBuilder>
        {
            private readonly int _itemMaxCapacity;

            public ObjectPool(int capacity, int itemMaxCapacity)
                : base(capacity)
            {
                _itemMaxCapacity = itemMaxCapacity;
            }

            protected override ExceptionTypesBuilder Create()
            {
                return new ExceptionTypesBuilder();
            }

            public override void Return(ExceptionTypesBuilder item)
            {
                if (item.Capacity > _itemMaxCapacity)
                    return;

                item.Clear();

                _ = ReturnCore(item);
            }
        }
    }
}
