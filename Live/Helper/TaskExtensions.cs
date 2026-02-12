
namespace Live.Helper
{
    public static class TaskExtensions
    {
        public static async Task<T> WithTimeout<T>(this Task<T> task, int timeoutMs)
        {
            using var cts = new CancellationTokenSource();
            var delayTask = Task.Delay(timeoutMs, cts.Token);
            var completedTask = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

            if (completedTask == delayTask)
            {
                throw new TimeoutException($"Operation timed out after {timeoutMs}ms");
            }

            cts.Cancel();
            return await task;
        }
    }
}