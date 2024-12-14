namespace nfzf;

public enum CaseMode
{
    CaseRespect,
    CaseSmart
}

public class Pattern
{
    public List<TermSet> TermSets { get; } = new();
    public bool OnlyInv { get; set; }
}

public class TermSet
{
    public List<Term> Terms { get; set; } = new();
}

public class Term
{
    public Term(FuzzySearcher.MatchFunctionDelegate matchFunction, bool inv, string originalText, string text, bool caseSensitive)
    {
        MatchFunction = matchFunction;
        Inv = inv;
        OriginalText = originalText;
        Text = text;
        CaseSensitive = caseSensitive;
    }

    public FuzzySearcher.MatchFunctionDelegate MatchFunction { get; }
    public bool Inv { get; }
    public string OriginalText { get; set; }
    public string Text { get; }
    public bool CaseSensitive { get; }
}

public struct FzfResult
{
    public int Start;
    public int End;
    public int Score;
}

public static class FuzzySearcher
{
    public const int ScoreMatch = 16;
    public const int ScoreGapStart = -3;
    public const int ScoreGapExtension = -1;
    public const int BoundaryBonus = ScoreMatch / 2;
    public const int NonWordBonus = ScoreMatch / 2;
    public const int CamelCaseBonus = BoundaryBonus + ScoreGapExtension;
    public const int BonusConsecutive = -(ScoreGapStart + ScoreGapExtension);
    public const int BonusFirstCharMultiplier = 2;
    
    enum CharClass
    {
        NonWord = 0,
        CharLower = 1,
        CharUpper = 2,
        Digit = 3,
    }
    
    public delegate FzfResult MatchFunctionDelegate(
        bool caseSensitive,
        ReadOnlySpan<char> text,
        ReadOnlySpan<char> pattern,
        Slab slab,
        List<int>? pos);

    public static IList<int> GetPositions(ReadOnlySpan<char> text, Pattern pattern, Slab slab)
    {
        var result = new List<int>();
        foreach (var termSet in pattern.TermSets)
        {
            var matched = false;
            foreach (var term in termSet.Terms)
            {
                if (term.Inv)
                {
                    var invAlgResult = term.MatchFunction(term.CaseSensitive, text, term.Text, slab, result);
                    if (invAlgResult.Start < 0)
                    {
                        matched = true;
                    }
                    continue;
                }

                var algResult2 = term.MatchFunction(term.CaseSensitive, text, term.Text, slab, result);
                if (algResult2.Start >= 0)
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                return new List<int>();
            }
        }
        return result;
    }

    public static Pattern ParsePattern(CaseMode caseMode, string pattern, bool fuzzy)
    {
        Pattern patObj = new();

        if (string.IsNullOrEmpty(pattern))
        {
            return patObj;
        }

        pattern = pattern.TrimStart();
        pattern = TrimSuffixSpaces(pattern);

        string patternCopy = pattern.Replace("\\ ", "\t");
        string[] tokens = patternCopy.Split(' ');

        TermSet set = new();
        bool switchSet = false;
        bool afterBar = false;

        foreach (var token in tokens)
        {
            string ptr = token;
            MatchFunctionDelegate matchFunction = fuzzy ? FzfFuzzyMatchV2 : FzfExactMatchNaive;
            bool inv = false;

            string text = ptr.Replace('\t', ' ');
            string ogStr = text;
            string lowerText = text.ToLower();
            bool caseSensitive = caseMode == CaseMode.CaseRespect || (caseMode == CaseMode.CaseSmart && text != lowerText);

            if (!caseSensitive)
            {
                text = lowerText;
                ogStr = lowerText;
            }

            if (!fuzzy)
            {
                matchFunction = FzfExactMatchNaive;
            }

            if (set.Terms.Count > 0 && !afterBar && text == "|")
            {
                switchSet = false;
                afterBar = true;
                continue;
            }

            afterBar = false;

            if (text.StartsWith("!"))
            {
                inv = true;
                matchFunction = FzfExactMatchNaive;
                text = text[1..];
            }

            if (text.EndsWith("$") && text != "$")
            {
                matchFunction = FzfSuffixMatch;
                text = text[..^1];
            }

            if (text.StartsWith("'"))
            {
                if (fuzzy && !inv)
                {
                    matchFunction = FzfExactMatchNaive;
                    text = text[1..];
                }
                else
                {
                    matchFunction = FzfFuzzyMatchV2;
                    text = text[1..];
                }
            }
            else if (text.StartsWith("^"))
            {
                matchFunction = FzfPrefixMatch;
                text = text[1..];
            }

            if (text.Length > 0)
            {
                if (switchSet)
                {
                    patObj.TermSets.Add(set);
                    set = new TermSet();
                }

                Term term = new(matchFunction: matchFunction, inv: inv, originalText: ogStr, text: text,
                    caseSensitive: caseSensitive);

                set.Terms.Add(term);
                switchSet = true;
            }
        }

        if (set.Terms.Count > 0)
        {
            patObj.TermSets.Add(set);
        }

        patObj.OnlyInv = patObj.TermSets.TrueForAll(s => s.Terms.Count == 1 && s.Terms[0].Inv);

        return patObj;
    }
    
