using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.CodeAnalysis.DiffMatchPatch {
    public static class Differ {
        public static ImmutableArray<Diff> ComputeDiff(string left, string right)
            => ComputeDiff(left.AsSpan(), right.AsSpan());

        public static ImmutableArray<Diff> ComputeDiff(ReadOnlySpan<char> left, ReadOnlySpan<char> right) {
            var builder = ImmutableArray.CreateBuilder<Diff>();
            ComputeDiff(left, right, builder);
            return builder.ToImmutable();
        }

        private static void ComputeDiff(ReadOnlySpan<char> left, ReadOnlySpan<char> right, ImmutableArray<Diff>.Builder builder) {
            // Check for equality
            if (left.SequenceEqual(right)) {
                builder.Add(new Diff(Operation.EQUAL, left));
                return;
            }

            // Trim off common prefix
            int commonPrefixLength = ComputeCommonPrefix(left, right);

            var commonPrefix = left.Slice(0, commonPrefixLength);
            left = left.Slice(commonPrefixLength);
            right = right.Slice(commonPrefixLength);

            // Trim off common suffix
            int commonSuffixLength = ComputeCommonSuffix(left, right);
            var commonSuffix = left.Slice(left.Length - commonSuffixLength);
            left = left.Slice(0, left.Length - commonSuffixLength);
            right = right.Slice(0, right.Length - commonSuffixLength);

            // Compute the diff on the middle block.
            ComputeDiffImpl(left, right, builder);

            // Restore the prefix and suffix.
            if (commonPrefix.Length > 0) {
                builder.Add(new Diff(Operation.EQUAL, commonPrefix));
            }
            if (commonSuffix.Length > 0) {
                builder.Add(new Diff(Operation.EQUAL, commonSuffix));
            }

            CleanUpMerge(builder);
        }

        private static int ComputeCommonPrefix(ReadOnlySpan<char> left, ReadOnlySpan<char> right) {
            int minLength = Math.Min(left.Length, right.Length);
            for (int i = 0; i < minLength; i++) {
                if (left[i] != right[i]) {
                    return i;
                }
            }

            return minLength;
        }

        private static int ComputeCommonSuffix(ReadOnlySpan<char> left, ReadOnlySpan<char> right) {
            int leftLength = left.Length;
            int rightLength = right.Length;
            int minLength = Math.Min(leftLength, rightLength);
            for (int i = 1; i <= minLength; i++) {
                if (left[leftLength - i] != right[rightLength - i]) {
                    return i - 1;
                }
            }
            return minLength;
        }

        private static void ComputeDiffImpl(ReadOnlySpan<char> left, ReadOnlySpan<char> right, ImmutableArray<Diff>.Builder builder) {
            // Only text was added
            if (left.Length == 0) {
                builder.Add(new Diff(Operation.INSERT, right));
                return;
            }

            // Only text was deleted
            if (right.Length == 0) {
                builder.Add(new Diff(Operation.DELETE, right));
                return;
            }

            var longText = left.Length > right.Length ? left : right;
            var shortText = left.Length > right.Length ? right : left;
            int index = longText.IndexOf(shortText, StringComparison.Ordinal);

            // Shorter text is inside the longer text
            if (index != -1) {
                Operation op = (left.Length > right.Length) ? Operation.DELETE : Operation.INSERT;
                builder.Add(new Diff(op, longText.Slice(0, index)));
                builder.Add(new Diff(Operation.EQUAL, shortText));
                builder.Add(new Diff(op, longText.Slice(index + shortText.Length)));
                return;
            }

            // Single character string.
            // After the previous speedup, the character can't be an equality.
            if (shortText.Length == 1) {
                builder.Add(new Diff(Operation.DELETE, left));
                builder.Add(new Diff(Operation.INSERT, right));
                return;
            }

            // Check to see if the problem can be split in two.
            var success = TryComputeHalfMatch(left, right, out var leftA, out var leftB, out var rightA, out var rightB, out var common);
            if (success) {
                ComputeDiff(leftA, rightA, builder);
                builder.Add(new Diff(Operation.EQUAL, common));
                ComputeDiff(leftB, rightB, builder);
                return;
            }

            if (left.Length > 100 && right.Length > 100) {
                ComputeLineModeDiff(left, right, builder);
                return;
            }

            Bisect(left, right, builder);
        }

        private static bool TryComputeHalfMatch(ReadOnlySpan<char> left, ReadOnlySpan<char> right,
                                                out ReadOnlySpan<char> leftA,
                                                out ReadOnlySpan<char> leftB,
                                                out ReadOnlySpan<char> rightA,
                                                out ReadOnlySpan<char> rightB,
                                                out ReadOnlySpan<char> common) {


            leftA = default;
            leftB = default;
            rightA = default;
            rightB = default;
            common = default;
            var longText = left.Length > right.Length ? left : right;
            var shortText = left.Length > right.Length ? right : left;
            if (longText.Length < 4 || shortText.Length * 2 < longText.Length) {
                return false; // Pointless.
            }

            // First check if the second quarter is the seed for a half-match.
            var halfMatch1 = ComputeHalfMatch(longText, shortText, (longText.Length + 3) / 4, out var bestLongTextA1, out var bestLongTextB1, out var bestShortTextA1, out var bestShortTextB1, out var bestCommon1);

            // Check again based on the third quarter.
            var halfMatch2 = ComputeHalfMatch(longText, shortText, (longText.Length + 1) / 2, out var bestLongTextA2, out var bestLongTextB2, out var bestShortTextA2, out var bestShortTextB2, out var bestCommon2);

            if (!halfMatch1 && !halfMatch2) {
                return false;
            }
            else if (!halfMatch2) {
                leftA = bestLongTextA1;
                leftB = bestLongTextB1;
                rightA = bestShortTextA1;
                rightB = bestShortTextB1;
                common = bestCommon1;
            }
            else if (!halfMatch1) {
                leftA = bestLongTextA2;
                leftB = bestLongTextB2;
                rightA = bestShortTextA2;
                rightB = bestShortTextB2;
                common = bestCommon2;
            }
            else {
                // Both matched.  Select the longest.
                if (bestCommon1.Length > bestCommon2.Length) {
                    leftA = bestLongTextA1;
                    leftB = bestLongTextB1;
                    rightA = bestShortTextA1;
                    rightB = bestShortTextB1;
                    common = bestCommon1;
                }
                else {
                    leftA = bestLongTextA2;
                    leftB = bestLongTextB2;
                    rightA = bestShortTextA2;
                    rightB = bestShortTextB2;
                    common = bestCommon2;
                }
            }

            return true;
        }

        private static bool ComputeHalfMatch(ReadOnlySpan<char> longText,
                                             ReadOnlySpan<char> shortText,
                                             int index,
                                             out ReadOnlySpan<char> bestLongTextA,
                                             out ReadOnlySpan<char> bestLongTextB,
                                             out ReadOnlySpan<char> bestShortTextA,
                                             out ReadOnlySpan<char> bestShortTextB,
                                             out ReadOnlySpan<char> bestCommon) {
            // Start with a 1/4 length Substring at position i as a seed.
            var seed = longText.Slice(index, longText.Length / 4);
            int j = -1;
            bestCommon = default;
            bestLongTextA = default;
            bestLongTextB = default;
            bestShortTextA = default;
            bestShortTextB = default;
            while (j < shortText.Length && (j = shortText.Slice(j + 1).IndexOf(seed)) != -1) {
                int prefixLength = ComputeCommonPrefix(longText.Slice(index), shortText.Slice(j));
                int suffixLength = ComputeCommonSuffix(longText.Slice(0, index), shortText.Slice(0, j));
                if (bestCommon.Length < suffixLength + prefixLength) {
                    bestCommon = (new string(shortText.Slice(j - suffixLength, suffixLength)) + new string(shortText.Slice(j, prefixLength))).AsSpan();
                    bestLongTextA = longText.Slice(0, index - suffixLength);
                    bestLongTextB = longText.Slice(index + prefixLength);
                    bestShortTextA = shortText.Slice(0, j - suffixLength);
                    bestShortTextB = shortText.Slice(j + prefixLength);
                }
            }
            if (bestCommon.Length * 2 >= longText.Length) {
                return true;
            }
            else {
                return false;
            }
        }

        private static void ComputeLineModeDiff(ReadOnlySpan<char> left, ReadOnlySpan<char> right, ImmutableArray<Diff>.Builder builder) {
            ComputeDiffLines(left, right, out var newLeft, out var newRight, out var linesArray);
            ComputeDiff(newRight, newLeft, builder);

            // Convert the diff back to original text.
            ConvertCharsToLines(builder, linesArray);
            // Eliminate freak matches (e.g. blank lines)
            CleanUpSemantic(builder);

            // Rediff any replacement blocks, this time character-by-character.
            // Add a dummy entry at the end.
            builder.Add(new Diff(Operation.EQUAL, string.Empty));
            int pointer = 0;
            int deleteCount = 0;
            int insertCount = 0;
            int? deletedTextStartIndex = default;
            int deletedTextEndIndex = default;
            int? insertedTextStartIndex = default;
            int insertedTextEndIndex = default;
            while (pointer < builder.Count) {
                switch (builder[pointer].Operation) {
                    case Operation.INSERT:
                        insertCount++;
                        if (insertedTextStartIndex == null) {
                            insertedTextStartIndex = builder[pointer].StartIndex;
                        }
                        insertedTextEndIndex = builder[pointer].EndIndex;
                        break;
                    case Operation.DELETE:
                        deleteCount++;
                        if (deletedTextStartIndex == null) {
                            deletedTextStartIndex = builder[pointer].StartIndex;
                        }
                        deletedTextEndIndex = builder[pointer].EndIndex;
                        break;
                    case Operation.EQUAL:
                        // Upon reaching an equality, check for prior redundancies.
                        if (deleteCount >= 1 && insertCount >= 1) {
                            // Delete the offending records and add the merged ones.
                            for (int i = pointer - deleteCount - insertCount; i < deleteCount + insertCount; i++) {
                                builder.RemoveAt(i);
                            }

                            pointer = pointer - deleteCount - insertCount;
                            var newBuilder = ImmutableArray.CreateBuilder<Diff>();
                            ComputeDiff(left.Slice(deletedTextStartIndex ?? 0, deletedTextEndIndex), right.Slice(insertedTextStartIndex ?? 0, insertedTextEndIndex), newBuilder);
                            for (int i = 0; i < newBuilder.Count; i++) {
                                builder.Insert(i + pointer, newBuilder[i]);
                            }
                            pointer = pointer + newBuilder.Count;
                        }
                        insertCount = 0;
                        deleteCount = 0;
                        deletedTextStartIndex = default;
                        deletedTextEndIndex = default;
                        insertedTextStartIndex = default;
                        insertedTextEndIndex = default;
                        break;
                    default:
                        break;
                }
                pointer++;
            }
        }

        private static void ComputeDiffLines(ReadOnlySpan<char> left, ReadOnlySpan<char> right, out ReadOnlySpan<char> newLeft, out ReadOnlySpan<char> newRight, out IList<string> lines) {
            lines = new List<string>();
            var lineHash = new Dictionary<string, int>();
            // e.g. linearray[4] == "Hello\n"
            // e.g. linehash.get("Hello\n") == 4

            // "\x00" is a valid character, but various debuggers don't like it.
            // So we'll insert a junk entry to avoid generating a null character.
            lines.Add(string.Empty);// Allocate 2/3rds of the space for text1, the rest for text2.
            newLeft = ComputeDiffLines(left, lines, lineHash, 40000);
            newRight = ComputeDiffLines(right, lines, lineHash, 65535);
        }

        // TODO: refactor to return a ref ReadOnlySpan<char>
        private static string ComputeDiffLines(ReadOnlySpan<char> text, IList<string> lines, Dictionary<string, int> lineHash, int maxLines) {
            int lineStart = 0;
            int lineEnd = -1;
            string line;
            StringBuilder chars = new StringBuilder();

            // Walk the text, pulling out a Substring for each line.
            // text.split('\n') would would temporarily double our memory footprint.
            // Modifying text would create many large strings to garbage collect.
            while (lineEnd < text.Length - 1) {
                lineEnd = text.Slice(lineStart).IndexOf('\n');
                if (lineEnd == -1) {
                    lineEnd = text.Length - 1;
                }
                line = new string(text.Slice(lineStart, (lineEnd - lineStart) + 1));

                if (lineHash.ContainsKey(line)) {
                    chars.Append(((char)(int)lineHash[line]));
                }
                else {
                    if (lines.Count == maxLines) {
                        // Bail out at 65535 because char 65536 == char 0.
                        line = new string(text.Slice(lineStart));
                        lineEnd = text.Length;
                    }
                    lines.Add(line);
                    lineHash.Add(line, lines.Count - 1);
                    chars.Append(((char)(lines.Count - 1)));
                }
                lineStart = lineEnd + 1;
            }
            return chars.ToString();
        }

        private static void ConvertCharsToLines(ImmutableArray<Diff>.Builder builder, IList<string> lines) {
            StringBuilder text;
            foreach (Diff diff in builder) {
                text = new StringBuilder();
                for (int j = 0; j < diff.Text.Length; j++) {
                    text.Append(lines[diff.Text.Span[j]]);
                }
                diff.Text = text.ToString().ToArray();
            }
        }

        private static void Bisect(ReadOnlySpan<char> left, ReadOnlySpan<char> right, ImmutableArray<Diff>.Builder builder) {
            // Cache the text lengths to prevent multiple calls.
            int leftLength = left.Length;
            int rightLength = right.Length;
            int max_d = (leftLength + rightLength + 1) / 2;
            int v_offset = max_d;
            int v_length = 2 * max_d;
            int[] v1 = new int[v_length];
            int[] v2 = new int[v_length];
            for (int x = 0; x < v_length; x++) {
                v1[x] = -1;
                v2[x] = -1;
            }
            v1[v_offset + 1] = 0;
            v2[v_offset + 1] = 0;
            int delta = leftLength - rightLength;
            // If the total number of characters is odd, then the front path will
            // collide with the reverse path.
            bool front = (delta % 2 != 0);
            // Offsets for start and end of k loop.
            // Prevents mapping of space beyond the grid.
            int k1start = 0;
            int k1end = 0;
            int k2start = 0;
            int k2end = 0;
            for (int d = 0; d < max_d; d++) {
                // Walk the front path one step.
                for (int k1 = -d + k1start; k1 <= d - k1end; k1 += 2) {
                    int k1_offset = v_offset + k1;
                    int x1;
                    if (k1 == -d || k1 != d && v1[k1_offset - 1] < v1[k1_offset + 1]) {
                        x1 = v1[k1_offset + 1];
                    }
                    else {
                        x1 = v1[k1_offset - 1] + 1;
                    }
                    int y1 = x1 - k1;
                    while (x1 < leftLength && y1 < rightLength
                          && left[x1] == right[y1]) {
                        x1++;
                        y1++;
                    }
                    v1[k1_offset] = x1;
                    if (x1 > leftLength) {
                        // Ran off the right of the graph.
                        k1end += 2;
                    }
                    else if (y1 > rightLength) {
                        // Ran off the bottom of the graph.
                        k1start += 2;
                    }
                    else if (front) {
                        int k2_offset = v_offset + delta - k1;
                        if (k2_offset >= 0 && k2_offset < v_length && v2[k2_offset] != -1) {
                            // Mirror x2 onto top-left coordinate system.
                            int x2 = leftLength - v2[k2_offset];
                            if (x1 >= x2) {
                                // Overlap detected.
                                BisectSplit(left, right, x1, y1, builder);
                                return;
                            }
                        }
                    }
                }

                // Walk the reverse path one step.
                for (int k2 = -d + k2start; k2 <= d - k2end; k2 += 2) {
                    int k2_offset = v_offset + k2;
                    int x2;
                    if (k2 == -d || k2 != d && v2[k2_offset - 1] < v2[k2_offset + 1]) {
                        x2 = v2[k2_offset + 1];
                    }
                    else {
                        x2 = v2[k2_offset - 1] + 1;
                    }
                    int y2 = x2 - k2;
                    while (x2 < leftLength && y2 < rightLength
                        && left[leftLength - x2 - 1]
                        == right[rightLength - y2 - 1]) {
                        x2++;
                        y2++;
                    }
                    v2[k2_offset] = x2;
                    if (x2 > leftLength) {
                        // Ran off the left of the graph.
                        k2end += 2;
                    }
                    else if (y2 > rightLength) {
                        // Ran off the top of the graph.
                        k2start += 2;
                    }
                    else if (!front) {
                        int k1_offset = v_offset + delta - k2;
                        if (k1_offset >= 0 && k1_offset < v_length && v1[k1_offset] != -1) {
                            int x1 = v1[k1_offset];
                            int y1 = v_offset + x1 - k1_offset;
                            // Mirror x2 onto top-left coordinate system.
                            x2 = leftLength - v2[k2_offset];
                            if (x1 >= x2) {
                                // Overlap detected.
                                BisectSplit(left, right, x1, y1, builder);
                                return;
                            }
                        }
                    }
                }
            }
            // Diff took too long and hit the deadline or
            // number of diffs equals number of characters, no commonality at all.
            builder.Add(new Diff(Operation.DELETE, left));
            builder.Add(new Diff(Operation.INSERT, right));
        }

        private static void BisectSplit(ReadOnlySpan<char> left, ReadOnlySpan<char> right, int x, int y, ImmutableArray<Diff>.Builder builder)
        {
            var text1a = left.Slice(0, x);
            var text2a = right.Slice(0, y);
            var text1b = left.Slice(x);
            var text2b = right.Slice(y);

            ComputeDiffImpl(text1a, text2a, builder);
            ComputeDiffImpl(text1b, text2b, builder);
        }

        private static void CleanUpSemantic(ImmutableArray<Diff>.Builder builder) {
            bool changes = false;
            // Stack of indices where equalities are found.
            Stack<int> equalities = new Stack<int>();
            // Always equal to equalities[equalitiesLength-1][1]
            (int startIndex, int endIndex) lastEquality = default;
            int pointer = 0;  // Index of current position.
            // Number of characters that changed prior to the equality.
            int length_insertions1 = 0;
            int length_deletions1 = 0;
            // Number of characters that changed after the equality.
            int length_insertions2 = 0;
            int length_deletions2 = 0;
            while (pointer < builder.Count) {
                if (builder[pointer].Operation == Operation.EQUAL) { // Equality found.
                    equalities.Push(pointer);
                    length_insertions1 = length_insertions2;
                    length_deletions1 = length_deletions2;
                    length_insertions2 = 0;
                    length_deletions2 = 0;
                    lastEquality = (builder[pointer].StartIndex, builder[pointer].EndIndex);
                }
                else {
                    if (builder[pointer].Operation == Operation.INSERT) {
                        length_insertions2 += builder[pointer].Length;
                    }
                    else {
                        length_deletions2 += builder[pointer].Length;
                    }
                    // Eliminate an equality that is smaller or equal to the edits on both
                    // sides of it.
                    if (lastEquality != default &&
                       ((lastEquality.endIndex - lastEquality.startIndex) <= Math.Max(length_insertions1, length_deletions1)) &&
                       ((lastEquality.endIndex - lastEquality.startIndex) <= Math.Max(length_insertions2, length_deletions2))) {
                        // Duplicate record.
                        builder.Insert(equalities.Peek(), new Diff(Operation.DELETE, lastEquality.startIndex, lastEquality.endIndex));
                        // Change second copy to insert.
                        builder[equalities.Peek() + 1].Operation = Operation.INSERT;
                        // Throw away the equality we just deleted.
                        equalities.Pop();
                        if (equalities.Count > 0) {
                            equalities.Pop();
                        }
                        pointer = equalities.Count > 0 ? equalities.Peek() : -1;
                        length_insertions1 = 0;  // Reset the counters.
                        length_deletions1 = 0;
                        length_insertions2 = 0;
                        length_deletions2 = 0;
                        lastEquality = default;
                        changes = true;
                    }
                }
                pointer++;
            }

            // Normalize the diff.
            if (changes) {
                CleanUpMerge(builder);
            }

            CleanupSemanticLossless(builder);

            // Find any overlaps between deletions and insertions.
            // e.g: <del>abcxxx</del><ins>xxxdef</ins>
            //   -> <del>abc</del>xxx<ins>def</ins>
            // e.g: <del>xxxabc</del><ins>defxxx</ins>
            //   -> <ins>def</ins>xxx<del>abc</del>
            // Only extract an overlap if it is as big as the edit ahead or behind it.
            pointer = 1;
            while (pointer < builder.Count) {
                if (builder[pointer - 1].Operation == Operation.DELETE &&
                    builder[pointer].Operation == Operation.INSERT) {
                    var deletion = builder[pointer - 1].Text.Span;
                    var insertion = builder[pointer].Text.Span;
                    int overlap_length1 = ComputeCommonOverlap(deletion, insertion);
                    int overlap_length2 = ComputeCommonOverlap(insertion, deletion);
                    if (overlap_length1 >= overlap_length2) {
                        if (overlap_length1 >= deletion.Length / 2.0 ||
                            overlap_length1 >= insertion.Length / 2.0) {
                            // Overlap found.
                            // Insert an equality and trim the surrounding edits.
                            builder.Insert(pointer, new Diff(Operation.EQUAL,
                                insertion.Slice(0, overlap_length1)));
                            builder[pointer - 1].Text = deletion.Slice(0, deletion.Length - overlap_length1).ToArray();
                            builder[pointer + 1].Text = insertion.Slice(overlap_length1).ToArray();
                            pointer++;
                        }
                    }
                    else {
                        if (overlap_length2 >= deletion.Length / 2.0 ||
                            overlap_length2 >= insertion.Length / 2.0) {
                            // Reverse overlap found.
                            // Insert an equality and swap and trim the surrounding edits.
                            builder.Insert(pointer, new Diff(Operation.EQUAL,
                                deletion.Slice(0, overlap_length2)));
                            builder[pointer - 1].Operation = Operation.INSERT;
                            builder[pointer - 1].Text = insertion.Slice(0, insertion.Length - overlap_length2).ToArray();
                            builder[pointer + 1].Operation = Operation.DELETE;
                            builder[pointer + 1].Text = deletion.Slice(overlap_length2).ToArray();
                            pointer++;
                        }
                    }
                    pointer++;
                }
                pointer++;
            }
        }

        private static int ComputeCommonOverlap(ReadOnlySpan<char> left, ReadOnlySpan<char> right) {
            int leftLength = left.Length;
            int rightLength = right.Length;

            // Eliminate the empty string case.
            if (leftLength == 0 || rightLength == 0) {
                return 0;
            }

            // Truncate the longer string.
            if (leftLength > rightLength) {
                left = left.Slice(leftLength - rightLength);
            }
            else if (leftLength < rightLength) {
                right = right.Slice(0, leftLength);
            }
            int textLength = Math.Min(leftLength, rightLength);

            // Quick check for the worst case.
            if (left.SequenceEqual(right)) {
                return textLength;
            }

            // Start by looking for a single character match
            // and increase length until no match is found.
            int best = 0;
            int length = 1;
            while (true) {
                var pattern = left.Slice(textLength - length);
                int found = right.IndexOf(pattern);
                if (found == -1) {
                    return best;
                }
                length += found;
                if (found == 0 || left.Slice(textLength - length).SequenceEqual(right.Slice(0, length))) {
                    best = length;
                    length++;
                }
            }
        }

        private static void CleanupSemanticLossless(ImmutableArray<Diff>.Builder builder) {
            int pointer = 1;
            // Intentionally ignore the first and last element (don't need checking).
            while (pointer < builder.Count - 1) {
                if (builder[pointer - 1].Operation == Operation.EQUAL &&
                    builder[pointer + 1].Operation == Operation.EQUAL) {
                    // This is a single edit surrounded by equalities.
                    ReadOnlySpan<char> equality1 = builder[pointer - 1].Text.Span;
                    ReadOnlySpan<char> edit = builder[pointer].Text.Span;
                    ReadOnlySpan<char> equality2 = builder[pointer + 1].Text.Span;

                    // First, shift the edit as far left as possible.
                    int commonOffset = ComputeCommonSuffix(equality1, edit);
                    if (commonOffset > 0) {
                        var commonString = edit.Slice(edit.Length - commonOffset);
                        equality1 = equality1.Slice(0, equality1.Length - commonOffset);
                        edit = (new string(commonString) + new string(edit.Slice(0, edit.Length - commonOffset))).AsSpan();
                        equality2 = (new string(commonString) + new string(equality2)).AsSpan();
                    }

                    // Second, step character by character right,
                    // looking for the best fit.
                    var bestEquality1 = equality1;
                    var bestEdit = edit;
                    var bestEquality2 = equality2;
                    int bestScore = CleanupSemanticScore(equality1, edit) + CleanupSemanticScore(edit, equality2);
                    while (edit.Length != 0 && equality2.Length != 0 && edit[0] == equality2[0]) {
                        equality1 = (new string(equality1) + edit[0]).AsSpan();
                        edit = (new string(edit.Slice(1)) + equality2[0]).AsSpan();
                        equality2 = equality2.Slice(1);
                        int score = CleanupSemanticScore(equality1, edit) + CleanupSemanticScore(edit, equality2);

                        // The >= encourages trailing rather than leading whitespace on
                        // edits.
                        if (score >= bestScore) {
                            bestScore = score;
                            bestEquality1 = equality1;
                            bestEdit = edit;
                            bestEquality2 = equality2;
                        }
                    }

                    if (!builder[pointer - 1].Text.Span.SequenceEqual(bestEquality1)) {
                        // We have an improvement, save it back to the diff.
                        if (bestEquality1.Length != 0) {
                            builder[pointer - 1].Text = bestEquality1.ToArray();
                        }
                        else {
                            builder.RemoveAt(pointer - 1);
                            pointer--;
                        }
                        builder[pointer].Text = bestEdit.ToArray();
                        if (bestEquality2.Length != 0) {
                            builder[pointer + 1].Text = bestEquality2.ToArray();
                        }
                        else {
                            builder.RemoveAt(pointer + 1);
                            pointer--;
                        }
                    }
                }
                pointer++;
            }
        }

        private static int CleanupSemanticScore(ReadOnlySpan<char> left, ReadOnlySpan<char> right) {
            if (left.Length == 0 || right.Length == 0) {
                // Edges are the best.
                return 6;
            }

            char char1 = left[left.Length - 1];
            char char2 = right[0];
            bool nonAlphaNumeric1 = !Char.IsLetterOrDigit(char1);
            bool nonAlphaNumeric2 = !Char.IsLetterOrDigit(char2);
            bool whitespace1 = nonAlphaNumeric1 && Char.IsWhiteSpace(char1);
            bool whitespace2 = nonAlphaNumeric2 && Char.IsWhiteSpace(char2);
            bool lineBreak1 = whitespace1 && Char.IsControl(char1);
            bool lineBreak2 = whitespace2 && Char.IsControl(char2);
            bool blankLine1 = lineBreak1 && BLANKLINEEND.IsMatch(new string(left));
            bool blankLine2 = lineBreak2 && BLANKLINESTART.IsMatch(new string(right));

            if (blankLine1 || blankLine2) {
                // Five points for blank lines.
                return 5;
            }
            else if (lineBreak1 || lineBreak2) {
                // Four points for line breaks.
                return 4;
            }
            else if (nonAlphaNumeric1 && !whitespace1 && whitespace2) {
                // Three points for end of sentences.
                return 3;
            }
            else if (whitespace1 || whitespace2) {
                // Two points for whitespace.
                return 2;
            }
            else if (nonAlphaNumeric1 || nonAlphaNumeric2) {
                // One point for non-alphanumeric.
                return 1;
            }
            return 0;
        }

        // Define some regex patterns for matching boundaries.
        private static Regex BLANKLINEEND = new Regex("\\n\\r?\\n\\Z", RegexOptions.Compiled);
        private static Regex BLANKLINESTART = new Regex("\\A\\r?\\n\\r?\\n", RegexOptions.Compiled);

        private static void CleanUpMerge(ImmutableArray<Diff>.Builder builder) {
            // Add a dummy entry at the end.
            builder.Add(new Diff(Operation.EQUAL, string.Empty));
            int pointer = 0;
            int count_delete = 0;
            int count_insert = 0;
            string text_delete = string.Empty;
            string text_insert = string.Empty;
            int commonlength;
            while (pointer < builder.Count) {
                switch (builder[pointer].Operation) {
                    case Operation.INSERT:
                        count_insert++;
                        text_insert += builder[pointer].Text;
                        pointer++;
                        break;
                    case Operation.DELETE:
                        count_delete++;
                        text_delete += builder[pointer].Text;
                        pointer++;
                        break;
                    case Operation.EQUAL:
                        // Upon reaching an equality, check for prior redundancies.
                        if (count_delete + count_insert > 1) {
                            if (count_delete != 0 && count_insert != 0) {
                                // Factor out any common prefixies.
                                commonlength = ComputeCommonPrefix(text_insert, text_delete);
                                if (commonlength != 0) {
                                    if ((pointer - count_delete - count_insert) > 0 &&
                                      builder[pointer - count_delete - count_insert - 1].Operation == Operation.EQUAL) {
                                        var existing = new string(builder[pointer - count_delete - count_insert - 1].Text.ToArray());
                                        existing += new string(text_insert.AsSpan().Slice(0, commonlength));
                                        builder[pointer - count_delete - count_insert - 1].Text = existing.AsMemory();
                                    }
                                    else {
                                        builder.Insert(0, new Diff(Operation.EQUAL,
                                            text_insert.Substring(0, commonlength)));
                                        pointer++;
                                    }
                                    text_insert = text_insert.Substring(commonlength);
                                    text_delete = text_delete.Substring(commonlength);
                                }
                                // Factor out any common suffixies.
                                commonlength = ComputeCommonSuffix(text_insert, text_delete);
                                if (commonlength != 0) {
                                    builder[pointer].Text = (text_insert.Substring(text_insert.Length - commonlength) + new string(builder[pointer].Text.ToArray())).AsMemory();
                                    text_insert = text_insert.Substring(0, text_insert.Length
                                        - commonlength);
                                    text_delete = text_delete.Substring(0, text_delete.Length
                                        - commonlength);
                                }
                            }
                            // Delete the offending records and add the merged ones.
                            pointer -= count_delete + count_insert;
                            builder.Splice(pointer, count_delete + count_insert);
                            if (text_delete.Length != 0) {
                                builder.Splice(pointer, 0,
                                    new Diff(Operation.DELETE, text_delete));
                                pointer++;
                            }
                            if (text_insert.Length != 0) {
                                builder.Splice(pointer, 0,
                                    new Diff(Operation.INSERT, text_insert));
                                pointer++;
                            }
                            pointer++;
                        }
                        else if (pointer != 0
                          && builder[pointer - 1].Operation == Operation.EQUAL) {
                            // Merge this equality with the previous one.
                            builder[pointer - 1].Text = (new string(builder[pointer - 1].Text.ToArray()) + new string(builder[pointer].Text.ToArray())).AsMemory();
                            builder.RemoveAt(pointer);
                        }
                        else {
                            pointer++;
                        }
                        count_insert = 0;
                        count_delete = 0;
                        text_delete = string.Empty;
                        text_insert = string.Empty;
                        break;
                }
            }
            if (builder[builder.Count - 1].Text.Length == 0) {
                builder.RemoveAt(builder.Count - 1);  // Remove the dummy entry at the end.
            }

            // Second pass: look for single edits surrounded on both sides by
            // equalities which can be shifted sideways to eliminate an equality.
            // e.g: A<ins>BA</ins>C -> <ins>AB</ins>AC
            bool changes = false;
            pointer = 1;
            // Intentionally ignore the first and last element (don't need checking).
            while (pointer < (builder.Count - 1)) {
                if (builder[pointer - 1].Operation == Operation.EQUAL &&
                  builder[pointer + 1].Operation == Operation.EQUAL) {
                    // This is a single edit surrounded by equalities.
                    if (builder[pointer].Text.Span.EndsWith(builder[pointer - 1].Text.Span,
                        StringComparison.Ordinal)) {
                        // Shift the edit over the previous equality.
                        string text1 = new string(builder[pointer - 1].Text.ToArray());
                        string text2 = new string(builder[pointer].Text.Slice(0, builder[pointer].Text.Length -builder[pointer - 1].Text.Length).ToArray());
                        builder[pointer].Text = (text1 + text2).AsMemory();
                        builder[pointer + 1].Text = (new string(builder[pointer - 1].Text.ToArray()) + new string(builder[pointer + 1].Text.ToArray())).AsMemory();
                        builder.Splice(pointer - 1, 1);
                        changes = true;
                    }
                    else if (builder[pointer].Text.Span.StartsWith(builder[pointer + 1].Text.Span,
                      StringComparison.Ordinal)) {
                        // Shift the edit over the next equality.
                        var text1 = new string(builder[pointer - 1].Text.ToArray());
                        var text2 = new string(builder[pointer + 1].Text.ToArray());
                        builder[pointer - 1].Text = (text1 + text2).AsMemory();
                        builder[pointer].Text =
                            (new string(builder[pointer].Text.Slice(builder[pointer + 1].Text.Length).ToArray())
                            + new string(builder[pointer + 1].Text.ToArray())).AsMemory();
                        builder.Splice(pointer + 1, 1);
                        changes = true;
                    }
                }
                pointer++;
            }
            // If shifts were made, the diff needs reordering and another shift sweep.
            if (changes) {
                CleanUpMerge(builder);
            }
        }
    }

    public class Diff : IEquatable<Diff> {
        public Operation Operation { get; set; }
        public ReadOnlyMemory<char> Text { get; set; }

        public int StartIndex { get; }
        public int EndIndex { get; }
        public int Length => EndIndex - StartIndex;

        public string GetText() { return new string(Text.ToArray()); }

        public Diff(Operation operation, ReadOnlySpan<char> text) {
            Operation = operation;
            Text = text.ToArray();
        }

        public Diff(Operation operation, int startIndex, int endIndex) {
            Operation = operation;
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public override int GetHashCode() {
            return HashCode.Combine(Operation, Text);
        }

        public override bool Equals(object? obj) {
            return base.Equals(obj);
        }

        public static bool operator ==(Diff left, Diff right) {
            return left.Equals(right);
        }

        public static bool operator !=(Diff left, Diff right) {
            return !(left == right);
        }

        public bool Equals([AllowNull] Diff other) {
            throw new NotImplementedException();
        }
    }

    public enum Operation {
        DELETE, INSERT, EQUAL
    }
}
