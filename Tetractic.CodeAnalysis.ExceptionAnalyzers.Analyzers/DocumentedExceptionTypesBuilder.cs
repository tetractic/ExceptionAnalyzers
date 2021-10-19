// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal sealed class DocumentedExceptionTypesBuilder
    {
        private static readonly ObjectPool<DocumentedExceptionTypesBuilder> _pool = new ObjectPool(Environment.ProcessorCount * 3, 64);

        private DocumentedExceptionType[] _items;

        private int _count;

        public DocumentedExceptionTypesBuilder()
        {
            _items = Array.Empty<DocumentedExceptionType>();
        }

        public int Count => _count;

        public int Capacity => _items.Length;

        public static DocumentedExceptionTypesBuilder Allocate()
        {
            return _pool.Take();
        }

        public void Free()
        {
            _pool.Return(this);
        }

        public void Add(DocumentedExceptionType documentedExceptionType)
        {
            Add(documentedExceptionType.ExceptionType, documentedExceptionType.AccessorKind);
        }

        public void Add(INamedTypeSymbol exceptionType, AccessorKind accessorKind)
        {
            Debug.Assert(exceptionType.TypeKind != TypeKind.Error);

            for (int i = 0; i < _count; ++i)
            {
                var item = _items[i];
                if (item.AccessorKind == accessorKind && SymbolEqualityComparer.Default.Equals(item.ExceptionType, exceptionType))
                    return;
            }

            if (_count == _items.Length)
            {
                int newCapacity = _items.Length == 0 ? 4 : _items.Length * 2;
                if (newCapacity < _items.Length)
#pragma warning disable Ex0100 // Member may throw undocumented exception
                    throw new OverflowException();
#pragma warning restore Ex0100 // Member may throw undocumented exception
                Array.Resize(ref _items, newCapacity);
            }

            _items[_count] = new DocumentedExceptionType(exceptionType, accessorKind);
            _count += 1;
        }

        public void Add(ISymbol symbol, INamedTypeSymbol exceptionType, AccessorKind accessorKind)
        {
            switch (symbol.Kind)
            {
                case SymbolKind.Event:
                    if (accessorKind == AccessorKind.Unspecified)
                    {
                        var eventSymbol = (IEventSymbol)symbol;
                        if (eventSymbol.AddMethod != null)
                            Add(exceptionType, AccessorKind.Add);
                        if (eventSymbol.RemoveMethod != null)
                            Add(exceptionType, AccessorKind.Remove);
                        break;
                    }

                    goto default;

                case SymbolKind.Property:
                    if (accessorKind == AccessorKind.Unspecified)
                    {
                        var propertySymbol = (IPropertySymbol)symbol;
                        if (propertySymbol.GetMethod != null)
                            Add(exceptionType, AccessorKind.Get);
                        if (propertySymbol.SetMethod != null)
                            Add(exceptionType, AccessorKind.Set);
                        break;
                    }

                    goto default;

                default:
                    Add(exceptionType, accessorKind);
                    break;
            }
        }

        public bool Remove(INamedTypeSymbol exceptionType, AccessorKind accessorKind)
        {
            for (int i = 0; i < _count; ++i)
            {
                var item = _items[i];
                if (item.AccessorKind == accessorKind && SymbolEqualityComparer.Default.Equals(item.ExceptionType, exceptionType))
                {
                    _count -= 1;
                    Array.Copy(_items, i + 1, _items, i, _count - i);
                    _items[_count] = default;
                    return true;
                }
            }

            return false;
        }

        public void Remove(ISymbol symbol, INamedTypeSymbol exceptionType, AccessorKind accessorKind)
        {
            switch (symbol.Kind)
            {
                case SymbolKind.Event:
                    if (accessorKind == AccessorKind.Unspecified)
                    {
                        var eventSymbol = (IEventSymbol)symbol;
                        if (eventSymbol.AddMethod != null)
                            _ = Remove(exceptionType, AccessorKind.Add);
                        if (eventSymbol.RemoveMethod != null)
                            _ = Remove(exceptionType, AccessorKind.Remove);
                        break;
                    }

                    goto default;

                case SymbolKind.Property:
                    if (accessorKind == AccessorKind.Unspecified)
                    {
                        var propertySymbol = (IPropertySymbol)symbol;
                        if (propertySymbol.GetMethod != null)
                            _ = Remove(exceptionType, AccessorKind.Get);
                        if (propertySymbol.SetMethod != null)
                            _ = Remove(exceptionType, AccessorKind.Set);
                        break;
                    }

                    goto default;

                default:
                    _ = Remove(exceptionType, accessorKind);
                    break;
            }
        }

        public void Clear()
        {
            Array.Clear(_items, 0, _count);
            _count = 0;
        }

        public ImmutableArray<DocumentedExceptionType> ToImmutable()
        {
            return ImmutableArray.Create(_items, 0, _count);
        }

        private sealed class ObjectPool : ObjectPool<DocumentedExceptionTypesBuilder>
        {
            private readonly int _itemMaxCapacity;

            public ObjectPool(int capacity, int itemMaxCapacity)
                : base(capacity)
            {
                _itemMaxCapacity = itemMaxCapacity;
            }

            protected override DocumentedExceptionTypesBuilder Create()
            {
                return new DocumentedExceptionTypesBuilder();
            }

            public override void Return(DocumentedExceptionTypesBuilder item)
            {
                if (item.Capacity > _itemMaxCapacity)
                    return;

                item.Clear();

                _ = ReturnCore(item);
            }
        }
    }
}