    public static FzfResult FzfFuzzyMatchV2(
        bool caseSensitive,
        ReadOnlySpan<char> text,
        ReadOnlySpan<char> pattern,
        Slab slab,
        List<int>? pos)
    {
        var patternSize = pattern.Length;
        var textSize = text.Length;

        if (patternSize == 0)
        {
            return new FzfResult { Start = 0, End = 0, Score = 0 };
        }

        if (patternSize * textSize >= slab.Cap)
        {
            return FuzzyMatchV1(caseSensitive, text, pattern, slab, pos);
        }

        var firstIndexOf = FuzzyIndexOf(text, pattern, caseSensitive);
        if (firstIndexOf < 0)
        {
            return new FzfResult { Start = -1, End = -1, Score = 0 };
        }

        var initialScoresSpan = slab.AllocInt(textSize);
        var consecutiveScoresSpan = slab.AllocInt(textSize);
        Span<int> firstOccurrenceOfEachChar = patternSize <= 128
            ? stackalloc int[patternSize]
            : slab.AllocInt(patternSize);

        var maxScore = 0;
        var maxScorePos = 0;

        var patternIndex = 0;
        var lastIndex = 0;

        var firstPatternChar = pattern[0];
        var currentPatternChar = pattern[0];
        var previousInitialScore = 0;
        var previousClass = CharClass.NonWord;
        var inGap = false;

        var textCopy = slab.AllocChar(textSize);
        text.TryCopyTo(textCopy);
        var textSlice = textCopy.Slice(firstIndexOf, textSize - firstIndexOf);
        var initialScoresSlice = initialScoresSpan.Slice(firstIndexOf);
        var consecutiveScoresSlice = consecutiveScoresSpan.Slice(firstIndexOf);
        
        var bonusesSpan = slab.AllocInt(textSize);
        var bonusesSlice = bonusesSpan.Slice(firstIndexOf, textSize - firstIndexOf);

        for (var i = 0; i < textSlice.Length; i++)
        {
            var currentChar = textSlice[i];
            var currentClass = ClassOf(currentChar);
            if (!caseSensitive && currentClass == CharClass.CharUpper)
            {
                currentChar = char.ToLower(currentChar);
            }

            textSlice[i] = currentChar;
            var bonus = CalculateBonus(previousClass, currentClass);
            bonusesSlice[i] = bonus;
            previousClass = currentClass;
            if (currentChar == currentPatternChar)
            {
                if (patternIndex < pattern.Length)
                {
                    firstOccurrenceOfEachChar[patternIndex] = firstIndexOf + i;
                    patternIndex++;
                    currentPatternChar = pattern[Math.Min(patternIndex, patternSize - 1)];
                }

                lastIndex = firstIndexOf + i;
            }

            if (currentChar == firstPatternChar)
            {
                var score = ScoreMatch + bonus * BonusFirstCharMultiplier;
                initialScoresSlice[i] = score;
                consecutiveScoresSlice[i] = 1;
                if (patternSize == 1 && (score > maxScore))
                {
                    maxScore = score;
                    maxScorePos = firstIndexOf + i;
                    if (bonus == BoundaryBonus)
                    {
                        break;
                    }
                }
                inGap = false;
            }
            else
            {
                if (inGap)
                {
                    initialScoresSlice[i] = Math.Max(previousInitialScore + ScoreGapExtension, 0);
                }
                else
                {
                    initialScoresSlice[i] = Math.Max(previousInitialScore + ScoreGapStart, 0);
                }

                consecutiveScoresSlice[i] = 0;
                inGap = true;
            }

            previousInitialScore = initialScoresSlice[i];
        }

        if (patternIndex != pattern.Length)
        {
            return new FzfResult { Start = -1, End = -1, Score = 0 };
        }

        if (pattern.Length == 1)
        {
            var result = new FzfResult { Start = maxScorePos, End = maxScorePos + 1, Score = maxScore };
            pos?.Add(maxScorePos);
            return result;
        }

        var firstOccurenceOfFirstChar = firstOccurrenceOfEachChar[0];
        var width = lastIndex - firstOccurenceOfFirstChar + 1;
        
        var scoreMatrix = slab.AllocInt(width * patternSize);
        var initialScoresSlice2 = initialScoresSpan.Slice(firstOccurenceOfFirstChar, lastIndex - firstOccurenceOfFirstChar);
        initialScoresSlice2.CopyTo(scoreMatrix.Slice(0, width + 1));
        
        var scoreSpan = scoreMatrix.Slice(0, width * patternSize);
        
        var consecutiveCharMatrixSize = width * patternSize;
        var consecutiveCharMatrix = slab.AllocInt(width * patternSize);
        var consecutiveScoresSlice2 = consecutiveScoresSpan.Slice(firstOccurenceOfFirstChar, lastIndex - firstOccurenceOfFirstChar);
        consecutiveScoresSlice2.CopyTo(consecutiveCharMatrix.Slice(0, width));
        
        var consecutiveCharMatrixSpan = consecutiveCharMatrix.Slice(0, width * patternSize);

        var firstOccurenceOfPatternCharSlice = firstOccurrenceOfEachChar.Slice(1, patternSize - 1);
        var patternSlice = pattern.Slice(1);

        for (var off = 0; off < firstOccurenceOfPatternCharSlice.Length; off++)
        {
            var patternCharOffset = firstOccurenceOfPatternCharSlice[off];
            currentPatternChar = patternSlice[off];
            patternIndex = off + 1;
            var row = patternIndex * width;
            inGap = false;
            var textSlice2 = textCopy.Slice(patternCharOffset, lastIndex - patternCharOffset + 1);
            var bonusSlice2 = bonusesSpan.Slice(patternCharOffset, textSlice2.Length);
            var consecutiveCharMatrixSlice =
                consecutiveCharMatrixSpan.Slice(row + patternCharOffset - firstOccurenceOfFirstChar, textSlice2.Length);
            var consecutiveCharMatrixDiagonalSlice =
                consecutiveCharMatrixSpan.Slice(row + patternCharOffset - firstOccurenceOfFirstChar - 1 - width,
                    textSlice2.Length);
            var scoreMatrixSlice =
                scoreSpan.Slice(row + patternCharOffset - firstOccurenceOfFirstChar, textSlice2.Length);
            var scoreMatrixDiagonalSlice =
                scoreSpan.Slice(row + patternCharOffset - firstOccurenceOfFirstChar - 1 - width, textSlice2.Length);
            var scoreMatrixLeftSlice =
                scoreSpan.Slice(row + patternCharOffset - firstOccurenceOfFirstChar - 1, textSlice2.Length);
            scoreMatrixLeftSlice[0] = 0;
            for (var j = 0; j < textSlice2.Length; j++)
            {
                var currentChar = textSlice2[j];
                var column = j + patternCharOffset;
                var score = 0;
                var diagonalScore = 0;
                var consecutive = 0;

                if (inGap)
                {
                    score = scoreMatrixLeftSlice[j] + ScoreGapExtension;
                }
                else
                {
                    score = scoreMatrixLeftSlice[j] + ScoreGapStart;
                }

                if (currentChar == currentPatternChar)
                {
                    diagonalScore = scoreMatrixDiagonalSlice[j] + ScoreMatch;
                    var bonus = bonusSlice2[j];
                    consecutive = consecutiveCharMatrixDiagonalSlice[j] + 1;
                    if (bonus == BoundaryBonus)
                    {
                        consecutive = 1;
                    }
                    else if (consecutive > 1)
                    {
                        bonus = Math.Max(bonus, Math.Max(BonusConsecutive, bonusesSpan[(column - consecutive) + 1]));
                    }

                    if (diagonalScore + bonus < score)
                    {
                        diagonalScore += bonusSlice2[j];
                        consecutive = 0;
                    }
                    else
                    {
                        diagonalScore += bonus;
                    }
                }

                consecutiveCharMatrixSlice[j] = consecutive;
                inGap = diagonalScore < score;
                var score2 = Math.Max(0, Math.Max(diagonalScore, score));
                if (patternIndex == patternSize - 1 && score2 > maxScore)
                {
                    maxScore = score2;
                    maxScorePos = column;
                }

                scoreMatrixSlice[j] = score2;
            }
        }

        var start = maxScorePos;
        if (pos != null)
        {
            var i2 = patternSize - 1;
            var preferMatch = true;
            for (;;)
            {
                var ii = i2 * width;
                var j0 = start - firstOccurenceOfFirstChar;
                var s = scoreMatrix[ii + j0];
        
                var s1 = 0;
                var s2 = 0;
                if (i2 > 0 && start >= firstOccurrenceOfEachChar[i2])
                {
                    s1 = scoreMatrix[ii - width + j0 - 1];
                }
        
                if (start > firstOccurrenceOfEachChar[i2])
                {
                    s2 = scoreMatrix[ii + j0 - 1];
                }
        
                if (s > s1 && (s > s2 || (s == s2 && preferMatch)))
                {
                    pos.Add(start);
                    if (i2 == 0)
                    {
                        break;
                    }
        
                    i2--;
                }
        
                start--;
                preferMatch = consecutiveCharMatrix[ii + j0] > 1 || (ii + width + j0 + 1 < consecutiveCharMatrixSize &&
                                                                     consecutiveCharMatrix[ii + width + j0 + 1] > 0);
            }
        }
        
        return new FzfResult {Start = start, End = maxScorePos + 1, Score = maxScore};
    }
    
