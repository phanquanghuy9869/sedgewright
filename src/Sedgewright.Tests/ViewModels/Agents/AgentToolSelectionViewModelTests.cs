using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sedgewright.Entities;
using Sedgewright.ViewModels.Agents;

namespace Sedgewright.Tests.ViewModels.Agents;

[TestFixture]
public sealed class AgentToolSelectionViewModelTests
{
    [Test]
    public void Constructor_PreservesInitialSelectionAndOrdersItems()
    {
        ToolDefinition secondTool = CreateTool("second");
        ToolDefinition firstTool = CreateTool("first");

        AgentToolSelectionViewModel viewModel = new(
            [secondTool, firstTool],
            [secondTool.Id]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Items.Select(item => item.Name), Is.EqualTo(["first", "second"]));
            Assert.That(viewModel.Items.Single(item => item.Name == "second").IsSelected, Is.True);
            Assert.That(viewModel.SelectedToolDefinitionIds, Is.EqualTo([secondTool.Id]));
        }
    }

    [Test]
    public void ApplyCommand_RaisesAcceptedSelection()
    {
        ToolDefinition tool = CreateTool("tool");
        AgentToolSelectionViewModel viewModel = new([tool], []);
        AgentToolSelectionCloseRequestedEventArgs? closeRequest = null;
        viewModel.CloseRequested += (_, args) => closeRequest = args;

        viewModel.Items[0].IsSelected = true;
        viewModel.ApplyCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(closeRequest, Is.Not.Null);
            Assert.That(closeRequest!.Accepted, Is.True);
            Assert.That(viewModel.SelectedToolDefinitionIds, Is.EqualTo([tool.Id]));
        }
    }

    [Test]
    public void CancelCommand_RaisesRejectedClose()
    {
        ToolDefinition tool = CreateTool("tool");
        AgentToolSelectionViewModel viewModel = new([tool], [tool.Id]);
        AgentToolSelectionCloseRequestedEventArgs? closeRequest = null;
        viewModel.CloseRequested += (_, args) => closeRequest = args;

        viewModel.CancelCommand.Execute(null);

        Assert.That(closeRequest?.Accepted, Is.False);
    }

    [Test]
    public void EmptyDefinitions_DisablesApply()
    {
        AgentToolSelectionViewModel viewModel = new(
            Array.Empty<ToolDefinition>(),
            Array.Empty<Guid>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.HasItems, Is.False);
            Assert.That(viewModel.ApplyCommand.CanExecute(null), Is.False);
        }
    }

    private static ToolDefinition CreateTool(string name)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = ToolDefinitionType.Workflow,
        };
}
