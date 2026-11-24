namespace Sedgewright.Models;

/// <summary>
/// Represents the concrete kinds of chat targets available in the picker.
/// </summary>
public enum ChatTargetType
{
    Agent,
    SequentialOrchestration,
    ConcurrentOrchestration,
    GroupChatOrchestration,
    MagenticOrchestration,
    HandoffOrchestration,
}
