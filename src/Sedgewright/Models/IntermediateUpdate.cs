namespace Sedgewright.Models;

/// <summary>
/// Describes the kind of information carried by an intermediate orchestration update.
/// </summary>
public enum IntermediateUpdateKind
{
	/// <summary>
	/// Incremental assistant or agent response text.
	/// </summary>
	Text,

	/// <summary>
	/// Reasoning content supplied by the model provider.
	/// </summary>
	Reasoning,

	/// <summary>
	/// An initial orchestration plan.
	/// </summary>
	Plan,

	/// <summary>
	/// A revised orchestration plan.
	/// </summary>
	Replan,

	/// <summary>
	/// Structured orchestration progress.
	/// </summary>
	Progress,

	/// <summary>
	/// An executor has started work.
	/// </summary>
	ExecutorStarted,

	/// <summary>
	/// An executor has completed work.
	/// </summary>
	ExecutorCompleted,

	/// <summary>
	/// A non-fatal workflow warning.
	/// </summary>
	Warning,

	/// <summary>
	/// A tool call was requested.
	/// </summary>
	ToolCall,

	/// <summary>
	/// A tool call produced a result.
	/// </summary>
	ToolResult,

	/// <summary>
	/// A tool call is waiting for user approval.
	/// </summary>
	ApprovalRequested,

	/// <summary>
	/// A tool call was automatically approved by application policy.
	/// </summary>
	ApprovalAutoApproved,

	/// <summary>
	/// A Magentic plan review was automatically approved by application policy.
	/// </summary>
	PlanAutoApproved,

	/// <summary>
	/// A tool approval decision was made by the user.
	/// </summary>
	ApprovalDecided,

	/// <summary>
	/// An executor failed.
	/// </summary>
	ExecutorFailed,

	/// <summary>
	/// A handoff changed the active agent.
	/// </summary>
	Handoff,

	/// <summary>
	/// Usage information was reported.
	/// </summary>
	Usage,

	/// <summary>
	/// Additional typed content was received.
	/// </summary>
	OtherContent,

	/// <summary>
	/// The agent or workflow reported an error.
	/// </summary>
	Error,

	/// <summary>
	/// The chat turn started.
	/// </summary>
	RunStarted,

	/// <summary>
	/// The chat turn completed.
	/// </summary>
	RunCompleted,

	/// <summary>
	/// An individual agent response finished.
	/// </summary>
	ResponseCompleted,

	/// <summary>
	/// The chat turn failed.
	/// </summary>
	RunFailed,

	/// <summary>
	/// The chat turn was canceled.
	/// </summary>
	RunCanceled,
}

/// <summary>
/// Contains the structured progress state emitted by a Magentic workflow.
/// </summary>
public sealed record MagenticProgressSnapshot(
	bool IsRequestSatisfied,
	bool IsInLoop,
	bool IsProgressBeingMade,
	string? NextSpeaker,
	string? InstructionOrQuestion);

/// <summary>
/// Represents streamed text or typed activity emitted while an orchestration executes.
/// </summary>
/// <param name="Source">The resolved agent or executor label associated with the update.</param>
/// <param name="Update">The text or concise status summary associated with the update.</param>
/// <param name="IsError">Whether the update represents an error condition.</param>
/// <param name="Kind">The update category; defaults to text for existing callback producers.</param>
/// <param name="ExecutorId">The source workflow executor identifier, when available.</param>
/// <param name="ResponseId">The agent response identifier, when available.</param>
/// <param name="MessageId">The agent message identifier, when available.</param>
/// <param name="Progress">Typed Magentic progress fields, when the update is a progress snapshot.</param>
public sealed record IntermediateUpdate(
	string Source,
	string Update,
	bool IsError,
	IntermediateUpdateKind Kind = IntermediateUpdateKind.Text,
	string? ExecutorId = null,
	string? ResponseId = null,
	string? MessageId = null,
	string? CallId = null,
	MagenticProgressSnapshot? Progress = null,
	string? Details = null)
{
	/// <summary>
	/// Gets whether the update includes a structured progress snapshot.
	/// </summary>
	public bool HasProgress => Progress is not null;

	/// <summary>
	/// Gets whether the update includes expandable detail text.
	/// </summary>
	public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

	/// <summary>
	/// Gets whether this activity includes a tool call identifier.
	/// </summary>
	public bool HasCallId => !string.IsNullOrWhiteSpace(CallId);

	/// <summary>
	/// Gets whether the update text should be shown separately from the activity title.
	/// </summary>
	public bool HasStandaloneUpdate => Kind is not IntermediateUpdateKind.Plan and not IntermediateUpdateKind.Replan
		&& !string.IsNullOrWhiteSpace(Update);

	/// <summary>
	/// Gets a concise, human-readable summary of the structured progress snapshot.
	/// </summary>
	public string ProgressSummary => Progress is MagenticProgressSnapshot progress
		? $"Request satisfied: {progress.IsRequestSatisfied}; In loop: {progress.IsInLoop}; Progressing: {progress.IsProgressBeingMade}; Next speaker: {progress.NextSpeaker}"
		: string.Empty;

	/// <summary>
	/// Gets the current progress instruction or question.
	/// </summary>
	public string ProgressInstruction => Progress?.InstructionOrQuestion ?? string.Empty;

	/// <summary>
	/// Gets the display title for this activity kind.
	/// </summary>
	public string ActivityTitle => Kind switch
	{
		IntermediateUpdateKind.ToolCall => "Tool requested",
		IntermediateUpdateKind.ToolResult => IsError ? "Tool failed" : "Tool completed",
		IntermediateUpdateKind.ApprovalRequested => "Approval required",
		IntermediateUpdateKind.ApprovalAutoApproved => "Tool auto-approved",
		IntermediateUpdateKind.PlanAutoApproved => "Plan auto-approved",
		IntermediateUpdateKind.ApprovalDecided => "Approval decision",
		IntermediateUpdateKind.Plan => "Initial plan",
		IntermediateUpdateKind.Replan => "Plan revised",
		IntermediateUpdateKind.Progress => "Progress",
		IntermediateUpdateKind.ExecutorStarted => "Agent started",
		IntermediateUpdateKind.ExecutorCompleted => "Agent completed",
		IntermediateUpdateKind.ExecutorFailed => "Agent failed",
		IntermediateUpdateKind.Warning => "Workflow warning",
		IntermediateUpdateKind.Handoff => "Handoff",
		IntermediateUpdateKind.Reasoning => "Provider reasoning",
		IntermediateUpdateKind.Usage => "Usage",
		IntermediateUpdateKind.Error => "Agent error",
		IntermediateUpdateKind.RunStarted => "Run started",
		IntermediateUpdateKind.RunCompleted => "Run completed",
		IntermediateUpdateKind.ResponseCompleted => "Response finished",
		IntermediateUpdateKind.RunFailed => "Run failed",
		IntermediateUpdateKind.RunCanceled => "Run canceled",
		_ => "Activity",
	};
}
