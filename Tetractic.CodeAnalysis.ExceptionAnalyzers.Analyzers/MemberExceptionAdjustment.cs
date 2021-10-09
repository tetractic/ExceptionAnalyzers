﻿// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis.Text;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal readonly struct MemberExceptionAdjustment
    {
        public readonly string? Accessor;

        public readonly ExceptionAdjustmentKind Kind;

        public readonly string ExceptionTypeId;

        public readonly TextSpan SymbolIdSpan;

        public readonly LinePositionSpan SymbolIdLineSpan;

        public readonly TextSpan AccessorSpan;

        public readonly LinePositionSpan AccessorLineSpan;

        public readonly TextSpan ExceptionTypeIdSpan;

        public readonly LinePositionSpan ExceptionTypeIdLineSpan;

        public MemberExceptionAdjustment(
            string? accessor,
            ExceptionAdjustmentKind kind,
            string exceptionTypeId,
            TextSpan symbolIdSpan,
            LinePositionSpan symbolIdLineSpan,
            TextSpan accessorSpan,
            LinePositionSpan accessorLineSpan,
            TextSpan exceptionTypeIdSpan,
            LinePositionSpan exceptionTypeIdLineSpan)
        {
            Kind = kind;
            ExceptionTypeId = exceptionTypeId;
            Accessor = accessor;
            SymbolIdSpan = symbolIdSpan;
            SymbolIdLineSpan = symbolIdLineSpan;
            AccessorSpan = accessorSpan;
            AccessorLineSpan = accessorLineSpan;
            ExceptionTypeIdSpan = exceptionTypeIdSpan;
            ExceptionTypeIdLineSpan = exceptionTypeIdLineSpan;
        }
    }
}