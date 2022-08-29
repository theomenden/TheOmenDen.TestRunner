using Blazorise;
using Xunit.Runner.Common;

namespace TheOmenDen.TestRunner.Utilities;

public class BrowserRunnerLogger : IRunnerLogger
{
    private readonly INotificationService _notificationService;
    
    public BrowserRunnerLogger(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public void LogMessage(StackFrameInfo stackFrame, string message)
    {
        _notificationService.Info(message);
    }

    public void LogImportantMessage(StackFrameInfo stackFrame, string message)
    {
        _notificationService.Warning(message);
    }

    public void LogWarning(StackFrameInfo stackFrame, string message)
    {
        _notificationService.Warning(message);
    }

    public void LogError(StackFrameInfo stackFrame, string message)
    {
        _notificationService.Error(message);
    }

    public object LockObject { get; } = new ();
}