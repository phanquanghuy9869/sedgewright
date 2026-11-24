namespace Sedgewright.Entities;

/// <summary>
/// Marks a target that can be selected from the chat picker.
/// </summary>
public interface IChatTarget;

public static class IChatTargetExtensions
{
    extension(IChatTarget target)
    {
        public bool IsAgent(out AgentDefinition? agent)
        {
            if (target is AgentDefinition a)
            {
                agent = a;
                return true;
            }

            agent = default;
            return false;
        }

        public bool IsOrchestration(out IOrchestration? orchestration)
        {
            if (target is IOrchestration o)
            {
                orchestration = o;
                return true;
            }

            orchestration = default;
            return false;
        }
    }
}
