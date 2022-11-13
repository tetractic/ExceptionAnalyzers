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
using VerifyCS = Tetractic.CodeAnalysis.ExceptionAnalyzers.Test.CSharpAnalyzerVerifier<
    Tetractic.CodeAnalysis.ExceptionAnalyzers.SupertypeExceptionsAnalyzer>;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers.Tests
{
    [TestClass]
    public sealed partial class SupertypeExceptionAnalyzerTests
    {
        [TestMethod]
        public async Task SupertypeWithUndocumentedMemberInSameCompilation()
        {
            var source = @"
using System;

interface I
{
    void M();
}

class C : I
{
    /// <exception cref=""Exception""/>
    public void {|#0:M|}() => throw new Exception();
}";

            var expected = VerifyCS.Diagnostic("Ex0200").WithLocation(0).WithArguments("M()", "I", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task SupertypeWithUndocumentedMemberInDifferentCompilation()
        {
            var additionalReferencedAssemblySource = @"
public interface I
{
    void M();
}";

            var source = @"
using System;

class C : I
{
    /// <exception cref=""Exception""/>
    public void {|#0:M|}() => throw new Exception();
}";

            await VerifyCS.VerifyAnalyzerWithAdditionalReferencedAssemblyAsync(source, additionalReferencedAssemblySource);
        }
    }
}
