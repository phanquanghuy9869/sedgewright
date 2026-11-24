using System;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Holds the core descriptive fields for an agent definition draft.
/// </summary>
/// <remarks>
/// Creates the information editor state.
/// </remarks>
/// <param name="draft">The initial draft values.</param>
/// <param name="draftChanged">Raised when the draft changes.</param>
public sealed class AgentDefinitionInformationViewModel(AgentDefinitionEditorDraft draft, Action draftChanged) : ViewModelBase
{
    private string _name = draft.Name;
    private string _description = draft.Description;
    private string _systemPrompt = draft.SystemPrompt;
    private string _userPrompt = draft.UserPrompt;
    private bool _isReadOnly = draft.IsReadOnly;
    private bool _isPromptPreviewMode;

    public string Name
    {
        get => _name;
        set
        {
            if (!SetProperty(ref _name, value))
            {
                return;
            }

            draftChanged();
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (!SetProperty(ref _description, value))
            {
                return;
            }

            draftChanged();
        }
    }

    public string SystemPrompt
    {
        get => _systemPrompt;
        set
        {
            if (!SetProperty(ref _systemPrompt, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SystemPromptCharacterCount));
            OnPropertyChanged(nameof(HasSystemPrompt));
            draftChanged();
        }
    }

    /// <summary>
    /// Gets the number of characters in the system prompt.
    /// </summary>
    public int SystemPromptCharacterCount => SystemPrompt.Length;

    /// <summary>
    /// Gets a value indicating whether the system prompt contains visible text.
    /// </summary>
    public bool HasSystemPrompt => !string.IsNullOrWhiteSpace(SystemPrompt);

    /// <summary>
    /// Gets or sets a UI-only flag that switches the prompt workspace to the rendered Markdown preview.
    /// </summary>
    /// <remarks>
    /// This flag is not part of the draft and never affects save or persistence.
    /// </remarks>
    /// <summary>
    /// Gets the label for the toggle that switches between the source editor and the rendered preview.
    /// </summary>
    public string PromptModeToggleText => IsPromptPreviewMode ? "Edit" : "Preview";

    public bool IsPromptPreviewMode
    {
        get => _isPromptPreviewMode;
        set
        {
            if (!SetProperty(ref _isPromptPreviewMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsPromptEditMode));
            OnPropertyChanged(nameof(PromptModeToggleText));
        }
    }

    /// <summary>
    /// Gets or sets a UI-only flag that switches the prompt workspace to the source editor.
    /// </summary>
    public bool IsPromptEditMode
    {
        get => !_isPromptPreviewMode;
        set => IsPromptPreviewMode = !value;
    }

    public string UserPrompt
    {
        get => _userPrompt;
        set
        {
            if (!SetProperty(ref _userPrompt, value))
            {
                return;
            }

            draftChanged();
        }
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            if (!SetProperty(ref _isReadOnly, value))
            {
                return;
            }

            draftChanged();
        }
    }
}
