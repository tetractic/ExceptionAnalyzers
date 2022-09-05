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
        public async Task MethodImplementedByClass()
        {
            var source = @"
using System;

interface I
{
    /// <summary/>
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
        public async Task MethodImplementedByClassInterfaceOfInterface()
        {
            var source = @"
using System;

interface I1
{
    /// <summary/>
    void M();
}

interface I2 : I1
{
}

class C : I2
{
    /// <exception cref=""Exception""/>
    public void {|#0:M|}() => throw new Exception();
}";

            var expected = VerifyCS.Diagnostic("Ex0200").WithLocation(0).WithArguments("M()", "I1", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task MethodImplementedByClassInterfaceOfBase()
        {
            var source = @"
using System;

interface I
{
    /// <summary/>
    void M();
}

class B : I
{
    public virtual void M() {}
}

class C : B
{
    public override void M() => throw new Exception();
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task MethodImplementedByInterface()
        {
            var source = @"
using System;

interface I1
{
    /// <summary/>
    void M();
}

interface I2 : I1
{
    /// <exception cref=""Exception""/>
    void M();
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task MethodImplementedByInterfaceInterfaceOfInterface()
        {
            var source = @"
using System;

interface I1
{
    /// <summary/>
    void M();
}


interface I2 : I1
{
    /// <exception cref=""Exception""/>
    void M();
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task MethodImplementedByStruct()
        {
            var source = @"
using System;

interface I
{
    /// <summary/>
    void M();
}

struct S : I
{
    /// <exception cref=""Exception""/>
    public void {|#0:M|}() => throw new Exception();
}";

            var expected = VerifyCS.Diagnostic("Ex0200").WithLocation(0).WithArguments("M()", "I", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task MethodImplementedByStructInterfaceOfInterface()
        {
            var source = @"
using System;

interface I1
{
    /// <summary/>
    void M();
}

interface I2 : I1
{
}

struct S : I2
{
    /// <exception cref=""Exception""/>
    public void {|#0:M|}() => throw new Exception();
}";

            var expected = VerifyCS.Diagnostic("Ex0200").WithLocation(0).WithArguments("M()", "I1", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task MethodOverriddenByClass()
        {
            var source = @"
using System;

class B
{
    /// <summary/>
    public virtual void M() {}
}

class C : B
{
    /// <exception cref=""Exception""/>
    public override void {|#0:M|}() => throw new Exception();
}";

            var expected = VerifyCS.Diagnostic("Ex0200").WithLocation(0).WithArguments("M()", "B", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task MethodOverriddenByClassBaseOfBase()
        {
            var source = @"
using System;

class B1
{
    /// <summary/>
    public virtual void M() {}
}

class B2 : B1
{
}

class C : B2
{
    /// <exception cref=""Exception""/>
    public override void {|#0:M|}() => throw new Exception();
}";

            var expected = VerifyCS.Diagnostic("Ex0200").WithLocation(0).WithArguments("M()", "B1", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }
    }
}
