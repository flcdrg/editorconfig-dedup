using System.CommandLine;
using System.CommandLine.Parsing;

using dotnet_editorconfig_dedup;
using System.IO.Abstractions;

var rootOption = new Option<string?>("--root", "-r");
var whatIfOption = new Option<bool>("--what-if", "-w");
IFileSystem fileSystem = new FileSystem();

var rootCommand = new RootCommand("Remove duplicate settings from .editorconfig files")
{
    rootOption,
    whatIfOption
};

rootCommand.SetAction(parseResult =>
{
    try
    {
        string? root = parseResult.GetValue(rootOption);
        bool whatIf = parseResult.GetValue(whatIfOption);
        
        root ??= ".";

        var files = Deduplicator.FindAllEditorConfigFiles(root, fileSystem);

        if (files.Count == 0)
        {
            Console.WriteLine("No .editorconfig files found.");
            return;
        }

        Console.WriteLine($"Found {files.Count} .editorconfig file(s)");

        var deduplicator = new Deduplicator(fileSystem);
        deduplicator.AnalyzeHierarchy(files);

        if (whatIf)
        {
            Console.WriteLine("\n" + deduplicator.Summary.GenerateReport());
        }
        else
        {
            if (deduplicator.Summary.TotalLinesRemoved == 0)
            {
                Console.WriteLine("No duplicates found.");
                return;
            }

            foreach (var file in files)
            {
                string tempPath = file.FilePath + ".tmp";
                file.WriteToFile(tempPath);
                fileSystem.File.Move(tempPath, file.FilePath, overwrite: true);
            }

            Console.WriteLine($"✓ Removed {deduplicator.Summary.TotalLinesRemoved} duplicate line(s)");
            Console.WriteLine("\n" + deduplicator.Summary.GenerateReport());
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
});

return rootCommand.Parse(args).Invoke();
