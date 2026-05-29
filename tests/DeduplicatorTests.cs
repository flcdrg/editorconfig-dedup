namespace tests;

using dotnet_editorconfig_dedup;

public class DeduplicatorTests
{
    [Fact]
    public void DeduplicateSingleFile_DuplicateKeysInSameSection_MarkFirstAsRedundant()
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
            var deduplicator = new Deduplicator();

            deduplicator.AnalyzeHierarchy(new List<EditorConfigFile> { file });

            var section = file.Sections[0];
            var properties = section.Properties.OrderBy(p => p.LineNumber).ToList();
            Assert.True(properties[0].IsRedundant);
            Assert.False(properties[1].IsRedundant);
            Assert.Equal(1, deduplicator.Summary.TotalLinesRemoved);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void DeduplicateHierarchy_BroaderPatternInParent_MarksChildAsRedundant()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            
            string parentFile = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(parentFile, """
                [*]
                indent_style = space
                """);

            string childDir = Path.Combine(tempDir, "subdir");
            Directory.CreateDirectory(childDir);
            string childFile = Path.Combine(childDir, ".editorconfig");
            File.WriteAllText(childFile, """
                [*]
                indent_style = space
                """);

            var parent = EditorConfigFile.Parse(parentFile);
            var child = EditorConfigFile.Parse(childFile);

            var deduplicator = new Deduplicator();
            deduplicator.AnalyzeHierarchy(new List<EditorConfigFile> { parent, child });

            var childProps = child.Sections[0].Properties.ToList();
            Assert.True(childProps[0].IsRedundant);
            Assert.Equal(1, deduplicator.Summary.TotalLinesRemoved);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void DeduplicateHierarchy_MoreSpecificPatternInChild_NotMarkedAsRedundant()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            
            string parentFile = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(parentFile, """
                [*]
                indent_style = space
                """);

            string childDir = Path.Combine(tempDir, "subdir");
            Directory.CreateDirectory(childDir);
            string childFile = Path.Combine(childDir, ".editorconfig");
            File.WriteAllText(childFile, """
                [*.js]
                indent_style = tab
                """);

            var parent = EditorConfigFile.Parse(parentFile);
            var child = EditorConfigFile.Parse(childFile);

            var deduplicator = new Deduplicator();
            deduplicator.AnalyzeHierarchy(new List<EditorConfigFile> { parent, child });

            var childProps = child.Sections[0].Properties.ToList();
            Assert.False(childProps[0].IsRedundant);
            Assert.Equal(0, deduplicator.Summary.TotalLinesRemoved);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FindAllEditorConfigFiles_RecursiveSearch_FindsAllFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, ".editorconfig"), "[*]\nindent=2");
            
            string subdir1 = Path.Combine(tempDir, "sub1");
            Directory.CreateDirectory(subdir1);
            File.WriteAllText(Path.Combine(subdir1, ".editorconfig"), "[*]\nindent=4");
            
            string subdir2 = Path.Combine(tempDir, "sub2");
            Directory.CreateDirectory(subdir2);
            File.WriteAllText(Path.Combine(subdir2, ".editorconfig"), "[*]\nindent=8");

            var files = Deduplicator.FindAllEditorConfigFiles(tempDir);

            Assert.Equal(3, files.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FindAllEditorConfigFiles_NoFilesFound_ReturnsEmpty()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            
            var files = Deduplicator.FindAllEditorConfigFiles(tempDir);

            Assert.Empty(files);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void AnalyzeHierarchy_EmptyList_DoesNotThrow()
    {
        var deduplicator = new Deduplicator();
        deduplicator.AnalyzeHierarchy(new List<EditorConfigFile>());
        
        Assert.Equal(0, deduplicator.Summary.TotalLinesRemoved);
    }
}
