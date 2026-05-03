namespace Typesetterer3;

internal static partial class PasteService
{
    public static void CopyText(string text)
    {
        Clipboard.SetText(text ?? string.Empty, TextDataFormat.UnicodeText);
    }
}
