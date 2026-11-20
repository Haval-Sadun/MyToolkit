using System.Text;

namespace MyToolkit.Text;

/// <summary>The two ways Kurdish is written that this converts between: Sorani in Arabic script, Kurmanji in the Latin (Hawar) alphabet.</summary>
public enum KurdishScript
{
    /// <summary>Not Kurdish, or not enough to tell: never converted.</summary>
    Unknown,

    /// <summary>Sorani, in Arabic script (کوردی).</summary>
    Arabic,

    /// <summary>Kurmanji, in the Latin Hawar alphabet (Kurdî).</summary>
    Latin,
}

/// <summary>
/// Rule-based conversion of Kurdish text between Arabic script and Latin script, and a check for which one a text is in. Platform-free.
/// It is a transliteration, not a translation: letters are mapped and the short vowel Arabic script leaves out is put back where it is most likely, so the result is readable but
/// not perfect (Sorani and Kurmanji also differ in a few sounds). A text that does not look Kurdish is left alone, so an English or Arabic song is never mangled.
/// </summary>
public static class KurdishTransliterator
{
    // Letters only Sorani uses: Persian and Arabic text has none of them, so a few of these mean Sorani.
    private const string SoraniOnlyLetters = "ەێۆڕڵ";

    private const int MinimumSoraniLetters = 3;

    // Words that are Kurmanji and almost nothing else; the circumflex letters (ê î û) are needed as well, which keeps French, Turkish and English out.
    private static readonly HashSet<string> KurmanjiWords = new(StringComparer.Ordinal)
    {
        "ez", "hûn", "ew", "ji", "jî", "ku", "wan", "yên", "yê", "dil", "dilê", "jîn", "evîn", "bê", "lê", "û", "rojê", "bi", "min", "em", "we", "wî", "wê", "çi", "çima", "nîne", "heye", "tune",
    };

    private const int MinimumKurmanjiWords = 4;

    private const int MinimumCircumflexLetters = 2;

    /// <summary>Which script <paramref name="text"/> is written in, or <see cref="KurdishScript.Unknown"/> when it does not look like Kurdish.</summary>
    public static KurdishScript Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return KurdishScript.Unknown;

        var soraniLetters = 0;
        var circumflex = 0;
        var kurmanjiWords = 0;
        var word = new StringBuilder();
        foreach (var raw in text)
        {
            var c = char.ToLowerInvariant(raw);
            if (SoraniOnlyLetters.Contains(c))
                soraniLetters++;

            if (c is 'ê' or 'î' or 'û')
                circumflex++;

            if (char.IsLetter(c))
            {
                word.Append(c);
                continue;
            }

            kurmanjiWords += Flush(word);
        }

        kurmanjiWords += Flush(word);
        if (soraniLetters >= MinimumSoraniLetters)
            return KurdishScript.Arabic;

        return circumflex >= MinimumCircumflexLetters && kurmanjiWords >= MinimumKurmanjiWords ? KurdishScript.Latin : KurdishScript.Unknown;

