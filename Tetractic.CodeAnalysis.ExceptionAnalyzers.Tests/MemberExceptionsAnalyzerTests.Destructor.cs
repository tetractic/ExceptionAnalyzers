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
        public async Task ThrowInDestructorBody()
        {
            var source = @"
using System;

class C1
{
    ~C1()
    {
        {|#0:throw new Exception();|}
    }
}

class C2<T>
{
    ~C2()
    {
        {|#1:throw new Exception();|}
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    ~C1()
    {
        throw new Exception();
    }
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    ~C2()
    {
        throw new Exception();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("~C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("~C2()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInDestructorExpressionBody()
        {
            var source = @"
using System;

class C1
{
    ~C1() => {|#0:throw new Exception()|};
}

class C2<T>
{
    ~C2() => {|#1:throw new Exception()|};
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    ~C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    ~C2() => throw new Exception();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("~C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("~C2()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorThrowInDestructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    ~C1()
    {
        {|#0:throw new Exception();|}
    }
}

class C2
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    ~C2()
    {
        {|#1:throw new Exception();|}
    }
}

class C3
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    ~C3()
    {
        {|#2:throw new Exception();|}
    }
}

class C4
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    ~C4()
    {
        {|#3:throw new Exception();|}
    }
}

class C5
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    ~C5()
    {
        {|#4:throw new Exception();|}
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("~C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("~C2()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("~C3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("~C4()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(4).WithArguments("~C5()", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }
    }
}
