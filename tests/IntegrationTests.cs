namespace tests;

using dotnet_editorconfig_dedup;
using System.IO.Abstractions.TestingHelpers;

public class IntegrationTests
{
    [Fact]
    public async Task EndToEnd_WhatIfMode_DoesNotModifyFiles()
    {
        MockFileSystem fileSystem = new();
        string rootPath = "/repo";
        string editorConfigPath = "/repo/.editorconfig";
        string originalContent = """
            [*]
            indent_style = space
            indent_style = tab
            """;
        fileSystem.AddFile(editorConfigPath, new MockFileData(originalContent));

        var files = Deduplicator.FindAllEditorConfigFiles(rootPath, fileSystem);
        var deduplicator = new Deduplicator(fileSystem);
        deduplicator.AnalyzeHierarchy(files);

        string currentContent = fileSystem.File.ReadAllText(editorConfigPath);
        
        await Verifier.Verify(new
        {
            FileUnmodified = originalContent == currentContent,
            TotalLinesRemoved = deduplicator.Summary.TotalLinesRemoved
        });
    }

    [Fact]
    public async Task EndToEnd_ModifyMode_RemovesDuplicateLines()
    {
        MockFileSystem fileSystem = new();
        string rootPath = "/repo";
        string editorConfigPath = "/repo/.editorconfig";
        fileSystem.AddFile(editorConfigPath, new MockFileData("""
            [*]
            indent_style = space
            indent_style = tab
            indent_size = 4
            """));

        var files = Deduplicator.FindAllEditorConfigFiles(rootPath, fileSystem);
        var deduplicator = new Deduplicator(fileSystem);
        deduplicator.AnalyzeHierarchy(files);

        foreach (var file in files)
        {
            string tempPath = file.FilePath + ".tmp";
            file.WriteToFile(tempPath);
            fileSystem.File.Move(tempPath, file.FilePath, overwrite: true);
        }

        string modifiedContent = fileSystem.File.ReadAllText(editorConfigPath);
        
        await Verifier.Verify(new
        {
            TotalLinesRemoved = deduplicator.Summary.TotalLinesRemoved,
            RemovedFirstIndentStyle = !modifiedContent.Contains("indent_style = space"),
            ContainsSecondIndentStyle = modifiedContent.Contains("indent_style = tab"),
            ContainsIndentSize = modifiedContent.Contains("indent_size = 4")
        });
    }

    [Fact]
    public async Task EndToEnd_ComplexHierarchy_DeduplicatesCorrectly()
    {
        MockFileSystem fileSystem = new();
        string rootPath = "/repo";
        string parentEditorConfigPath = "/repo/.editorconfig";
        string childEditorConfigPath = "/repo/src/.editorconfig";
        fileSystem.AddFile(parentEditorConfigPath, new MockFileData("""
            [*]
            indent_style = space
            end_of_line = lf
            
            [*.cs]
            charset = utf-8
            """));
        fileSystem.AddFile(childEditorConfigPath, new MockFileData("""
            [*]
            indent_style = space
            
            [*.cs]
            indent_size = 4
            """));

        var files = Deduplicator.FindAllEditorConfigFiles(rootPath, fileSystem);
        var deduplicator = new Deduplicator(fileSystem);
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