    public static FzfResult FzfExactMatchNaive(
        bool caseSensitive,
        ReadOnlySpan<char> text,
        ReadOnlySpan<char> pattern,
        Slab slab,
        List<int>? pos)
    {
        int M = pattern.Length;
        int N = text.Length;

        if (M == 0)
        {
            return new FzfResult { End = 0, Score = 0, Start = 0 };
        }
        if (N < M)
        {
            return new FzfResult { End = -1, Score = 0, Start = -1 };
        }
        if (FuzzyIndexOf(text, pattern, caseSensitive) < 0)
        {
            return new FzfResult { End = -1, Score = 0, Start = -1 };
        }

        var pidx = 0;
        var bestPos = -1;
        var bonus = 0;
        var bestBonus = -1;

        for (int idx = 0; idx < N; idx++)
        {
            char c = text[idx];
            if (!caseSensitive)
            {
                c = char.ToLowerInvariant(c);
            }

            char pc = pattern[pidx];
            if (!caseSensitive)
            {
                pc = char.ToLowerInvariant(pc);
            }

            if (c == pc)
            {
                if (pidx == 0)
                {
                    bonus = BonusAt(text, idx);
                }
                pidx++;
                if (pidx == M)
                {
                    if (bonus > bestBonus)
                    {
                        bestPos = idx;
                        bestBonus = bonus;
                    }
                    if (bonus == BoundaryBonus)
                    {
                        break;
                    }
                    idx -= pidx - 1;
                    pidx = 0;
                    bonus = 0;
                }
            }
            else
            {
                idx -= pidx;
                pidx = 0;
                bonus = 0;
            }
        }

        if (bestPos >= 0)
        {
            int bp = bestPos;
            int sidx = bp - M + 1;
            int eidx = bp + 1;
            int score = CalculateScore(caseSensitive, text, pattern, sidx, eidx, null);
            if (pos != null)
            {
                for (var i = sidx; i < eidx; i++)
                {
                    pos.Add(i);
                }
            }
            return new FzfResult { Start = sidx, End = eidx, Score = score };
        }

        return new FzfResult { End = -1, Start = -1, Score = 0 };
    }
    
