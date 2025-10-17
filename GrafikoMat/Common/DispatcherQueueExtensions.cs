using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

namespace GrafikoMat.Common
{
    public static class DispatcherQueueExtensions
    {
        public static Task EnqueueAsync(this DispatcherQueue dispatcher, Func<Task> asyncAction)
        {
            var tcs = new TaskCompletionSource<bool>();

            dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    await asyncAction();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }
    }
}