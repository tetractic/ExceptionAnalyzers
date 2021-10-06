// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal enum AccessorKind
    {
        Unspecified = AccessorKinds.Unspecified,
        Get = AccessorKinds.Get,
        Set = AccessorKinds.Set,
        Add = AccessorKinds.Add,
        Remove = AccessorKinds.Remove,
    }

    internal static class AccessorKindExtensions
    {
        public static bool IsAnyOf(this AccessorKind accessorKind, AccessorKinds accessorKinds)
        {
            return (accessorKind.ToAccessorKinds() & accessorKinds) != 0;
        }

        public static AccessorKinds ToAccessorKinds(this AccessorKind accessorKind)
        {
            return (AccessorKinds)accessorKind;
        }
    }
}
