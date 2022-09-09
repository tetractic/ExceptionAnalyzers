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
        public async Task ThrowInOperatorBody()
        {
            var source = @"
using System;

class C
{
    public static C operator +(C c1, C c2)
    {
        {|#0:throw new Exception();|}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static C operator +(C c1, C c2)
    {
        throw new Exception();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("operator +(C, C)", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInOperatorExpressionBody()
        {
            var source = @"
using System;

class C
{
    public static C operator +(C c1, C c2) => {|#0:throw new Exception()|};
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static C operator +(C c1, C c2) => throw new Exception();
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("operator +(C, C)", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInImplicitOperatorBody()
        {
            var source = @"
using System;

class C
{
    public static bool operator true(C c)
    {
        {|#0:throw new Exception();|}
    }

    public static bool operator false(C c)
    {
        {|#1:throw new Exception();|}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static bool operator true(C c)
    {
        throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public static bool operator false(C c)
    {
        throw new Exception();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("operator true(C)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("operator false(C)", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInImplicitOperatorExpressionBody()
        {
            var source = @"
using System;

class C
{
    public static bool operator true(C c) => {|#0:throw new Exception()|};

    public static bool operator false(C c) => {|#1:throw new Exception()|};
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static bool operator true(C c) => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public static bool operator false(C c) => throw new Exception();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("operator true(C)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("operator false(C)", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorThrowInOperator()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public static C operator +(C c1, C c2) => {|#0:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public static C operator -(C c1, C c2) => {|#1:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public static C operator *(C c1, C c2) => {|#2:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public static C operator /(C c1, C c2) => {|#3:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public static C operator %(C c1, C c2) => {|#4:throw new Exception()|};
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("operator +(C, C)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("operator -(C, C)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("operator *(C, C)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("operator /(C, C)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(4).WithArguments("operator %(C, C)", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorOperatorAccess()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public static C operator +(C c1, C c2) => null;

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public static C operator -(C c1, C c2) => null;

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public static C operator *(C c1, C c2) => null;

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public static C operator /(C c1, C c2) => null;

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public static C operator %(C c1, C c2) => null;

    public void M()
    {
        _ = new C() + new C();

        _ = new C() - new C();

        _ = new C() * new C();

        _ = new C() / new C();

        _ = new C() % new C();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowingTrueOrFalseOperatorCall()
        {
            // This operator is implicit, so no analysis is performed.

            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static bool operator true(C c) => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public static bool operator false(C c) => throw new Exception();

    public static C operator &(C c1, C c2) => c1;

    public static C operator |(C c1, C c2) => c1;

    public void M()
    {
        if (new C() || new C())
            ;
        if (new C() && new C())
            ;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowingIncrementOrDecrementOperatorCall()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static C operator ++(C c) => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public static C operator --(C c) => throw new Exception();

    public void M()
    {
        var c = new C();
        {|#0:++|}c;
        c{|#1:++|};
        {|#2:--|}c;
        c{|#3:--|};
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M()", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [DataTestMethod]
        [DataRow("+")]
        [DataRow("-")]
        [DataRow("~")]
        [DataRow("!")]
        public async Task ThrowingUnaryOperatorCall(string op)
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static C operator " + op + @"(C c) => throw new Exception();

    public void M()
    {
        var c = new C();
        _ = {|#0:" + op + @"|}c;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [DataTestMethod]
        [DataRow("*")]
        [DataRow("/")]
        [DataRow("%")]
        [DataRow("+")]
        [DataRow("-")]
        [DataRow("<<")]
        [DataRow(">>")]
        [DataRow("&")]
        [DataRow("^")]
        [DataRow("|")]
        public async Task ThrowingBinaryOperatorCall(string op)
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static C operator " + op + @"(C c, int i) => throw new Exception();

    public void M()
    {
        var c = new C();
        _ = c {|#0:" + op + @"|} 1;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [DataTestMethod]
        [DataRow("<", ">")]
        [DataRow(">", "<")]
        [DataRow("<=", ">=")]
        [DataRow(">=", "<=")]
        [DataRow("==", "!=")]
        [DataRow("!=", "==")]
        public async Task ThrowingPairedBinaryOperatorCall(string op1, string op2)
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public static C operator " + op1 + @"(C c, int i) => throw new Exception();

    public static C operator " + op2 + @"(C c, int i) => c;

    public void M()
    {
        var c = new C();
        _ = c {|#0:" + op1 + @"|} 1;
        _ = c " + op2 + @" 1;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [DataTestMethod]
        [DataRow("&", "&&", "|", "||")]
        [DataRow("|", "||", "&", "&&")]
        public async Task ThrowingBinaryLogicalOperatorCall(string op1, string logicalOp1, string op2, string logicalOp2)
        {
            var source = @"
using System;

class C
{
    public static bool operator true(C c) => true;

    public static bool operator false(C c) => false;

    /// <exception cref=""Exception""></exception>
    public static C operator " + op1 + @"(C c1, C c2) => throw new Exception();

    public static C operator " + op2 + @"(C c1, C c2) => c1;

    public void M()
    {
        var c = new C();
        _ = c {|#0:" + logicalOp1 + @"|} c;
        _ = c " + logicalOp2 + @" c;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }
    }
}
