using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.State;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class OperatorRconOperationCoordinatorTests
{
    [TestMethod]
    public async Task OperationsAreSerialized_AndIdleWaitIncludesQueuedWork()
    {
        var coordinator = new OperatorRconOperationCoordinator();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximumActive = 0;

        var first = coordinator.RunExclusiveAsync(async token =>
        {
            var current = Interlocked.Increment(ref active);
            maximumActive = Math.Max(maximumActive, current);
            firstEntered.TrySetResult();
            await releaseFirst.Task.WaitAsync(token);
            Interlocked.Decrement(ref active);
        });
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var second = coordinator.RunExclusiveAsync(_ =>
        {
            var current = Interlocked.Increment(ref active);
            maximumActive = Math.Max(maximumActive, current);
            secondEntered.TrySetResult();
            Interlocked.Decrement(ref active);
            return Task.CompletedTask;
        });
        var idle = coordinator.WaitForIdleAsync();

        await Task.Delay(100);
        Assert.IsFalse(secondEntered.Task.IsCompleted);
        Assert.IsFalse(idle.IsCompleted);

        releaseFirst.TrySetResult();
        await Task.WhenAll(first, second, idle).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(1, maximumActive);
    }

    [TestMethod]
    public async Task ShutdownGate_RejectsNewWorkAndLetsExistingWorkFinish()
    {
        var coordinator = new OperatorRconOperationCoordinator();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = coordinator.RunExclusiveAsync(async token =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        coordinator.StopAcceptingNewOperations();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            coordinator.RunExclusiveAsync(_ => Task.CompletedTask));
        Assert.IsFalse(coordinator.WaitForIdleAsync().IsCompleted);

        release.TrySetResult();
        await running.WaitAsync(TimeSpan.FromSeconds(2));
        await coordinator.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(2));
    }
}