    public static int GetScore(string[] paths, ReadOnlySpan<char> fileName, Pattern pattern, Slab slab)
    {
        Span<char> combined = stackalloc char[2048];
        var combinedResult = JoinFilePath(paths, fileName, combined);
        return GetScore(combinedResult, pattern, slab);
    }
    
    public static int GetScore(string[] paths, Pattern pattern, Slab slab)
    {
        Span<char> combined = stackalloc char[2048];
        var combinedResult = JoinFilePath(paths, combined);
        return GetScore(combinedResult, pattern, slab);
    }
    
    private static ReadOnlySpan<char> JoinFilePath(string[] dirs, Span<char> target)
    {
        var position = 0;

        for (var i = 0; i < dirs.Length; i++)
        {
            var dir = dirs[i];
            if (string.IsNullOrEmpty(dir))
                continue;

            var dirSpan = dir.AsSpan();

            if (position + dirSpan.Length > target.Length)
                throw new ArgumentException("The target buffer is not large enough to hold the joined file path.");

            dirSpan.CopyTo(target[position..]);
            position += dirSpan.Length;

            if (i != dirs.Length - 1 && dirSpan[^1] != '\\')
            {
                if (position >= target.Length)
                    throw new ArgumentException("The target buffer is not large enough to hold the joined file path.");
            
                target[position] = '\\';
                position++;
            }
        }

        return target[..position];
    }
    
