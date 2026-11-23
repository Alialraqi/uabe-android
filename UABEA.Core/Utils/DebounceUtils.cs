using System;
using System.Threading;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    /// <summary>
    /// Simple debounce utility: when called multiple times within the specified window, only the last call actually runs.
    /// Used to delay disk writes after a config change, avoiding synchronous IO on every property change.
    /// Implementation follows DebounceUtils of UABEANext, using CancellationTokenSource to cancel executions not yet fired.
    /// </summary>
    public static class DebounceUtils
    {
        public static Action<T> Debounce<T>(Action<T> func, int milliseconds)
        {
            CancellationTokenSource? cts = null;

            return arg =>
            {
                try
                {
                    cts?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The previous token has finished and was disposed; ignore
                }

                cts = new CancellationTokenSource();

                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(milliseconds, cts.Token);
                        func(arg);
                    }
                    catch (TaskCanceledException)
                    {
                        // Cancelled by a new call, normal case
                    }
                }, cts.Token);
            };
        }
    }
}
