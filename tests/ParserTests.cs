namespace tests;

using dotnet_editorconfig_dedup;

public class EditorConfigParserTests
{
    [Fact]
    public void Parse_SimpleEditorConfig_ParsesCorrectly()
    {
        string tempFile = Path.GetTempFileName().Replace(".tmp", ".editorconfig");
        try
        {
            File.WriteAllText(tempFile, """
                root = true
                
                [*]
                indent_style = space
                
                [*.cs]
                indent_size = 4
                """);

            var file = EditorConfigFile.Parse(tempFile);

            Assert.True(file.IsRoot);
            Assert.Equal(2, file.Sections.Count);
            Assert.Equal("[*]", file.Sections[0].Pattern);
            Assert.Equal("[*.cs]", file.Sections[1].Pattern);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_WithComments_IgnoresComments()
    {
        string tempFile = Path.GetTempFileName().Replace(".tmp", ".editorconfig");
        try
        {
            File.WriteAllText(tempFile, """
                # Comment
                root = true
                ; Semicolon comment
                
                [*]
                indent_style = space
                """);

            var file = EditorConfigFile.Parse(tempFile);

            Assert.True(file.IsRoot);
            Assert.Single(file.Sections);
            Assert.Single(file.Sections[0].Properties);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_WithDuplicateKeys_BothAreStored()
    {
        string tempFile = Path.GetTempFileName().Replace(".tmp", ".editorconfig");
        try
        {
            File.WriteAllText(tempFile, """
                [*]
                indent_style = tab
                indent_style = space
                """);

            var file = EditorConfigFile.Parse(tempFile);

            Assert.Single(file.Sections);
            Assert.Equal(2, file.Sections[0].Properties.Count);
            Assert.Equal("tab", file.Sections[0].Properties[0].Value);
            Assert.Equal("space", file.Sections[0].Properties[1].Value);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_MultiplePatterns_ParsesAllSections()
    {
        string tempFile = Path.GetTempFileName().Replace(".tmp", ".editorconfig");
        try
        {
            File.WriteAllText(tempFile, """
                [*]
                prop = val1
                
                [*.cs]
                prop = val2
                
                [*.{js,ts}]
                prop = val3
                """);

            var file = EditorConfigFile.Parse(tempFile);

            Assert.Equal(3, file.Sections.Count);
            Assert.Equal("[*]", file.Sections[0].Pattern);
            Assert.Equal("[*.cs]", file.Sections[1].Pattern);
            Assert.Equal("[*.{js,ts}]", file.Sections[2].Pattern);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

public class PatternMatcherTests
{
    [Fact]
    public void IsScopeBroader_StarVsSpecific_StarIsBroader()
    {
        Assert.True(PatternMatcher.IsScopeBroader("*", "*.cs"));
        Assert.False(PatternMatcher.IsScopeBroader("*.cs", "*"));
    }

    [Fact]
    public void IsScopeBroader_MoreWildcardsVsFewerWildcards_MoreIsBroader()
    {
        Assert.True(PatternMatcher.IsScopeBroader("*.{js,ts}", "*.js"));
        Assert.False(PatternMatcher.IsScopeBroader("*.js", "*.{js,ts}"));
    }

    [Fact]
    public void IsScopeBroader_SamePattern_ReturnsFalse()
    {
        Assert.False(PatternMatcher.IsScopeBroader("*.cs", "*.cs"));
    }

    [Fact]
    public void IsScopeBroader_SameWildcardCount_LongerIsBroader()
    {
        Assert.True(PatternMatcher.IsScopeBroader("[src/**/*.cs]", "[*.cs]"));
        Assert.False(PatternMatcher.IsScopeBroader("[*.cs]", "[src/**/*.cs]"));
    }

    [Fact]
    public void PatternMatches_SimpleGlob_MatchesCorrectly()
    {
        Assert.True(PatternMatcher.PatternMatches("*.cs", "file.cs"));
        Assert.False(PatternMatcher.PatternMatches("*.cs", "file.js"));
    }

    [Fact]
    public void PatternMatches_StarPattern_MatchesAll()
    {
        Assert.True(PatternMatcher.PatternMatches("*", "file.cs"));
        Assert.True(PatternMatcher.PatternMatches("*", "file.js"));
        Assert.True(PatternMatcher.PatternMatches("*", "anything"));
    }

    [Fact]
    public void PatternMatches_QuestionMark_MatchesSingleChar()
    {
        Assert.True(PatternMatcher.PatternMatches("fil?.cs", "file.cs"));
        Assert.False(PatternMatcher.PatternMatches("fil?.cs", "files.cs"));
    }
}
