# Exception Analyzers

Exception Analyzers helps you check that exceptions are caught or documented in C#. 

[![NuGet package](https://img.shields.io/nuget/vpre/Tetractic.CodeAnalysis.ExceptionAnalyzers?logo=nuget)](https://www.nuget.org/packages/Tetractic.CodeAnalysis.ExceptionAnalyzers/)

## Getting Started

 1. Steel yourself.
 2. Add a package reference to [Tetractic.CodeAnalysis.ExceptionAnalyzers](https://www.nuget.org/packages/Tetractic.CodeAnalysis.ExceptionAnalyzers/) to your project.

## How It Works

An analyzer examines `throw` statements, `try`/`catch` blocks, and the `<exception>` elements in documentation comment XML for symbols referenced by your code to determine what exception types could escape your code.  Diagnostics are reported for any of those exception types that do not appear in the `<exception>` elements in the documentation comment XML for your code.

### Example

```C#
void Demo1() => throw new NotSupportedException();
//              ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//              Ex0100: 'Demo1()' may throw undocumented exception: NotSupportedException

/// <exception cref="NotSupportedException"/>
void Demo2() => throw new NotSupportedException();  // No diagnostic; exception is documented.

void Demo3() => Demo2();
//              ~~~~~
//              Ex0100: 'Demo3()' may throw undocumented exception: NotSupportedException

void Demo4(bool b)
{
    try
    {
        if (b)
            throw new NotSupportedException();  // No diagnostic; Exception is caught.
        else
            throw new Exception();
        //  ~~~~~~~~~~~~~~~~~~~~~~
        //  Ex0100: 'Demo4(bool)' may throw undocumented exception: Exception
    }
    catch (NotSupportedException)
    {
        throw;
    //  ~~~~~~
    //  Ex0100: 'Demo4(bool)' may throw undocumented exception: NotSupportedException
    }
}
```

## Configuration

### .editorconfig

#### dotnet_ignored_exceptions

Provides a list of exception types that will be ignored by exception analysis.

Setting name: `dotnet_ignored_exceptions`\
Value: A comma-separated list of fully-qualified type names.\
Default value: `System.NullReferenceException, System.StackOverflowException, System.UnreachableException`

#### dotnet_intransitive_exceptions

Provides a list of exception types that will be ignored by exception analysis when not thrown directly.  This is useful for exception types that generally indicate incorrect code when thrown.  The conditions under which these exception types are thrown should be documented, but those conditions should be avoidable and avoided in correctly-written code.

For example, it should be possible to avoid providing unacceptable arguments that would cause an `ArgumentException` to be thrown.  `ArgumentException` and its subtypes, in particular, are so pervasive that reporting a diagnostic for every method invocation that might throw one of them would be unhelpful.

Setting name: `dotnet_intransitive_exceptions`\
Value: A comma-separated list of fully-qualified type names.\
Default value: `System.ArgumentException, System.IndexOutOfRangeException, System.InvalidCastException, System.InvalidOperationException, System.Collections.Generic.KeyNotFoundException`

### Exception Adjustments

TODO

## Limitations

### Documentation XML

Exception Analyzers relies heavily upon `<exception>` elements in documentation XML in determining what exception types may be thrown.  If the documentation XML is missing or inaccurate then the exception analysis will also be incomplete or inaccurate.

Even when documentation XML is provide, it often does not provide enough information for accurate exception analysis.  The `<exception>` element, as described in the C# Specification, does not specify which accessor of a member (ex. the `get` or `set` accessor of a property) may throw the exception.  Exception Analyzers uses an `accessor` attribute for this purpose, but its use is not common.

In your own code, you can resolve these problems by adding or correcting the documentation XML.  For external code, it is possible to use the [exception adjustments](#exception-adjustments) mechanism to add or correct the exception information.

### Delegates (and Lambdas/Anonymous Methods)

Delegates are somewhat problematic for exception analysis.  It is often the case that a delegate must throw an exception type that is not documented on the delegate type.  For example, the general-purpose delegate types `Action` and `Func` are naturally not documented as throwing exceptions, but they are used in places where the delegate is allowed to throw exceptions.

In some cases, exception analysis could be performed using data-flow analysis to track which code will run when a delegate is invoked.  In the general case, it becomes infeasible.

Exception Analyzers does not perform any data-flow analysis.  As long as a delegate only throws the exception types documented on the delegate type, exception analysis will work as expected; otherwise, not.  Exception Analyzers does have analysis rules (`Ex0120` and `Ex0121`) that can check whether a delegate may throw exception types that are not documented on the delegate type, but these rules are off by default, as they are typically more noisy than useful.

### Asynchronous Code

Asynchronous code is somewhat problematic for exception analysis because control flow becomes data (in the form of a `Task`).  Similar to delegates, in some cases, exception analysis of asynchronous code could be performed using data-flow analysis to track which code will run when a `Task` is awaited.  In the general case, it becomes infeasible.

Exception Analyzers does not perform any data-flow analysis.  As long as the `Task` returned from an asynchronous method invocation is awaited immediately, exception analysis will work as expected; otherwise, not.

### Implicit Conversion Operators

Exception Analyzers does not currently consider implicit conversion operator invocations during analysis.

### Implicit Method Invocations

Some C# syntax implicitly invokes methods.  For example, `using` invokes `Dispose()`.  Exception Analyzers does not currently consider these implicit method invocations during analysis.
