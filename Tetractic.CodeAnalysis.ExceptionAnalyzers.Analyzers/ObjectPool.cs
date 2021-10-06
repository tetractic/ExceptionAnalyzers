// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;
using System.Diagnostics;
using System.Threading;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal abstract class ObjectPool<T>
        where T : class
    {
        private readonly Wrapper[] _items;

        private T _firstItem;

        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than
        ///     one.</exception>
        protected ObjectPool(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _items = new Wrapper[capacity - 1];
        }

        public T Take()
        {
            var item = _firstItem;
            if (item != null && Interlocked.CompareExchange(ref _firstItem, null, item) == item)
                return item;

            var items = _items;
            for (int i = 0; i < items.Length; ++i)
            {
                item = items[i].Value;
                if (item != null && Interlocked.CompareExchange(ref items[i].Value, null, item) == item)
                    return item;
            }

            item = Create();
            return item;
        }

        public virtual void Return(T item) => ReturnCore(item);

        protected abstract T Create();

        protected bool ReturnCore(T item)
        {
            if (_firstItem == null && Interlocked.CompareExchange(ref _firstItem, item, null) == null)
                return true;

            var items = _items;
            for (int i = 0; i < items.Length; ++i)
                if (items[i].Value == null && Interlocked.CompareExchange(ref items[i].Value, item, null) == null)
                    return true;

            return false;
        }

        // Avoid array covariance checks.
        [DebuggerDisplay("{Value}")]
        private struct Wrapper
        {
            public T Value;
        }
    }
}
