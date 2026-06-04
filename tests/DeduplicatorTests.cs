namespace tests;

using dotnet_editorconfig_dedup;
using System.IO.Abstractions.TestingHelpers;

public class DeduplicatorTests
{
    [Fact]
    public async Task DeduplicateSingleFile_DuplicateKeysInSameSection_MarkFirstAsRedundant()
    {
        MockFileSystem fileSystem = new();
        string editorConfigPath = "/repo/.editorconfig";
        fileSystem.AddFile(editorConfigPath, new MockFileData("""
            [*]
            indent_style = tab
            indent_style = space
            """));

        var file = EditorConfigFile.Parse(editorConfigPath, fileSystem);
        var deduplicator = new Deduplicator(fileSystem);

        deduplicator.AnalyzeHierarchy(new List<EditorConfigFile> { file });

        var section = file.Sections[0];
        var properties = section.Properties.OrderBy(p => p.LineNumber).ToList();
        
        await Verifier.Verify(new
        {
            FirstPropertyIsRedundant = properties[0].IsRedundant,
            SecondPropertyIsRedundant = properties[1].IsRedundant,
            TotalLinesRemoved = deduplicator.Summary.TotalLinesRemoved
        });
    }

    [Fact]
    public async Task DeduplicateHierarchy_BroaderPatternInParent_MarksChildAsRedundant()
    {
        MockFileSystem fileSystem = new();
        string parentFilePath = "/repo/.editorconfig";
        string childFilePath = "/repo/subdir/.editorconfig";
        fileSystem.AddFile(parentFilePath, new MockFileData("""
            [*]
            indent_style = space
            """));
        fileSystem.AddFile(childFilePath, new MockFileData("""
            [*]
            indent_style = space
            """));

        var parent = EditorConfigFile.Parse(parentFilePath, fileSystem);
        var child = EditorConfigFile.Parse(childFilePath, fileSystem);

        var deduplicator = new Deduplicator(fileSystem);
        deduplicator.AnalyzeHierarchy(new List<EditorConfigFile> { parent, child });

        var childProps = child.Sections[0].Properties.ToList();
        
        await Verify(new
        {
            ChildPropertyIsRedundant = childProps[0].IsRedundant,
            TotalLinesRemoved = deduplicator.Summary.TotalLinesRemoved
        });
    }

    [Fact]
    public async Task DeduplicateHierarchy_MoreSpecificPatternInChild_NotMarkedAsRedundant()
    {
        MockFileSystem fileSystem = new();
        string parentFilePath = "/repo/.editorconfig";
        string childFilePath = "/repo/subdir/.editorconfig";
        fileSystem.AddFile(parentFilePath, new MockFileData("""
            [*]
            indent_style = space
            """));
        fileSystem.AddFile(childFilePath, new MockFileData("""
            [*.js]
            indent_style = tab
            """));

        var parent = EditorConfigFile.Parse(parentFilePath, fileSystem);
        var child = EditorConfigFile.Parse(childFilePath, fileSystem);

        var deduplicator = new Deduplicator(fileSystem);
        deduplicator.AnalyzeHierarchy(new List<EditorConfigFile> { parent, child });

        var childProps = child.Sections[0].Properties.ToList();
        
        await Verify(new
        {
            ChildPropertyIsRedundant = childProps[0].IsRedundant,
            TotalLinesRemoved = deduplicator.Summary.TotalLinesRemoved
        });
    }

    [Fact]
    public async Task DeduplicateSingleFile_DuplicateKeyInBroaderSection_MarksNarrowerAsRedundant()
    {
        MockFileSystem fileSystem = new();
        string editorConfigPath = "/repo/.editorconfig";
        fileSystem.AddFile(editorConfigPath, new MockFileData("""
            [*]
            indent_style = space

            [*.cs]
            indent_style = space
            """));

        var file = EditorConfigFile.Parse(editorConfigPath, fileSystem);
        var deduplicator = new Deduplicator(fileSystem);

        deduplicator.AnalyzeHierarchy(new List<EditorConfigFile> { file });

        var broaderProp = file.Sections[0].Properties[0];
        var narrowerProp = file.Sections[1].Properties[0];

        await Verifier.Verify(new
        {
            BroaderSectionPropertyIsRedundant = broaderProp.IsRedundant,
            NarrowerSectionPropertyIsRedundant = narrowerProp.IsRedundant,
            TotalLinesRemoved = deduplicator.Summary.TotalLinesRemoved
        });
    }

    [Fact]
    public async Task DeduplicateSingleFile_LaterBroaderSection_DoesNotRemoveEarlierSpecificSection()
    {
        MockFileSystem fileSystem = new();
        string editorConfigPath = "/repo/.editorconfig";
        fileSystem.AddFile(editorConfigPath, new MockFileData("""
            [*.cs]
            indent_size = 4
            tab_width = 4

            [*.{cs,ts}]
            indent_size = 4
            tab_width = 4
            """));

        var file = EditorConfigFile.Parse(editorConfigPath, fileSystem);
        var deduplicator = new Deduplicator(fileSystem);

        deduplicator.AnalyzeHierarchy(new List<EditorConfigFile> { file });

        var earlierSection = file.Sections[0];
        var laterSection = file.Sections[1];

        await Verifier.Verify(new
        {
            EarlierIndentSizeRedundant = earlierSection.Properties[0].IsRedundant,
            EarlierTabWidthRedundant = earlierSection.Properties[1].IsRedundant,
            LaterIndentSizeRedundant = laterSection.Properties[0].IsRedundant,
            LaterTabWidthRedundant = laterSection.Properties[1].IsRedundant,
            TotalLinesRemoved = deduplicator.Summary.TotalLinesRemoved
        });
    }

    [Fact]
    public void FindAllEditorConfigFiles_RecursiveSearch_FindsAllFiles()
    {
        MockFileSystem fileSystem = new();
        fileSystem.AddFile("/repo/.editorconfig", new MockFileData("[*]\nindent=2"));
        fileSystem.AddFile("/repo/sub1/.editorconfig", new MockFileData("[*]\nindent=4"));
        fileSystem.AddFile("/repo/sub2/.editorconfig", new MockFileData("[*]\nindent=8"));

        var files = Deduplicator.FindAllEditorConfigFiles("/repo", fileSystem);

        Assert.Equal(3, files.Count);
    }

    [Fact]
    public void FindAllEditorConfigFiles_NoFilesFound_ReturnsEmpty()
    {
        MockFileSystem fileSystem = new();
        fileSystem.AddDirectory("/repo");

        var files = Deduplicator.FindAllEditorConfigFiles("/repo", fileSystem);

        Assert.Empty(files);
    }

    [Fact]
    public void AnalyzeHierarchy_EmptyList_DoesNotThrow()
    {
        var deduplicator = new Deduplicator();
        deduplicator.AnalyzeHierarchy(new List<EditorConfigFile>());
        
        Assert.Equal(0, deduplicator.Summary.TotalLinesRemoved);
    }
}
