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
    [TestClass]
    public sealed partial class MemberExceptionsAnalyzerTests
    {
        [TestMethod]
        public async Task Empty()
        {
            var source = @"";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowSubtypeOfDocumented()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M()
    {
        throw new NotSupportedException();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowSameTypeAsDocumented()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""></exception>
    public void M()
    {
        throw new NotSupportedException();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowSupertypeOfDocumented()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""></exception>
    public void M()
    {
        {|#0:throw new Exception();|}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""></exception>
    /// <exception cref=""Exception""></exception>
    public void M()
    {
        throw new Exception();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowGeneric()
        {
            var source = @"
using System;

class Ex<T> : Exception
{
}

class C
{
    public void M()
    {
        {|#0:throw new Ex<int>();|}
    }
}";

            var fixedSource = @"
using System;

class Ex<T> : Exception
{
}

class C
{
    /// <exception cref=""Ex{T}""></exception>
    public void M()
    {
        throw new Ex<int>();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Ex<Int32>");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowGenericNested()
        {
            var source = @"
using System;

class C1<T>
{
    public class Ex : Exception
    {
    }
}

class C2
{
    public void M()
    {
        {|#0:throw new C1<int>.Ex();|}
    }
}";

            var fixedSource = @"
using System;

class C1<T>
{
    public class Ex : Exception
    {
    }
}

class C2
{
    /// <exception cref=""C1{T}.Ex""></exception>
    public void M()
    {
        throw new C1<int>.Ex();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "C1<Int32>.Ex");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowOpenGenericNested()
        {
            var source = @"
using System;

class C<T>
{
    class Ex : Exception
    {
    }

    public void M()
    {
        {|#0:throw new Ex();|}
    }
}";

            var fixedSource = @"
using System;

class C<T>
{
    class Ex : Exception
    {
    }

    /// <exception cref=""Ex""></exception>
    public void M()
    {
        throw new Ex();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "C<T>.Ex");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowOpenClassGeneric()
        {
            var source = @"
using System;

class Ex<T1> : Exception
{
}

class C<T2>
{
    public void M()
    {
        {|#0:throw new Ex<T2>();|}
    }
}";

            var fixedSource = @"
using System;

class Ex<T1> : Exception
{
}

class C<T2>
{
    /// <exception cref=""Ex{T1}""></exception>
    public void M()
    {
        throw new Ex<T2>();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Ex<T2>");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowOpenMethodGeneric()
        {
            var source = @"
using System;

class Ex<T1> : Exception
{
}

class C
{
    public void M<T2>()
    {
        {|#0:throw new Ex<T2>();|}
    }
}";

            var fixedSource = @"
using System;

class Ex<T1> : Exception
{
}

class C
{
    /// <exception cref=""Ex{T1}""></exception>
    public void M<T2>()
    {
        throw new Ex<T2>();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M<T2>()", "Ex<T2>");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowNull()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        throw null;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowConditional()
        {
            var source = @"
using System;

class C
{
    public void M(bool x)
    {
        {|#0:throw x
            ? new ArgumentException()
            : new InvalidOperationException();|}
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M(bool)", "ArgumentException, InvalidOperationException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowSwitch()
        {
            var source = @"
using System;

class C
{
    public void M(bool x)
    {
        {|#0:throw x switch
        {
            true => new ArgumentException(),
            false => new InvalidOperationException(),
        };|}
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M(bool)", "ArgumentException, InvalidOperationException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowIgnored()
        {
            var source = @"
using System.Diagnostics;

namespace System.Diagnostics
{
    public class UnreachableException : Exception {}
}

class C
{
    public void M()
    {
        throw new UnreachableException();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowIntransitive()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        {|#0:throw new ArgumentException();|}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""ArgumentException""></exception>
    public void M()
    {
        throw new ArgumentException();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "ArgumentException");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task IntransitiveExceptionThrowingMethodCall()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""ArgumentException""></exception>
    public void M1() => throw new ArgumentException();

    public void M2()
    {
        M1();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task IntransitiveExceptionThrowingPrivateMethodCall()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""ArgumentException""/>
    /// <exception cref=""InvalidOperationException""/>
    private void M1() => throw new InvalidOperationException();

    public void M2()
    {
        {|#0:M1|}();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M2()", "InvalidOperationException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task IntransitiveExceptionThrowingInternalMethodCall()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""ArgumentException""/>
    /// <exception cref=""InvalidOperationException""/>
    internal void M1() => throw new InvalidOperationException();

    public void M2()
    {
        {|#0:M1|}();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M2()", "InvalidOperationException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task IntransitiveExceptionThrowingLocalFunctionMethodCall()
        {
            var source = @"
using System;

class C
{
    public void M(bool b)
    {
        {|#0:F1|}(b);

        {|#1:F2|}(b);

        void F1(bool b)
        {
            if (b)
                throw new ArgumentException();
            else
                throw new InvalidOperationException();
        }

        /// <exception cref=""ArgumentException""/>
        /// <exception cref=""InvalidOperationException""/>
        void F2(bool b)
        {
            if (b)
                throw new ArgumentException();
            else
                throw new InvalidOperationException();
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M(bool)", "InvalidOperationException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M(bool)", "InvalidOperationException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task NameOf()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    public void M2()
    {
        _ = nameof(M1);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task InheritDoc()
        {
            var source = @"
using System;

abstract class B
{
    /// <exception cref=""Exception""></exception>
    public abstract void M();
}

class C : B
{
    /// <inheritdoc/>
    public override void M() => throw new Exception();
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task InheritDocCref()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <inheritdoc cref=""M1()""/>
    public void M2() => throw new Exception();
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task InheritDocGenericCref()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1<T>() => throw new Exception();

    /// <inheritdoc cref=""M1{T}()""/>
    public void M2() => throw new Exception();
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task InheritDocCircularCref()
        {
            var source = @"
using System;

class C
{
    /// <inheritdoc cref=""M2()""/>
    public void M1() => {|#0:throw new Exception()|};

    /// <inheritdoc cref=""M1()""/>
    public void M2() => {|#1:throw new Exception()|};
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M2()", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task InheritDocIndirectInterface()
        {
            var source = @"
using System;

interface I1
{
    /// <exception cref=""Exception""></exception>
    void M();
}

interface I2 : I1
{
}

class C : I2
{
    /// <inheritdoc/>
    public void M() => throw new Exception();
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowingBclMethodCall()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        Console.{|#0:WriteLine|}();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "IOException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ArrayElementAccessWithImplicitOperator()
        {
            // This test case is interesting only because `GetSymbolInfo(node)` returns the implicit
            // operator symbol when `node` is `ElementAccessExpressionSyntax` and
            // `ElementAccessExpressionSyntax.Expression` is an array.

            var source = @"
using System;

class C
{
    /// <exception cref=""NotImplementedException""/>
    public static implicit operator int(C c) => throw new NotImplementedException();

    public void M()
    {
        var cs = new[] { new C() };
        int i = cs[0];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }
    }
}
