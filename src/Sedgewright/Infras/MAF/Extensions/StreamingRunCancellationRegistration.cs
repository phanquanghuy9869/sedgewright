using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;

namespace Sedgewright.Infras.MAF.Extensions;

internal sealed class StreamingRunCancellationRegistration : IAsyncDisposable
{
	private readonly StreamingRun _run;
	private readonly CancellationTokenRegistration _registration;
	private Task? _cancellationTask;
	private int _cancellationStarted;

	public StreamingRunCancellationRegistration(StreamingRun run, CancellationToken cancellationToken)
	{
		_run = run;
		_registration = cancellationToken.Register(
			static state => ((StreamingRunCancellationRegistration)state!).CancelRun(),
			this);
	}

	public async ValueTask DisposeAsync()
	{
		_registration.Dispose();
		if (_cancellationTask is not null)
		{
			await _cancellationTask.ConfigureAwait(false);
		}
	}

	private void CancelRun()
	{
		if (Interlocked.Exchange(ref _cancellationStarted, 1) != 0)
		{
			return;
		}

		try
		{
			_cancellationTask = _run.CancelRunAsync().AsTask();
		}
		catch (Exception exception)
		{
			_cancellationTask = Task.FromException(exception);
		}
	}
}
