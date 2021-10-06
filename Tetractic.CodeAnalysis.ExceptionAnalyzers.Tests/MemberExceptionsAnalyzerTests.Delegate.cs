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
using VerifyCS = Tetractic.CodeAnalysis.ExceptionAnalyzers.Test.CSharpCodeFixVerifier<
    Tetractic.CodeAnalysis.ExceptionAnalyzers.MemberExceptionsAnalyzer,
    Tetractic.CodeAnalysis.ExceptionAnalyzers.MemberExceptionsCodeFixProvider>;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers.Tests
{
    public partial class MemberExceptionsAnalyzerTests
    {
        [TestMethod]
        public async Task ThrowInDelegateBody()
        {
            var source = @"
using System;

class C
{
    public delegate void D(int x);

    /// <exception cref=""Exception""></exception>
    public void M1(int x) => throw new Exception();

    public void M2()
    {
        D d1 = {|#0:M1|};
        D d2 = this.{|#1:M1|};
        D d3 = {|#2:delegate|} (int x) { throw new Exception(); };
        D d4 = x {|#3:=>|} throw new Exception();
        D d5 = (x) {|#4:=>|} throw new Exception();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0120").WithLocation(0).WithArguments("D", "M1", "Exception"),
                VerifyCS.Diagnostic("Ex0120").WithLocation(1).WithArguments("D", "M1", "Exception"),
                VerifyCS.Diagnostic("Ex0121").WithLocation(2).WithArguments("D", "Exception"),
                VerifyCS.Diagnostic("Ex0121").WithLocation(3).WithArguments("D", "Exception"),
                VerifyCS.Diagnostic("Ex0121").WithLocation(4).WithArguments("D", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowDocumentedInDelegateBody()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public delegate void D(int x);

    /// <exception cref=""Exception""></exception>
    public void M1(int x) => throw new Exception();

    public void M2()
    {
        D d1 = M1;
        D d2 = this.M1;
        D d3 = delegate (int x) { throw new Exception(); };
        D d4 = x => throw new Exception();
        D d5 = (x) => throw new Exception();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowInExpressionTreeDelegateBody()
        {
            var source = @"
using System;
using System.Linq.Expressions;

class C
{
    public delegate int D(int x);

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P => throw new Exception();

    public void M1(Expression<D> d)
    {
    }

    public void M2()
    {
        M1(x {|#0:=>|} P);
        M1((x) {|#1:=>|} P);
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0121").WithLocation(0).WithArguments("D", "Exception"),
                VerifyCS.Diagnostic("Ex0121").WithLocation(1).WithArguments("D", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task InvokeDelgateOnAssignment()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1(int x) => throw new Exception();

    public void M2()
    {
        D d;
        (d = M1){|#0:(|}0);
    }

    /// <exception cref=""Exception""></exception>
    public delegate void D(int x);
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M2()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task InvokeAssignedDelegateTypePropertyValue()
        {
            var source = @"
using System;

class C
{
    public D P
    {
        set => _ = value;
    }

    /// <exception cref=""Exception""></exception>
    public void M1(int x) => throw new Exception();

    public void M2()
    {
        (P = M1){|#0:(|}0);
    }

    /// <exception cref=""Exception""></exception>
    public delegate void D(int x);
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M2()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task InvokeDelegateTypeMethodReturnValue()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public delegate void D(int x);

    public D M1() => x => throw new Exception();

    public void M2()
    {
        M1(){|#0:(|}0);
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M2()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task InvokeDelegateTypePropertyValue()
        {
            var source = @"
using System;

class C
{
    public D P => x => throw new Exception();

    public void M()
    {
        P{|#0:(|}0);
    }

    /// <exception cref=""Exception""></exception>
    public delegate void D(int x);
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task InvokeAssignedRefDelegateTypeMethodReturnValue()
        {
            var source = @"
using System;

class C
{
    private D d;

    /// <exception cref=""Exception""></exception>
    public void M1(int x) => throw new Exception();

    public ref D M2() => ref d;

    public void M3()
    {
        (M2() = M1){|#0:(|}0);
    }

    /// <exception cref=""Exception""></exception>
    public delegate void D(int x);
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M3()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task InvokeDelegate()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1(int x) => throw new Exception();

    public void M2()
    {
        D d = M1;
        d{|#0:(|}0);
    }

    /// <exception cref=""Exception""></exception>
    public delegate void D(int x);
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M2()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task InvokeDelegateByInvokeMethod()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1(int x) => throw new Exception();

    public void M2()
    {
        D d = M1;
        d.Invoke{|#0:(|}0);
    }

    /// <exception cref=""Exception""></exception>
    public delegate void D(int x);
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M2()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }
    }
}
