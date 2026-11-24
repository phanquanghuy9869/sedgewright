using NUnit.Framework;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Tests.ViewModels.Agents;

[TestFixture]
public sealed class AgentHarnessOptionsSelectionViewModelTests
{
    [Test]
    public void Constructor_PreservesInitialSelection()
    {
        AgentHarnessOptionsSelection initialSelection = CreateSelection();

        AgentHarnessOptionsSelectionViewModel viewModel = new(initialSelection);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Selection, Is.EqualTo(initialSelection));
            Assert.That(viewModel.WindowTitle, Is.EqualTo("Harness options"));
        }
    }

    [Test]
    public void NewAgentDraft_DisablesWebSearchAndDefaultsFileAccess()
    {
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(draft.DisableWebSearch, Is.True);
            Assert.That(draft.DisableAgentSkillsProvider, Is.True);
            Assert.That(draft.EnableFileAccess, Is.True);
        }
    }

    [Test]
    public void ApplyCommand_RaisesAcceptedSelection()
    {
        AgentHarnessOptionsSelection initialSelection = CreateSelection();
        AgentHarnessOptionsSelectionViewModel viewModel = new(initialSelection);
        AgentHarnessOptionsSelectionCloseRequestedEventArgs? closeRequest = null;
        viewModel.CloseRequested += (_, args) => closeRequest = args;
        viewModel.MaxOutputTokens = 4096;
        viewModel.EnableFileAccess = false;
        viewModel.EnableCodeExecution = true;

        viewModel.ApplyCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(closeRequest, Is.Not.Null);
            Assert.That(closeRequest!.Accepted, Is.True);
            Assert.That(viewModel.Selection.MaxOutputTokens, Is.EqualTo(4096));
            Assert.That(viewModel.Selection.EnableFileAccess, Is.False);
            Assert.That(viewModel.Selection.EnableCodeExecution, Is.True);
        }
    }

    [Test]
    public void CancelCommand_RaisesRejectedCloseWithoutDiscardingDialogState()
    {
        AgentHarnessOptionsSelectionViewModel viewModel =
            new(CreateSelection());
        AgentHarnessOptionsSelectionCloseRequestedEventArgs? closeRequest = null;
        viewModel.CloseRequested += (_, args) => closeRequest = args;
        viewModel.DisableWebSearch = true;

        viewModel.CancelCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(closeRequest, Is.Not.Null);
            Assert.That(closeRequest!.Accepted, Is.False);
            Assert.That(viewModel.DisableWebSearch, Is.True);
        }
    }

    [Test]
    public void EnablingHarnessAgent_RaisesOptionsSelectionRequest()
    {
        AgentDefinitionEditorDraft draft = AgentDefinitionDraftMapper.CreateDraft(null);
        AgentDefinitionHarnessOptionsViewModel viewModel =
            new(draft, true, () => { });
        AgentHarnessOptionsSelectionRequestedEventArgs? request = null;
        viewModel.HarnessOptionsSelectionRequested += (_, args) => request = args;

        viewModel.IsHarnessAgent = true;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.InitialSelection.IsHarnessAgent, Is.True);
            Assert.That(request.RevertSelection, Is.Not.Null);
            Assert.That(request.RevertSelection!.IsHarnessAgent, Is.False);
        }
    }

    private static AgentHarnessOptionsSelection CreateSelection()
        => new(
            true,
            true,
            false,
            true,
            false,
            true,
            false,
            true,
            8192,
            2048,
            true,
            false);
}
