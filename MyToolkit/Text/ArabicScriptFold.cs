using System.Globalization;

namespace MyToolkit.Text;

/// <summary>Makes the spellings of one Arabic-script letter compare equal. Kurdish (Sorani), Persian and Arabic keyboards type the same word with different code points
/// (ي / ی, ك / ک, ە / ه), so a search or a title match typed on one layout must find text typed on another.</summary>
public static class ArabicScriptFold
{
    public static bool IsArabicScript(char c) => c is (>= '؀' and <= 'ۿ') or (>= 'ݐ' and <= 'ݿ') or (>= 'ﭐ' and <= '﷿') or (>= 'ﹰ' and <= '﻿');

    public static bool ContainsArabicScript(string? text) => text is not null && text.Any(IsArabicScript);

    /// <summary>The letter everything spelled like <paramref name="c"/> folds to, or '\0' for a character that carries no sound (the tatweel stretch).
    /// Digits of any script become ASCII digits. Anything else comes back unchanged.</summary>
    public static char Map(char c)
    {
        if (c == 'ـ')
            return '\0';

        if (c > '\u007F' && char.IsDigit(c))
            return (char)('0' + CharUnicodeInfo.GetDecimalDigitValue(c));

        return c switch
        {
            'ي' or 'ى' or 'ی' or 'ێ' or 'ے' => 'ی', // ي ى ی ێ ے → ی
            'ك' or 'ک' => 'ک', // ك ک → ک
            'ه' or 'ە' or 'ھ' or 'ہ' => 'ه', // ه ە ھ ہ → ه
            'ۆ' or 'ۇ' or 'ۉ' => 'و', // ۆ ۇ ۉ → و
            'ڕ' => 'ر', // ڕ → ر
            'ڵ' => 'ل', // ڵ → ل
            'ڤ' => 'ف', // ڤ → ف
            _ => c,
        };
    }
}
