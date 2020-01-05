using System;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.DiffMatchPatch {
    public static class Patcher {
        public static ImmutableArray<Patch> ComputePatch(string left, string right) {
            return ComputePatch(left.AsSpan(), right.AsSpan());
        }

        public static ImmutableArray<Patch> ComputePatch(ReadOnlySpan<char> left, ReadOnlySpan<char> right) {
            throw new NotImplementedException();
        }

        public static ImmutableArray<Patch> ComputePatch(ImmutableArray<Diff> diffs) {
            throw new NotImplementedException();
        }

        public static ImmutableArray<Patch> ComputePatch(ReadOnlySpan<char> text, ImmutableArray<Diff> diffs) {
            throw new NotImplementedException();
        }

        public static ImmutableArray<Patch> ComputePatch(ReadOnlySpan<char> left, ReadOnlySpan<char> right, ImmutableArray<Diff> diffs) {
            throw new NotImplementedException();
        }

        public static PatchResult ApplyPatch(ImmutableArray<Patch> patches, string text) {
            throw new NotImplementedException();
        }

        public static (bool Success, PatchResult PatchResult) TryApplyPatch(ImmutableArray<Patch> patches, string text) {
            throw new NotImplementedException();
        }
    }

    public class PatchResult {
    }

    public class Patch {
    }
}
