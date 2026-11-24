namespace Sedgewright.Infras.MAF.Extensions;

public sealed record StdioConfig(string Name, string Command, string[] Arguments) : IMcpClientTransport;
