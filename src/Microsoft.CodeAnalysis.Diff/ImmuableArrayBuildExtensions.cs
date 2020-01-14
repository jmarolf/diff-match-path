using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Microsoft.CodeAnalysis.Text
{
    internal static class ImmuableArrayBuildExtensions
    {

        public static void AddIfNotEmpty(this ImmutableArray<Diff>.Builder input, Diff diff) {
            if (diff.Text.Length > 0) {
                input.Add(diff);
            }
        }

        public static ImmutableArray<T>.Builder Splice<T>(this ImmutableArray<T>.Builder input, int start, int count, params T[] rangeToAdd)
        {
            var deletedRange = input.GetRange(start, count);
            var immutableArray = input.ToImmutable();
            immutableArray = immutableArray.RemoveRange(start, count);
            immutableArray = immutableArray.InsertRange(start, rangeToAdd);
            input = immutableArray.ToBuilder();
            return deletedRange;
        }

        public static ImmutableArray<T>.Builder GetRange<T>(this ImmutableArray<T>.Builder input, int index, int count)
        {
            var range = input.ToList().GetRange(index, count);
            var builder = ImmutableArray.CreateBuilder<T>(count);
            builder.AddRange(range);
            return builder;
        }
    }
}
