using Microsoft.CodeAnalysis.DiffMatchPatch;
using Xunit;

namespace Microsoft.CodeAnalysis.Diff.Tests
{
    public class DiffTests
    {
        [Fact]
        public void NoChange()
        {
            var result = Differ.ComputeDiff("aaaaa", "aaaaa");
            var diffResult = Assert.Single(result);
            Assert.Equal(Operation.EQUAL, diffResult.Operation);
        }

        [Fact]
        public void SimpleDelete()
        {
            var result = Differ.ComputeDiff("aaaaa", "");
            var diffResult = Assert.Single(result);
            Assert.Equal(Operation.DELETE, diffResult.Operation);
        }

        [Fact]
        public void SimpleInsert()
        {
            var result = Differ.ComputeDiff("", "aaaaa");
            var diffResult = Assert.Single(result);
            Assert.Equal(Operation.INSERT, diffResult.Operation);
        }
    }
}
