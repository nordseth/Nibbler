using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace Nibbler.Test;

/// <summary>
/// Based on https://github.com/golang/go/blob/master/src/path/filepath/match_test.go
/// </summary>
[TestClass]
public class MatchTest
{
    [TestMethod]
    [DataRow("abc", "abc", true)]
    [DataRow("*", "abc", true)]
    [DataRow("*c", "abc", true)]
    [DataRow("a*", "a", true)]
    [DataRow("a*", "abc", true)]
    [DataRow("a*", "ab/c", false)]
    [DataRow("a*/b", "abc/b", true)]
    [DataRow("a*/b", "a/c/b", false)]
    [DataRow("a*b*c*d*e*/f", "axbxcxdxe/f", true)]
    [DataRow("a*b*c*d*e*/f", "axbxcxdxexxx/f", true)]
    [DataRow("a*b*c*d*e*/f", "axbxcxdxe/xxx/f", false)]
    [DataRow("a*b*c*d*e*/f", "axbxcxdxexxx/fff", false)]
    [DataRow("a*b?c*x", "abxbbxdbxebxczzx", true)]
    [DataRow("a*b?c*x", "abxbbxdbxebxczzy", false)]
    [DataRow("ab[c]", "abc", true)]
    [DataRow("ab[b-d]", "abc", true)]
    [DataRow("ab[e-g]", "abc", false)]
    [DataRow("ab[^c]", "abc", false)]
    [DataRow("ab[^b-d]", "abc", false)]
    [DataRow("ab[^e-g]", "abc", true)]
    [DataRow("a\\*b", "a*b", true)]
    [DataRow("a\\*b", "ab", false)]
    [DataRow("a?b", "a☺b", true)]
    [DataRow("a[^a]b", "a☺b", true)]
    [DataRow("a???b", "a☺b", false)]
    [DataRow("a[^a][^a][^a]b", "a☺b", false)]
    [DataRow("[a-ζ]*", "α", true)]
    [DataRow("*[a-ζ]", "A", false)]
    [DataRow("a?b", "a/b", false)]
    [DataRow("a*b", "a/b", false)]
    [DataRow("[\\]a]", "]", true)]
    [DataRow("[\\-]", "-", true)]
    [DataRow("[x\\-]", "x", true)]
    [DataRow("[x\\-]", "-", true)]
    [DataRow("[x\\-]", "z", false)]
    [DataRow("[\\-x]", "x", true)]
    [DataRow("[\\-x]", "-", true)]
    [DataRow("[\\-x]", "a", false)]
    [DataRow("*x", "xxx", true)]
    [Ignore]
    public void Match_Regex(string pattern, string input, bool isMatch)
    {
        var regex = GetRegex(pattern);
        bool result = Regex.IsMatch(input, regex, RegexOptions.Singleline);
        Assert.AreEqual(isMatch, result, regex);
    }

    [TestMethod]
    [DataRow("abc", "abc", true)]
    [DataRow("*", "abc", true)]
    [DataRow("*c", "abc", true)]
    [DataRow("a*", "a", true)]
    [DataRow("a*", "abc", true)]
    //[DataRow("a*", "ab/c", false)]
    [DataRow("a*/b", "abc/b", true)]
    [DataRow("a*/b", "a/c/b", false)]
    [DataRow("a*b*c*d*e*/f", "axbxcxdxe/f", true)]
    [DataRow("a*b*c*d*e*/f", "axbxcxdxexxx/f", true)]
    [DataRow("a*b*c*d*e*/f", "axbxcxdxe/xxx/f", false)]
    [DataRow("a*b*c*d*e*/f", "axbxcxdxexxx/fff", false)]
    [DataRow("a*b?c*x", "abxbbxdbxebxczzx", true)]
    [DataRow("a*b?c*x", "abxbbxdbxebxczzy", false)]
    [DataRow("ab[c]", "abc", true)]
    [DataRow("ab[b-d]", "abc", true)]
    [DataRow("ab[e-g]", "abc", false)]
    [DataRow("ab[^c]", "abc", false)]
    [DataRow("ab[^b-d]", "abc", false)]
    [DataRow("ab[^e-g]", "abc", true)]
    //[DataRow("a\\*b", "a*b", true)]
    [DataRow("a\\*b", "ab", false)]
    [DataRow("a?b", "a☺b", true)]
    [DataRow("a[^a]b", "a☺b", true)]
    [DataRow("a???b", "a☺b", false)]
    [DataRow("a[^a][^a][^a]b", "a☺b", false)]
    [DataRow("[a-ζ]*", "α", true)]
    //[DataRow("*[a-ζ]", "A", false)]
    [DataRow("a?b", "a/b", false)]
    [DataRow("a*b", "a/b", false)]
    [DataRow("[\\]a]", "]", true)]
    [DataRow("[\\-]", "-", true)]
    [DataRow("[x\\-]", "x", true)]
    [DataRow("[x\\-]", "-", true)]
    [DataRow("[x\\-]", "z", false)]
    [DataRow("[\\-x]", "x", true)]
    [DataRow("[\\-x]", "-", true)]
    [DataRow("[\\-x]", "a", false)]
    [DataRow("*x", "xxx", true)]
    public void Match_Ignore(string pattern, string input, bool isMatch)
    {
        var result = MatchWithIgnore(pattern, input);
        Assert.AreEqual(isMatch, result);
    }

