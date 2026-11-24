using System;
using GitHub.Copilot;
using Sedgewright.Models;

namespace Sedgewright.Infras.MAF.Extensions;

internal static class SessionEventExtensions
{
    extension(SessionEvent copilotEvent)
    {
        internal IntermediateUpdate? ToActivityUpdate(
            string source,
            string? executorId = null,
            string? responseId = null,
            string? messageId = null)
        {
            switch (copilotEvent)
            {
                case AssistantIntentEvent intentEvent:
                    return new IntermediateUpdate(
                        source,
                        intentEvent.Data.Intent,
                        false,
                        IntermediateUpdateKind.Progress,
                        executorId,
                        responseId,
                        messageId,
                        Details: FormatCopilotEventDetails(intentEvent, "Assistant intent"));

                case SessionInfoEvent infoEvent:
                    string infoDetails = $"Category: {infoEvent.Data.InfoType}";
                    if (!string.IsNullOrWhiteSpace(infoEvent.Data.Tip))
                    {
                        infoDetails = $"{infoDetails}{Environment.NewLine}Tip: {infoEvent.Data.Tip}";
                    }

                    return new IntermediateUpdate(
                        source,
                        infoEvent.Data.Message,
                        false,
                        IntermediateUpdateKind.OtherContent,
                        executorId,
                        responseId,
                        messageId,
                        Details: FormatCopilotEventDetails(infoEvent, infoDetails));

                case SessionWarningEvent warningEvent:
                    return new IntermediateUpdate(
                        source,
                        warningEvent.Data.Message,
                        false,
                        IntermediateUpdateKind.Warning,
                        executorId,
                        responseId,
                        messageId,
                        Details: FormatCopilotEventDetails(warningEvent, $"Warning type: {warningEvent.Data.WarningType}"));

                case SessionModelChangeEvent modelChangeEvent:
                    string modelChange = string.IsNullOrWhiteSpace(modelChangeEvent.Data.PreviousModel)
                        ? $"Copilot model changed to {modelChangeEvent.Data.NewModel}."
                        : $"Copilot model changed from {modelChangeEvent.Data.PreviousModel} to {modelChangeEvent.Data.NewModel}.";
                    string? modelChangeDetails = string.IsNullOrWhiteSpace(modelChangeEvent.Data.Cause)
                        ? null
                        : $"Cause: {modelChangeEvent.Data.Cause}";

                    return new IntermediateUpdate(
                        source,
                        modelChange,
                        false,
                        IntermediateUpdateKind.OtherContent,
                        executorId,
                        responseId,
                        messageId,
                        Details: FormatCopilotEventDetails(modelChangeEvent, modelChangeDetails));

                case SessionPlanChangedEvent planChangedEvent:
                    string planOperation = planChangedEvent.Data.Operation.ToString().ToLowerInvariant();
                    return new IntermediateUpdate(
                        source,
                        $"Copilot plan {planOperation}.",
                        false,
                        IntermediateUpdateKind.OtherContent,
                        executorId,
                        responseId,
                        messageId,
                        Details: FormatCopilotEventDetails(planChangedEvent, $"Operation: {planOperation}"));

                case SessionWorkspaceFileChangedEvent workspaceFileChangedEvent:
                    string fileOperation = workspaceFileChangedEvent.Data.Operation.ToString().ToLowerInvariant();
                    return new IntermediateUpdate(
                        source,
                        $"Workspace file {fileOperation}: {workspaceFileChangedEvent.Data.Path}",
                        false,
                        IntermediateUpdateKind.OtherContent,
                        executorId,
                        responseId,
                        messageId,
                        Details: FormatCopilotEventDetails(workspaceFileChangedEvent));

                case AssistantMessageEvent assistantMessage when !string.IsNullOrWhiteSpace(assistantMessage.Data.MessageId):
                    return new IntermediateUpdate(
                        source,
                        "Copilot assistant response completed.",
                        false,
                        IntermediateUpdateKind.OtherContent,
                        executorId,
                        responseId,
                        messageId,
                        Details: FormatCopilotEventDetails(
                            assistantMessage,
                            $"Message ID: {assistantMessage.Data.MessageId}"));

                default:
                    return null;
            }
        }
    }

    private static string FormatCopilotEventDetails(SessionEvent copilotEvent, string? payloadDetails = null)
    {
        string details = $"Event type: {copilotEvent.Type}";
        if (copilotEvent.Id != Guid.Empty)
        {
            details = $"{details}{Environment.NewLine}Event ID: {copilotEvent.Id}";
        }

        if (copilotEvent.Timestamp != default)
        {
            details = $"{details}{Environment.NewLine}Timestamp: {copilotEvent.Timestamp:O}";
        }

        if (!string.IsNullOrWhiteSpace(payloadDetails))
        {
            details = $"{details}{Environment.NewLine}{payloadDetails}";
        }

        return details;
    }
}
