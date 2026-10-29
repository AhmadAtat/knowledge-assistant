namespace KnowledgeAssistant.Worker.Processing;

public static class WordCounter
{
    // Splits on any whitespace (spaces, tabs, newlines); a run of whitespace counts as one separator.
    public static int Count(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
