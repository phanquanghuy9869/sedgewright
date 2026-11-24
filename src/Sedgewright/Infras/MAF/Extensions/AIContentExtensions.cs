using System;
using System.Collections.Generic;
using System.Text.Json;
using GitHub.Copilot;
using Microsoft.Extensions.AI;
using Sedgewright.Models;
using ToolApprovalRequestContent = Microsoft.Extensions.AI.ToolApprovalRequestContent;

namespace Sedgewright.Infras.MAF.Extensions;

/// <summary>
/// Provides display-oriented extensions for Microsoft Agent Framework response content.
/// </summary>
public static class AIContentExtensions
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        WriteIndented = true,
    };

    extension(AIContent content)
    {
        /// <summary>
        /// Converts non-text Agent Framework content into a safe activity summary.
        /// </summary>
        public IntermediateUpdate? ToActivityUpdate(
            string source,
            string? executorId = null,
            string? responseId = null,
            string? messageId = null)
            => content switch
            {
                TextContent => null,
                TextReasoningContent reasoning when !string.IsNullOrEmpty(reasoning.Text) => new IntermediateUpdate(
                    source,
                    reasoning.Text,
                    false,
                    IntermediateUpdateKind.Reasoning,
                    executorId,
                    responseId,
                    messageId),
                TextReasoningContent => null,
                FunctionCallContent functionCall => CreateToolCallUpdate(functionCall, source, executorId, responseId, messageId),
                FunctionResultContent functionResult => CreateToolResultUpdate(functionResult, source, executorId, responseId, messageId),
                ToolApprovalRequestContent approvalRequest => CreateApprovalUpdate(approvalRequest, source, executorId, responseId, messageId),
                UsageContent usage => CreateUsageUpdate(usage, source, executorId, responseId, messageId),
                ErrorContent error => new IntermediateUpdate(
                    source,
                    "The agent reported an error.",
                    true,
                    IntermediateUpdateKind.Error,
                    executorId,
                    responseId,
                    messageId,
                    Details: error.Message),
                AIContent rawContent when rawContent.GetType() == typeof(AIContent)
                    && rawContent.RawRepresentation is SessionEvent copilotEvent => copilotEvent.ToActivityUpdate(
                        source,
                        executorId,
                        responseId,
                        messageId),
                AIContent rawContent when rawContent.GetType() == typeof(AIContent)
                    && rawContent.RawRepresentation is not null => null,
                _ => new IntermediateUpdate(
                    source,
                    $"Additional agent content received ({content.GetType().Name}).",
                    false,
                    IntermediateUpdateKind.OtherContent,
                    executorId,
                    responseId,
                    messageId),
            };
    }

    private static IntermediateUpdate CreateToolCallUpdate(
        FunctionCallContent functionCall,
        string source,
        string? executorId,
        string? responseId,
        string? messageId)
    {
        string toolName = string.IsNullOrWhiteSpace(functionCall.Name) ? "unknown tool" : functionCall.Name;
        string details = $"Arguments:{Environment.NewLine}{SerializePayload(functionCall.Arguments ?? new Dictionary<string, object?>())}";
        if (!string.IsNullOrWhiteSpace(functionCall.CallId))
        {
            details = $"Call ID: {functionCall.CallId}{Environment.NewLine}{details}";
        }

        return new IntermediateUpdate(
            source,
            $"{toolName} was requested.",
            false,
            IntermediateUpdateKind.ToolCall,
            executorId,
            responseId,
            messageId,
            CallId: functionCall.CallId,
            Details: details);
    }

    private static IntermediateUpdate CreateToolResultUpdate(
        FunctionResultContent functionResult,
        string source,
        string? executorId,
        string? responseId,
        string? messageId)
    {
        string? errorDetails = functionResult.Exception?.Message;
        string details = $"Result:{Environment.NewLine}{SerializePayload(functionResult.Result)}";
        if (!string.IsNullOrWhiteSpace(functionResult.CallId))
        {
            details = $"Call ID: {functionResult.CallId}{Environment.NewLine}{details}";
        }

        if (!string.IsNullOrWhiteSpace(errorDetails))
        {
            details = $"{details}{Environment.NewLine}Error:{Environment.NewLine}{functionResult.Exception}";
        }

        return new IntermediateUpdate(
            source,
            functionResult.Exception is null ? "Tool call completed." : "Tool call failed.",
            functionResult.Exception is not null,
            IntermediateUpdateKind.ToolResult,
            executorId,
            responseId,
            messageId,
            CallId: functionResult.CallId,
            Details: details);
    }

    private static string SerializePayload(object? payload)
    {
        if (payload is null)
        {
            return "null";
        }

        try
        {
            return JsonSerializer.Serialize(payload, payload.GetType(), PayloadJsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return $"Unable to serialize {payload.GetType().Name} as JSON: {exception.Message}{Environment.NewLine}{payload}";
        }
    }

    private static IntermediateUpdate CreateApprovalUpdate(
        ToolApprovalRequestContent approvalRequest,
        string source,
        string? executorId,
        string? responseId,
        string? messageId)
    {
        string toolName = approvalRequest.ToolCall is FunctionCallContent functionCall
            && !string.IsNullOrWhiteSpace(functionCall.Name)
                ? functionCall.Name
                : "tool";
        string? callId = approvalRequest.ToolCall is FunctionCallContent namedCall
            ? namedCall.CallId
            : null;
        return new IntermediateUpdate(
            source,
            $"Waiting for approval to run {toolName}.",
            false,
            IntermediateUpdateKind.ApprovalRequested,
            executorId,
            responseId,
            messageId,
            CallId: callId);
    }

    private static IntermediateUpdate CreateUsageUpdate(
        UsageContent usage,
        string source,
        string? executorId,
        string? responseId,
        string? messageId)
    {
        UsageDetails details = usage.Details;
        string summary = $"Tokens reported: input {details.InputTokenCount?.ToString() ?? "?"}, output {details.OutputTokenCount?.ToString() ?? "?"}, total {details.TotalTokenCount?.ToString() ?? "?"}.";
        return new IntermediateUpdate(source, summary, false, IntermediateUpdateKind.Usage, executorId, responseId, messageId);
    }
}
