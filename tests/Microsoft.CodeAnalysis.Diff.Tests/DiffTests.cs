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

        [Fact]
        public void NothingInCommon()
        {
            var result = Differ.ComputeDiff("abc", "xyz");
            Assert.Equal(2, result.Length);
            Assert.Equal(Operation.DELETE, result[0].Operation);
            Assert.Equal("abc", result[0].GetText());
            Assert.Equal(Operation.INSERT, result[1].Operation);
            Assert.Equal("xyz", result[1].GetText());
        }

        [Fact]
        public void PrefixInCommon()
        {
            var result = Differ.ComputeDiff("1234abcdef", "1234xyz");
            Assert.Equal(3, result.Length);
            Assert.Equal(Operation.DELETE, result[0].Operation);
            Assert.Equal("abcdef", result[0].GetText());
            Assert.Equal(Operation.INSERT, result[1].Operation);
            Assert.Equal("xyz", result[1].GetText());
            Assert.Equal(Operation.EQUAL, result[2].Operation);
            Assert.Equal("1234", result[2].GetText());
        }

        [Fact]
        public void OnlyPrefixInCommon()
        {
            var result = Differ.ComputeDiff("1234", "1234xyz");
            Assert.Equal(2, result.Length);
            Assert.Equal(Operation.INSERT, result[0].Operation);
            Assert.Equal("xyz", result[0].GetText());
            Assert.Equal(Operation.EQUAL, result[1].Operation);
            Assert.Equal("1234", result[1].GetText());
        }
    }
}
