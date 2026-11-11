// Copyright Carl Reinke
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
        private static async Task ExpectTrueAsync(string filter)
        {
            var source = @"
using System;

class C
{
    bool B => Environment.TickCount % 2 == 0;

    void M()
    {
        try
        {
            throw new Exception();
        }
        catch when (" + filter + @")
        {
        }
        catch
        {
            throw;
        }

        try
        {
            {|#0:throw new Exception();|}
        }
        catch when (" + filter + @")
        {
            throw;
        }
        catch
        {
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        private static async Task ExpectFalseAsync(string filter)
        {
            var source = @"
using System;

class C
{
    bool B => Environment.TickCount % 2 == 0;

    void M()
    {
        try
        {
            throw new Exception();
        }
        catch when (" + filter + @")
        {
            throw;
        }
        catch
        {
        }

        try
        {
            {|#0:throw new Exception();|}
        }
        catch when (" + filter + @")
        {
        }
        catch
        {
            throw;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        private static async Task ExpectMaybeAsync(string filter)
        {
            var source = @"
using System;

class C
{
    bool B => Environment.TickCount % 2 == 0;

    void M()
    {
        try
        {
            {|#0:throw new Exception();|}
        }
        catch when (" + filter + @")
        {
            throw;
        }
        catch
        {
        }

        try
        {
            {|#1:throw new Exception();|}
        }
        catch when (" + filter + @")
        {
        }
        catch
        {
            throw;
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M()", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        private static async Task ExpectTrueAsync(string throwType, string catchDeclaration, string filter)
        {
            var source = @"
using System;

class C
{
    bool B => Environment.TickCount % 2 == 0;

    void M()
    {
        try
        {
            throw new " + throwType + @"();
        }
        catch (" + catchDeclaration + @") when (" + filter + @")
        {
        }
        catch
        {
            throw;
        }

        try
        {
            {|#0:throw new " + throwType + @"();|}
        }
        catch (" + catchDeclaration + @") when (" + filter + @")
        {
            throw;
        }
        catch
        {
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", throwType);
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        private static async Task ExpectFalseAsync(string throwType, string catchDeclaration, string filter)
        {
            var source = @"
using System;

class C
{
    bool B => Environment.TickCount % 2 == 0;

    void M()
    {
        try
        {
            throw new " + throwType + @"();
        }
        catch (" + catchDeclaration + @") when (" + filter + @")
        {
            throw;
        }
        catch
        {
        }

        try
        {
            {|#0:throw new " + throwType + @"();|}
        }
        catch (" + catchDeclaration + @") when (" + filter + @")
        {
        }
        catch
        {
            throw;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", throwType);
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        private static async Task ExpectMaybeAsync(string throwType, string catchDeclaration, string filter, string? filteredType = null)
        {
            var source = @"
using System;

class C
{
    bool B => Environment.TickCount % 2 == 0;

    void M()
    {
        try
        {
            {|#0:throw new " + throwType + @"();|}
        }
        catch (" + catchDeclaration + @") when (" + filter + @")
        {
            throw;
        }
        catch
        {
        }

        try
        {
            {|#1:throw new " + throwType + @"();|}
        }
        catch (" + catchDeclaration + @") when (" + filter + @")
        {
        }
        catch
        {
            throw;
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", filteredType ?? throwType),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M()", throwType),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchFilterTrue()
        {
            await ExpectTrueAsync("true");
        }

        [TestMethod]
        public async Task TryCatchFilterFalse()
        {
            await ExpectFalseAsync("false");
        }

        [TestMethod]
        public async Task TryCatchFilterMaybe()
        {
            await ExpectMaybeAsync("B");

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not RankException");
        }

        [TestMethod]
        public async Task TryCatchFilterParenthesized()
        {
            await ExpectTrueAsync("(true)");
            await ExpectFalseAsync("(false)");
            await ExpectMaybeAsync("(B)");

            await ExpectTrueAsync("Exception", "Exception ex", "(ex is Exception)");
            await ExpectFalseAsync("Exception", "Exception ex", "(ex is not Exception)");
            await ExpectMaybeAsync("Exception", "Exception ex", "(ex is not RankException)");
        }

        [TestMethod]
        public async Task TryCatchFilterUnaryNot()
        {
            await ExpectTrueAsync("!false");
            await ExpectFalseAsync("!true");
            await ExpectMaybeAsync("!B");

            await ExpectTrueAsync("Exception", "Exception ex", "!(ex is not Exception)");  // !false
            await ExpectFalseAsync("Exception", "Exception ex", "!(ex is Exception)");  // !true
            await ExpectMaybeAsync("Exception", "Exception ex", "!(ex is RankException)");  // !maybe
        }

        [TestMethod]
        public async Task TryCatchFilterLogicalOr()
        {
            await ExpectTrueAsync("true || true");

            await ExpectTrueAsync("true || false");
            await ExpectTrueAsync("false || true");

            await ExpectFalseAsync("false || false");

            await ExpectTrueAsync("true || B");
            await ExpectTrueAsync("B || true");

            await ExpectMaybeAsync("false || B");
            await ExpectMaybeAsync("B || false");

            await ExpectMaybeAsync("B || B");

            await ExpectTrueAsync("Exception", "Exception ex", "ex is Exception || ex is Exception");  // true || true

            await ExpectTrueAsync("Exception", "Exception ex", "ex is Exception || false");  // true || false
            await ExpectTrueAsync("Exception", "Exception ex", "ex is not Exception || ex is Exception");  // false || true

            await ExpectFalseAsync("Exception", "Exception ex", "ex is not Exception || false");  // false || false

            await ExpectTrueAsync("Exception", "Exception ex", "ex is Exception || B");  // true || maybe
            await ExpectTrueAsync("Exception", "Exception ex", "ex is not RankException || ex is Exception");  // maybe || true

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not Exception || B");  // false || maybe
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not RankException || false");  // maybe || false

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not RankException || B");  // maybe || maybe
        }

        [TestMethod]
        public async Task TryCatchFilterLogicalAnd()
        {
            await ExpectTrueAsync("true && true");

            await ExpectFalseAsync("true && false");
            await ExpectFalseAsync("false && true");

            await ExpectFalseAsync("false && false");

            await ExpectMaybeAsync("true && B");
            await ExpectMaybeAsync("B && true");

            await ExpectFalseAsync("false && B");
            await ExpectFalseAsync("B && false");

            await ExpectMaybeAsync("B && B");

            await ExpectTrueAsync("Exception", "Exception ex", "ex is Exception && true");  // true && true

            await ExpectFalseAsync("Exception", "Exception ex", "ex is Exception && ex is not Exception");  // true && false
            await ExpectFalseAsync("Exception", "Exception ex", "ex is not Exception && true");  // false && true

            await ExpectFalseAsync("Exception", "Exception ex", "ex is not Exception && ex is not Exception");  // false && false

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is Exception && B");  // true && maybe
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not RankException && true");  // maybe && true

            await ExpectFalseAsync("Exception", "Exception ex", "ex is not Exception && B");  // false && maybe
            await ExpectFalseAsync("Exception", "Exception ex", "ex is not RankException && ex is not Exception");  // maybe && false

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not RankException && B");  // maybe && maybe
        }

        [TestMethod]
        public async Task TryCatchFilterConditional()
        {
            await ExpectTrueAsync("true ? true : true");
            await ExpectTrueAsync("true ? true : false");
            await ExpectTrueAsync("true ? true : B");

            await ExpectFalseAsync("true ? false : true");
            await ExpectFalseAsync("true ? false : false");
            await ExpectFalseAsync("true ? false : B");

            await ExpectMaybeAsync("true ? B : true");
            await ExpectMaybeAsync("true ? B : false");
            await ExpectMaybeAsync("true ? B : B");

            await ExpectTrueAsync("false ? true : true");
            await ExpectTrueAsync("false ? false : true");
            await ExpectTrueAsync("false ? B : true");

            await ExpectFalseAsync("false ? true : false");
            await ExpectFalseAsync("false ? false : false");
            await ExpectFalseAsync("false ? B : false");

            await ExpectMaybeAsync("false ? true : B");
            await ExpectMaybeAsync("false ? false : B");
            await ExpectMaybeAsync("false ? B : B");

            await ExpectTrueAsync("B ? true : true");
            await ExpectMaybeAsync("B ? false : true");
            await ExpectMaybeAsync("B ? B : true");

            await ExpectMaybeAsync("B ? true : false");
            await ExpectFalseAsync("B ? false : false");
            await ExpectMaybeAsync("B ? B : false");

            await ExpectMaybeAsync("B ? true : B");
            await ExpectMaybeAsync("B ? false : B");
            await ExpectMaybeAsync("B ? B : B");

            await ExpectTrueAsync("Exception", "Exception ex", "ex is Exception ? ex is Exception : true");  // true ? true : true
            await ExpectTrueAsync("Exception", "Exception ex", "ex is Exception ? true : false");  // true ? true : false
            await ExpectTrueAsync("Exception", "Exception ex", "ex is Exception ? true : B");  // true ? true : maybe

            await ExpectFalseAsync("Exception", "Exception ex", "ex is Exception ? false : true");  // true ? false : true
            await ExpectFalseAsync("Exception", "Exception ex", "ex is Exception ? ex is not Exception : false");  // true ? false : false
            await ExpectFalseAsync("Exception", "Exception ex", "ex is Exception ? false : B");  // true ? false : maybe

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is Exception ? B : true");  // true ? maybe : true
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is Exception ? B : false");  // true ? maybe : false
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is Exception ? ex is not RankException : B");  // true ? maybe : maybe

            await ExpectTrueAsync("Exception", "Exception ex", "ex is not Exception ? ex is Exception : true");  // false ? true : true
            await ExpectTrueAsync("Exception", "Exception ex", "ex is not Exception ? false : true");  // false ? false : true
            await ExpectTrueAsync("Exception", "Exception ex", "ex is not Exception ? B : true");  // false ? maybe : true

            await ExpectFalseAsync("Exception", "Exception ex", "ex is not Exception ? true : false");  // false ? true : false
            await ExpectFalseAsync("Exception", "Exception ex", "ex is not Exception ? ex is not Exception : false");  // false ? false : false
            await ExpectFalseAsync("Exception", "Exception ex", "ex is not Exception ? B : false");  // false ? maybe : false

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not Exception ? true : B");  // false ? true : maybe
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not Exception ? false : B");  // false ? false : maybe
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not Exception ? ex is not RankException : B");  // false ? maybe : maybe

            await ExpectTrueAsync("Exception", "Exception ex", "ex is RankException ? ex is Exception : true");  // maybe ? true : true
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is RankException ? false : true");  // maybe ? false : true
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is RankException ? B : true");  // maybe ? maybe : true

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not RankException ? true : false");  // maybe ? true : false
            await ExpectFalseAsync("Exception", "Exception ex", "ex is not RankException ? ex is not Exception : false");  // maybe ? false : false
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not RankException ? B : false");  // maybe ? maybe : false

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is RankException ? true : B");  // maybe ? true : maybe
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is RankException ? false : B");  // maybe ? false : maybe
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is RankException ? ex is not RankException : B");  // maybe ? maybe : maybe
        }

        [TestMethod]
        public async Task TryCatchFilterIsSubtype()
        {
            await ExpectMaybeAsync("ArithmeticException", "Exception ex", "ex is OverflowException", "OverflowException");
        }

        [TestMethod]
        public async Task TryCatchFilterIsSameType()
        {
            await ExpectTrueAsync("ArithmeticException", "Exception ex", "ex is ArithmeticException");
        }

        [TestMethod]
        public async Task TryCatchFilterIsSupertype()
        {
            await ExpectTrueAsync("OverflowException", "Exception ex", "ex is ArithmeticException");
        }

        [TestMethod]
        public async Task TryCatchFilterIsInterface()
        {
            await ExpectMaybeAsync("ArithmeticException", "Exception ex", "ex is ICloneable");
        }

        [TestMethod]
        public async Task TryCatchFilterIsUnrelated()
        {
            await ExpectFalseAsync("InvalidOperationException", "Exception ex", "ex is ArithmeticException");
        }

        [TestMethod]
        public async Task TryCatchFilterIsLeftParenthesized()
        {
            await ExpectTrueAsync("ArithmeticException", "Exception ex", "(ex) is ArithmeticException");
            await ExpectFalseAsync("InvalidOperationException", "Exception ex", "(ex) is ArithmeticException");
            await ExpectMaybeAsync("Exception", "Exception ex", "(ex) is ArithmeticException", "ArithmeticException");
        }

        [TestMethod]
        public async Task TryCatchFilterIsPatternSubtype()
        {
            await ExpectMaybeAsync("ArithmeticException", "Exception ex", "ex is (OverflowException)", "OverflowException");
            await ExpectMaybeAsync("ArithmeticException", "Exception ex", "ex is OverflowException overflowEx", "OverflowException");
        }

        [TestMethod]
        public async Task TryCatchFilterIsPatternSameType()
        {
            await ExpectTrueAsync("ArithmeticException", "Exception ex", "ex is (ArithmeticException)");
            await ExpectTrueAsync("ArithmeticException", "Exception ex", "ex is ArithmeticException arithmeticEx");
        }

        [TestMethod]
        public async Task TryCatchFilterIsPatternSupertype()
        {
            await ExpectTrueAsync("OverflowException", "Exception ex", "ex is (ArithmeticException)");
            await ExpectTrueAsync("OverflowException", "Exception ex", "ex is ArithmeticException arithmeticEx");
        }

        [TestMethod]
        public async Task TryCatchFilterIsPatternInterface()
        {
            await ExpectMaybeAsync("ArithmeticException", "Exception ex", "ex is (ICloneable)");
            await ExpectMaybeAsync("ArithmeticException", "Exception ex", "ex is ICloneable cloneable");
        }

        [TestMethod]
        public async Task TryCatchFilterIsPatternUnrelated()
        {
            await ExpectFalseAsync("InvalidOperationException", "Exception ex", "ex is (ArithmeticException)");
            await ExpectFalseAsync("InvalidOperationException", "Exception ex", "ex is ArithmeticException arithmeticEx");
        }

        [TestMethod]
        public async Task TryCatchFilterIsPatternLeftParenthesized()
        {
            await ExpectTrueAsync("Exception", "Exception ex", "(ex) is (Exception)");
            await ExpectTrueAsync("Exception", "Exception ex", "(ex) is Exception ex2");
            await ExpectFalseAsync("InvalidOperationException", "Exception ex", "(ex) is (ArithmeticException)");
            await ExpectFalseAsync("InvalidOperationException", "Exception ex", "(ex) is ArithmeticException arithmeticEx");
            await ExpectMaybeAsync("Exception", "Exception ex", "(ex) is (ArithmeticException)", "ArithmeticException");
            await ExpectMaybeAsync("Exception", "Exception ex", "(ex) is ArithmeticException arithmeticEx", "ArithmeticException");
        }

        [TestMethod]
        public async Task TryCatchFilterParenthesizedPattern()
        {
            await ExpectTrueAsync("Exception", "Exception ex", "ex is (Exception)");
            await ExpectFalseAsync("Exception", "Exception ex", "ex is not (Exception)");
            await ExpectFalseAsync("Exception", "Exception ex", "ex is (not Exception)");
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is (ArithmeticException)", "ArithmeticException");
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not (ArithmeticException)");
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is (not ArithmeticException)");
        }

        [TestMethod]
        public async Task TryCatchFilterNotPattern()
        {
            await ExpectFalseAsync("Exception", "Exception ex", "ex is not Exception");

            await ExpectTrueAsync("InvalidOperationException", "Exception ex", "ex is not ArithmeticException");

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not ArithmeticException");
        }

        [TestMethod]
        public async Task TryCatchFilterOrPattern()
        {
            await ExpectTrueAsync("Exception", "Exception ex", "ex is Exception or Exception");  // true or true

            await ExpectTrueAsync("Exception", "Exception ex", "ex is Exception or not Exception");  // true or false
            await ExpectTrueAsync("Exception", "Exception ex", "ex is not Exception or Exception");  // false or true

            await ExpectFalseAsync("Exception", "Exception ex", "ex is not Exception or not Exception");  // false or false

            await ExpectTrueAsync("Exception", "Exception ex", "ex is Exception or ArithmeticException");  // true or maybe
            await ExpectTrueAsync("Exception", "Exception ex", "ex is ArithmeticException or Exception");  // maybe or true

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is not Exception or ArithmeticException", "ArithmeticException");  // false or maybe
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is ArithmeticException or not Exception", "ArithmeticException");  // maybe or false

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is ArithmeticException or ArithmeticException", "ArithmeticException");  // maybe or maybe
        }

        [TestMethod]
        public async Task TryCatchFilterAndPattern()
        {
            await ExpectTrueAsync("Exception", "Exception ex", "ex is Exception and Exception");  // true and true

            await ExpectFalseAsync("ArithmeticException", "Exception ex", "ex is Exception and InvalidOperationException");  // true and false
            await ExpectFalseAsync("ArithmeticException", "Exception ex", "ex is InvalidOperationException and Exception");  // false and true

            await ExpectFalseAsync("Exception", "Exception ex", "ex is not Exception and not Exception");  // false and false

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is Exception and IDisposable");  // true and maybe
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is IDisposable and Exception");  // maybe and true

            await ExpectFalseAsync("ArithmeticException", "Exception ex", "ex is InvalidOperationException and IDisposable");  // false and maybe
            await ExpectFalseAsync("ArithmeticException", "Exception ex", "ex is IDisposable and InvalidOperationException");  // maybe and false

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is IDisposable and IDisposable");  // maybe and maybe
        }

        [TestMethod]
        public async Task TryCatchFilterPropertyPattern()
        {
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is { HResult: int hResult }");
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is { HResult: int hResult } ex2");

            await ExpectMaybeAsync("Exception", "Exception ex", "ex is Exception { HResult: int hResult }");
            await ExpectMaybeAsync("Exception", "Exception ex", "ex is Exception { HResult: int hResult } ex2");
        }
    }
}
