// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
    internal readonly struct MemberExceptionAdjustment
    {
        public readonly string? Accessor;

        public readonly string? Flag;

        public readonly ExceptionAdjustmentKind Kind;

        public readonly string ExceptionTypeId;

        public readonly TextSpan SymbolIdSpan;

        public readonly TextSpan AccessorSpan;

        public readonly TextSpan FlagSpan;

        public readonly TextSpan ExceptionTypeIdSpan;

        public MemberExceptionAdjustment(
            string? accessor,
            string? flag,
            ExceptionAdjustmentKind kind,
            string exceptionTypeId,
            TextSpan symbolIdSpan,
            TextSpan accessorSpan,
            TextSpan flagSpan,
            TextSpan exceptionTypeIdSpan)
        {
            Kind = kind;
            ExceptionTypeId = exceptionTypeId;
            Accessor = accessor;
            Flag = flag;
            SymbolIdSpan = symbolIdSpan;
            AccessorSpan = accessorSpan;
            FlagSpan = flagSpan;
            ExceptionTypeIdSpan = exceptionTypeIdSpan;
        }

        private string GetDebuggerDisplay()
        {
            string result = string.Empty;
            if (Accessor != null)
                result += $"{Accessor} ";
            if (Flag != null)
                result += $"${Flag} ";
            result += Kind switch
            {
                ExceptionAdjustmentKind.Addition => "+",
                ExceptionAdjustmentKind.Removal => "-",
                _ => throw new UnreachableException()
            };
            return result + ExceptionTypeId;
        }
    }
}