    private static ReadOnlySpan<char> JoinFilePath(string[] dirs, ReadOnlySpan<char> fileName, Span<char> target)
    {
        var position = 0;
        foreach (var dir in dirs)
        {
            var dirSpan = dir.AsSpan();
            dirSpan.CopyTo(target[position..]);
            position += dirSpan.Length;
            if (dirSpan.Length > 0 && dirSpan[^1] != '\\')
            {
                target[position] = '\\';
                position++;
            }
        }
        fileName.CopyTo(target.Slice(position));
        position += fileName.Length;
        return target[..position];
    }

    public static int GetScore(ReadOnlySpan<char> text, ReadOnlySpan<char> text2, Pattern pattern, Slab slab)
    {
        if (text.Length + text2.Length > 2048)
        {
            throw new ArgumentException("Combined text length exceeds buffer size.");
        }
        
        Span<char> combined = stackalloc char[2048];
        text.CopyTo(combined);
        text2.CopyTo(combined[(text.Length)..]);
        return GetScore(combined[0..(text.Length + text2.Length)], pattern, slab);
    }

    public static int GetScore(ReadOnlySpan<char> text, Pattern pattern, Slab slab)
    {
        slab.Reset();
        if (pattern.TermSets.Count == 0)
        {
            return 1;
        }

        if (pattern.OnlyInv)
        {
            int final = 0;
            foreach (var termSet in pattern.TermSets)
            {
                var term = termSet.Terms[0];
                var res = term.MatchFunction(term.CaseSensitive, text, term.Text, slab, null);
                final += res.Score;
            }
            return (final > 0) ? 0 : 1;
        }

        int totalScore = 0;
        foreach (var termSet in pattern.TermSets)
        {
            int currentScore = 0;
            bool matched = false;
            foreach (var term in termSet.Terms)
            {
                var res = term.MatchFunction(term.CaseSensitive, text, term.Text, slab, null);
                if (res.Start >= 0)
                {
                    if (term.Inv)
                    {
                        continue;
                    }
                    currentScore = res.Score;
                    matched = true;
                    break;
                }

                if (term.Inv)
                {
                    currentScore = 0;
                    matched = true;
                }
            }
            if (matched)
            {
                totalScore += currentScore;
            }
            else
            {
                totalScore = 0;
                break;
            }
        }

        return totalScore;
    }
    
    public static FzfResult FzfPrefixMatch(
        bool caseSensitive,
        ReadOnlySpan<char> text,
        ReadOnlySpan<char> pattern,
        Slab slab,
        List<int>? pos)
    {
        if (pattern.Length == 0)
        {
            return new FzfResult { End = -1, Start = -1, Score = 0 };
        }

        var trimmedLen = 0;
        if (!char.IsWhiteSpace(pattern[0]))
        {
            trimmedLen = LeadingWhiteSpace(text);
        }

        if (text.Length - trimmedLen < pattern.Length)
        {
            return new FzfResult { End = -1, Start = -1, Score = 0 };
        }

        for (int i = 0; i < pattern.Length; i++)
        {
            int textIdx = trimmedLen + i;
            char c = text[textIdx];
            char p = pattern[i];

            if (!caseSensitive)
            {
                c = char.ToLowerInvariant(c);
                p = char.ToLowerInvariant(p);
            }
            if (c != p)
            {
                return new FzfResult { End = -1, Start = -1, Score = 0 };
            }
        }

        int start = trimmedLen;
        int end = trimmedLen + pattern.Length;
        int score = CalculateScore(caseSensitive, text, pattern, start, end, null);
        if (pos != null)
        {
            for (var i = start; i < end; i++)
            {
                pos.Add(i);
            }
        }
        return new FzfResult { Start = start, End = end, Score = score };
    }
    
