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
        public async Task ThrowInPropertyGetAccessorBody()
        {
            var source = @"
using System;

class C
{
    public int P
    {
        get
        {
            {|#0:throw new Exception();|}
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get
        {
            throw new Exception();
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInPropertyGetAccessorExpressionBody()
        {
            var source = @"
using System;

class C
{
    public int P
    {
        get => {|#0:throw new Exception()|};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInPropertyExpressionBody()
        {
            var source = @"
using System;

class C
{
    public int P => {|#0:throw new Exception()|};
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P => throw new Exception();
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInPropertySetAccessorBody()
        {
            var source = @"
using System;

class C
{
    public int P
    {
        set
        {
            {|#0:throw new Exception();|}
        }
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set
        {
            throw new Exception();
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInPropertySetAccessorExpressionBody()
        {
            var source = @"
using System;

class C
{
    public int P
    {
        set => {|#0:throw new Exception()|};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowDocumentedWithAccessorInProperty()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public int P1
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P2
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P3
    {
        get => {|#0:throw new Exception()|};
    }

    /// <exception cref=""Exception""></exception>
    public int P4
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P5
    {
        set => {|#1:throw new Exception()|};
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P6
    {
        set => throw new Exception();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P3", "get", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("P5", "set", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorThrowInProperty()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public int P1
    {
        get => {|#0:throw new Exception()|};
        set => {|#1:throw new Exception()|};
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public int P2
    {
        get => {|#2:throw new Exception()|};
        set => {|#3:throw new Exception()|};
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public int P3
    {
        get => {|#4:throw new Exception()|};
        set => {|#5:throw new Exception()|};
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P1", "get", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("P1", "set", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(2).WithArguments("P2", "get", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(3).WithArguments("P2", "set", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(4).WithArguments("P3", "get", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(5).WithArguments("P3", "set", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithAccessorPropertyAccess()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public int P1
    {
        get => throw new Exception();
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P2
    {
        get => throw new Exception();
        set {}
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P3
    {
        get => 0;
        set => throw new Exception();
    }

    /// <exception cref=""NotImplementedException"" accessor=""get""></exception>
    /// <exception cref=""NotSupportedException"" accessor=""set""></exception>
    public int P4
    {
        get => throw new NotImplementedException();
        set => throw new NotSupportedException();
    }

    public void M()
    {
        _ = {|#0:P1|};
        {|#1:P1|} = 0;
        {|#2:P1|} += 0;

        _ = {|#3:P2|};
        P2 = 0;
        {|#4:P2|} += 0;

        _ = P3;
        {|#5:P3|} = 0;
        {|#6:P3|} += 0;

        _ = {|#7:P4|};
        {|#8:P4|} = 0;
        {|#9:P4|} += 0;
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(4).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(5).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(6).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(7).WithArguments("M()", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(8).WithArguments("M()", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(9).WithArguments("M()", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(9).WithArguments("M()", "NotSupportedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorPropertyAccess()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public int P1
    {
        get => 0;
        set {}
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public int P2
    {
        get => 0;
        set {}
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public int P3
    {
        get => 0;
        set {}
    }

    public void M()
    {
        _ = P1;
        P1 = 0;
        P1 += 0;

        _ = P2;
        P2 = 0;
        P2 += 0;

        _ = P3;
        P3 = 0;
        P3 += 0;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowingPropertyGetAccessorInConstructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    public C1()
    {
        _ = {|#0:P|};
    }
}

class C2<T>
{
    public C2(C1 c1)
    {
        _ = c1.{|#1:P|};
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public C1()
    {
        _ = P;
    }
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2(C1 c1)
    {
        _ = c1.P;
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
        public async Task ThrowingPropertySetAccessorInConstructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    public C1()
    {
        {|#0:P|} = 0;
    }
}

class C2<T>
{
    public C2(C1 c1)
    {
        c1.{|#1:P|} = 0;
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public C1()
    {
        P = 0;
    }
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2(C1 c1)
    {
        c1.P = 0;
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
        public async Task ThrowingPropertyGetAccessorInDestructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    ~C1()
    {
        _ = {|#0:P|};
    }
}

class C2<T>
{
    private C1 _c1;

    ~C2()
    {
        _ = _c1.{|#1:P|};
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    ~C1()
    {
        _ = P;
    }
}

class C2<T>
{
    private C1 _c1;

    /// <exception cref=""Exception""></exception>
    ~C2()
    {
        _ = _c1.P;
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
        public async Task ThrowingPropertySetAccessorInDestructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    ~C1()
    {
        {|#0:P|} = 0;
    }
}

class C2<T>
{
    private C1 _c1;

    ~C2()
    {
        _c1.{|#1:P|} = 0;
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    ~C1()
    {
        P = 0;
    }
}

class C2<T>
{
    private C1 _c1;

    /// <exception cref=""Exception""></exception>
    ~C2()
    {
        _c1.P = 0;
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
        public async Task ThrowingPropertyGetAccessorInEventAddAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    public event Action E
    {
        add => _ = {|#0:P|};
        remove {}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => _ = P;
        remove {}
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "add", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingPropertyGetAccessorInEventRemoveAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    public event Action E
    {
        add {}
        remove => _ = {|#0:P|};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => _ = P;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "remove", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingPropertySetAccessorInEventAddAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    public event Action E
    {
        add => {|#0:P|} = 0;
        remove {}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => {|#0:P|} = 0;
        remove {}
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "add", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingPropertySetAccessorInEventRemoveAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    public event Action E
    {
        add {}
        remove => {|#0:P|} = 0;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => P = 0;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "remove", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingPropertyGetAccessorInPropertyGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P1
    {
        get => throw new Exception();
    }

    public int P2
    {
        get => {|#0:P1|};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P1
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P2
    {
        get => P1;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P2", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingPropertyGetAccessorInPropertySetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P1
    {
        get => throw new Exception();
    }

    public int P2
    {
        set => _ = {|#0:P1|};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P1
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P2
    {
        set => _ = P1;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P2", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingPropertySetAccessorInPropertyGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P1
    {
        set => throw new Exception();
    }

    public int P2
    {
        get => {|#0:P1|} = 0;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P1
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P2
    {
        get => {|#0:P1|} = 0;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P2", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingPropertySetAccessorInPropertySetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P1
    {
        set => throw new Exception();
    }

    public int P2
    {
        set => {|#0:P1|} = value;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P1
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P2
    {
        set => P1 = value;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P2", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingPropertyGetAccessorInIndexerGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    public int this[int x]
    {
        get => {|#0:P|};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => P;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingPropertyGetAccessorInIndexerSetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    public int this[int x]
    {
        set => _ = {|#0:P|};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => _ = P;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingPropertySetAccessorInIndexerGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    public int this[int x]
    {
        get => {|#0:P|} = 0;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => {|#0:P|} = 0;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingPropertySetAccessorInIndexerSetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    public int this[int x]
    {
        set => {|#0:P|} = value;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => P = value;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingPropertyGetAccessorInMethod()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    public void M1()
    {
        _ = {|#0:P|};
    }

    public void M2<T>()
    {
        _ = {|#1:P|};
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public void M1()
    {
        _ = P;
    }

    /// <exception cref=""Exception""></exception>
    public void M2<T>()
    {
        _ = P;
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
        public async Task ThrowingPropertySetAccessorInMethod()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    public void M1()
    {
        {|#0:P|} = 0;
    }

    public void M2<T>()
    {
        {|#1:P|} = 0;
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public void M1()
    {
        P = 0;
    }

    /// <exception cref=""Exception""></exception>
    public void M2<T>()
    {
        P = 0;
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
        public async Task ThrowingPropertyAccessChain()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public C P1
    {
        get => throw new Exception();
        set {}
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public C P2
    {
        get => this;
        set => throw new Exception();
    }

    public void M()
    {
        _ = {|#0:P1|}.P2;
        {|#1:P1|}.{|#2:P2|} = this;
        _ = P2.{|#3:P1|};
        P2.P1 = this;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public C P1
    {
        get => throw new Exception();
        set {}
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public C P2
    {
        get => this;
        set => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public void M()
    {
        _ = P1.P2;
        P1.P2 = this;
        _ = P2.P1;
        P2.P1 = this;
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task AssignRefTypePropertyValue()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public ref int P => throw new Exception();

    public void M()
    {
        {|#0:P|} = 0;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }
    }
}
