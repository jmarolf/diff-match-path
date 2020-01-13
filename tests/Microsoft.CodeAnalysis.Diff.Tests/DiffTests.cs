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

        [Fact]
        public void SuffixInCommon()
        {
            var result = Differ.ComputeDiff("abcdef1234", "xyz1234");
            Assert.Equal(3, result.Length);
            Assert.Equal(Operation.DELETE, result[0].Operation);
            Assert.Equal("abcdef", result[0].GetText());
            Assert.Equal(Operation.INSERT, result[1].Operation);
            Assert.Equal("xyz", result[1].GetText());
            Assert.Equal(Operation.EQUAL, result[2].Operation);
            Assert.Equal("1234", result[2].GetText());
        }

        [Fact]
        public void OnlySuffixInCommon()
        {
            var result = Differ.ComputeDiff("1234", "xyz1234");
            Assert.Equal(2, result.Length);
            Assert.Equal(Operation.INSERT, result[0].Operation);
            Assert.Equal("xyz", result[0].GetText());
            Assert.Equal(Operation.EQUAL, result[1].Operation);
            Assert.Equal("1234", result[1].GetText());
        }

        [Fact]
        public void CommonOverlap()
        {
            var result = Differ.ComputeDiff("abc", "abcd");
            Assert.Equal(2, result.Length);
            Assert.Equal(Operation.INSERT, result[0].Operation);
            Assert.Equal("d", result[0].GetText());
            Assert.Equal(Operation.EQUAL, result[1].Operation);
            Assert.Equal("abc", result[1].GetText());
        }

        [Fact]
        public void CommonOverlap2()
        {
            var result = Differ.ComputeDiff("123456xxx", "xxxabcd");
        }

        [Fact]
        public void CommonOverlapInside()
        {
            var result = Differ.ComputeDiff("12345", "23");
        }

        [Fact]
        public void CommonOverlapInside2()
        {
            var result = Differ.ComputeDiff("1234567890", "a345678z");
        }

        [Fact]
        public void CommonOverlapInside3()
        {
            var result = Differ.ComputeDiff("a345678z", "1234567890");
        }

        [Fact]
        public void CommonOverlapInside4()
        {
            var result = Differ.ComputeDiff("abc56789z", "1234567890");
        }

        [Fact]
        public void CommonOverlapInside5()
        {
            var result = Differ.ComputeDiff("a23456xyz", "1234567890");
        }

        [Fact]
        public void CommonOverlapInside6()
        {
            var result = Differ.ComputeDiff("121231234123451234123121", "a1234123451234z");
        }

        [Fact]
        public void CommonOverlapInside7()
        {
            var result = Differ.ComputeDiff("x-=-=-=-=-=-=-=-=-=-=-=-=", "xx-=-=-=-=-=-=-=");
        }


        [Fact]
        public void CommonOverlapInside8()
        {
            var result = Differ.ComputeDiff("-=-=-=-=-=-=-=-=-=-=-=-=y", "-=-=-=-=-=-=-=yy");
        }

        [Fact]
        public void CommonOverlapInside9()
        {
            var result = Differ.ComputeDiff("qHilloHelloHew", "xHelloHeHulloy");
        }

        [Fact]
        public void MultilineDiff()
        {
            var result = Differ.ComputeDiff("alpha\nbeta\nalpha\n", "beta\nalpha\nbeta\n");
        }

        [Fact]
        public void Insert1()
        {
            var result = Differ.ComputeDiff("abc", "ab123c");
        }

        [Fact]
        public void Insert2()
        {
            var result = Differ.ComputeDiff("abc", "a123b456c");
        }

        [Fact]
        public void Delete1()
        {
            var result = Differ.ComputeDiff("a123bc", "abc");
        }

        [Fact]
        public void Delete2()
        {
            var result = Differ.ComputeDiff("a123b456c", "abc");
        }

        [Fact]
        public void SimpleDiffCase1()
        {
            var result = Differ.ComputeDiff("a", "b");
        }

        [Fact]
        public void SimpleDiffCase2()
        {
            var result = Differ.ComputeDiff("Apples are a fruit.", "Bananas are also fruit.");
        }

        [Fact]
        public void SimpleDiffCase3()
        {
            var result = Differ.ComputeDiff("ax\t", "\u0680x");
        }
        [Fact]
        public void SimpleDiffCase4()
        {
            var result = Differ.ComputeDiff("1ayb2", "abxab");
        }

        [Fact]
        public void SimpleDiffCase5()
        {
            var result = Differ.ComputeDiff("1ayb2", "abxab");
        }

        [Fact]
        public void SimpleDiffCase6()
        {
            var result = Differ.ComputeDiff("abcy", "xaxcxabc");
        }

        [Fact]
        public void SimpleDiffCase7()
        {
            var result = Differ.ComputeDiff("ABCDa=bcd=efghijklmnopqrsEFGHIJKLMNOefg", "a-bcd-efghijklmnopqrs");
        }

        [Fact]
        public void SimpleDiffCase8()
        {
            var result = Differ.ComputeDiff("a [[Pennsylvania]] and [[New", " and [[Pennsylvania]]");
        }

        [Fact]
        public void MediumDiff()
        {
            string a = "`Twas brillig, and the slithy toves\nDid gyre and gimble in the wabe:\nAll mimsy were the borogoves,\nAnd the mome raths outgrabe.\n";
            string b = "I am the very model of a modern major general,\nI've information vegetable, animal, and mineral,\nI know the kings of England, and I quote the fights historical,\nFrom Marathon to Waterloo, in order categorical.\n";
            for (int i = 0; i < 10; i++)
            {
                a += a;
                b += b;
            }
            var result = Differ.ComputeDiff(a, b);
        }

        [Fact]
        public void MediumDiff2()
        {
            string a = "1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n";
            string b = "abcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\n";
            var result = Differ.ComputeDiff(a, b);
        }

        [Fact]
        public void MediumDiff3()
        {
            string a = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
            string b = "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghij";
            var result = Differ.ComputeDiff(a, b);
        }

        [Fact]
        public void SimpleDiff_ExactMatch()
        {
            var result = Differ.ComputeDiff("abcdefghijk", "fgh");
        }

        [Fact]
        public void SimpleDiff_FuzzyMatch1()
        {
            var result = Differ.ComputeDiff("abcdefghijk", "efxhi");
        }

        [Fact]
        public void SimpleDiff_FuzzyMatch2()
        {
            var result = Differ.ComputeDiff("abcdefghijk", "cdefxyhijk");
        }

        [Fact]
        public void SimpleDiffCase12()
        {
            var result = Differ.ComputeDiff("abcdefghijk", "bxy");
        }

        [Fact]
        public void SimpleDiff_Overflow()
        {
            var result = Differ.ComputeDiff("123456789xx0", "3456789x0");
        }

        [Fact]
        public void SimpleDiff_BeforeStartMatch()
        {
            var result = Differ.ComputeDiff("abcdef", "xxabc");
        }

        [Fact]
        public void SimpleDiff_BeyondEndMatch1()
        {
            var result = Differ.ComputeDiff("abcdef", "defyy");
        }

        [Fact]
        public void SimpleDiff__OverSizedPattern1()
        {
            var result = Differ.ComputeDiff("abcdef", "xabcdefy");
        }

        [Fact]
        public void SimpleDiff_OverSizedPattern2()
        {
            var result = Differ.ComputeDiff("abcdef", "abcdefy");
        }

        [Fact]
        public void SimpleDiff_MultipleSelect1()
        {
            var result = Differ.ComputeDiff("abcdexyzabcde", "abccde");
        }

        [Fact]
        public void SimpleDiff_MultipleSelect2()
        {
            var result = Differ.ComputeDiff("abcdefghijklmnopqrstuvwxyz", "abccde");
        }

        [Fact]
        public void SimpleDiff_Distance1()
        {
            var result = Differ.ComputeDiff("abcdefghijklmnopqrstuvwxyz", "abcdefg");
        }

        [Fact]
        public void SimpleDiff_Distance2()
        {
            var result = Differ.ComputeDiff("abcdefghijklmnopqrstuvwxyz", "abcdxxefg");
        }

        [Fact]
        public void SimpleDiff_ExactMatch()
        {
            var result = Differ.ComputeDiff("abcdef", "de");
        }

        [Fact]
        public void SimpleDiff_BeyondEndMatch2()
        {
            var result = Differ.ComputeDiff("abcdef", "defy");
        }

        [Fact]
        public void ComplexDiffCase()
        {
            var result = Differ.ComputeDiff("I am the very model of a modern major general.", " that berry ");
        }
    }
}
