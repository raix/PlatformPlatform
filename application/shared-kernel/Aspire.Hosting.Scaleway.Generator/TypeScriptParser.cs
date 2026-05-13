using System.Text.RegularExpressions;

namespace Aspire.Hosting.Scaleway.Generator;

/// <summary>
///     Parses TypeScript types.gen.ts files from the Scaleway JS SDK.
/// </summary>
public static partial class TypeScriptParser
{
    public static ParsedService Parse(string serviceName, string content)
    {
        var enums = ParseEnums(content);
        var createRequests = ParseCreateRequests(content);
        return new ParsedService(serviceName, createRequests, enums);
    }

    private static List<ParsedEnum> ParseEnums(string content)
    {
        var enums = new List<ParsedEnum>();

        foreach (var match in EnumPattern().Matches(content).Cast<Match>())
        {
            var name = match.Groups[1].Value;
            var body = match.Groups[2].Value;

            var values = new List<string>();
            foreach (var valueMatch in EnumValuePattern().Matches(body).Cast<Match>())
            {
                values.Add(valueMatch.Groups[1].Value);
            }

            if (values.Count > 0)
            {
                enums.Add(new ParsedEnum(name, values));
            }
        }

        return enums;
    }

    private static List<ParsedCreateRequest> ParseCreateRequests(string content)
    {
        var requests = new List<ParsedCreateRequest>();

        foreach (var match in CreateRequestPattern().Matches(content).Cast<Match>())
        {
            var name = match.Groups[1].Value;
            var body = match.Groups[2].Value;

            var fields = new List<ParsedField>();
            foreach (var fieldMatch in FieldPattern().Matches(body).Cast<Match>())
            {
                var fieldName = fieldMatch.Groups[1].Value;
                var isOptional = fieldMatch.Groups[2].Value == "?";
                var fieldType = fieldMatch.Groups[3].Value.Trim();

                fields.Add(new ParsedField(fieldName, fieldType, isOptional));
            }

            if (fields.Count > 0)
            {
                requests.Add(new ParsedCreateRequest(name, fields));
            }
        }

        return requests;
    }

    // Matches: export type SomeName = \n  | 'value1' \n  | 'value2'
    [GeneratedRegex(@"export type (\w+)\s*=\s*\n((?:\s*\| '[^']+'\s*\n?)+)", RegexOptions.Multiline)]
    private static partial Regex EnumPattern();

    // Matches individual enum values: | 'value'
    [GeneratedRegex(@"\|\s*'([^']+)'")]
    private static partial Regex EnumValuePattern();

    // Matches: export type Create*Request = { ... }
    [GeneratedRegex(@"export type (Create\w+Request)\s*=\s*\{([^}]+)\}", RegexOptions.Singleline)]
    private static partial Regex CreateRequestPattern();

    // Matches fields: fieldName?: type  or  fieldName: type
    [GeneratedRegex(@"(\w+)(\??):\s*([^\n]+)")]
    private static partial Regex FieldPattern();
}

public sealed record ParsedService(string Name, List<ParsedCreateRequest> CreateRequests, List<ParsedEnum> Enums);

public sealed record ParsedCreateRequest(string Name, List<ParsedField> Fields);

public sealed record ParsedField(string Name, string TypeScriptType, bool IsOptional);

public sealed record ParsedEnum(string Name, List<string> Values);
