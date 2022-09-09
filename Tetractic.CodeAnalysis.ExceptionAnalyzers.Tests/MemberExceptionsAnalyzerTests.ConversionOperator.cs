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
        public async Task ThrowInConversionOperatorBody()
        {
            var source = @"
using System;

class C1
{
    public static explicit operator int(C1 c)
    {
        {|#0:throw new Exception();|}
    }

    public static explicit operator C1(int i)
    {
        {|#1:throw new Exception();|}
    }
}

class C2
{
    public static implicit operator int(C2 c)
    {
        {|#2:throw new Exception();|}
    }

    public static implicit operator C2(int i)
    {
        {|#3:throw new Exception();|}
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public static explicit operator int(C1 c)
    {
        throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public static explicit operator C1(int i)
    {
        throw new Exception();
    }
}

class C2
{
    /// <exception cref=""Exception""></exception>
    public static implicit operator int(C2 c)
    {
        throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public static implicit operator C2(int i)
    {
        throw new Exception();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("explicit operator int(C1)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("explicit operator C1(int)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("implicit operator int(C2)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("implicit operator C2(int)", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInConversionOperatorExpressionBody()
        {
            var source = @"
using System;

class C1
{
    public static explicit operator int(C1 c) => {|#0:throw new Exception()|};

    public static explicit operator C1(int i) => {|#1:throw new Exception()|};
}

class C2
{
    public static implicit operator int(C2 c) => {|#2:throw new Exception()|};

    public static implicit operator C2(int i) => {|#3:throw new Exception()|};
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public static explicit operator int(C1 c) => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public static explicit operator C1(int i) => throw new Exception();
}

class C2
{
    /// <exception cref=""Exception""></exception>
    public static implicit operator int(C2 c) => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public static implicit operator C2(int i) => throw new Exception();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("explicit operator int(C1)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("explicit operator C1(int)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("implicit operator int(C2)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("implicit operator C2(int)", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorThrowInConversionOperator()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public static explicit operator sbyte(C1 c) => {|#0:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public static explicit operator byte(C1 c) => {|#1:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public static explicit operator short(C1 c) => {|#2:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public static explicit operator ushort(C1 c) => {|#3:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public static explicit operator int(C1 c) => {|#4:throw new Exception()|};

}

class C2
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public static implicit operator sbyte(C2 c) => {|#5:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public static implicit operator byte(C2 c) => {|#6:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public static implicit operator short(C2 c) => {|#7:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public static implicit operator ushort(C2 c) => {|#8:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public static implicit operator int(C2 c) => {|#9:throw new Exception()|};

}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("explicit operator sbyte(C1)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("explicit operator byte(C1)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("explicit operator short(C1)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("explicit operator ushort(C1)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(4).WithArguments("explicit operator int(C1)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(5).WithArguments("implicit operator sbyte(C2)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(6).WithArguments("implicit operator byte(C2)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(7).WithArguments("implicit operator short(C2)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(8).WithArguments("implicit operator ushort(C2)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(9).WithArguments("implicit operator int(C2)", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorConversionOperatorAccess()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public static explicit operator sbyte(C1 c) => 0;

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public static explicit operator byte(C1 c) => 0;

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public static explicit operator short(C1 c) => 0;

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public static explicit operator ushort(C1 c) => 0;

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public static explicit operator int(C1 c) => 0;

    public void M()
    {
        _ = (sbyte)new C1();
        _ = (byte)new C1();
        _ = (short)new C1();
        _ = (ushort)new C1();
        _ = (int)new C1();
    }
}

class C2
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public static implicit operator sbyte(C2 c) => 0;

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public static implicit operator byte(C2 c) => 0;

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public static implicit operator short(C2 c) => 0;

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public static implicit operator ushort(C2 c) => 0;

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public static implicit operator int(C2 c) => 0;

    public void M()
    {
        sbyte sb = new C2();
        byte b = new C2();
        short s = new C2();
        ushort us = new C2();
        int i = new C2();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowingExplicitConversionOperatorCall()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static explicit operator int(C c) => throw new Exception();

    public void M()
    {
        var c = new C();
        _ = {|#0:(int)|}c;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowingImplicitConversionOperatorCall()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static implicit operator int(C c) => throw new Exception();

    public void M()
    {
        var c = new C();
        int i = c;
        _ = {|#0:(int)|}c;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }
    }
}
