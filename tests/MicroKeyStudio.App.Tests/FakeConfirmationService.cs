using MicroKeyStudio.App.Services;

namespace MicroKeyStudio.App.Tests;

internal sealed class FakeConfirmationService : IUserConfirmationService
{
    private readonly bool _result;
    private TaskCompletionSource<bool>? _blockedResult;
    private TaskCompletionSource _requestObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public FakeConfirmationService(bool result = true)
    {
        _result = result;
    }

    public int CallCount { get; private set; }

    public string? LastTitle { get; private set; }

    public string? LastMessage { get; private set; }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        CallCount++;
        LastTitle = title;
        LastMessage = message;
        _requestObserved.TrySetResult();
        return _blockedResult?.Task ?? Task.FromResult(_result);
    }

    public void BlockNextConfirmation()
    {
        _blockedResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requestObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task WaitForRequestAsync()
    {
        return _requestObserved.Task;
    }

    public void CompleteConfirmation(bool result)
    {
        _blockedResult?.TrySetResult(result);
    }
}
