namespace dotnet_editorconfig_dedup;

public class Deduplicator
{
    public DeduplicationSummary Summary { get; private set; } = new();

    public static List<EditorConfigFile> FindAllEditorConfigFiles(string rootPath)
    {
        var files = new List<EditorConfigFile>();
        var searchPath = Path.IsPathRooted(rootPath) ? rootPath : Path.Combine(Directory.GetCurrentDirectory(), rootPath);

        if (!Directory.Exists(searchPath))
            throw new DirectoryNotFoundException($"Directory not found: {searchPath}");

        foreach (string filePath in Directory.EnumerateFiles(searchPath, ".editorconfig", SearchOption.AllDirectories))
        {
            try
            {
                files.Add(EditorConfigFile.Parse(filePath));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to parse {filePath}: {ex.Message}");
            }
        }

        return files.OrderBy(f => f.FilePath).ToList();
    }

    public void AnalyzeHierarchy(List<EditorConfigFile> files)
    {
        if (files.Count == 0)
            return;

        foreach (var file in files)
        {
            DeduplicateSingleFile(file);
        }

        foreach (var childFile in files)
        {
            string? childDir = Path.GetDirectoryName(childFile.FilePath);
            if (string.IsNullOrEmpty(childDir))
                continue;

            List<EditorConfigFile> parentFiles = files
                .Where(f =>
                {
                    string? parentDir = Path.GetDirectoryName(f.FilePath);
                    return !string.IsNullOrEmpty(parentDir) &&
                           parentDir != childDir &&
                           IsParentOf(parentDir, childDir);
                })
                .OrderByDescending(f => Path.GetDirectoryName(f.FilePath)!.Length)
                .ToList();

            foreach (EditorConfigFile parentFile in parentFiles)
            {
                DeduplicateAcrossFiles(childFile, parentFile);
                if (parentFile.IsRoot)
                    break;
            }
        }
    }

    private void DeduplicateSingleFile(EditorConfigFile file)
    {
        foreach (var section in file.Sections)
        {
            var propertiesByKey = new Dictionary<string, List<PropertyDefinition>>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in section.Properties)
            {
                if (!propertiesByKey.ContainsKey(prop.Key))
                {
                    propertiesByKey[prop.Key] = new List<PropertyDefinition>();
                }
                propertiesByKey[prop.Key].Add(prop);
            }

            foreach (var (key, props) in propertiesByKey)
            {
                if (props.Count > 1)
                {
                    var sorted = props.OrderBy(p => p.LineNumber).ToList();
                    for (int i = 0; i < sorted.Count - 1; i++)
                    {
                        sorted[i].IsRedundant = true;
                        Summary.AddDuplicate(file.FilePath, section.Pattern, key, sorted[i].Value);
                        Summary.TotalLinesRemoved++;
                    }
                }
            }
        }

        DeduplicateCrossSectionsWithinFile(file);
    }

    private void DeduplicateCrossSectionsWithinFile(EditorConfigFile file)
    {
        List<EditorConfigSection> sections = file.Sections;

        for (int i = 0; i < sections.Count; i++)
        {
            for (int j = 0; j < sections.Count; j++)
            {
                if (i == j)
                    continue;

                EditorConfigSection broaderSection = sections[i];
                EditorConfigSection narrowerSection = sections[j];

                if (!PatternMatcher.IsScopeBroader(broaderSection.Pattern, narrowerSection.Pattern))
                    continue;

                foreach (var narrowerProp in narrowerSection.Properties)
                {
                    if (narrowerProp.IsRedundant)
                        continue;

                    var matchingBroaderProp = broaderSection.Properties.FirstOrDefault(p =>
                        string.Equals(p.Key, narrowerProp.Key, StringComparison.OrdinalIgnoreCase) &&
                        !p.IsRedundant);

                    if (matchingBroaderProp != null && matchingBroaderProp.Value == narrowerProp.Value)
                    {
                        narrowerProp.IsRedundant = true;
                        Summary.AddDuplicate(file.FilePath, narrowerSection.Pattern, narrowerProp.Key, narrowerProp.Value);
                        Summary.TotalLinesRemoved++;
                    }
                }
            }
        }
    }

    private void DeduplicateAcrossFiles(EditorConfigFile childFile, EditorConfigFile parentFile)
    {
        foreach (var childSection in childFile.Sections)
        {
            var matchingParentSections = parentFile.Sections
                .Where(s => PatternMatcher.IsScopeBroader(s.Pattern, childSection.Pattern) || s.Pattern == childSection.Pattern)
                .ToList();

            if (matchingParentSections.Count == 0)
                continue;

            foreach (var parentSection in matchingParentSections)
            {
                foreach (var childProp in childSection.Properties)
                {
                    if (childProp.IsRedundant)
                        continue;

                    var matchingParentProp = parentSection.Properties.LastOrDefault(p =>
                        string.Equals(p.Key, childProp.Key, StringComparison.OrdinalIgnoreCase) &&
                        !p.IsRedundant);

                    if (matchingParentProp != null && matchingParentProp.Value == childProp.Value)
                    {
                        childProp.IsRedundant = true;
                        Summary.AddDuplicate(childFile.FilePath, childSection.Pattern, 
                                            childProp.Key, childProp.Value);
                        Summary.TotalLinesRemoved++;
                    }
                }
            }
        }
    }

    private static bool IsParentOf(string? potentialParent, string? child)
    {
        if (string.IsNullOrEmpty(potentialParent) || string.IsNullOrEmpty(child))
            return false;

        string normalizedParent = Path.GetFullPath(potentialParent);
        string normalizedChild = Path.GetFullPath(child);

        return normalizedChild.StartsWith(normalizedParent + Path.DirectorySeparatorChar);
    }
}
