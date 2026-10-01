using System;

namespace Breakwater.Tool;

/// <summary>
/// Maps a tokenized statement's trimmed text back to a 1-based line number in the original
/// script. Statements are looked up in source order and the search cursor only moves forward, so
/// two statements with identical text (e.g. two near-identical <c>INSERT</c> calls) each resolve
/// to their own occurrence instead of both pointing at the first one.
/// </summary>
internal sealed class LineLocator
{
    private readonly string _text;
    private int _cursor;

    public LineLocator(string text)
    {
        _text = text;
    }

    public int LineOf(string statementText)
    {
        var index = _text.IndexOf(statementText, _cursor, StringComparison.Ordinal);
        if (index < 0)
        {
            // Should not happen (the text came from tokenizing this same script), but fall back
            // to "wherever we last were" rather than throwing over a cosmetic line number.
            index = _cursor;
        }
        else
        {
            _cursor = index + statementText.Length;
        }

        return LineOfOffset(_text, index);
    }

    public static int LineOfOffset(string text, int offset)
    {
        var line = 1;
        var end = Math.Min(offset, text.Length);
        for (var i = 0; i < end; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }
}
