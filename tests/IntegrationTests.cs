namespace tests;

using dotnet_editorconfig_dedup;

public class IntegrationTests
{
    [Fact]
    public async Task EndToEnd_WhatIfMode_DoesNotModifyFiles()
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
            
            await Verifier.Verify(new
            {
                FileUnmodified = originalContent == currentContent,
                TotalLinesRemoved = deduplicator.Summary.TotalLinesRemoved
            });
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EndToEnd_ModifyMode_RemovesDuplicateLines()
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

            foreach (var file in files)
            {
                string tempPath = file.FilePath + ".tmp";
                file.WriteToFile(tempPath);
                File.Delete(file.FilePath);
                File.Move(tempPath, file.FilePath);
            }

            string modifiedContent = File.ReadAllText(editorConfig);
            
            await Verifier.Verify(new
            {
                TotalLinesRemoved = deduplicator.Summary.TotalLinesRemoved,
                RemovedFirstIndentStyle = !modifiedContent.Contains("indent_style = space"),
                ContainsSecondIndentStyle = modifiedContent.Contains("indent_style = tab"),
                ContainsIndentSize = modifiedContent.Contains("indent_size = 4")
            });
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EndToEnd_ComplexHierarchy_DeduplicatesCorrectly()
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
            var deduplicator = new Deduplicator();
            deduplicator.AnalyzeHierarchy(files);

            var childFile = files.FirstOrDefault(f => f.FilePath.Contains("src"));
            var section = childFile?.Sections.FirstOrDefault(s => s.Pattern == "[*]");
            var indentProp = section?.Properties.FirstOrDefault(p => p.Key == "indent_style");
            
            await Verifier.Verify(new
            {
                TotalFilesFound = files.Count,
                TotalLinesRemoved = deduplicator.Summary.TotalLinesRemoved,
                ChildFileFound = childFile != null,
                SectionFound = section != null,
                PropertyFound = indentProp != null,
                PropertyIsRedundant = indentProp?.IsRedundant ?? false
            });
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SummaryGeneration_ProducesSummaryReport()
    {
        var summary = new DeduplicationSummary();
        summary.AddDuplicate("/path/.editorconfig", "[*]", "indent_style", "space");
        summary.AddDuplicate("/path/.editorconfig", "[*.cs]", "charset", "utf-8");
        summary.TotalLinesRemoved = 2;

        string report = summary.GenerateReport();

        await Verifier.Verify(new
        {
            ContainsHeader = report.Contains("Deduplication Summary:"),
            ContainsFilePath = report.Contains("/path/.editorconfig"),
            ContainsFirstPattern = report.Contains("[*]"),
            ContainsSecondPattern = report.Contains("[*.cs]"),
            ContainsFirstProperty = report.Contains("indent_style = space"),
            ContainsSecondProperty = report.Contains("charset = utf-8"),
            ContainsTotal = report.Contains("Total duplicates found: 2")
        });
    }

    [Fact]
    public void SummaryGeneration_NoDuplicates_ReturnsNoMessage()
    {
        var summary = new DeduplicationSummary();
        
        string report = summary.GenerateReport();
        
        Assert.Equal("No duplicates found.", report);
    }
}
