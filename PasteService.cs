namespace Typesetterer3;

internal static partial class PasteService
{
    public static bool CopyText(string text)
    {
        var value = text ?? string.Empty;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                Clipboard.SetText(value, TextDataFormat.UnicodeText);
                return true;
            }
            catch
            {
                if (attempt == 7)
                {
                    return false;
                }

                Thread.Sleep(20);
            }
        }

        return false;
    }
}
