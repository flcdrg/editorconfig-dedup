namespace dotnet_editorconfig_dedup;

public class PatternMatcher
{
    /// <summary>
    /// Determines if pattern1 is broader/more general than pattern2.
    /// More general patterns should be kept, more specific should be removed.
    /// Examples: [*] is broader than [*.cs], [*.cs] is broader than [*.{cs,vb}]
    /// </summary>
    public static bool IsScopeBroader(string pattern1, string pattern2)
    {
        if (pattern1 == pattern2)
            return false;

        string bare1 = pattern1.TrimStart('[').TrimEnd(']');
        string bare2 = pattern2.TrimStart('[').TrimEnd(']');

        if (bare1 == bare2)
            return false;

        if (bare1 == "*")
            return true;
        if (bare2 == "*")
            return false;

        int wildcards1 = CountWildcards(bare1);
        int wildcards2 = CountWildcards(bare2);

        if (wildcards1 != wildcards2)
            return wildcards1 > wildcards2;

        int braces1 = CountBraces(bare1);
        int braces2 = CountBraces(bare2);

        if (braces1 != braces2)
            return braces1 > braces2;

        return bare1.Length > bare2.Length;
    }

    /// <summary>
    /// Count wildcards (* and ?) which make a pattern more general.
    /// </summary>
    private static int CountWildcards(string pattern)
    {
        int count = 0;
        foreach (char c in pattern)
        {
            if (c == '*' || c == '?')
                count++;
        }
        return count;
    }

    /// <summary>
    /// Count brace alternations {a,b} which make a pattern less specific than wildcards.
    /// </summary>
    private static int CountBraces(string pattern)
    {
        int count = 0;
        foreach (char c in pattern)
        {
            if (c == '{' || c == '}')
                count++;
        }
        return count;
    }

    /// <summary>
    /// Matches a file path against an editorconfig pattern.
    /// Simplified implementation for common patterns.
    /// </summary>
    public static bool PatternMatches(string pattern, string filePath)
    {
        if (pattern == "*")
            return true;

        string normalizedPath = filePath.Replace("\\", "/");
        pattern = pattern.Replace("\\", "/");

        return SimpleGlobMatch(pattern, normalizedPath);
    }

    private static bool SimpleGlobMatch(string pattern, string text)
    {
        return SimpleGlobMatchRecursive(pattern, 0, text, 0);
    }

    private static bool SimpleGlobMatchRecursive(string pattern, int pIdx, string text, int tIdx)
    {
        if (pIdx == pattern.Length)
            return tIdx == text.Length;

        if (tIdx == text.Length)
            return pIdx == pattern.Length - 1 && pattern[pIdx] == '*';

        char pChar = pattern[pIdx];

        if (pChar == '*')
        {
            for (int i = tIdx; i <= text.Length; i++)
            {
                if (SimpleGlobMatchRecursive(pattern, pIdx + 1, text, i))
                    return true;
            }
            return false;
        }

        if (pChar == '?')
            return SimpleGlobMatchRecursive(pattern, pIdx + 1, text, tIdx + 1);

        if (pChar == text[tIdx])
            return SimpleGlobMatchRecursive(pattern, pIdx + 1, text, tIdx + 1);

        return false;
    }
}
