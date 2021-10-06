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
        public async Task ThrowInEventAddAccessorBody()
        {
            var source = @"
using System;

class C
{
    public event Action E
    {
        add
        {
            {|#0:throw new Exception();|}
        }
        remove {}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add
        {
            throw new Exception();
        }
        remove {}
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "add", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInEventAddAccessorExpressionBody()
        {
            var source = @"
using System;

class C
{
    public event Action E
    {
        add => {|#0:throw new Exception()|};
        remove {}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "add", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInEventRemoveAccessorBody()
        {
            var source = @"
using System;

class C
{
    public event Action E
    {
        add {}
        remove
        {
            {|#0:throw new Exception();|}
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove
        {
            throw new Exception();
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "remove", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInEventRemoveAccessorExpressionBody()
        {
            var source = @"
using System;

class C
{
    public event Action E
    {
        add {}
        remove => {|#0:throw new Exception()|};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "remove", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowDocumentedWithAccessorInEvent()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public event Action E1
    {
        add => throw new Exception();
        remove {}
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E2
    {
        add => throw new Exception();
        remove {}
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E3
    {
        add => {|#0:throw new Exception()|};
        remove {}
    }

    /// <exception cref=""Exception""></exception>
    public event Action E4
    {
        add {}
        remove => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E5
    {
        add {}
        remove => {|#1:throw new Exception()|};
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E6
    {
        add {}
        remove => throw new Exception();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E3", "add", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("E5", "remove", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorThrowInEvent()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public event Action E1
    {
        add => {|#0:throw new Exception()|};
        remove => {|#1:throw new Exception()|};
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public event Action E2
    {
        add => {|#2:throw new Exception()|};
        remove => {|#3:throw new Exception()|};
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public event Action E3
    {
        add => {|#4:throw new Exception()|};
        remove => {|#5:throw new Exception()|};
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E1", "add", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("E1", "remove", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(2).WithArguments("E2", "add", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(3).WithArguments("E2", "remove", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(4).WithArguments("E3", "add", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(5).WithArguments("E3", "remove", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithAccessorEventAccess()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public event Action E1
    {
        add => throw new Exception();
        remove => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E2
    {
        add => throw new Exception();
        remove {}
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E3
    {
        add {}
        remove => throw new Exception();
    }

    /// <exception cref=""NotImplementedException"" accessor=""add""></exception>
    /// <exception cref=""NotSupportedException"" accessor=""remove""></exception>
    public event Action E4
    {
        add => throw new NotImplementedException();
        remove => throw new NotSupportedException();
    }

    public void M()
    {
        {|#0:E1|} += () => {};
        {|#1:E1|} -= () => {};

        {|#2:E2|} += () => {};
        E2 -= () => {};

        E3 += () => {};
        {|#3:E3|} -= () => {};

        {|#4:E4|} += () => {};
        {|#5:E4|} -= () => {};
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(4).WithArguments("M()", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(5).WithArguments("M()", "NotSupportedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorEventAccess()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public event Action E1
    {
        add {}
        remove {}
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public event Action E2
    {
        add {}
        remove {}
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public event Action E3
    {
        add {}
        remove {}
    }

    public void M()
    {
        E1 += () => {};
        E1 -= () => {};

        E2 += () => {};
        E2 -= () => {};

        E3 += () => {};
        E3 -= () => {};
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task DocumentedEventFieldAccess()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public event Action E1;

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E2;

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E3;

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public event Action E4;

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public event Action E5;

    public void M()
    {
        {|#0:E1|} += () => {};
        {|#1:E1|} -= () => {};
        _ = E1;
        E1();

        {|#2:E2|} += () => {};
        E2 -= () => {};
        _ = E2;
        E2();

        E3 += () => {};
        {|#3:E3|} -= () => {};
        _ = E3;
        E3();

        E4 += () => {};
        E4 -= () => {};
        _ = E4;
        E4();

        E5 += () => {};
        E5 -= () => {};
        _ = E5;
        E5();
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

        [TestMethod]
        public async Task ThrowingEventAddAccessorInConstructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    public C1()
    {
        {|#0:E|} += () => {};
    }
}

class C2<T>
{
    public C2(C1 c1)
    {
        c1.{|#1:E|} += () => {};
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    /// <exception cref=""Exception""></exception>
    public C1()
    {
        E += () => {};
    }
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2(C1 c1)
    {
        c1.E += () => {};
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C2(C1)", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventRemoveAccessorInConstructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    public C1()
    {
        {|#0:E|} -= () => {};
    }
}

class C2<T>
{
    public C2(C1 c1)
    {
        c1.{|#1:E|} -= () => {};
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public C1()
    {
        E -= () => {};
    }
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2(C1 c1)
    {
        c1.E -= () => {};
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C2(C1)", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventAddAccessorInDestructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    ~C1()
    {
        {|#0:E|} += () => {};
    }
}

class C2<T>
{
    private C1 _c1;

    ~C2()
    {
        _c1.{|#1:E|} += () => {};
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    /// <exception cref=""Exception""></exception>
    ~C1()
    {
        E += () => {};
    }
}

class C2<T>
{
    private C1 _c1;

    /// <exception cref=""Exception""></exception>
    ~C2()
    {
        _c1.E += () => {};
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
        public async Task ThrowingEventRemoveAccessorInDestructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    ~C1()
    {
        {|#0:E|} -= () => {};
    }
}

class C2<T>
{
    private C1 _c1;

    ~C2()
    {
        _c1.{|#1:E|} -= () => {};
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    ~C1()
    {
        E -= () => {};
    }
}

class C2<T>
{
    private C1 _c1;

    /// <exception cref=""Exception""></exception>
    ~C2()
    {
        _c1.E -= () => {};
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
        public async Task ThrowingEventAddAccessorInEventAddAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E1
    {
        add => throw new Exception();
        remove {}
    }

    public event Action E2
    {
        add => {|#0:E1|} += value;
        remove {}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E1
    {
        add => throw new Exception();
        remove {}
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E2
    {
        add => E1 += value;
        remove {}
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E2", "add", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventAddAccessorInEventRemoveAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E1
    {
        add => throw new Exception();
        remove {}
    }

    public event Action E2
    {
        add {}
        remove => {|#0:E1|} += value;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E1
    {
        add => throw new Exception();
        remove {}
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E2
    {
        add {}
        remove => E1 += value;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E2", "remove", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventRemoveAccessorInEventAddAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E1
    {
        add {}
        remove => throw new Exception();
    }

    public event Action E2
    {
        add => {|#0:E1|} -= value;
        remove {}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E1
    {
        add {}
        remove => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E2
    {
        add => {|#0:E1|} -= value;
        remove {}
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E2", "add", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventRemoveAccessorInEventRemoveAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E1
    {
        add {}
        remove => throw new Exception();
    }

    public event Action E2
    {
        add {}
        remove => {|#0:E1|} -= value;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E1
    {
        add {}
        remove => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E2
    {
        add {}
        remove => E1 -= value;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E2", "remove", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventAddAccessorInPropertyGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    public int P
    {
        get
        {
            {|#0:E|} += () => {};
            return 0;
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get
        {
            E += () => {};
            return 0;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventAddAccessorInPropertySetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    public int P
    {
        set => {|#0:E|} += () => {};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => E += () => {};
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventRemoveAccessorInPropertyGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    public int P
    {
        get
        {
            {|#0:E|} -= () => {};
            return 0;
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get
        {
            {|#0:E|} -= () => {};
            return 0;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventRemoveAccessorInPropertySetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    public int P
    {
        set => {|#0:E|} -= () => {};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => E -= () => {};
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventAddAccessorInIndexerGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    public int this[int x]
    {
        get
        {
            {|#0:E|} += () => {};
            return 0;
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get
        {
            E += () => {};
            return 0;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventAddAccessorInIndexerSetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    public int this[int x]
    {
        set => {|#0:E|} += () => {};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => E += () => {};
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventRemoveAccessorInIndexerGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    public int this[int x]
    {
        get
        {
            {|#0:E|} -= () => {};
            return 0;
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get
        {
            {|#0:E|} -= () => {};
            return 0;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventRemoveAccessorInIndexerSetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    public int this[int x]
    {
        set => {|#0:E|} -= () => {};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => E -= () => {};
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingEventAddAccessorInMethod()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    public void M1()
    {
        {|#0:E|} += () => {};
    }

    public void M2<T>()
    {
        {|#1:E|} += () => {};
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => throw new Exception();
        remove {}
    }

    /// <exception cref=""Exception""></exception>
    public void M1()
    {
        E += () => {};
    }

    /// <exception cref=""Exception""></exception>
    public void M2<T>()
    {
        E += () => {};
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
        public async Task ThrowingEventRemoveAccessorInMethod()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    public void M1()
    {
        {|#0:E|} -= () => {};
    }

    public void M2<T>()
    {
        {|#1:E|} -= () => {};
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public void M1()
    {
        E -= () => {};
    }

    /// <exception cref=""Exception""></exception>
    public void M2<T>()
    {
        E -= () => {};
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
        public async Task InvokeEvent()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public delegate void D();

    /// <exception cref=""Exception""></exception>
    public event Action E1;

    public event D E2;

    public void M()
    {
        E1();
        E2{|#0:(|});
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task InvokeEventByInvokeMethod()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public delegate void D();

    /// <exception cref=""Exception""></exception>
    public event Action E1;

    public event D E2;

    public void M()
    {
        E1.Invoke();
        E2.Invoke{|#0:(|});
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }
    }
}
