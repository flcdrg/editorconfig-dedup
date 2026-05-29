using dotnet_editorconfig_dedup;

var rootPath = ".";
var whatIf = false;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] is "-r" or "--root" && i + 1 < args.Length)
    {
        rootPath = args[++i];
    }
    else if (args[i] is "-w" or "--what-if")
    {
        whatIf = true;
    }
    else if (args[i] is "-h" or "--help")
    {
        PrintHelp();
        return 0;
    }
}

try
{
    var files = Deduplicator.FindAllEditorConfigFiles(rootPath);

    if (files.Count == 0)
    {
        Console.WriteLine("No .editorconfig files found.");
        return 0;
    }

    Console.WriteLine($"Found {files.Count} .editorconfig file(s)");

    var deduplicator = new Deduplicator();
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
            return 0;
        }

        foreach (var file in files)
        {
            string tempPath = file.FilePath + ".tmp";
            file.WriteToFile(tempPath);
            File.Delete(file.FilePath);
            File.Move(tempPath, file.FilePath);
        }

        Console.WriteLine($"✓ Removed {deduplicator.Summary.TotalLinesRemoved} duplicate line(s)");
        Console.WriteLine("\n" + deduplicator.Summary.GenerateReport());
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("""
        dotnet-editorconfig-dedup - Remove duplicate settings from .editorconfig files
        
        Usage:
          dotnet-editorconfig-dedup [options]
        
        Options:
          -r, --root <path>    Root path to start searching for .editorconfig files (default: current directory)
          -w, --what-if        Show what would be removed without modifying files
          -h, --help           Show this help message
        """);
}
