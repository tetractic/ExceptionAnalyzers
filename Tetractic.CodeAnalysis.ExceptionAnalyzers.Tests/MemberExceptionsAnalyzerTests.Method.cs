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
        public async Task ThrowInMethodBody()
        {
            var source = @"
using System;

class C
{
    public void M1()
    {
        {|#0:throw new Exception();|}
    }

    public void M2<T>()
    {
        {|#1:throw new Exception();|}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1()
    {
        throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public void M2<T>()
    {
        throw new Exception();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M2<T>()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInMethodExpressionBody()
        {
            var source = @"
using System;

class C
{
    public int M1() => {|#0:throw new Exception()|};

    public int M2<T>() => {|#1:throw new Exception()|};
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public int M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public int M2<T>() => throw new Exception();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M2<T>()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorThrowInMethod()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public void M1() => {|#0:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public void M2() => {|#1:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public void M3() => {|#2:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public void M4() => {|#3:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public void M5() => {|#4:throw new Exception()|};
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M2()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M4()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(4).WithArguments("M5()", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorMethodAccess()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public void M1() {}

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public void M2() {}

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public void M3() {}

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public void M4() {}

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public void M5() {}

    public void M()
    {
        M1();
        _ = new Action(M1);

        M2();
        _ = new Action(M2);

        M3();
        _ = new Action(M3);

        M4();
        _ = new Action(M4);

        M5();
        _ = new Action(M5);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInConstructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    public C1()
    {
        {|#0:M1|}();
        {|#1:M2<int>|}();
    }
}

class C2<T>
{
    public C2(C1 c1)
    {
        c1.{|#2:M1|}();
        c1.{|#3:M2<int>|}();
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public C1()
    {
        M1();
        M2<int>();
    }
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2(C1 c1)
    {
        c1.M1();
        c1.M2<int>();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("C2(C1)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("C2(C1)", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInDestructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    ~C1()
    {
        {|#0:M1|}();
        {|#1:M2<int>|}();
    }
}

class C2<T>
{
    private C1 _c1;

    ~C2()
    {
        _c1.{|#2:M1|}();
        _c1.{|#3:M2<int>|}();
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    ~C1()
    {
        M1();
        M2<int>();
    }
}

class C2<T>
{
    private C1 _c1;

    /// <exception cref=""Exception""></exception>
    ~C2()
    {
        _c1.M1();
        _c1.M2<int>();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("~C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("~C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("~C2()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("~C2()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInEventAddAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    public event Action E
    {
        add
        {
            {|#0:M1|}();
            {|#1:M2<int>|}();
        }
        remove {}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add
        {
            M1();
            M2<int>();
        }
        remove {}
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "add", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("E", "add", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInEventRemoveAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    public event Action E
    {
        add {}
        remove
        {
            {|#0:M1|}();
            {|#1:M2<int>|}();
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove
        {
            M1();
            M2<int>();
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "remove", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("E", "remove", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInPropertyGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    public int P
    {
        get
        {
            {|#0:M1|}();
            {|#1:M2<int>|}();
            return 0;
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get
        {
            M1();
            M2<int>();
            return 0;
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "get", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("P", "get", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInPropertySetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    public int P
    {
        set
        {
            {|#0:M1|}();
            {|#1:M2<int>|}();
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set
        {
            M1();
            M2<int>();
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "set", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("P", "set", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInIndexerGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    public int this[int x]
    {
        get
        {
            {|#0:M1|}();
            {|#1:M2<int>|}();
            return 0;
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get
        {
            M1();
            M2<int>();
            return 0;
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "get", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("this[int]", "get", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInIndexerSetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    public int this[int x]
    {
        set
        {
            {|#0:M1|}();
            {|#1:M2<int>|}();
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set
        {
            M1();
            M2<int>();
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "set", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("this[int]", "set", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingMethodCallInMethod()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    public void M3()
    {
        {|#0:M1|}();
        {|#1:M2<int>|}();
    }

    public void M4<T>()
    {
        {|#2:M1|}();
        {|#3:M2<int>|}();
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public void M1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M2<T>() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public void M3()
    {
        M1();
        M2<int>();
    }

    /// <exception cref=""Exception""></exception>
    public void M4<T>()
    {
        M1();
        M2<int>();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M4<T>()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M4<T>()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingMethodCallChain()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public C M1() => throw new Exception();

    public C M2() => this;

    public void M3()
    {
        {|#0:M1|}().M2();
        _ = {|#1:M1|}().M2();
        M2().{|#2:M1|}();
        _ = M2().{|#3:M1|}();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M3()", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AssignRefTypeMethodReturnValue()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public ref int M1() => throw new Exception();

    public void M2()
    {
        {|#0:M1|}() = 0;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M2()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowUndocumentedInIteratorMethodBody()
        {
            var source = @"
using System;
using System.Collections;

class C
{
    public IEnumerator M()
    {
        yield return 0;
        {|#0:throw new Exception();|}
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0105").WithLocation(0).WithArguments("Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowDocumentedOnMethodInIteratorMethodBody()
        {
            var source = @"
using System;
using System.Collections;

class C
{
    /// <exception cref=""Exception""></exception>
    public IEnumerator M()
    {
        yield return 0;
        {|#0:throw new Exception();|}
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0105").WithLocation(0).WithArguments("Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowDocumentedOnMoveNextInIteratorMethodBody()
        {
            var source = @"
using System;
using System.Collections;

class C
{
    public IEnumerator M()
    {
        yield return 0;
        throw new InvalidOperationException();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }
    }
}
