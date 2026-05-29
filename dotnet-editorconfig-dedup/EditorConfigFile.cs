namespace dotnet_editorconfig_dedup;

public class EditorConfigFile
{
    public string FilePath { get; set; }
    public List<EditorConfigSection> Sections { get; private set; }
    public List<string> RawLines { get; private set; }
    public bool IsRoot { get; set; }

    public EditorConfigFile(string filePath)
    {
        FilePath = filePath;
        Sections = new List<EditorConfigSection>();
        RawLines = new List<string>();
        IsRoot = false;
    }

    public void AddSection(EditorConfigSection section)
    {
        Sections.Add(section);
    }

    public static EditorConfigFile Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        EditorConfigFile file = new(filePath);
        string[] lines = File.ReadAllLines(filePath);
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
        using StreamWriter writer = new(outputPath);
        foreach (string line in RawLines)
        {
            bool shouldWrite = true;

            foreach (EditorConfigSection section in Sections)
            {
                foreach (PropertyDefinition prop in section.Properties)
                {
                    if (prop.IsRedundant && prop.LineNumber <= RawLines.Count && 
                        RawLines[prop.LineNumber - 1] == line)
                    {
                        shouldWrite = false;
                        break;
                    }
                }
                if (!shouldWrite)
                    break;
            }

            if (shouldWrite)
                writer.WriteLine(line);
        }
    }

    public override string ToString() => $"{Path.GetFileName(FilePath)} with {Sections.Count} sections";
}
