using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Typesetterer3;

internal static partial class ScriptParser
{
    public static List<ScriptLine> ParseText(string? text)
    {
        var normalized = (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        return ParseFormattedLines(normalized.Split('\n'));
    }

    public static string CleanSingleLine(string rawText)
    {
        var tags = GetIgnoredTags();
        return CleanLeading(rawText, tags);
    }

    public static List<ScriptLine> ParseFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var text = extension switch
        {
            ".docx" => ReadDocx(path),
            ".txt" or ".md" => File.ReadAllText(path, Encoding.UTF8),
            _ => File.ReadAllText(path, Encoding.UTF8),
        };

        return ParseText(text);
    }

    private static string[] GetIgnoredTags()
    {
        return WindowSettings.GetIgnoredTags()
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToArray();
    }

    private static string ReadDocx(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var document = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("The .docx file does not contain word/document.xml.");
        using var stream = document.Open();
        var xml = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = xml.Descendants(w + "p").ToList();
        var removedIndexes = new HashSet<int>();

        for (var i = 1; i < paragraphs.Count; i++)
        {
            if (HasColoredText(paragraphs[i], w))
            {
                removedIndexes.Add(i - 1);
            }
        }

        var builder = new StringBuilder();
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (removedIndexes.Contains(i)) continue;

            var pieces = paragraphs[i].Descendants(w + "t").Select(node => node.Value);
            builder.AppendLine(string.Concat(pieces));
        }

        return DocxWhitespaceRegex().Replace(builder.ToString(), "\n").TrimEnd();
    }

    private static List<ScriptLine> ParseFormattedLines(IEnumerable<string> rawLines)
    {
        var result = new List<ScriptLine>();
        var sourceNumber = 0;
        var tags = GetIgnoredTags();

        foreach (var rawLine in rawLines)
        {
            sourceNumber++;
            var trimmedRaw = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmedRaw)) continue;

            // Find if there's a tag
            string? foundTag = null;
            var lineForTagCheck = trimmedRaw;
            foreach (var tag in tags)
            {
                if (lineForTagCheck.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
                {
                    foundTag = tag;
                    break;
                }
            }

            var cleaned = CleanLeading(trimmedRaw, tags);
            var parts = cleaned
                .Split("//", StringSplitOptions.None)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToList();

            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var display = part;
                
                // Prepend tag to the first part for visual feedback
                if (i == 0 && foundTag != null)
                {
                    display = foundTag + " " + part;
                }
                else if (i > 0)
                {
                    display = "// " + part;
                }

                result.Add(new ScriptLine
                {
                    SourceNumber = sourceNumber,
                    RawText = display,
                    CleanText = part,
                    Pasteable = true
                });
            }
        }

        return result;
    }

    private static string CleanLeading(string line, string[] tags)
    {
        var trimmed = line.TrimStart();
        if (IsPunctuationOnly(trimmed)) return trimmed;

        foreach (var tag in tags)
        {
            if (trimmed.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = trimmed[tag.Length..].TrimStart();
                // If it was followed by a space, we strip that space too (implicit in TrimStart above)
                return remainder;
            }
        }

        var firstSpace = trimmed.IndexOf(' ', StringComparison.Ordinal);
        return firstSpace >= 0 ? trimmed[(firstSpace + 1)..] : trimmed;
    }

    private static bool IsPunctuationOnly(string line)
    {
        var stripped = line.Trim();
        return stripped.Length > 0 && stripped.All(character => !char.IsLetterOrDigit(character));
    }

    private static bool HasColoredText(XElement paragraph, XNamespace w)
    {
        foreach (var color in paragraph.Descendants(w + "color"))
        {
            var value = ((string?)color.Attribute(w + "val"))?.ToUpperInvariant();
            var theme = (string?)color.Attribute(w + "themeColor");
            if (!string.IsNullOrEmpty(theme)) return true;
            if (!string.IsNullOrEmpty(value) && value is not "AUTO" and not "000000")
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex DocxWhitespaceRegex();
}

internal sealed class ScriptLine
{
    public int SourceNumber { get; init; }
    public string RawText { get; init; } = string.Empty;
    public string CleanText { get; init; } = string.Empty;
    public bool Pasteable { get; init; }
    public bool IsPasted { get; set; }

    public ScriptLine() { }

    public ScriptLine(int sourceNumber, string text, bool pasteable)
    {
        SourceNumber = sourceNumber;
        RawText = text;
        CleanText = text;
        Pasteable = pasteable;
    }

    public override string ToString() => RawText;
}
