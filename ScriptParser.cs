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

        var isColored = new bool[paragraphs.Count];
        for (var i = 0; i < paragraphs.Count; i++)
        {
            isColored[i] = HasColoredText(paragraphs[i], w);
        }

        var idx = 0;
        while (idx < paragraphs.Count)
        {
            if (isColored[idx])
            {
                // Find the length of this colored run
                var start = idx;
                while (idx < paragraphs.Count && isColored[idx])
                {
                    idx++;
                }
                var length = idx - start;

                // Delete up to 'length' preceding black paragraphs
                var deleteStart = Math.Max(0, start - length);
                for (var j = deleteStart; j < start; j++)
                {
                    if (!isColored[j])
                    {
                        removedIndexes.Add(j);
                    }
                }
            }
            else
            {
                idx++;
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
                    if (lineForTagCheck.Length > tag.Length && char.IsLetter(lineForTagCheck[tag.Length]))
                    {
                        continue;
                    }
                    foundTag = tag;
                    break;
                }
            }

            var cleaned = CleanLeading(trimmedRaw, tags);
            var parts = Regex.Split(cleaned, @"(?<!:)//")
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
                if (trimmed.Length > tag.Length && char.IsLetter(trimmed[tag.Length]))
                {
                    continue;
                }
                var remainder = trimmed[tag.Length..].TrimStart();
                // If it was followed by a space, we strip that space too (implicit in TrimStart above)
                return remainder;
            }
        }

        return trimmed;
    }

    private static bool IsPunctuationOnly(string line)
    {
        var stripped = line.Trim();
        return stripped.Length > 0 && stripped.All(character => !char.IsLetterOrDigit(character));
    }

    private static bool HasColoredText(XElement paragraph, XNamespace w)
    {
        var totalLength = 0;
        var coloredLength = 0;

        foreach (var run in paragraph.Descendants(w + "r"))
        {
            var text = string.Concat(run.Descendants(w + "t").Select(t => t.Value));
            if (string.IsNullOrEmpty(text)) continue;

            totalLength += text.Length;

            var isRunColored = false;
            var rPr = run.Element(w + "rPr");
            if (rPr != null)
            {
                var color = rPr.Element(w + "color");
                if (color != null)
                {
                    var value = ((string?)color.Attribute(w + "val"))?.ToUpperInvariant();
                    var theme = ((string?)color.Attribute(w + "themeColor"))?.ToUpperInvariant();

                    // Ignore default dark/text theme colors
                    if (theme is not null and not "TEXT1" and not "TEXT2" and not "DARK1" and not "DARK2")
                    {
                        isRunColored = true;
                    }
                    else if (!string.IsNullOrEmpty(value) && value is not "AUTO" and not "000000")
                    {
                        // Ignore shades of gray or very dark hex colors
                        if (value.Length != 6 || !IsDarkOrGray(value))
                        {
                            isRunColored = true;
                        }
                    }
                }
            }

            if (isRunColored)
            {
                coloredLength += text.Length;
            }
        }

        if (totalLength == 0) return false;

        double ratio = (double)coloredLength / totalLength;
        return ratio >= 0.5;
    }

    private static bool IsDarkOrGray(string hex)
    {
        if (hex.Length != 6) return false;
        try
        {
            var r = Convert.ToInt32(hex[..2], 16);
            var g = Convert.ToInt32(hex[2..4], 16);
            var b = Convert.ToInt32(hex[4..6], 16);

            // If all RGB components are very dark (e.g., < 80 out of 255)
            if (r < 80 && g < 80 && b < 80) return true;

            // If it's a shade of gray (R, G, and B are very close, e.g., within 15 of each other)
            if (Math.Abs(r - g) < 15 && Math.Abs(g - b) < 15 && Math.Abs(r - b) < 15) return true;

            return false;
        }
        catch
        {
            return false;
        }
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
