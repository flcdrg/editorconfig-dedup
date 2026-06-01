using System.IO.Abstractions;

namespace dotnet_editorconfig_dedup;

public class EditorConfigFile
{
    private readonly IFileSystem _fileSystem;

    public string FilePath { get; set; }
    public List<EditorConfigSection> Sections { get; private set; }
    public List<string> RawLines { get; private set; }
    public bool IsRoot { get; set; }

    public EditorConfigFile(string filePath, IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        FilePath = filePath;
        Sections = new List<EditorConfigSection>();
        RawLines = new List<string>();
        IsRoot = false;
    }

    public void AddSection(EditorConfigSection section)
    {
        Sections.Add(section);
    }

    public static EditorConfigFile Parse(string filePath, IFileSystem? fileSystem = null)
    {
        IFileSystem resolvedFileSystem = fileSystem ?? new FileSystem();
        if (!resolvedFileSystem.File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        EditorConfigFile file = new(filePath, resolvedFileSystem);
        string[] lines = resolvedFileSystem.File.ReadAllLines(filePath);
        file.RawLines.AddRange(lines);

        EditorConfigSection? currentSection = null;
        int lineNumber = 0;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("#"))
                continue;

            if (line == "root = true")
            {
                file.IsRoot = true;
                continue;
            }

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                string pattern = line;
                currentSection = new EditorConfigSection(pattern, lineNumber);
                file.AddSection(currentSection);
                continue;
            }

            if (currentSection != null && line.Contains("="))
            {
                int eqIndex = line.IndexOf('=');
                string key = line[..eqIndex].Trim();
                string value = line[(eqIndex + 1)..].Trim();
                PropertyDefinition prop = new(key, value, lineNumber);
                currentSection.AddProperty(prop);
            }
        }

        return file;
    }

    public void WriteToFile(string outputPath)
    {
        HashSet<int> redundantLineNumbers = new();
        foreach (EditorConfigSection section in Sections)
        {
            foreach (PropertyDefinition prop in section.Properties)
            {
                if (prop.IsRedundant)
                    redundantLineNumbers.Add(prop.LineNumber);
            }
        }

        IEnumerable<string> outputLines = RawLines.Where((line, index) => !redundantLineNumbers.Contains(index + 1));
        _fileSystem.File.WriteAllLines(outputPath, outputLines);
    }

    public override string ToString() => $"{_fileSystem.Path.GetFileName(FilePath)} with {Sections.Count} sections";
}
