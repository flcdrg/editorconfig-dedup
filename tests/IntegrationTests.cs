namespace tests;

using dotnet_editorconfig_dedup;

public class IntegrationTests
{
    [Fact]
    public void EndToEnd_WhatIfMode_DoesNotModifyFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            
            string editorConfig = Path.Combine(tempDir, ".editorconfig");
            string originalContent = """
                [*]
                indent_style = space
                indent_style = tab
                """;
            File.WriteAllText(editorConfig, originalContent);

            var files = Deduplicator.FindAllEditorConfigFiles(tempDir);
            var deduplicator = new Deduplicator();
            deduplicator.AnalyzeHierarchy(files);

            string currentContent = File.ReadAllText(editorConfig);
            Assert.Equal(originalContent, currentContent);
            Assert.Equal(1, deduplicator.Summary.TotalLinesRemoved);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void EndToEnd_ModifyMode_RemovesDuplicateLines()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            
            string editorConfig = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfig, """
                [*]
                indent_style = space
                indent_style = tab
                indent_size = 4
                """);

            var files = Deduplicator.FindAllEditorConfigFiles(tempDir);
            var deduplicator = new Deduplicator();
            deduplicator.AnalyzeHierarchy(files);

            Assert.Equal(1, deduplicator.Summary.TotalLinesRemoved);

            foreach (var file in files)
            {
                string tempPath = file.FilePath + ".tmp";
                file.WriteToFile(tempPath);
                File.Delete(file.FilePath);
                File.Move(tempPath, file.FilePath);
            }

            string modifiedContent = File.ReadAllText(editorConfig);
            Assert.DoesNotContain("indent_style = space", modifiedContent);
            Assert.Contains("indent_style = tab", modifiedContent);
            Assert.Contains("indent_size = 4", modifiedContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void EndToEnd_ComplexHierarchy_DeduplicatesCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            
            string parentEditorConfig = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(parentEditorConfig, """
                [*]
                indent_style = space
                end_of_line = lf
                
                [*.cs]
                charset = utf-8
                """);

            string childDir = Path.Combine(tempDir, "src");
            Directory.CreateDirectory(childDir);
            string childEditorConfig = Path.Combine(childDir, ".editorconfig");
            File.WriteAllText(childEditorConfig, """
                [*]
                indent_style = space
                
                [*.cs]
                indent_size = 4
                """);

            var files = Deduplicator.FindAllEditorConfigFiles(tempDir);
            Assert.Equal(2, files.Count);

            var deduplicator = new Deduplicator();
            deduplicator.AnalyzeHierarchy(files);

            Assert.Equal(1, deduplicator.Summary.TotalLinesRemoved);

            var childFile = files.FirstOrDefault(f => f.FilePath.Contains("src"));
            Assert.NotNull(childFile);
            var section = childFile!.Sections.FirstOrDefault(s => s.Pattern == "[*]");
            Assert.NotNull(section);
            var indentProp = section!.Properties.FirstOrDefault(p => p.Key == "indent_style");
            Assert.NotNull(indentProp);
            Assert.True(indentProp!.IsRedundant);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SummaryGeneration_ProducesSummaryReport()
    {
        var summary = new DeduplicationSummary();
        summary.AddDuplicate("/path/.editorconfig", "[*]", "indent_style", "space");
        summary.AddDuplicate("/path/.editorconfig", "[*.cs]", "charset", "utf-8");
        summary.TotalLinesRemoved = 2;

        string report = summary.GenerateReport();

        Assert.Contains("Deduplication Summary:", report);
        Assert.Contains("/path/.editorconfig", report);
        Assert.Contains("[*]", report);
        Assert.Contains("[*.cs]", report);
        Assert.Contains("indent_style = space", report);
        Assert.Contains("charset = utf-8", report);
        Assert.Contains("Total duplicates found: 2", report);
    }

    [Fact]
    public void SummaryGeneration_NoDuplicates_ReturnsNoMessage()
    {
        var summary = new DeduplicationSummary();
        
        string report = summary.GenerateReport();
        
        Assert.Equal("No duplicates found.", report);
    }
}
