using System.Collections.Generic;

namespace Sedgewright.Infras.MAF.Extensions;

public sealed record HttpTransportConfig(string Name, string Endpoint, Dictionary<string, string> Headers) : IMcpClientTransport;
