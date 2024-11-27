using System.Threading.Tasks;

namespace nfm.menu;

internal sealed class AsyncAutoResetEvent
{
    private static readonly Task s_completed = Task.FromResult(true);
    private readonly object _lock = new();
    private TaskCompletionSource<bool>? _tcs;
    private bool _signaled;

    public Task WaitAsync()
    {
        lock (_lock)
        {
            if (_signaled)
            {
                _signaled = false;
                return s_completed;
            }
            return (_tcs = new TaskCompletionSource<bool>()).Task;
        }
    }

    public void Set()
    {
        TaskCompletionSource<bool>? tcs = null;
        lock (_lock)
        {
            if (_tcs != null)
            {
                tcs = _tcs;
                _tcs = null;
            }
            else
                _signaled = true;
        }
        tcs?.SetResult(true);
    }
}