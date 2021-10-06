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
        public async Task ThrowInIndexerGetAccessorBody()
        {
            var source = @"
using System;

class C
{
    public int this[int index]
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
    public int this[int index]
    {
        get
        {
            throw new Exception();
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInIndexerGetAccessorExpressionBody()
        {
            var source = @"
using System;

class C
{
    public int this[int index]
    {
        get => {|#0:throw new Exception()|};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int index]
    {
        get => throw new Exception();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInIndexerExpressionBody()
        {
            var source = @"
using System;

class C
{
    public int this[int index] => {|#0:throw new Exception()|};
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int index] => throw new Exception();
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInIndexerSetAccessorBody()
        {
            var source = @"
using System;

class C
{
    public int this[int index]
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
    public int this[int index]
    {
        set
        {
            throw new Exception();
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInIndexerSetAccessorExpressionBody()
        {
            var source = @"
using System;

class C
{
    public int this[int index]
    {
        set => {|#0:throw new Exception()|};
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int index]
    {
        set => throw new Exception();
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowDocumentedWithAccessorInIndexer()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public int this[int i1]
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int i1, int i2]
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int i1, int i2, int i3]
    {
        get => {|#0:throw new Exception()|};
    }

    /// <exception cref=""Exception""></exception>
    public int this[int i1, int i2, int i3, int i4]
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int i1, int i2, int i3, int i4, int i5]
    {
        set => {|#1:throw new Exception()|};
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int i1, int i2, int i3, int i4, int i5, int i6]
    {
        set => throw new Exception();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int, int, int]", "get", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("this[int, int, int, int, int]", "set", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorThrowInIndexer()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public int this[int i1]
    {
        get => {|#0:throw new Exception()|};
        set => {|#1:throw new Exception()|};
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public int this[int i1, int i2]
    {
        get => {|#2:throw new Exception()|};
        set => {|#3:throw new Exception()|};
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public int this[int i1, int i2, int i3]
    {
        get => {|#4:throw new Exception()|};
        set => {|#5:throw new Exception()|};
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int]", "get", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("this[int]", "set", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(2).WithArguments("this[int, int]", "get", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(3).WithArguments("this[int, int]", "set", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(4).WithArguments("this[int, int, int]", "get", "Exception"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(5).WithArguments("this[int, int, int]", "set", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithAccessorIndexerAccess()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception""></exception>
    public int this[int i1]
    {
        get => throw new Exception();
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int i1, int i2]
    {
        get => throw new Exception();
        set {}
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int i1, int i2, int i3]
    {
        get => 0;
        set => throw new Exception();
    }

    /// <exception cref=""NotImplementedException"" accessor=""get""></exception>
    /// <exception cref=""NotSupportedException"" accessor=""set""></exception>
    public int this[int i1, int i2, int i3, int i4]
    {
        get => throw new NotImplementedException();
        set => throw new NotSupportedException();
    }

    public void M()
    {
        _ = this{|#0:[|}0];
        this{|#1:[|}0] = 0;
        this{|#2:[|}0] += 0;

        _ = this{|#3:[|}0, 0];
        this[0, 0] = 0;
        this{|#4:[|}0, 0] += 0;

        _ = this[0, 0, 0];
        this{|#5:[|}0, 0, 0] = 0;
        this{|#6:[|}0, 0, 0] += 0;

        _ = this{|#7:[|}0, 0, 0, 0];
        this{|#8:[|}0, 0, 0, 0] = 0;
        this{|#9:[|}0, 0, 0, 0] += 0;
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
        public async Task DocumentedWithInvalidAccessorIndexerAccess()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public int this[int i1]
    {
        get => 0;
        set {}
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public int this[int i1, int i2]
    {
        get => 0;
        set {}
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public int this[int i1, int i2, int i3]
    {
        get => 0;
        set {}
    }

    public void M()
    {
        _ = this[0];
        this[0] = 0;
        this[0] += 0;

        _ = this[0, 0];
        this[0, 0] = 0;
        this[0, 0] += 0;

        _ = this[0, 0, 0];
        this[0, 0, 0] = 0;
        this[0, 0, 0] += 0;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowingIndexerGetAccessorInConstructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    public C1()
    {
        _ = this{|#0:[|}0];
    }
}

class C2<T>
{
    public C2(C1 c1)
    {
        _ = c1{|#1:[|}0];
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public C1()
    {
        _ = this[0];
    }
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2(C1 c1)
    {
        _ = c1[0];
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
        public async Task ThrowingIndexerSetAccessorInConstructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    public C1()
    {
        this{|#0:[|}0] = 0;
    }
}

class C2<T>
{
    public C2(C1 c1)
    {
        c1{|#1:[|}0] = 0;
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public C1()
    {
        this[0] = 0;
    }
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2(C1 c1)
    {
        c1[0] = 0;
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
        public async Task ThrowingIndexerGetAccessorInDestructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    ~C1()
    {
        _ = this{|#0:[|}0];
    }
}

class C2<T>
{
    private C1 _c1;

    ~C2()
    {
        _ = _c1{|#1:[|}0];
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    ~C1()
    {
        _ = this[0];
    }
}

class C2<T>
{
    private C1 _c1;

    /// <exception cref=""Exception""></exception>
    ~C2()
    {
        _ = _c1[0];
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
        public async Task ThrowingIndexerSetAccessorInDestructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    ~C1()
    {
        this{|#0:[|}0] = 0;
    }
}

class C2<T>
{
    private C1 _c1;

    ~C2()
    {
        _c1{|#1:[|}0] = 0;
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    ~C1()
    {
        this[0] = 0;
    }
}

class C2<T>
{
    private C1 _c1;

    /// <exception cref=""Exception""></exception>
    ~C2()
    {
        _c1[0] = 0;
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
        public async Task ThrowingIndexerGetAccessorInEventAddAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    public event Action E
    {
        add => _ = this{|#0:[|}0];
        remove {}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => _ = this[0];
        remove {}
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "add", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingIndexerGetAccessorInEventRemoveAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    public event Action E
    {
        add {}
        remove => _ = this{|#0:[|}0];
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => _ = this{|#0:[|}0];
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "remove", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingIndexerSetAccessorInEventAddAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    public event Action E
    {
        add => this{|#0:[|}0] = 0;
        remove {}
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add => this[0] = 0;
        remove {}
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "add", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingIndexerSetAccessorInEventRemvoeAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    public event Action E
    {
        add {}
        remove => this{|#0:[|}0] = 0;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove => this{|#0:[|}0] = 0;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("E", "remove", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingIndexerGetAccessorInPropertyGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    public int P
    {
        get => this{|#0:[|}0];
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => this[0];
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingIndexerGetAccessorInPropertySetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    public int P
    {
        set => _ = this{|#0:[|}0];
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => _ = this{|#0:[|}0];
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingIndexerSetAccessorInPropertyGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    public int P
    {
        get => this{|#0:[|}0] = 0;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get => this[0] = 0;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingIndexerSetAccessorInPropertySetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    public int P
    {
        set => this{|#0:[|}0] = value;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set => this{|#0:[|}0] = value;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingIndexerGetAccessorInIndexerGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    public int this[int x, int y]
    {
        get => this{|#0:[|}0];
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x, int y]
    {
        get => this[0];
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int, int]", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingIndexerGetAccessorInIndexerSetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    public int this[int x, int y]
    {
        set => _ = this{|#0:[|}0];
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x, int y]
    {
        set => _ = this{|#0:[|}0];
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int, int]", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingIndexerSetAccessorInIndexerGetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    public int this[int x, int y]
    {
        get => this{|#0:[|}0] = 0;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x, int y]
    {
        get => this[0] = 0;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int, int]", "get", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingIndexerSetAccessorInIndexerSetAccessor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    public int this[int x, int y]
    {
        set => this{|#0:[|}0] = value;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x, int y]
    {
        set => this{|#0:[|}0] = value;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("this[int, int]", "set", "Exception");
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingIndexerGetAccessorInMethod()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    public void M1()
    {
        _ = this{|#0:[|}0];
    }

    public void M2<T>()
    {
        _ = this{|#1:[|}0];
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public void M1()
    {
        _ = this[0];
    }

    /// <exception cref=""Exception""></exception>
    public void M2<T>()
    {
        _ = this[0];
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
        public async Task ThrowingIndexerSetAccessorInMethod()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    public void M1()
    {
        this{|#0:[|}0] = 0;
    }

    public void M2<T>()
    {
        this{|#1:[|}0] = 0;
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public void M1()
    {
        this[0] = 0;
    }

    /// <exception cref=""Exception""></exception>
    public void M2<T>()
    {
        this[0] = 0;
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
        public async Task ThrowingIndexerAccessChain()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public C this[int x]
    {
        get => throw new Exception();
        set {}
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public C this[int x, int y]
    {
        get => this;
        set => throw new Exception();
    }

    public void M()
    {
        _ = this{|#0:[|}0][0, 0];
        this{|#1:[|}0]{|#2:[|}0, 0] = this;
        _ = this[0, 0]{|#3:[|}0];
        this[0, 0][0] = this;
    }
}";

            var fixedSource = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public C this[int x]
    {
        get => throw new Exception();
        set {}
    }

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public C this[int x, int y]
    {
        get => this;
        set => throw new Exception();
    }

    /// <exception cref=""Exception""></exception>
    public void M()
    {
        _ = this[0][0, 0];
        this[0][0, 0] = this;
        _ = this[0, 0][0];
        this[0, 0][0] = this;
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
        public async Task AssignRefTypeIndexerValue()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public ref int this[int x] => throw new Exception();

    public void M()
    {
        this{|#0:[|}0] = 0;
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }
    }
}
