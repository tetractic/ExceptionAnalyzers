// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = Tetractic.CodeAnalysis.ExceptionAnalyzers.Test.CSharpCodeFixVerifier<
    Tetractic.CodeAnalysis.ExceptionAnalyzers.MemberExceptionsAnalyzer,
    Tetractic.CodeAnalysis.ExceptionAnalyzers.MemberExceptionsCodeFixProvider>;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers.Tests
{
    public partial class MemberExceptionsAnalyzerTests
    {
        [TestMethod]
        public async Task ThrowingMethodCallInFieldInitializer()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static int M() => throw new Exception();

    public static readonly int F1 = {|#0:M|}();

    public readonly int F2 = {|#1:M|}();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0104").WithLocation(0).WithArguments("F1", "Exception"),
                VerifyCS.Diagnostic("Ex0104").WithLocation(1).WithArguments("F2", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInDocumentedFieldInitializer()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static int M() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public static readonly int F1 = {|#0:M|}();

    /// <exception cref=""Exception""></exception>
    public readonly int F2 = {|#1:M|}();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0104").WithLocation(0).WithArguments("F1", "Exception"),
                VerifyCS.Diagnostic("Ex0104").WithLocation(1).WithArguments("F2", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInAdjustedFieldInitializer()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static int M() => throw new Exception();

    // ExceptionAdjustment: M:C.M -T:System.Exception
    public static readonly int F1 = M();

    // ExceptionAdjustment: M:C.M -T:System.Exception
    public readonly int F2 = M();
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInPropertyInitializer()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static int M() => throw new Exception();

    public static int P1 { get; } = {|#0:M|}();

    public int P2 { get; } = {|#1:M|}();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0104").WithLocation(0).WithArguments("P1", "Exception"),
                VerifyCS.Diagnostic("Ex0104").WithLocation(1).WithArguments("P2", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInDocumentedPropertyInitializer()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static int M() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public static int P1 { get; } = {|#0:M|}();

    /// <exception cref=""Exception""></exception>
    public int P2 { get; } = {|#1:M|}();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0104").WithLocation(0).WithArguments("P1", "Exception"),
                VerifyCS.Diagnostic("Ex0104").WithLocation(1).WithArguments("P2", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInAdjustedPropertyInitializer()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static int M() => throw new Exception();

    // ExceptionAdjustment: M:C.M -T:System.Exception
    public static int P1 { get; } = M();

    // ExceptionAdjustment: M:C.M -T:System.Exception
    public int P2 { get; } = M();
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }
    }
}
