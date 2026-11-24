using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace AnWaSolar.Core.Datasheets;

/// <summary>A word of a datasheet with its position on the page (PDF points, y grows upwards).</summary>
public sealed record DatasheetWord(string Text, int Page, double Left, double Right, double Bottom, double Top)
{
    public double CenterX => (Left + Right) / 2;

    public double CenterY => (Bottom + Top) / 2;

    public double Height => Math.Max(1, Top - Bottom);
}

/// <summary>Reads the words of a PDF file with PdfPig. Scanned datasheets without a text layer give no words.</summary>
public static class PdfTextReader
{
    /// <summary>Some PDF generators separate words with control characters instead of spaces.</summary>
    internal static string Clean(string text) =>
        string.Join(' ', new string([.. text.Select(c => char.IsControl(c) ? ' ' : c)]).Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <exception cref="IOException">The file cannot be read or is not a valid PDF.</exception>
    public static IReadOnlyList<DatasheetWord> Read(string path)
    {
        try
        {
            using PdfDocument document = PdfDocument.Open(path);
            var words = new List<DatasheetWord>();
            foreach (Page page in document.GetPages())
            {
                foreach (Word word in page.GetWords())
                {
                    string text = Clean(word.Text);
                    if (text.Length > 0)
                    {
                        words.Add(new DatasheetWord(text, page.Number, word.BoundingBox.Left, word.BoundingBox.Right,
                            word.BoundingBox.Bottom, word.BoundingBox.Top));
                    }
                }
            }

            return words;
        }
        catch (Exception ex) when (ex is not IOException and not OutOfMemoryException)
        {
            throw new IOException($"The PDF file cannot be read: {ex.Message}", ex);
        }
    }
}
