namespace Sedgewright.Models;

/// <summary>
/// Represents one ordered message or activity entry in a chat turn.
/// </summary>
/// <param name="Message">The transcript or streaming message represented by this entry.</param>
/// <param name="Activity">The typed activity represented by this entry.</param>
public sealed record ChatTimelineItem(MessageModel? Message, IntermediateUpdate? Activity)
{
	/// <summary>
	/// Gets whether this entry contains a message.
	/// </summary>
	public bool IsMessage => Message is not null;

	/// <summary>
	/// Gets whether this entry contains an activity.
	/// </summary>
	public bool IsActivity => Activity is not null;

	/// <summary>
	/// Creates a timeline entry for a message.
	/// </summary>
	public static ChatTimelineItem ForMessage(MessageModel message) => new(message, null);

	/// <summary>
	/// Creates a timeline entry for an activity.
	/// </summary>
	public static ChatTimelineItem ForActivity(IntermediateUpdate activity) => new(null, activity);
}
