// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    [Flags]
    internal enum AccessorKinds
    {
        None = 0,
        Unspecified = 1,
        Get = 2,
        Set = 4,
        Add = 8,
        Remove = 16,
    }

    internal static class AccessorKindsExtensions
    {
        public static AccessorKindEnumerator GetEnumerator(this AccessorKinds accessorKinds)
        {
            return new AccessorKindEnumerator(accessorKinds);
        }

        public struct AccessorKindEnumerator : IEnumerator<AccessorKind>
        {
            private AccessorKinds _accessorKinds;

            private AccessorKind _accessorKind;

            public AccessorKindEnumerator(AccessorKinds accessorKinds)
            {
                _accessorKinds = accessorKinds;
                _accessorKind = default;
            }

            public AccessorKind Current => _accessorKind;

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (_accessorKinds == 0)
                    return false;

                int temp = (int)_accessorKinds;
                _accessorKind = (AccessorKind)(temp & ~(temp - 1));
                _accessorKinds ^= (AccessorKinds)(int)_accessorKind;
                return true;
            }

            public void Reset() => throw new NotSupportedException();
        }
    }
}