    public static FzfResult FzfSuffixMatch(
        bool caseSensitive,
        ReadOnlySpan<char> text,
        ReadOnlySpan<char> pattern,
        Slab slab,
        List<int>? pos)
    {
        ReadOnlySpan<char> trimmedText = text;
        if (pattern.Length == 0 || !char.IsWhiteSpace(pattern[pattern.Length - 1]))
        {
            trimmedText = TrimTrailingSpaces(text);
        }

        if (pattern.Length == 0)
        {
            return new FzfResult { End = -1, Start = -1, Score = 0 };
        }

        int diff = trimmedText.Length - pattern.Length;
        if (diff < 0)
        {
            return new FzfResult { End = -1, Start = -1, Score = 0 };
        }

        for (int idx = 0; idx < pattern.Length; idx++)
        {
            int textIdx = idx + diff;
            char c = text[textIdx];
            char p = pattern[idx];

            if (!caseSensitive)
            {
                c = char.ToLowerInvariant(c);
                p = char.ToLowerInvariant(p);
            }
            if (c != p)
            {
                return new FzfResult { End = -1, Start = -1, Score = 0 };
            }
        }

        int start = trimmedText.Length - pattern.Length;
        int end = trimmedText.Length;
        int score = CalculateScore(caseSensitive, trimmedText, pattern, start, end, null);
        if (pos != null)
        {
            for (var i = start; i < end; i++)
            {
                pos.Add(i);
            }
        }
        return new FzfResult { End = end, Start = start, Score = score };
    }
    
    public static FzfResult FuzzyMatchV1(
        bool caseSensitive,
        ReadOnlySpan<char> text,
        ReadOnlySpan<char> pattern,
        Slab slab,
        List<int>? pos)
    {
        if (pattern.Length == 0)
        {
            return new FzfResult { End = 0, Start = 0, Score = 0 };
        }
        
        if (FuzzyIndexOf(text, pattern, caseSensitive) < 0)
        {
            return new FzfResult { End = 0, Start = 0, Score = 0 };
        }

        var patternIndex = 0;
        var startIndex = -1;
        var endIndex = -1;

        for (var i = 0; i < text.Length; i++)
        {
            var currentChar = text[i];
            if (!caseSensitive)
            {
                currentChar = char.ToLower(currentChar);
            }

            if (currentChar == pattern[patternIndex])
            {
                if (startIndex < 0)
                {
                    startIndex = i;
                }

                patternIndex++;
                if (patternIndex == pattern.Length)
                {
                    endIndex = i + 1;
                    break;
                }
            }
        }
        
        if (startIndex >= 0 && endIndex >= 0)
        {
            patternIndex--;
            for (var i = endIndex - 1; i >= startIndex; i--)
            {
                var currentChar = text[i];
                if (caseSensitive)
                {
                    currentChar = char.ToLower(currentChar);
                }

                if (currentChar == pattern[patternIndex])
                {
                    patternIndex--;
                    if (patternIndex < 0)
                    {
                        startIndex = i;
                        break;
                    }
                }
            }
        }

        var score = CalculateScore(caseSensitive, text, pattern, startIndex, endIndex, pos);
        
        var result = new FzfResult { End = endIndex, Start = startIndex, Score = score };
        return result;
    }
    
    private static ReadOnlySpan<char> TrimTrailingSpaces(ReadOnlySpan<char> pattern)
    {
        while (pattern.EndsWith(" ") && !pattern.EndsWith("\\ "))
        {
            pattern = pattern.Slice(0, pattern.Length - 1);
        }
        return pattern;
    }

