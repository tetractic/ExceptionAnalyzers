// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = Tetractic.CodeAnalysis.ExceptionAnalyzers.Test.CSharpAnalyzerVerifier<
    Tetractic.CodeAnalysis.ExceptionAnalyzers.SupertypeExceptionsAnalyzer>;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers.Tests
{
    public sealed partial class SupertypeExceptionAnalyzerTests
    {
        [TestMethod]
        public async Task GetOnlyPropertyImplementedByGetSetProperty()
        {
            var source = @"
using System;

interface I
{
    /// <exception cref=""Exception""/>
    int P { get; }
}

class C : I
{
    /// <exception cref=""Exception""/>
    public int P { get; set; }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task SetOnlyPropertyImplementedByGetSetProperty()
        {
            var source = @"
using System;

interface I
{
    /// <exception cref=""Exception""/>
    int P { set; }
}

class C : I
{
    /// <exception cref=""Exception""/>
    public int P { get; set; }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [DataTestMethod]
        [DataRow("get")]
        [DataRow("set")]
        public async Task PropertyImplementedByClassAndInterfaceIncludesAccessorAndImplementorExcludesAccessor(string accessor)
        {
            var source = @"
using System;

interface I
{
    /// <exception cref=""Exception""/>
    int P { get; set; }
}

class C : I
{
    /// <exception cref=""Exception"" accessor=""" + accessor + @"""/>
    public int P { get; set; }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [DataTestMethod]
        [DataRow("get", "set")]
        [DataRow("set", "get")]
        public async Task PropertyImplementedByClassAndInterfaceExcludesAccessorAndImplementorIncludesAccessor(string accessor, string otherAccessor)
        {
            var source = @"
using System;

interface I
{
    /// <exception cref=""Exception"" accessor=""" + accessor + @"""/>
    int P { get; set; }
}

class C : I
{
    /// <exception cref=""Exception""/>
    public int {|#0:P|} { get; set; }
}";

            var expected = VerifyCS.Diagnostic("Ex0201").WithLocation(0).WithArguments("P", otherAccessor, "I", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }
    }
}
