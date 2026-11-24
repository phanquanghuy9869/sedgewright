using System.Collections.Generic;
using System.ComponentModel;
using NUnit.Framework;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Tests.ViewModels.Agents;

[TestFixture]
public sealed class AgentDefinitionInformationViewModelTests
{
    [Test]
    public void SystemPrompt_UpdatesCharacterCountAndNotifiesBindings()
    {
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(null);
        AgentDefinitionInformationViewModel viewModel = new(draft, () => { });
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.SystemPrompt = "Research carefully.";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.SystemPromptCharacterCount, Is.EqualTo(19));
            Assert.That(changedProperties, Does.Contain(nameof(viewModel.SystemPrompt)));
            Assert.That(changedProperties, Does.Contain(nameof(viewModel.SystemPromptCharacterCount)));
        }
    }

    [Test]
    public void PromptPreviewMode_TogglesEditModeWithoutMarkingDraftChanged()
    {
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(null);
        int draftChangedCount = 0;
        AgentDefinitionInformationViewModel viewModel = new(draft, () => draftChangedCount++);
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.IsPromptPreviewMode = true;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.IsPromptEditMode, Is.False);
            Assert.That(viewModel.PromptModeToggleText, Is.EqualTo("Edit"));
            Assert.That(changedProperties, Does.Contain(nameof(viewModel.IsPromptEditMode)));
            Assert.That(draftChangedCount, Is.Zero);
        }
    }

    [Test]
    public void HasSystemPrompt_IsFalseForWhitespaceOnly()
    {
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(null);
        AgentDefinitionInformationViewModel viewModel = new(draft, () => { });

        viewModel.SystemPrompt = "   \n\t ";

        Assert.That(viewModel.HasSystemPrompt, Is.False);
    }
}
