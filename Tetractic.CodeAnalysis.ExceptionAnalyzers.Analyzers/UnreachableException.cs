// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    [Serializable]
#pragma warning disable CA1032 // Implement standard exception constructors
#pragma warning disable CA1064 // Exceptions should be public
    internal sealed class UnreachableException : Exception
    {
        public UnreachableException()
            : base("The program executed an instruction that was thought to be unreachable.")
        {
        }
    }
}
