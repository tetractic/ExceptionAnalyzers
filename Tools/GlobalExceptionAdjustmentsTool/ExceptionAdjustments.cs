// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal class ExceptionAdjustments
    {
        private const string _filenamePrefix = "ExceptionAdjustments.";

        private const string _filenameSuffix = ".txt";

        public static bool IsFileName(string fileName)
        {
            return fileName.StartsWith(_filenamePrefix, StringComparison.Ordinal) && fileName.EndsWith(_filenameSuffix, StringComparison.Ordinal);
        }
    }
}
