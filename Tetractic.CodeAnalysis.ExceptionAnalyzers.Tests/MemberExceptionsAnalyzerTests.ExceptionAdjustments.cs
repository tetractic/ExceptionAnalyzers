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
        public async Task AdjustExceptionsInConstructorBody()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""/>
    /// <exception cref=""NotImplementedException""/>
    public void M()
    {
        if (Environment.TickCount % 2 == 0)
            throw new NotSupportedException();
        else
            throw new NotImplementedException();
    }

    public C()
    {
        {|#0:M|}();
    }

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    public C(int i1)
    {
        {|#1:M|}();
    }

    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public C(int i1, int i2)
    {
        {|#2:M|}();
    }

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public C(int i1, int i2, int i3)
    {
        M();
    }

    // ExceptionAdjustment: M:C.M -T:System.Exception
    public C(int i1, int i2, int i3, int i4)
    {
        {|#3:M|}();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C()", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C(int)", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("C(int, int)", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("C(int, int, int, int)", "NotSupportedException, NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AdjustExceptionsInConstructorExpressionBody()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""/>
    /// <exception cref=""NotImplementedException""/>
    public void M()
    {
        if (Environment.TickCount % 2 == 0)
            throw new NotSupportedException();
        else
            throw new NotImplementedException();
    }

    public C() => {|#0:M|}();

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    public C(int i1) => {|#1:M|}();

    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public C(int i1, int i2) => {|#2:M|}();

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public C(int i1, int i2, int i3) => M();

    // ExceptionAdjustment: M:C.M -T:System.Exception
    public C(int i1, int i2, int i3, int i4) => {|#3:M|}();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C()", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C(int)", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("C(int, int)", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("C(int, int, int, int)", "NotSupportedException, NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AdjustExceptionsInConstructorInitializer()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""/>
    /// <exception cref=""NotImplementedException""/>
    public C()
    {
        if (Environment.TickCount % 2 == 0)
            throw new NotSupportedException();
        else
            throw new NotImplementedException();
    }

    public C(int i1)
        : {|#0:this|}()
    {
    }

    // ExceptionAdjustment: M:C.#ctor -T:System.NotSupportedException
    public C(int i1, int i2)
        : {|#1:this|}()
    {
    }

    // ExceptionAdjustment: M:C.#ctor -T:System.NotImplementedException
    public C(int i1, int i2, int i3)
        : {|#2:this|}()
    {
    }

    // ExceptionAdjustment: M:C.#ctor -T:System.NotSupportedException
    // ExceptionAdjustment: M:C.#ctor -T:System.NotImplementedException
    public C(int i1, int i2, int i3, int i4)
        : this()
    {
    }

    // ExceptionAdjustment: M:C.#ctor -T:System.Exception
    public C(int i1, int i2, int i3, int i4, int i5)
        : {|#3:this|}()
    {
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("C(int)", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("C(int, int)", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("C(int, int, int)", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("C(int, int, int, int, int)", "NotSupportedException, NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AdjustExceptionsInPropertyGetAccessorBody()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""/>
    /// <exception cref=""NotImplementedException""/>
    public int M()
    {
        if (Environment.TickCount % 2 == 0)
            throw new NotSupportedException();
        else
            throw new NotImplementedException();
    }

    public int P1
    {
        get
        {
            return {|#0:M|}();
        }
    }

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    public int P2
    {
        get
        {
            return {|#1:M|}();
        }
    }

    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public int P3
    {
        get
        {
            return {|#2:M|}();
        }
    }

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public int P4
    {
        get
        {
            return M();
        }
    }

    // ExceptionAdjustment: M:C.M -T:System.Exception
    public int P5
    {
        get
        {
            return {|#3:M|}();
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P1", "get", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("P2", "get", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(2).WithArguments("P3", "get", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(3).WithArguments("P5", "get", "NotSupportedException, NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AdjustExceptionsInPropertyGetAccessorExpressionBody()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""/>
    /// <exception cref=""NotImplementedException""/>
    public int M()
    {
        if (Environment.TickCount % 2 == 0)
            throw new NotSupportedException();
        else
            throw new NotImplementedException();
    }

    public int P1
    {
        get => {|#0:M|}();
    }

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    public int P2
    {
        get => {|#1:M|}();
    }

    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public int P3
    {
        get => {|#2:M|}();
    }

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public int P4
    {
        get => M();
    }

    // ExceptionAdjustment: M:C.M -T:System.Exception
    public int P5
    {
        get => {|#3:M|}();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P1", "get", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("P2", "get", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(2).WithArguments("P3", "get", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(3).WithArguments("P5", "get", "NotSupportedException, NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AdjustExceptionsInPropertyExpressionBody()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""/>
    /// <exception cref=""NotImplementedException""/>
    public int M()
    {
        if (Environment.TickCount % 2 == 0)
            throw new NotSupportedException();
        else
            throw new NotImplementedException();
    }

    public int P1 => {|#0:M|}();

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    public int P2 => {|#1:M|}();

    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public int P3 => {|#2:M|}();

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public int P4 => M();

    // ExceptionAdjustment: M:C.M -T:System.Exception
    public int P5 => {|#3:M|}();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P1", "get", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("P2", "get", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(2).WithArguments("P3", "get", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(3).WithArguments("P5", "get", "NotSupportedException, NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AdjustExceptionsInPropertySetAccessorBody()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""/>
    /// <exception cref=""NotImplementedException""/>
    public int M()
    {
        if (Environment.TickCount % 2 == 0)
            throw new NotSupportedException();
        else
            throw new NotImplementedException();
    }

    public int P1
    {
        set
        {
            {|#0:M|}();
        }
    }

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    public int P2
    {
        set
        {
            {|#1:M|}();
        }
    }

    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public int P3
    {
        set
        {
            {|#2:M|}();
        }
    }

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public int P4
    {
        set
        {
            M();
        }
    }

    // ExceptionAdjustment: M:C.M -T:System.Exception
    public int P5
    {
        set
        {
            {|#3:M|}();
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P1", "set", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("P2", "set", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(2).WithArguments("P3", "set", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(3).WithArguments("P5", "set", "NotSupportedException, NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AdjustExceptionsInPropertySetAccessorExpressionBody()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""/>
    /// <exception cref=""NotImplementedException""/>
    public int M()
    {
        if (Environment.TickCount % 2 == 0)
            throw new NotSupportedException();
        else
            throw new NotImplementedException();
    }

    public int P1
    {
        set => {|#0:M|}();
    }

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    public int P2
    {
        set => {|#1:M|}();
    }

    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public int P3
    {
        set => {|#2:M|}();
    }

    // ExceptionAdjustment: M:C.M -T:System.NotSupportedException
    // ExceptionAdjustment: M:C.M -T:System.NotImplementedException
    public int P4
    {
        set => M();
    }

    // ExceptionAdjustment: M:C.M -T:System.Exception
    public int P5
    {
        set => {|#3:M|}();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0101").WithLocation(0).WithArguments("P1", "set", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(1).WithArguments("P2", "set", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(2).WithArguments("P3", "set", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0101").WithLocation(3).WithArguments("P5", "set", "NotSupportedException, NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AdjustExceptionsInMethodBody()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""/>
    /// <exception cref=""NotImplementedException""/>
    public void M1()
    {
        if (Environment.TickCount % 2 == 0)
            throw new NotSupportedException();
        else
            throw new NotImplementedException();
    }

    public void M2()
    {
        {|#0:M1|}();
    }

    // ExceptionAdjustment: M:C.M1 -T:System.NotSupportedException
    public void M3()
    {
        {|#1:M1|}();
    }

    // ExceptionAdjustment: M:C.M1 -T:System.NotImplementedException
    public void M4()
    {
        {|#2:M1|}();
    }

    // ExceptionAdjustment: M:C.M1 -T:System.NotSupportedException
    // ExceptionAdjustment: M:C.M1 -T:System.NotImplementedException
    public void M5()
    {
        M1();
    }

    // ExceptionAdjustment: M:C.M1 -T:System.Exception
    public void M6()
    {
        {|#3:M1|}();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M2()", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M3()", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M4()", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M6()", "NotSupportedException, NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AdjustExceptionsInMethodExpressionBody()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""/>
    /// <exception cref=""NotImplementedException""/>
    public void M1()
    {
        if (Environment.TickCount % 2 == 0)
            throw new NotSupportedException();
        else
            throw new NotImplementedException();
    }

    public void M2() => {|#0:M1|}();

    // ExceptionAdjustment: M:C.M1 -T:System.NotSupportedException
    public void M3() => {|#1:M1|}();

    // ExceptionAdjustment: M:C.M1 -T:System.NotImplementedException
    public void M4() => {|#2:M1|}();

    // ExceptionAdjustment: M:C.M1 -T:System.NotSupportedException
    // ExceptionAdjustment: M:C.M1 -T:System.NotImplementedException
    public void M5() => M1();

    // ExceptionAdjustment: M:C.M1 -T:System.Exception
    public void M6() => {|#3:M1|}();
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M2()", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M3()", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M4()", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M6()", "NotSupportedException, NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AdjustExceptionsInLocalFunctionBody()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""/>
    /// <exception cref=""NotImplementedException""/>
    public void M1()
    {
        if (Environment.TickCount % 2 == 0)
            throw new NotSupportedException();
        else
            throw new NotImplementedException();
    }

    public void M2()
    {
        {|#0:F|}();

        void F()
        {
            M1();
        }
    }

    public void M3()
    {
        {|#1:F|}();

        // ExceptionAdjustment: M:C.M1 -T:System.NotSupportedException
        void F()
        {
            M1();
        }
    }

    public void M4()
    {
        {|#2:F|}();

        // ExceptionAdjustment: M:C.M1 -T:System.NotImplementedException
        void F()
        {
            M1();
        }
    }

    public void M5()
    {
        F();

        // ExceptionAdjustment: M:C.M1 -T:System.NotSupportedException
        // ExceptionAdjustment: M:C.M1 -T:System.NotImplementedException
        void F()
        {
            M1();
        }
    }

    public void M6()
    {
        {|#3:F|}();

        // ExceptionAdjustment: M:C.M1 -T:System.Exception
        void F()
        {
            M1();
        }
    }

    // ExceptionAdjustment: M:C.M1 -T:System.NotSupportedException
    // ExceptionAdjustment: M:C.M1 -T:System.NotImplementedException
    public void M7()
    {
        {|#4:F|}();

        void F()
        {
            M1();
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M2()", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M3()", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M4()", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M6()", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(4).WithArguments("M7()", "NotSupportedException, NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AdjustExceptionsInLocalFunctionExpressionBody()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""NotSupportedException""/>
    /// <exception cref=""NotImplementedException""/>
    public void M1()
    {
        if (Environment.TickCount % 2 == 0)
            throw new NotSupportedException();
        else
            throw new NotImplementedException();
    }

    public void M2()
    {
        {|#0:F|}();

        void F() => M1();
    }

    public void M3()
    {
        {|#1:F|}();

        // ExceptionAdjustment: M:C.M1 -T:System.NotSupportedException
        void F() => M1();
    }

    public void M4()
    {
        {|#2:F|}();

        // ExceptionAdjustment: M:C.M1 -T:System.NotImplementedException
        void F() => M1();
    }

    public void M5()
    {
        F();

        // ExceptionAdjustment: M:C.M1 -T:System.NotSupportedException
        // ExceptionAdjustment: M:C.M1 -T:System.NotImplementedException
        void F() => M1();
    }

    public void M6()
    {
        {|#3:F|}();

        // ExceptionAdjustment: M:C.M1 -T:System.Exception
        void F() => M1();
    }

    // ExceptionAdjustment: M:C.M1 -T:System.NotSupportedException
    // ExceptionAdjustment: M:C.M1 -T:System.NotImplementedException
    public void M7()
    {
        {|#4:F|}();

        void F() => M1();
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M2()", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M3()", "NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M4()", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M6()", "NotSupportedException, NotImplementedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(4).WithArguments("M7()", "NotSupportedException, NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task AdjustExtensionMethod()
        {
            var source = @"
using System;

static class C1
{
    /// <exception cref=""NotSupportedException""/>
    public static void M1(this C2 @this) => throw new NotSupportedException();

    /// <exception cref=""NotSupportedException""/>
    public static void M2(this C3<int> @this) => throw new NotSupportedException();

    /// <exception cref=""NotSupportedException""/>
    public static void M3<T>(this C3<T> @this) => throw new NotSupportedException();
}

class C2
{
}

class C3<T>
{
}

class C4
{
    // ExceptionAdjustment: M:C1.M1(C2) -T:System.NotSupportedException
    public void M1() => new C2().M1();

    // ExceptionAdjustment: M:C1.M2(C3{System.Int32}) -T:System.NotSupportedException
    public void M2() => new C3<int>().M2();

    // ExceptionAdjustment: M:C1.M3``1(C3{``0}) -T:System.NotSupportedException
    public void M3() => new C3<int>().M3();
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }
    }
}