        static int Flush(StringBuilder buffer)
        {
            if (buffer.Length == 0)
                return 0;

            var hit = KurmanjiWords.Contains(buffer.ToString()) ? 1 : 0;
            buffer.Clear();
            return hit;
        }
    }

    /// <summary><paramref name="text"/> in <paramref name="target"/> script, when it is Kurdish written in the other one; otherwise unchanged.</summary>
    public static string Convert(string text, KurdishScript target)
    {
        var source = Detect(text);
        if (source == KurdishScript.Unknown || source == target || target == KurdishScript.Unknown)
            return text;

        return target == KurdishScript.Latin ? ToLatin(text) : ToArabic(text);
    }

    // ---- Arabic script → Latin ----

    /// <summary>Sorani in Arabic script, as Latin. Each line starts with a capital.</summary>
    public static string ToLatin(string text)
    {
        var result = new StringBuilder(text.Length + 8);
        var run = new StringBuilder();
        foreach (var c in text)
        {
            if (IsArabicLetter(c))
            {
                run.Append(c);
                continue;
            }

            FlushArabicRun(run, result);
            result.Append(MapPunctuation(c));
        }

        FlushArabicRun(run, result);
        return CapitaliseLines(result.ToString());
    }

    // Every letter of the Arabic block and its Kurdish and Persian additions, up to ە (U+06D5), plus tatweel, the zero-width joiners and the vowel signs.
    private static bool IsArabicLetter(char c) => c is (>= '\u0621' and <= '\u063A') or (>= '\u0641' and <= '\u064A') or (>= '\u0670' and <= '\u06D5') or '\u0640' or '\u200C' or '\u200D' or (>= '\u064B' and <= '\u065F');

    private static char MapPunctuation(char c) => c switch
    {
        '،' => ',',
        '؟' => '?',
        '؛' => ';',
        >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
        >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
        _ => c,
    };

    private static void FlushArabicRun(StringBuilder run, StringBuilder result)
    {
        if (run.Length == 0)
            return;

        result.Append(WordToLatin(run.ToString()));
        run.Clear();
    }

    private static bool IsSoraniVowel(char c) => c is 'ا' or 'ە' or 'ێ' or 'ۆ';

    // Words whose unwritten short vowel the rules cannot place; kept short, add the common ones as they turn up.
    private static readonly Dictionary<string, string> KnownWords = new(StringComparer.Ordinal)
    {
        ["کوردستان"] = "kurdistan",
    };

    private static string WordToLatin(string word)
    {
        var letters = Fold(word);
        if (KnownWords.TryGetValue(letters, out var known))
            return known;

        var output = new StringBuilder(letters.Length + 4);
        for (var i = 0; i < letters.Length; i++)
        {
            var c = letters[i];
            var atStart = i == 0;
            var previous = atStart ? '\0' : letters[i - 1];
            var next = i + 1 < letters.Length ? letters[i + 1] : '\0';
            var afterVowel = IsSoraniVowel(previous) || previous is 'و' or 'ی';

            switch (c)
            {
                case 'ئ':
                    break; // the vowel carrier at the start of a word: nothing to say
                case 'ا': output.Append('a'); break;
                case 'ە': output.Append('e'); break;
                case 'ێ': output.Append('ê'); break;
                case 'ۆ': output.Append('o'); break;
                case 'و':
                    if (next == 'و' && !(atStart && IsSoraniVowel(letters.ElementAtOrDefault(i + 2))))
                    {
                        output.Append('û');
                        i++;
                    }
                    else if (atStart || afterVowel || IsSoraniVowel(next))
                    {
                        output.Append('w');
                    }
                    else
                    {
                        output.Append('u');
                    }

                    break;
                case 'ی':
                    // A consonant y at the start of a word before a vowel and after a vowel; a vowel î otherwise, with the glide put in between a consonant and a vowel (ژیان: jiyan).
                    if (atStart)
                        output.Append(IsSoraniVowel(next) || next == 'و' ? "y" : "î");
                    else if (IsSoraniVowel(previous))
                        output.Append('y');
                    else
                        output.Append(IsSoraniVowel(next) ? "iy" : "î");

                    break;
                case 'ب': output.Append('b'); break;
                case 'پ': output.Append('p'); break;
                case 'ت': output.Append('t'); break;
                case 'ج': output.Append('c'); break;
                case 'چ': output.Append('ç'); break;
                case 'ح': output.Append('h'); break;
                case 'خ': output.Append('x'); break;
                case 'د': output.Append('d'); break;
                case 'ر':
                case 'ڕ': output.Append('r'); break;
                case 'ز': output.Append('z'); break;
                case 'ژ': output.Append('j'); break;
                case 'س': output.Append('s'); break;
                case 'ش': output.Append('ş'); break;
                case 'ع':
                    if (!atStart)
                        output.Append('\'');

                    break;
                case 'غ': output.Append('x'); break;
                case 'ف': output.Append('f'); break;
                case 'ڤ': output.Append('v'); break;
                case 'ق': output.Append('q'); break;
                case 'ک': output.Append('k'); break;
                case 'گ': output.Append('g'); break;
                case 'ل':
                case 'ڵ': output.Append('l'); break;
                case 'م': output.Append('m'); break;
                case 'ن': output.Append('n'); break;
                case 'ھ': output.Append('h'); break;
                case 'ه':
                    // The plain heh is the Sorani "e" at the end of a word (a keyboard habit) and "h" elsewhere.
                    output.Append(next == '\0' && !atStart && !IsSoraniVowel(previous) ? 'e' : 'h');
                    break;
                default:
                    break; // marks that carry no sound: tatweel, zero-width joiners, short-vowel signs
            }

            // The short "i" Arabic script leaves out: a word that opens with two consonants has it after the first.
            if (i == 0 && (IsPlainConsonant(c) || c == 'و') && IsPlainConsonant(next) && output.Length > 0)
                output.Append('i');
        }

        return output.ToString();
    }

    // The first letter of a word is a consonant (a start-of-word و counts: it is a w); the second must be a plain one, not a vowel or a semi-vowel.
    private static bool IsPlainConsonant(char c) => c != '\0' && !IsSoraniVowel(c) && c is not ('و' or 'ی' or 'ئ');

    // Spellings of one letter the keyboards produce differently (Arabic ي ك ى) to the Sorani letter, and marks without a sound dropped.
    private static string Fold(string word)
    {
        var folded = new StringBuilder(word.Length);
        foreach (var c in word)
        {
            if (c is 'ـ' or '‌' or '‍' or (>= 'ً' and <= 'ٟ') or 'ٰ')
                continue;

            folded.Append(c switch
            {
                'ي' or 'ى' => 'ی',
                'ك' => 'ک',
                'ة' => 'ە',
                'ث' or 'ص' => 'س',
                'ذ' or 'ض' or 'ظ' => 'ز',
                'ط' => 'ت',
                _ => c,
            });
        }

        return folded.ToString();
    }

    private static string CapitaliseLines(string text)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            for (var j = 0; j < line.Length; j++)
            {
                if (!char.IsLetter(line[j]))
                    continue;

                lines[i] = string.Concat(line.AsSpan(0, j), char.ToUpperInvariant(line[j]).ToString(), line.AsSpan(j + 1));
                break;
            }
        }

        return string.Join('\n', lines);
    }

    // ---- Latin → Arabic script ----

    /// <summary>Kurmanji in the Latin alphabet, as Sorani in Arabic script.</summary>
    public static string ToArabic(string text)
    {
        var result = new StringBuilder(text.Length + 8);
        var run = new StringBuilder();
        foreach (var c in ComposeCircumflex(text))
        {
            if (IsLatinLetter(c))
            {
                run.Append(c);
                continue;
            }

            FlushLatinRun(run, result);
            result.Append(c switch
            {
                ',' => '،',
                '?' => '؟',
                ';' => '؛',
                _ => c,
            });
        }

        FlushLatinRun(run, result);
        return result.ToString();
    }

    private static bool IsLatinLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or 'ê' or 'Ê' or 'î' or 'Î' or 'û' or 'Û' or 'ç' or 'Ç' or 'ş' or 'Ş' or 'İ' or 'ı';

    // "e" + a combining circumflex (how some keyboards and files store ê) becomes the single letter.
    private static string ComposeCircumflex(string text) => text.Contains('̂')
        ? text.Replace("ê", "ê").Replace("î", "î").Replace("û", "û").Replace("Ê", "Ê").Replace("Î", "Î").Replace("Û", "Û")
        : text;

    private static void FlushLatinRun(StringBuilder run, StringBuilder result)
    {
        if (run.Length == 0)
            return;

        result.Append(WordToArabic(run.ToString()));
        run.Clear();
    }

    private static char Lower(char c) => c switch
    {
        'İ' => 'i',
        'ı' => 'i',
        _ => char.ToLowerInvariant(c),
    };

    private static bool IsLatinVowel(char c) => c is 'a' or 'e' or 'ê' or 'i' or 'î' or 'o' or 'u' or 'û';

    private static string WordToArabic(string word)
    {
        var letters = new string(word.Select(Lower).ToArray());
        var output = new StringBuilder(letters.Length + 4);
        for (var i = 0; i < letters.Length; i++)
        {
            var c = letters[i];
            var atStart = i == 0;
            var atEnd = i == letters.Length - 1;
            switch (c)
            {
                case 'a': output.Append(atStart ? "ئا" : "ا"); break;
                case 'e': output.Append(atStart ? "ئە" : "ە"); break;
                case 'ê': output.Append(atStart ? "ئێ" : "ێ"); break;
                case 'î': output.Append(atStart ? "ئی" : "ی"); break;
                case 'i':
                    // The short vowel is not written inside a word; it is at the start (carried by ئ) and at the end (a ی).
                    if (atStart)
                        output.Append("ئی");
                    else if (atEnd)
                        output.Append('ی');

                    break;
                case 'o': output.Append(atStart ? "ئۆ" : "ۆ"); break;
                case 'u': output.Append(atStart ? "ئو" : "و"); break;
                case 'û': output.Append(atStart ? "ئوو" : "وو"); break;
                case 'w': output.Append('و'); break;
                case 'y': output.Append('ی'); break;
                case 'b': output.Append('ب'); break;
                case 'p': output.Append('پ'); break;
                case 't': output.Append('ت'); break;
                case 'c': output.Append('ج'); break;
                case 'ç': output.Append('چ'); break;
                case 'd': output.Append('د'); break;
                case 'r':
                    // A trilled r: at the start of a word and when doubled; the plain r otherwise.
                    if (i + 1 < letters.Length && letters[i + 1] == 'r')
                    {
                        output.Append('ڕ');
                        i++;
                    }
                    else
                    {
                        output.Append(atStart ? 'ڕ' : 'ر');
                    }

                    break;
                case 'z': output.Append('ز'); break;
                case 'j': output.Append('ژ'); break;
                case 's': output.Append('س'); break;
                case 'ş': output.Append('ش'); break;
                case 'f': output.Append('ف'); break;
                case 'v': output.Append('ڤ'); break;
                case 'q': output.Append('ق'); break;
                case 'k': output.Append('ک'); break;
                case 'g': output.Append('گ'); break;
                case 'l':
                    if (i + 1 < letters.Length && letters[i + 1] == 'l')
                    {
                        output.Append('ڵ');
                        i++;
                    }
                    else
                    {
                        output.Append('ل');
                    }

                    break;
                case 'm': output.Append('م'); break;
                case 'n': output.Append('ن'); break;
                case 'h': output.Append('ھ'); break;
                case 'x': output.Append('خ'); break;
                default: output.Append(word[i]); break;
            }
        }

        return output.ToString();
    }
}
