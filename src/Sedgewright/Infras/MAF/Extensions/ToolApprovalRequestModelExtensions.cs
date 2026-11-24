using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Sedgewright.Models;

namespace Sedgewright.Infras.MAF.Extensions;

public static class ToolApprovalRequestModelExtensions
{
    extension(ToolApprovalRequestModel)
    {
        public static ToolApprovalRequestModel From(ToolApprovalRequestContent approvalRequest)
        {
            string toolName = "Unknown tool";
            string arguments = string.Empty;
            string callId = approvalRequest.ToolCall.CallId ?? string.Empty;

            if (approvalRequest.ToolCall is FunctionCallContent functionCall)
            {
                toolName = string.IsNullOrWhiteSpace(functionCall.Name) ? toolName : functionCall.Name;
                arguments = SerializeArguments(functionCall.Arguments);
            }

            return new ToolApprovalRequestModel(
                approvalRequest.RequestId,
                callId,
                toolName,
                arguments);
        }

        private static string SerializeArguments(IDictionary<string, object?>? arguments)
        {
            if (arguments is null || arguments.Count == 0)
            {
                return string.Empty;
            }

            try
            {
                return JsonSerializer.Serialize(arguments, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception)
            {
                return arguments.ToString() ?? string.Empty;
            }
        }
    }
}
