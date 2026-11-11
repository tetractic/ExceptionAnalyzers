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
            {|#0:throw new NotSupportedException();|}
        }
        catch (NotSupportedException)
        {
            throw;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryFilteredCatch()
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
        catch (NotSupportedException ex) when (B)
        {
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryFilteredCatchRethrow()
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
        catch (NotSupportedException ex) when (B)
        {
            throw;
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
            {|#0:throw new NotSupportedException();|}
        }
        catch
        {
            throw;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryGeneralFilteredCatch()
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
        catch when (B)
        {
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryGeneralFilteredCatchRethrow()
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
        catch when (B)
        {
            throw;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
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

        [TestMethod]
        public async Task TryCatchRethrowInTryCatch()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            try
            {
                {|#0:throw new NotSupportedException();|}
            }
            catch (NotSupportedException)
            {
                throw;
            }
        }
        catch (NotSupportedException)
        {
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task TryCatchRethrowInTryCatchRethrow()
        {
            var source = @"
using System;

class C
{
    public void M()
    {
        try
        {
            try
            {
                {|#0:throw new NotSupportedException();|}
            }
            catch (NotSupportedException)
            {
                throw;
            }
        }
        catch (NotSupportedException)
        {
            throw;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryFilteredCatchRethrowInTryCatchRethrow()
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
            try
            {
                {|#0:throw new NotSupportedException();|}
            }
            catch (NotSupportedException) when (B)
            {
                throw;
            }
        }
        catch (NotSupportedException)
        {
            throw;
        }
    }
}";

            // TODO:
            // It is unintuitive that there are two diagnostics at the same location.  One comes
            // from the path where the the exception type passes the filter, so the exception type
            // is caught but then it is rethrown and escapes to the outer "try", and the other comes
            // from the path where the the exception type does not pass the filter, so the exception
            // type escapes directly to the outer "try".  When this happens, the diagnostics are not
            // necessarily identical; it is possible for the exception types to be different.
            // Ideally there would be one diagnostic per location.  Or perhaps the rethrow location
            // should be added to the diagnostic as an additional location.
            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchRethrowInTryFilteredCatchRethrow()
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
            try
            {
                {|#0:throw new NotSupportedException();|}
            }
            catch (NotSupportedException)
            {
                throw;
            }
        }
        catch (NotSupportedException) when (B)
        {
            throw;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchRethrownInCatch()
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
            try
            {
                throw;
            }
            catch (NotSupportedException)
            {
            }
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task TryCatchRethrownInCatchRethrow()
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
        catch (NotSupportedException)
        {
            try
            {
                throw;
            }
            catch (NotSupportedException)
            {
                throw;
            }
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchRethrownInFilteredCatchRethrow()
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
        catch (NotSupportedException) when (B)
        {
            try
            {
                throw;
            }
            catch (NotSupportedException)
            {
                throw;
            }
        }
    }
}";

            // See comment above about two diagnostics at the same location.
            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryFilteredCatchRethrownInCatchRethrow()
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
        catch (NotSupportedException)
        {
            try
            {
                throw;
            }
            catch (NotSupportedException) when (B)
            {
                throw;
            }
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryFilteredCatchRethrowNarrow()
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
        catch (Exception ex) when (ex is ArithmeticException or NotSupportedException)
        {
            throw;
        }
        catch
        {
        }
    }
}";

            var expected = VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "ArithmeticException, NotSupportedException");
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchConsume()
        {
            var source = @"
using System;

class C
{
    /// <exception cref=""OverflowException""></exception>
    /// <exception cref=""NotSupportedException""></exception>
    public void Throw()
    {
    }

    public void M()
    {
        try
        {
            {|#0:Throw|}();
        }
        catch (OverflowException)
        {
        }

        try
        {
            {|#1:Throw|}();
        }
        catch (OverflowException)
        {
        }
        catch (ArithmeticException)
        {
            throw;
        }

        try
        {
            {|#2:Throw|}();
        }
        catch (ArithmeticException ex) when (ex is OverflowException)
        {
        }

        try
        {
            {|#3:Throw|}();
        }
        catch (ArithmeticException ex) when (ex is OverflowException)
        {
        }
        catch (OverflowException)
        {
            throw;
        }
        catch (ArithmeticException)
        {
            throw;
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M()", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M()", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M()", "NotSupportedException"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M()", "NotSupportedException"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryCatchTypeArgument()
        {
            var source = @"
using System;

class C
{
    public void M<T>() where T : Exception
    {
        try
        {
            {|#0:throw new Exception();|}
        }
        catch (T ex)
        {
        }

        try
        {
            {|#1:throw new Exception();|}
        }
        catch (T ex)
        {
            throw;
        }
        catch
        {
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M<T>()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M<T>()", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task TryFilteredCatchTypeArgument()
        {
            var source = @"
using System;

class C
{
    public void M<T>() where T : Exception
    {
        try
        {
            {|#0:throw new Exception();|}
        }
        catch (T ex) when (ex is Exception)
        {
        }

        try
        {
            {|#1:throw new Exception();|}
        }
        catch (T ex) when (ex is ArithmeticException)
        {
        }

        try
        {
            {|#2:throw new Exception();|}
        }
        catch (T ex) when (ex is ArithmeticException)
        {
            throw;
        }
        catch
        {
        }

        try
        {
            {|#3:throw new Exception();|}
        }
        catch (T ex) when (ex is (ArithmeticException))
        {
        }

        try
        {
            {|#4:throw new Exception();|}
        }
        catch (T ex) when (ex is (ArithmeticException))
        {
            throw;
        }
        catch
        {
        }
    }
}";

            var expected = new[]
            {
                VerifyCS.Diagnostic("Ex0100").WithLocation(0).WithArguments("M<T>()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(1).WithArguments("M<T>()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(2).WithArguments("M<T>()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(3).WithArguments("M<T>()", "Exception"),
                VerifyCS.Diagnostic("Ex0100").WithLocation(4).WithArguments("M<T>()", "Exception"),
            };
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }
    }
}
