using System.Threading;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.Tests.ViewModels.Shared;

[TestFixture]
public sealed class LoadingFeedbackViewModelTests
{
    [Test]
    public void BeginAndCancel_UpdatesFeedbackAndRequestsCancellation()
    {
        LoadingFeedbackViewModel viewModel = new();

        CancellationToken cancellationToken = viewModel.Begin("Discovering models...");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.IsLoading, Is.True);
            Assert.That(viewModel.Message, Is.EqualTo("Discovering models..."));
            Assert.That(viewModel.CanCancel, Is.True);
            Assert.That(viewModel.CancelCommand.CanExecute(null), Is.True);
            Assert.That(cancellationToken.CanBeCanceled, Is.True);
        }

        viewModel.CancelCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cancellationToken.IsCancellationRequested, Is.True);
            Assert.That(viewModel.Message, Is.EqualTo("Canceling..."));
            Assert.That(viewModel.CanCancel, Is.False);
            Assert.That(viewModel.CancelCommand.CanExecute(null), Is.False);
        }

        viewModel.Complete();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.IsLoading, Is.False);
            Assert.That(viewModel.Message, Is.Empty);
            Assert.That(viewModel.CanCancel, Is.False);
        }
    }

    [Test]
    public void Begin_WhenCancellationIsUnsupported_HidesCancelAction()
    {
        LoadingFeedbackViewModel viewModel = new();

        CancellationToken cancellationToken = viewModel.Begin("Saving changes...", canCancel: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.IsLoading, Is.True);
            Assert.That(viewModel.CanCancel, Is.False);
            Assert.That(viewModel.CancelCommand.CanExecute(null), Is.False);
            Assert.That(cancellationToken.CanBeCanceled, Is.False);
        }

        viewModel.Complete();
    }
}
