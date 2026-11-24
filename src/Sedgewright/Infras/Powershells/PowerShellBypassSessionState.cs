using System.Management.Automation.Runspaces;
using Microsoft.PowerShell;

namespace Sedgewright.Infrastructure.Powershells;

public class PowerShellBypassSessionState
{
    public InitialSessionState SessionState { get; private set; }
    private static PowerShellBypassSessionState? _instance;

    private PowerShellBypassSessionState()
    {
        SessionState = InitialSessionState.CreateDefault();
        SessionState.ExecutionPolicy = ExecutionPolicy.Bypass;
    }

    public static PowerShellBypassSessionState GetInstance()
    {
        _instance ??= new PowerShellBypassSessionState();
        return _instance;
    }
}