    [TestMethod]
    [DataRow("[]a]", "]")]
    [DataRow("[-]", "-")]
    [DataRow("[x-]", "x")]
    [DataRow("[x-]", "-")]
    [DataRow("[x-]", "z")]
    [DataRow("[-x]", "x")]
    [DataRow("[-x]", "-")]
    [DataRow("[-x]", "a")]
    [DataRow("\\", "a")]
    [DataRow("[a-b-c]", "a")]
    [DataRow("[", "a")]
    [DataRow("[^", "a")]
    [DataRow("[^bc", "a")]
    [DataRow("a[", "a")]
    [DataRow("a[", "ab")]
    [DataRow("a[", "x")]
    [DataRow("a/b[", "x")]
    public void Match_InvalidPatterns(string pattern, string input)
    {
        try
        {
            var regex = GetRegex(pattern);
            bool result = Regex.IsMatch(input, regex, RegexOptions.Singleline);
            Assert.Fail($"Expected Exception: {regex}");
        }
        catch
        {

        }
    }

    [TestMethod]
    [DataRow("[]a]", "]")]
    [DataRow("[-]", "-")]
    [DataRow("[x-]", "x")]
    [DataRow("[x-]", "-")]
    [DataRow("[x-]", "z")]
    [DataRow("[-x]", "x")]
    [DataRow("[-x]", "-")]
    [DataRow("[-x]", "a")]
    [DataRow("\\", "a")]
    [DataRow("[a-b-c]", "a")]
    [DataRow("[", "a")]
    [DataRow("[^", "a")]
    [DataRow("[^bc", "a")]
    [DataRow("a[", "a")]
    [DataRow("a[", "ab")]
    [DataRow("a[", "x")]
    [DataRow("a/b[", "x")]
    public void Match_Ignore_InvalidPatterns(string pattern, string input)
    {
        try
        {
            var result = MatchWithIgnore(pattern, input);
            Assert.Fail("Expected Exception");
        }
        catch
        {

        }
    }

    [TestMethod]
    [DataRow(@"../../../../tests/TestData/", ".gitignore")]
    public void Ignore_With_gitignore(string path, string ignoreFile)
    {
        var ignoreFilePath = Path.Combine(path, ignoreFile);
        Assert.IsTrue(File.Exists(ignoreFilePath));
        var fileContent = File.ReadAllLines(ignoreFilePath);
        var ignore = new Ignore.Ignore();
        ignore.Add(fileContent);

        Assert.IsTrue(ignore.IsIgnored("bin/"));
        Assert.IsTrue(ignore.IsIgnored("Bin/"));
        Assert.IsTrue(ignore.IsIgnored("Bin/test"));
        Assert.IsTrue(ignore.IsIgnored("test/bin/"));
        Assert.IsTrue(ignore.IsIgnored("test/bin/test"));
    }

    public static string GetRegex(string pattern)
    {
        // Replace Go's special characters with equivalent regex patterns
        return "^" + Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".")
            .Replace(@"\[\!", "[^")
            .Replace(@"\[", "[")
            .Replace(@"\]", "]")
            .Replace(@"\!", "^")
            .Replace(@"\(", "(")
            .Replace(@"\)", ")") + "$";
    }

    public static bool MatchWithIgnore(string pattern, string input)
    {
        var i = new Ignore.Ignore();
        i.Add(pattern);
        return i.IsIgnored(input);
    }
}
