// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal sealed class SymbolStack : Stack<ISymbol>
    {
        public new bool Contains(ISymbol item)
        {
            foreach (var x in this)
                if (SymbolEqualityComparer.Default.Equals(x, item))
                    return true;

            return false;
        }
    }
}
