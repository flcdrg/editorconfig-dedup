using dotnet_editorconfig_dedup;

var result = PatternMatcher.IsScopeBroader("[*]", "[*]");
Console.WriteLine($"IsScopeBroader([*], [*]) = {result}");

result = PatternMatcher.IsScopeBroader("[*]", "[*.js]");
Console.WriteLine($"IsScopeBroader([*], [*.js]) = {result}");

result = PatternMatcher.IsScopeBroader("[*.js]", "[*]");
Console.WriteLine($"IsScopeBroader([*.js], [*]) = {result}");