    private static string TrimSuffixSpaces(string input)
    {
        while (input.EndsWith(" ") && !input.EndsWith("\\ "))
        {
            input = input[..^1];
        }
        return input;
    }
    
    private static int BonusAt(ReadOnlySpan<char> input, int idx)
    {
        if (idx == 0) {
            return BoundaryBonus;
        }
        return CalculateBonus(ClassOf(input[idx - 1]), ClassOf(input[idx]));
    }


    private static int LeadingWhiteSpace(ReadOnlySpan<char> text)
    {
        var whiteSpaces = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != ' ')
            {
                break;
            }

            whiteSpaces++;
        }

        return whiteSpaces;
    }
    
    
    private static int FuzzyIndexOf(ReadOnlySpan<char> input, ReadOnlySpan<char> pattern, bool caseSensitive)
    {
        int idx = 0;
        int firstIdx = 0;

        for (var pidx = 0; pidx < pattern.Length; pidx++)
        {
            idx = IndexOfChar(input, pattern[pidx], idx, caseSensitive);
            if (idx < 0)
            {
                return -1;
            }
            if (pidx == 0 && idx > 0)
            {
                firstIdx = idx - 1;
            }
            idx++;
        }

        return firstIdx;
    }
    
    private static int IndexOfChar(ReadOnlySpan<char> input, char value, int startIndex, bool caseSensitive)
    {
        for (int i = startIndex; i < input.Length; i++)
        {
            if (caseSensitive)
            {
                if (input[i] == value)
                {
                    return i;
                }
            }
            else
            {
                if (char.ToUpperInvariant(input[i]) == char.ToUpperInvariant(value))
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static CharClass ClassOf(char ch)
    {
        if (char.IsLower(ch))
        {
            return CharClass.CharLower;
        }

        if (char.IsUpper(ch))
        {
            return CharClass.CharUpper;
        }

        if (char.IsDigit(ch))
        {
            return CharClass.Digit;
        }

        return CharClass.NonWord;
    }

    private static int CalculateBonus(CharClass prevClass, CharClass currentClass)
    {
        if (prevClass == CharClass.NonWord && currentClass != CharClass.NonWord)
        {
            return BoundaryBonus;
        }

        if ((prevClass == CharClass.CharLower && currentClass == CharClass.CharUpper) ||
            (prevClass != CharClass.Digit && currentClass == CharClass.Digit))
        {
            return CamelCaseBonus;
        }

        if (currentClass == CharClass.NonWord)
        {
            return NonWordBonus;
        }

        return 0;
    }

    private static int CalculateScore(
        bool caseSensitive,
        ReadOnlySpan<char> text,
        ReadOnlySpan<char> pattern,
        int startIndex,
        int endIndex,
        List<int>? pos)
    {
        var patternIndex = 0;
        var score = 0;
        var consecutive = 0;
        var inGap = false;
        var firstBonus = 0;

        var prevClass = CharClass.NonWord;
        if (startIndex > 0)
        {
            prevClass = ClassOf(text[startIndex - 1]);
        }

        for (var i = startIndex; i < endIndex; i++)
        {
            var currentChar = text[i];
            var currentClass = ClassOf(currentChar);
            if (!caseSensitive)
            {
                currentChar = char.ToLower(currentChar);
            }

            if (currentChar == pattern[patternIndex])
            {
                if (pos != null)
                {
                    pos.Add(i);
                }
                score += ScoreMatch;
                var bonus = CalculateBonus(prevClass, currentClass);
                if (consecutive == 0)
                {
                    firstBonus = bonus;
                }
                else
                {
                    if (bonus == BoundaryBonus)
                    {
                        firstBonus = bonus;
                    }
                    bonus = int.Max(int.Max(bonus, firstBonus), BonusConsecutive);
                }

                if (patternIndex == 0)
                {
                    score += bonus * BonusFirstCharMultiplier;
                }
                else
                {
                    score += bonus;
                }

                inGap = false;
                consecutive++;
                patternIndex++;
            }
            else
            {
                if (inGap)
                {
                    score += ScoreGapExtension;
                }
                else
                {
                    score += ScoreGapStart;
                }

                inGap = true;
                consecutive = 0;
                firstBonus = 0;
            }

            prevClass = currentClass;
        }

        return score;
    }
}