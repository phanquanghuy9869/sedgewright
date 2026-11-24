using Sedgewright.Entities;

namespace Sedgewright.ViewModels.Chat;

/// <summary>
/// Builds display text for chat targets.
/// </summary>
public static class ChatTargetDisplayInfoExtensions
{
    extension(ChatTargetDisplayInfo)
    {
        /// <summary>
        /// Creates display information for a chat target.
        /// </summary>
        public static ChatTargetDisplayInfo From(IChatTarget target)
            => target switch
            {
                AgentDefinition agent => From(agent),
                SequentialOrchestration orchestration => FromOrchestration("Sequential", orchestration.Name, orchestration.Description),
                ConcurrentOrchestration orchestration => FromOrchestration("Concurrent", orchestration.Name, orchestration.Description),
                GroupChatOrchestration orchestration => FromOrchestration("Group chat", orchestration.Name, orchestration.Description),
                MagenticOrchestration orchestration => FromOrchestration("Magentic", orchestration.Name, orchestration.Description),
                HandoffOrchestration orchestration => FromOrchestration("Handoff", orchestration.Name, orchestration.Description),
                _ => new ChatTargetDisplayInfo(target.GetType().Name, string.Empty),
            };

        /// <summary>
        /// Creates display information for an agent target.
        /// </summary>
        public static ChatTargetDisplayInfo From(AgentDefinition agent)
        {
            string providerName = string.IsNullOrWhiteSpace(agent.Provider?.DisplayName) ? agent.Model : agent.Provider.DisplayName;
            return new ChatTargetDisplayInfo(agent.Name, $"{agent.Model} · {providerName}");
        }

        /// <summary>
        /// Creates display information for a sequential orchestration target.
        /// </summary>
        public static ChatTargetDisplayInfo From(SequentialOrchestration orchestration)
            => FromOrchestration("Sequential", orchestration.Name, orchestration.Description);

        /// <summary>
        /// Creates display information for a concurrent orchestration target.
        /// </summary>
        public static ChatTargetDisplayInfo From(ConcurrentOrchestration orchestration)
            => FromOrchestration("Concurrent", orchestration.Name, orchestration.Description);

        /// <summary>
        /// Creates display information for a group chat orchestration target.
        /// </summary>
        public static ChatTargetDisplayInfo From(GroupChatOrchestration orchestration)
            => FromOrchestration("Group chat", orchestration.Name, orchestration.Description);

        /// <summary>
        /// Creates display information for a Magentic orchestration target.
        /// </summary>
        public static ChatTargetDisplayInfo From(MagenticOrchestration orchestration)
            => FromOrchestration("Magentic", orchestration.Name, orchestration.Description);

        /// <summary>
        /// Creates display information for a handoff orchestration target.
        /// </summary>
        public static ChatTargetDisplayInfo From(HandoffOrchestration orchestration)
            => FromOrchestration("Handoff", orchestration.Name, orchestration.Description);

        private static ChatTargetDisplayInfo FromOrchestration(string kindLabel, string name, string? description)
            => new($"{kindLabel} · {name}", string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim());
    }
}