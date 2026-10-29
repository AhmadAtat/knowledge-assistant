using KnowledgeAssistant.Worker.Processing;

namespace KnowledgeAssistant.Worker.Tests;

public class WordCounterTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("hello", 1)]
    [InlineData("The quick brown fox", 4)]
    [InlineData("  leading and trailing  ", 3)]
    [InlineData("multiple   spaces\tand\ttabs", 4)]
    [InlineData("line one\nline two\r\nline three", 6)]
    public void Count_ReturnsNumberOfWhitespaceSeparatedWords(string text, int expected)
    {
        Assert.Equal(expected, WordCounter.Count(text));
    }
}
