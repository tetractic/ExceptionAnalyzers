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
        public async Task ThrowInConstructorBody()
        {
            var source = @"
using System;

class C1
{
    public C1()
    {
        {|#0:throw new Exception();|}
    }
}

class C2<T>
{
    public C2()
    {
        {|#1:throw new Exception();|}
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1()
    {
        throw new Exception();
    }
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2()
    {
        throw new Exception();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C2()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInConstructorExpressionBody()
        {
            var source = @"
using System;

class C1
{
    public C1() => {|#0:throw new Exception()|};
}

class C2<T>
{
    public C2() => {|#1:throw new Exception()|};
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C2()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorThrowInConstructor()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public C() => {|#0:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public C(int i1) => {|#1:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public C(int i1, int i2) => {|#2:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public C(int i1, int i2, int i3) => {|#3:throw new Exception()|};

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public C(int i1, int i2, int i3, int i4) => {|#4:throw new Exception()|};
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C(int)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("C(int, int)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("C(int, int, int)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(4).WithArguments("C(int, int, int, int)", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorConstructorAccess()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    public C() {}

    /// <exception cref=""Exception"" accessor=""get""></exception>
    public C(int i1) {}

    /// <exception cref=""Exception"" accessor=""set""></exception>
    public C(int i1, int i2) {}

    /// <exception cref=""Exception"" accessor=""add""></exception>
    public C(int i1, int i2, int i3) {}

    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public C(int i1, int i2, int i3, int i4) {}

    public void M()
    {
        _ = new C();

        _ = new C(0);

        _ = new C(0, 0);

        _ = new C(0, 0, 0);

        _ = new C(0, 0, 0, 0);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task ThrowingConstructorCallInConstructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    public C3()
    {
        new {|#0:C1|}();
        new {|#1:C2<int>|}();
    }
}

class C4<T>
{
    public C4()
    {
        new {|#2:C1|}();
        new {|#3:C2<int>|}();
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    /// <exception cref=""Exception""></exception>
    public C3()
    {
        new C1();
        new C2<int>();
    }
}

class C4<T>
{
    /// <exception cref=""Exception""></exception>
    public C4()
    {
        new C1();
        new C2<int>();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("C4()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("C4()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingConstructorCallInEventAddAccessor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    public event Action E
    {
        add
        {
            new {|#0:C1|}();
            new {|#1:C2<int>|}();
        }
        remove {}
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    public event Action E
    {
        add
        {
            new C1();
            new C2<int>();
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
        public async Task ThrowingConstructorCallInEventRemoveAccessor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    public event Action E
    {
        add {}
        remove
        {
            new {|#0:C1|}();
            new {|#1:C2<int>|}();
        }
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    public event Action E
    {
        add {}
        remove
        {
            new C1();
            new C2<int>();
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
        public async Task ThrowingConstructorCallInPropertyGetAccessor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    public int P
    {
        get
        {
            new {|#0:C1|}();
            new {|#1:C2<int>|}();
            return 0;
        }
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int P
    {
        get
        {
            new C1();
            new C2<int>();
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
        public async Task ThrowingConstructorCallInPropertySetAccessor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    public int P
    {
        set
        {
            new {|#0:C1|}();
            new {|#1:C2<int>|}();
        }
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int P
    {
        set
        {
            new C1();
            new C2<int>();
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
        public async Task ThrowingConstructorCallInIndexerGetAccessor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    public int this[int x]
    {
        get
        {
            new {|#0:C1|}();
            new {|#1:C2<int>|}();
            return 0;
        }
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    public int this[int x]
    {
        get
        {
            new C1();
            new C2<int>();
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
        public async Task ThrowingConstructorCallInIndexerSetAccessor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    public int this[int x]
    {
        set
        {
            new {|#0:C1|}();
            new {|#1:C2<int>|}();
        }
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    public int this[int x]
    {
        set
        {
            new C1();
            new C2<int>();
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
        public async Task ThrowingConstructorCallInMethod()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    public void M1()
    {
        new {|#0:C1|}();
        new {|#1:C2<int>|}();
    }

    public void M2<T>()
    {
        new {|#2:C1|}();
        new {|#3:C2<int>|}();
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    /// <exception cref=""Exception""></exception>
    public void M1()
    {
        new C1();
        new C2<int>();
    }

    /// <exception cref=""Exception""></exception>
    public void M2<T>()
    {
        new C1();
        new C2<int>();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M2<T>()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M2<T>()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingInferredConstructorCallInMethod()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    public void M1()
    {
        C1 c1 = {|#0:new|}();
        C2<int> c2 = {|#1:new|}();
    }

    public void M2<T>()
    {
        C1 c1 = {|#2:new|}();
        C2<int> c2 = {|#3:new|}();
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();
}

class C3
{
    /// <exception cref=""Exception""></exception>
    public void M1()
    {
        C1 c1 = new();
        C2<int> c2 = new();
    }

    /// <exception cref=""Exception""></exception>
    public void M2<T>()
    {
        C1 c1 = new();
        C2<int> c2 = new();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M2<T>()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M2<T>()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingBaseConstructorCallInConstructor()
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
        : {|#0:base|}()
    {
    }
}

class C3<T> : C1
{
    public C3()
        : {|#1:base|}()
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
    public C5()
        : {|#2:base|}()
    {
    }
}

class C6<T> : C4<T>
{
    public C6()
        : {|#3:base|}()
    {
    }
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
        : base()
    {
    }
}

class C3<T> : C1
{
    /// <exception cref=""Exception""></exception>
    public C3()
        : base()
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
        : base()
    {
    }
}

class C6<T> : C4<T>
{
    /// <exception cref=""Exception""></exception>
    public C6()
        : base()
    {
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C2()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("C5()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("C6()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingBaseConstructorImplicitCallInConstructor()
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
    public {|#0:C2|}()
    {
    }
}

class C3<T> : C1
{
    public {|#1:C3|}()
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
    public {|#2:C5|}()
    {
    }
}

class C6<T> : C4<T>
{
    public {|#3:C6|}()
    {
    }
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
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C2()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("C5()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("C6()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowingThisConstructorCallInConstructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();

    public C1(int x)
        : {|#0:this|}()
    {
    }
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();

    public C2(int x)
        : {|#1:this|}()
    {
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    public C1() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public C1(int x)
        : this()
    {
    }
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    public C2() => throw new Exception();

    /// <exception cref=""Exception""></exception>
    public C2(int x)
        : this()
    {
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C1(int)", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C2(int)", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInClassConstructorBody()
        {
            var source = @"
using System;

class C1
{
    static C1()
    {
        {|#0:throw new Exception();|}
    }
}

class C2<T>
{
    static C2()
    {
        {|#1:throw new Exception();|}
    }
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    static C1()
    {
        throw new Exception();
    }
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    static C2()
    {
        throw new Exception();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("static C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("static C2()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task ThrowInClassConstructorExpressionBody()
        {
            var source = @"
using System;

class C1
{
    static C1() => {|#0:throw new Exception()|};
}

class C2<T>
{
    static C2() => {|#1:throw new Exception()|};
}";

            var fixedSource = @"
using System;

class C1
{
    /// <exception cref=""Exception""></exception>
    static C1() => throw new Exception();
}

class C2<T>
{
    /// <exception cref=""Exception""></exception>
    static C2() => throw new Exception();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("static C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("static C2()", "Exception"),
            };
            await VerifyCS.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [TestMethod]
        public async Task DocumentedWithInvalidAccessorThrowInClassConstructor()
        {
            var source = @"
using System;

class C1
{
    /// <exception cref=""Exception"" accessor=""""></exception>
    static C1() => {|#0:throw new Exception()|};
}

class C2
{
    /// <exception cref=""Exception"" accessor=""get""></exception>
    static C2() => {|#1:throw new Exception()|};
}

class C3
{
    /// <exception cref=""Exception"" accessor=""set""></exception>
    static C3() => {|#2:throw new Exception()|};
}

class C4
{
    /// <exception cref=""Exception"" accessor=""add""></exception>
    static C4() => {|#3:throw new Exception()|};
}

class C5
{
    /// <exception cref=""Exception"" accessor=""remove""></exception>
    static C5() => {|#4:throw new Exception()|};
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("static C1()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("static C2()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("static C3()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("static C4()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(4).WithArguments("static C5()", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }
    }
}
