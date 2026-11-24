using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.MAF.Extensions;

public delegate Task<Result<string?, IApplicationError>> RunWorkflow(List<string> messages);

public record WorkflowFunction(RunWorkflow Run, string Name, string? Description);

public static class AIToolExtensions
{
    private static readonly JsonSerializerOptions WorkflowFunctionSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    extension(AITool)
    {
        public static async Task<Result<IEnumerable<AITool>, IApplicationError>> FromTools(McpClient client)
        {
            try
            {
                IList<McpClientTool> tools = await client.ListToolsAsync();
                return Result.Success<IEnumerable<AITool>, IApplicationError>(tools.Cast<AITool>());
            }
            catch (McpException ex)
            {
                return new ExceptionError([ex.Message], ex);
            }
        }

        public async static Task<Result<IEnumerable<AITool>, IApplicationError>> FromAgentDefinitionMcp(AgentDefinition agentDefinition)
        {
            Result<McpClient, IApplicationError>[] mcpClientResult = await McpClient.FromAgentDefinition(agentDefinition).ConfigureAwait(false);

            if (mcpClientResult.Any(r => r.IsFailure))
            {
                return new GenericError(mcpClientResult.Select(x => x.Error).GetMessages());
            }

            IEnumerable<Task<Result<IEnumerable<AITool>, IApplicationError>>> getToolTasks = mcpClientResult.Select(r => r.Value).Select(AITool.FromTools);
            Result<IEnumerable<AITool>, IApplicationError>[] getToolResults = await Task.WhenAll(getToolTasks).ConfigureAwait(false);

            if (getToolResults.Any(r => r.IsFailure))
            {
                return new GenericError(getToolResults.Select(x => x.Error).GetMessages());
            }

            return Result.Success<IEnumerable<AITool>, IApplicationError>(getToolResults.SelectMany(r => r.Value));
        }

        public static async Task<Result<IEnumerable<AITool>, IApplicationError>> FromAgentDefinition(AgentDefinition agentDefinition)
        {
            Result<IEnumerable<AITool>, IApplicationError> mcpTools =
                await FromAgentDefinitionMcp(agentDefinition).ConfigureAwait(false);

            if (mcpTools.IsFailure)
            {
                return mcpTools;
            }

            try
            {
                Result<AIFunction, IApplicationError>[] definitionToolResults = await Task
                    .WhenAll(agentDefinition.ToolDefinitions.Select(AIFunction.FromToolDefinition))
                    .ConfigureAwait(false);

                if (definitionToolResults.Any(r => r.IsFailure))
                {
                    return new GenericError(definitionToolResults.Select(r => r.Error).GetMessages());
                }

                List<AITool> definitionTools = [.. definitionToolResults.Select(r => (AITool)r.Value)];
                return Result.Success<IEnumerable<AITool>, IApplicationError>(mcpTools.Value.Concat(definitionTools));
            }
            catch (System.Exception exception)
            {
                return new ExceptionError([exception.Message], exception);
            }
        }
    }

    extension(AIFunction)
    {
        public static Task<Result<AIFunction, IApplicationError>> FromToolDefinition(ToolDefinition toolDefinition)
        {
            return Workflow
                    .From(toolDefinition)
                    .Map(CreateAIFunction);
        }

        private static AIFunction CreateAIFunction(WorkflowRuntime runtime)
        {
            string? description = string.IsNullOrWhiteSpace(runtime.ToolDescription)
                ? runtime.Description
                : runtime.ToolDescription;

            return AIFunctionFactory.CreateWithSnakeCase(
                        RunWorkflow,
                        runtime.Name,
                        description);

            async Task<string> RunWorkflow(List<string> msgs)
            {
                Result<string?, IApplicationError> runResult = await runtime.Workflow.Run(msgs, runtime.Kind).ConfigureAwait(false);
                return runResult.IsSuccess ? runResult.Value ?? string.Empty : throw new InvalidOperationException(runResult.Error.Messages.FirstOrDefault());
            }
        }
    }

    extension(AIFunctionFactory)
    {
        /// <summary>
        /// Creates an AI function with fixed snake_case JSON serialization and the supplied metadata.
        /// </summary>
        public static AIFunction CreateWithSnakeCase(Delegate method, string name, string? description)
            => AIFunctionFactory.Create(
                method,
                name,
                description,
                WorkflowFunctionSerializerOptions);
    }
}
