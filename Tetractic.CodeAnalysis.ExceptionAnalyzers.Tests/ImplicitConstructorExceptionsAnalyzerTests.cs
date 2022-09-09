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
    Tetractic.CodeAnalysis.ExceptionAnalyzers.ImplicitConstructorExceptionsAnalyzer,
    Tetractic.CodeAnalysis.ExceptionAnalyzers.ImplicitConstructorExceptionsCodeFixProvider>;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers.Tests
{
    [TestClass]
    public sealed class ImplicitConstructorExceptionsAnalyzerTests
    {
        [TestMethod]
        public async Task ExplicitConstructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2 : C1
{
    public C2()
    {
    }
}

class C3<T>
{
    /// <exception cref=""Exception""></exception>
    public C3() => throw new Exception();
}


class C4 : C3<int>
{
    public C4()
    {
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowingBaseConstructorImplicitCallInImplicitConstructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class {|#0:C2|} : C1
{
}

class {|#1:C3|}<T> : C1
{
}

class C4<T>
{
    /// <exception cref=""Exception""></exception>
    public C4() => throw new Exception();
}


class {|#2:C5|} : C4<int>
{
}

class {|#3:C6|}<T> : C4<T>
{
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2 : C1
{
    /// <exception cref=""Exception""></exception>
    public C2()
    {
    }
}

class C3<T> : C1
{
    /// <exception cref=""Exception""></exception>
    public C3()
    {
    }
}

class C4<T>
{
    /// <exception cref=""Exception""></exception>
    public C4() => throw new Exception();
}


class C5 : C4<int>
{
    /// <exception cref=""Exception""></exception>
    public C5()
    {
    }
}

class C6<T> : C4<T>
{
    /// <exception cref=""Exception""></exception>
    public C6()
    {
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0103").WithLocation(0).WithArguments("C2", "Exception"),
                VerifyCS.Diagnostic("Ex0103").WithLocation(1).WithArguments("C3<T>", "Exception"),
                VerifyCS.Diagnostic("Ex0103").WithLocation(2).WithArguments("C5", "Exception"),
                VerifyCS.Diagnostic("Ex0103").WithLocation(3).WithArguments("C6<T>", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }
    }
}
