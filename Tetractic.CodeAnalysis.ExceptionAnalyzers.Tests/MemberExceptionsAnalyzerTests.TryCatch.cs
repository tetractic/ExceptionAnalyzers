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
        public async Task TryCatchThrowSubtype()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            throw new NotSupportedException();
        }
        catch (Exception)
        {
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task TryCatchThrowSameType()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            throw new Exception();
        }
        catch (Exception)
        {
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task TryCatchThrowSupertype()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            {|#0:throw new Exception();|}
        }
        catch (NotSupportedException)
        {
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchRethrow()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            throw new NotSupportedException();
        }
        catch (NotSupportedException)
        {
            {|#0:throw;|}
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchFiltered()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            {|#0:throw new NotSupportedException();|}
        }
        catch (NotSupportedException ex)
            when (ex != null)
        {
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchFilteredMatchingIsPattern()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            throw new NotSupportedException();
        }
        catch (Exception ex)
            when (ex is NotSupportedException)
        {
        }

        try
        {
            throw new NotImplementedException();
        }
        catch (Exception ex)
            when (ex is NotImplementedException ||
                  ex is NotSupportedException)
        {
        }

        try
        {
            throw new NotSupportedException();
        }
        catch (Exception ex)
            when (ex is NotImplementedException ||
                  ex is NotSupportedException)
        {
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task TryCatchFilteredMatchingIsPatternRethrow()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            throw new NotSupportedException();
        }
        catch (Exception ex)
            when (ex is NotSupportedException)
        {
            {|#0:throw;|}
        }

        try
        {
            throw new NotImplementedException();
        }
        catch (Exception ex)
            when (ex is NotImplementedException ||
                  ex is NotSupportedException)
        {
            {|#1:throw;|}
        }

        try
        {
            throw new NotSupportedException();
        }
        catch (Exception ex)
            when (ex is NotImplementedException ||
                  ex is NotSupportedException)
        {
            {|#2:throw;|}
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M()", "NotImplementedException, NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M()", "NotImplementedException, NotSupportedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchFilteredNonmatchingIsPattern()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            {|#0:throw new Exception();|}
        }
        catch (Exception ex)
            when (ex is NotSupportedException)
        {
        }

        try
        {
            {|#1:throw new NotImplementedException();|}
        }
        catch (Exception ex)
            when (ex is NotSupportedException)
        {
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M()", "NotImplementedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchFilteredPartialIsPattern()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            throw new NotSupportedException();
        }
        catch (Exception ex)
            when (ex is NotSupportedException ||
                  ex.HResult != 0)
        {
        }

        try
        {
            throw new NotSupportedException();
        }
        catch (Exception ex)
            when (ex is NotSupportedException ||
                  (ex is NotImplementedException && ex.HResult != 0))
        {
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task TryCatchFilteredPartialIsPatternRethrow()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            throw new NotSupportedException();
        }
        catch (Exception ex)
            when (ex is NotSupportedException ||
                  ex.HResult != 0)
        {
            {|#0:throw;|}
        }

        try
        {
            throw new NotSupportedException();
        }
        catch (Exception ex)
            when (ex is NotSupportedException ||
                  (ex is NotImplementedException && ex.HResult != 0))
        {
            {|#1:throw;|}
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException, Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M()", "NotSupportedException, Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchFilteredInvalidIsPattern()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            {|#0:throw new NotSupportedException();|}
        }
        catch (NotImplementedException ex)
            when (ex is NotSupportedException)
        {
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task ThrowUnrelatedInCatch()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            throw new NotSupportedException();
        }
        catch (NotSupportedException)
        {
            {|#0:throw new NotImplementedException();|}
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotImplementedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryGeneralCatch()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            throw new Exception();
        }
        catch
        {
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task TryGeneralCatchRethrow()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            throw new NotSupportedException();
        }
        catch
        {
            {|#0:throw;|}
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryGeneralCatchFiltered()
        {
            var source = @"
using System;

class C
{
    public bool B => Environment.TickCount % 2 == 0;

    public void M()
    {
        try
        {
            {|#0:throw new NotSupportedException();|}
        }
        catch when (B || false)
        {
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryGeneralCatchFilteredRethrow()
        {
            var source = @"
using System;

class C
{
    public bool B => Environment.TickCount % 2 == 0;

    public void M()
    {
        try
        {
            {|#0:throw new NotSupportedException();|}
        }
        catch when (B || false)
        {
            throw;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryGeneralCatchFilteredTruePattern()
        {
            var source = @"
using System;

class C
{
    public bool B => Environment.TickCount % 2 == 0;

    public void M()
    {
        try
        {
            throw new NotSupportedException();
        }
        catch when (B || true)
        {
        }

        try
        {
            throw new NotSupportedException();
        }
        catch when (true || B)
        {
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task TryGeneralCatchFilteredTruePatternRethrow()
        {
            var source = @"
using System;

class C
{
    public bool B => Environment.TickCount % 2 == 0;

    public void M()
    {
        try
        {
            throw new NotSupportedException();
        }
        catch when (B || true)
        {
            {|#0:throw;|}
        }

        try
        {
            throw new NotSupportedException();
        }
        catch when (true || B)
        {
            {|#1:throw;|}
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M()", "NotSupportedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryNoCatch()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            {|#0:throw new Exception();|}
        }
        finally
        {
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "Exception");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchLocalFunction()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            _ = L();
        }
        catch (Exception)
        {
        }

        int L()
        {
            throw new Exception();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }
    }
}
