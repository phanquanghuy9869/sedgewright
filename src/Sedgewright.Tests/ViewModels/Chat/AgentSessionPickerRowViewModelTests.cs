using Sedgewright.Models;
using Sedgewright.ViewModels.Chat;

namespace Sedgewright.Tests.ViewModels.Chat;

[TestFixture]
public sealed class AgentSessionPickerRowViewModelTests
{
    [Test]
    public void DisplayText_CombinesSessionAndProviderMetadata()
    {
        AgentSessionPickerRowViewModel row = CreateRow(true, _ => Task.CompletedTask, _ => Task.CompletedTask);

        Assert.That(row.DisplayText, Is.EqualTo("Research Agent · Asked about architecture · gpt-5 · OpenAI"));
    }

    [Test]
    public async Task SelectCommand_InvokesSelectionCallback()
    {
        TaskCompletionSource<AgentSessionSnapshot> callback = new();
        AgentSessionPickerRowViewModel row = CreateRow(
            true,
            session =>
            {
                callback.SetResult(session);
                return Task.CompletedTask;
            },
            _ => Task.CompletedTask);

        row.SelectCommand.Execute(null);

        AgentSessionSnapshot selectedSession = await callback.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(selectedSession.Id, Is.EqualTo(7));
    }

    [Test]
    public async Task DeleteCommand_InvokesDeleteCallback_WhenDeletionIsAllowed()
    {
        TaskCompletionSource<AgentSessionSnapshot> callback = new();
        AgentSessionPickerRowViewModel row = CreateRow(
            true,
            _ => Task.CompletedTask,
            session =>
            {
                callback.SetResult(session);
                return Task.CompletedTask;
            });

        Assert.That(row.DeleteCommand.CanExecute(null), Is.True);

        row.DeleteCommand.Execute(null);

        AgentSessionSnapshot deletedSession = await callback.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(deletedSession.Id, Is.EqualTo(7));
    }

    [Test]
    public void DeleteCommand_IsDisabled_WhenDeletionIsNotAllowed()
    {
        AgentSessionPickerRowViewModel row = CreateRow(false, _ => Task.CompletedTask, _ => Task.CompletedTask);

        Assert.That(row.CanDelete, Is.False);
        Assert.That(row.DeleteCommand.CanExecute(null), Is.False);
    }

    private static AgentSessionPickerRowViewModel CreateRow(
        bool canDelete,
        Func<AgentSessionSnapshot, Task> selectedCallback,
        Func<AgentSessionSnapshot, Task> deleteCallback)
    {
        AgentSessionSnapshot session = new(
            7,
            3,
            "Research Agent",
            "Helps with research",
            "gpt-5",
            "OpenAI",
            "Asked about architecture",
            DateTime.UtcNow,
            DateTime.UtcNow);

        return new AgentSessionPickerRowViewModel(
            session,
            canDelete,
            selectedCallback,
            deleteCallback);
    }
}
