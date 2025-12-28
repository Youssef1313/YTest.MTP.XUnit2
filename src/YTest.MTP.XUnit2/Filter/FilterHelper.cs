// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// NOTE: This file is copied as-is from VSTest source code.

using System;
using System.Collections.Generic;
using System.Text;

namespace YTest.MTP.XUnit2.Filter;

internal static class FilterHelper
{
    public const char EscapeCharacter = '\\';
    private static readonly char[] s_specialCharacters = ['\\', '(', ')', '&', '|', '=', '!', '~'];
    private static readonly HashSet<char> s_specialCharactersSet = new(s_specialCharacters);

    /// <summary>
    /// Converts any escaped characters in the input filter string.
    /// </summary>
    /// <param name="str">The input string that contains the text to convert.</param>
    /// <returns>A filter string of characters with any escaped characters converted to their un-escaped form.</returns>
    public static string Unescape(string str)
    {
        if (str is null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        if (str.IndexOf(EscapeCharacter) < 0)
        {
            return str;
        }

        var builder = new StringBuilder();
        for (int i = 0; i < str.Length; ++i)
        {
            var currentChar = str[i];
            if (currentChar == EscapeCharacter)
            {
                if (++i == str.Length || !s_specialCharactersSet.Contains(currentChar = str[i]))
                {
                    // "\" should be followed by a special character.
                    throw new ArgumentException($"Filter string '{str}' includes unrecognized escape sequence.");
                }
            }

            // Strictly speaking, string to be un-escaped shouldn't contain any of the special characters,
            // other than being part of escape sequence, but we will ignore that to avoid additional overhead.

            builder.Append(currentChar);
        }

        return builder.ToString();
    }
}
