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
        public async Task ThrowInLocalFunctionBody()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        _ = {|#0:F|}();

        int F()
        {
            throw new Exception();
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M()
    {
        _ = F();

        int F()
        {
            throw new Exception();
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInLocalFunctionExpressionBody()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        _ = {|#0:F|}();

        int F() => throw new Exception();
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M()
    {
        _ = F();

        int F() => throw new Exception();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInChainedLocalFunction()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        _ = {|#0:F1|}();

        int F1() => F2();

        int F2() => throw new Exception();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowInRecursiveLocalFunction()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        _ = {|#0:F|}();

        int F()
        {
            F();

            throw new Exception();
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowInNestedLocalFunction()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        _ = {|#0:F1|}();

        int F1()
        {
            return F2();

            int F2() => throw new Exception();
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowInDocumentedLocalFunction()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M()
    {
        _ = F();

        /// <returns>Never.</returns>
        int F() => {|#0:throw new Exception()|};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M()
    {
        _ = F();

        /// <returns>Never.</returns>
        /// <exception cref=""Exception""></exception>
        int F() => throw new Exception();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("F()", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorThrowInLocalFunction()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        /// <exception cref=""Exception"" accessor=""""></exception>
        int F1() => {|#0:throw new Exception()|};

        /// <exception cref=""Exception"" accessor=""get""></exception>
        int F2() => {|#1:throw new Exception()|};

        /// <exception cref=""Exception"" accessor=""set""></exception>
        int F3() => {|#2:throw new Exception()|};

        /// <exception cref=""Exception"" accessor=""add""></exception>
        int F4() => {|#3:throw new Exception()|};

        /// <exception cref=""Exception"" accessor=""remove""></exception>
        int F5() => {|#4:throw new Exception()|};
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("F1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("F2()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("F3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("F4()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(4).WithArguments("F5()", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorLocalFunctionAccess()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        /// <exception cref=""Exception"" accessor=""""></exception>
        void F1() {}

        /// <exception cref=""Exception"" accessor=""get""></exception>
        void F2() {}

        /// <exception cref=""Exception"" accessor=""set""></exception>
        void F3() {}

        /// <exception cref=""Exception"" accessor=""add""></exception>
        void F4() {}

        /// <exception cref=""Exception"" accessor=""remove""></exception>
        void F5() {}

        F1();
        _ = new Action(F1);

        F2();
        _ = new Action(F2);

        F3();
        _ = new Action(F3);

        F4();
        _ = new Action(F4);

        F5();
        _ = new Action(F5);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowUndocumentedInIteratorLocalFunctionBody()
        {
            var source = @"
using System;
using System.Collections;

class C
{
    public void M()
    {
        F1();
        F2();

        IEnumerator F1()
        {
            yield return 0;
            {|#0:throw new Exception();|}
        }

        /// <returns>An enumerator.</returns>
        IEnumerator F2()
        {
            yield return 0;
            {|#1:throw new Exception();|}
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0105").WithLocation(0).WithArguments("Exception"),
                VerifyCS.Diagnostic("Ex0105").WithLocation(1).WithArguments("Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowDocumentedOnLocalFunctionInIteratorLocalFunctionBody()
        {
            var source = @"
using System;
using System.Collections;

class C
{
    public void M()
    {
        {|#0:F|}();

        /// <exception cref=""Exception""></exception>
        IEnumerator F()
        {
            yield return 0;
            {|#1:throw new Exception();|}
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0105").WithLocation(1).WithArguments("Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowDocumentedOnMoveNextInIteratorLocalFunctionBody()
        {
            var source = @"
using System;
using System.Collections;

class C
{
    public void M()
    {
        F();

        IEnumerator F()
        {
            yield return 0;
            throw new InvalidOperationException();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }
    }
}
