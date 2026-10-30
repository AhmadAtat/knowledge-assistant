namespace KnowledgeAssistant.Worker.Tests;

// The Worker processes messages asynchronously, so tests can't assert right after publishing.
// Polls the condition until it holds or the timeout expires.
internal static class Eventually
{
    public static async Task<bool> TrueWithinAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }
}
