using System.Drawing;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Typesetterer3;

internal static partial class ScriptParser
{
    public static List<ScriptLine> ParseText(string? text)
    {
        var cleanedText = FixBrokenUnicodeEscapes(text ?? string.Empty);
        var normalized = cleanedText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        return ParseFormattedLines(normalized.Split('\n'));
    }

    internal static string FixBrokenUnicodeEscapes(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var decoded = WebUtility.HtmlDecode(text)
            .Replace('\u00A0', ' ')
            .Replace("u201C", "“", StringComparison.Ordinal)
            .Replace("u201D", "”", StringComparison.Ordinal)
            .Replace("u2019", "’", StringComparison.Ordinal)
            .Replace("u2018", "‘", StringComparison.Ordinal)
            .Replace("u0022", "\"", StringComparison.Ordinal)
            .Replace("u2026", "…", StringComparison.Ordinal)
            .Replace("u2764", "❤", StringComparison.Ordinal)
            .Replace("uFE0F", "", StringComparison.Ordinal)
            .Replace("u2014", "—", StringComparison.Ordinal);

        return decoded;
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

    private static string[] GetIgnoredLines()
    {
        return WindowSettings.GetIgnoredLines()
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToArray();
    }

    internal static bool IsIgnoredLine(string line, string[] ignoredLines)
    {
        foreach (var entry in ignoredLines)
        {
            if (line.Equals(entry, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (line.StartsWith(entry, StringComparison.OrdinalIgnoreCase))
            {
                if (line.Length > entry.Length && char.IsLetter(line[entry.Length]))
                {
                    continue;
                }
                return true;
            }
        }

        return false;
    }

    private static string ReadDocx(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var document = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("The .docx file does not contain word/document.xml.");
        using var stream = document.Open();
        var xml = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = xml.Descendants(w + "p").ToList();

        var formatted = new List<FormattedParagraph>();
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var pieces = paragraphs[i].Descendants(w + "t").Select(node => node.Value);
            var text = string.Concat(pieces);
            var isColored = HasColoredText(paragraphs[i], w);
            formatted.Add(new FormattedParagraph { Text = text, IsColored = isColored });
        }

        return FilterColoredParagraphs(formatted);
    }

    private static string FilterColoredParagraphs(List<FormattedParagraph> paragraphs)
    {
        var removedIndexes = new HashSet<int>();
        var idx = 0;
        while (idx < paragraphs.Count)
        {
            if (paragraphs[idx].IsColored)
            {
                var start = idx;
                while (idx < paragraphs.Count && paragraphs[idx].IsColored)
                {
                    idx++;
                }
                var length = idx - start;
                var deleteStart = Math.Max(0, start - length);
                for (var j = deleteStart; j < start; j++)
                {
                    if (!paragraphs[j].IsColored)
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
            builder.AppendLine(paragraphs[i].Text);
        }

        return DocxWhitespaceRegex().Replace(builder.ToString(), "\n").TrimEnd();
    }

    public static List<ScriptLine> ParseRtf(string rtfText)
    {
        if (string.IsNullOrWhiteSpace(rtfText)) return [];

        try
        {
            using var rtb = new RichTextBox();
            rtb.Rtf = rtfText;
            var paragraphs = new List<FormattedParagraph>();

            var lines = rtb.Lines;
            int pos = 0;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    pos += line.Length + 1;
                    continue;
                }

                int lineLen = line.Length;
                bool isColored = false;

                int end = Math.Min(pos + lineLen, rtb.TextLength);
                for (int p = pos; p < end; p++)
                {
                    rtb.Select(p, 1);
                    var color = rtb.SelectionColor;
                    if (!color.IsEmpty && color != Color.Black && !IsDarkOrGrayColor(color))
                    {
                        isColored = true;
                        break;
                    }
                }

                paragraphs.Add(new FormattedParagraph { Text = line, IsColored = isColored });
                pos += lineLen + 1;
            }

            if (paragraphs.Any(p => p.IsColored))
            {
                var filteredText = FilterColoredParagraphs(paragraphs);
                return ParseText(filteredText);
            }

            return ParseText(rtb.Text);
        }
        catch
        {
            return ParseText(rtfText);
        }
    }

    public static List<ScriptLine> ParseHtml(string htmlText)
    {
        if (string.IsNullOrWhiteSpace(htmlText)) return [];

        try
        {
            // Extract fragment if Windows HTML clipboard comments are present
            int startFrag = htmlText.IndexOf("<!--StartFragment-->", StringComparison.OrdinalIgnoreCase);
            int endFrag = htmlText.IndexOf("<!--EndFragment-->", StringComparison.OrdinalIgnoreCase);
            if (startFrag >= 0 && endFrag > startFrag)
            {
                htmlText = htmlText.Substring(startFrag + "<!--StartFragment-->".Length, endFrag - startFrag - "<!--StartFragment-->".Length);
            }

            // Strip Windows HTML Clipboard headers (Version:..., StartHTML:..., etc.)
            htmlText = Regex.Replace(htmlText, @"^(?:Version|StartHTML|EndHTML|StartFragment|EndFragment|SourceURL):.*[\r\n]*", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            // Extract CSS class color mappings before stripping <style> tags
            var classColors = ExtractCssClassColors(htmlText);

            // Strip <head>, <style>, and <script> tags to avoid parsing raw CSS rules as text lines!
            htmlText = Regex.Replace(htmlText, @"<head\b[^>]*>[\s\S]*?</head>", string.Empty, RegexOptions.IgnoreCase);
            htmlText = Regex.Replace(htmlText, @"<style\b[^>]*>[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
            htmlText = Regex.Replace(htmlText, @"<script\b[^>]*>[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);

            var rawBlocks = Regex.Split(htmlText, @"<(?:p|div|br|tr|li)\b[^>]*>", RegexOptions.IgnoreCase);
            var paragraphs = new List<FormattedParagraph>();

            foreach (var rawBlock in rawBlocks)
            {
                if (string.IsNullOrWhiteSpace(rawBlock)) continue;

                var text = WebUtility.HtmlDecode(Regex.Replace(rawBlock, @"<[^>]+>", string.Empty)).Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                bool isColored = HasHtmlColor(rawBlock, classColors);
                paragraphs.Add(new FormattedParagraph { Text = text, IsColored = isColored });
            }

            if (paragraphs.Count > 0)
            {
                if (paragraphs.Any(p => p.IsColored))
                {
                    var filteredText = FilterColoredParagraphs(paragraphs);
                    return ParseText(filteredText);
                }

                var combined = string.Join("\n", paragraphs.Select(p => p.Text));
                return ParseText(combined);
            }

            var plainText = WebUtility.HtmlDecode(Regex.Replace(htmlText, @"<[^>]+>", string.Empty));
            return ParseText(plainText);
        }
        catch
        {
            return ParseText(htmlText);
        }
    }

    private static Dictionary<string, bool> ExtractCssClassColors(string htmlText)
    {
        var classColors = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        var styleMatches = Regex.Matches(htmlText, @"<style\b[^>]*>([\s\S]*?)</style>", RegexOptions.IgnoreCase);
        foreach (Match styleMatch in styleMatches)
        {
            var cssText = styleMatch.Groups[1].Value;
            var ruleMatches = Regex.Matches(cssText, @"\.([a-zA-Z0-9_-]+)\s*\{([^}]+)\}", RegexOptions.IgnoreCase);
            foreach (Match ruleMatch in ruleMatches)
            {
                var className = ruleMatch.Groups[1].Value;
                var body = ruleMatch.Groups[2].Value;

                bool isColored = false;

                // Check text font color ONLY (ignore background-color marker highlights)
                var colorMatch = Regex.Match(body, @"(?:^|;|\s)color\s*:\s*([^;}]+)", RegexOptions.IgnoreCase);
                if (colorMatch.Success && IsCustomColorValue(colorMatch.Groups[1].Value))
                {
                    isColored = true;
                }

                classColors[className] = isColored;
            }
        }

        return classColors;
    }

    private static bool HasHtmlColor(string htmlSnippet, Dictionary<string, bool>? classColors = null)
    {
        // 1. Check direct font color attributes in snippet (style="color:..." or color="...")
        var colorMatch = Regex.Match(htmlSnippet, @"(?:^|;|\s|[""'])color\s*[:=]\s*[""']?\s*(#[0-9a-fA-F]{3,8}|rgb\([^)]+\)|[a-zA-Z]+)", RegexOptions.IgnoreCase);
        if (colorMatch.Success && IsCustomColorValue(colorMatch.Groups[1].Value))
        {
            return true;
        }

        // 2. Check CSS class attributes (class="c1 c2 ...")
        if (classColors != null && classColors.Count > 0)
        {
            var classMatches = Regex.Matches(htmlSnippet, @"class\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            foreach (Match classMatch in classMatches)
            {
                var classNames = classMatch.Groups[1].Value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var className in classNames)
                {
                    if (classColors.TryGetValue(className, out var isColored) && isColored)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsCustomColorValue(string val)
    {
        val = val.Trim().ToLowerInvariant();
        if (val is "black" or "#000000" or "#000" or "auto" or "inherit" or "initial" or "#111" or "#222" or "#333" or "#000000ff") return false;

        if (val.StartsWith('#'))
        {
            var hex = val.TrimStart('#');
            if (hex.Length == 8) hex = hex[..6];
            if (hex.Length == 3) hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            return !IsDarkOrGray(hex);
        }

        if (val.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var numbers = Regex.Matches(val, @"\d+").Select(m => int.Parse(m.Value)).ToArray();
            if (numbers.Length >= 3)
            {
                return !IsDarkOrGrayColor(Color.FromArgb(numbers[0], numbers[1], numbers[2]));
            }
        }

        return true;
    }

    private static bool IsCustomBackgroundColorValue(string val)
    {
        val = val.Trim().ToLowerInvariant();
        if (val is "white" or "#ffffff" or "#fff" or "#ffffffff" or "transparent" or "none" or "auto" or "inherit" or "initial") return false;

        if (val.StartsWith('#'))
        {
            var hex = val.TrimStart('#');
            if (hex.Length == 8) hex = hex[..6];
            if (hex.Length == 3) hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            if (hex is "ffffff" or "fff" or "fefefe" or "fdfdfd" or "fafafa") return false;
            return true;
        }

        if (val.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var numbers = Regex.Matches(val, @"\d+").Select(m => int.Parse(m.Value)).ToArray();
            if (numbers.Length >= 3)
            {
                if (numbers[0] > 245 && numbers[1] > 245 && numbers[2] > 245) return false;
                return true;
            }
        }

        return true;
    }

    private static bool IsDarkOrGrayColor(Color color)
    {
        var r = color.R;
        var g = color.G;
        var b = color.B;

        if (r < 80 && g < 80 && b < 80) return true;
        if (Math.Abs(r - g) < 15 && Math.Abs(g - b) < 15 && Math.Abs(r - b) < 15) return true;
        return false;
    }

    private static List<ScriptLine> ParseFormattedLines(IEnumerable<string> rawLines)
    {
        var result = new List<ScriptLine>();
        var sourceNumber = 0;
        var tags = GetIgnoredTags();
        var ignoredLines = GetIgnoredLines();

        foreach (var rawLine in rawLines)
        {
            sourceNumber++;
            var trimmedRaw = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmedRaw)) continue;

            if (IsIgnoredLine(trimmedRaw, ignoredLines))
            {
                result.Add(new ScriptLine
                {
                    SourceNumber = sourceNumber,
                    RawText = trimmedRaw,
                    CleanText = trimmedRaw,
                    Pasteable = false
                });
                continue;
            }

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

            // Ignore lines that only got the tag and nothing else.
            // This includes lines that are exactly the tag, or lines where the remainder
            // contains only punctuation/whitespace and the matched tag is numeric (no letters, contains digits).
            if (foundTag != null)
            {
                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    continue;
                }

                bool isNumericTag = foundTag.Any(char.IsDigit) && !foundTag.Any(char.IsLetter);
                if (isNumericTag && cleaned.All(c => !char.IsLetterOrDigit(c)))
                {
                    continue;
                }
            }

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

internal sealed class FormattedParagraph
{
    public string Text { get; set; } = string.Empty;
    public bool IsColored { get; set; }
}

internal sealed class ScriptLine
{
    public int SourceNumber { get; init; }
    public string RawText { get; init; } = string.Empty;
    public string CleanText { get; init; } = string.Empty;
    public bool Pasteable { get; init; }
    public bool IsPasted { get; set; }
    public bool IsModified { get; set; }

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
