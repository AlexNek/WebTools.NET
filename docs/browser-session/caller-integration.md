# Caller Integration

`BrowserSession` supplies browser capabilities and current state. The caller
decides which operation to request and what to do with each snapshot.

## Sequential workflow

```csharp
var snapshot = await session.StartAsync("https://test.example.com");

while (snapshot.Error is null && snapshot.HasMoreContent)
{
    snapshot = await session.ExecuteAsync(
        new BrowserOperation(EBrowserOperationType.ScrollDown));
}
```

A caller can instead choose operations from application state, a workflow
engine, or an external orchestration layer. The library does not interpret the
snapshot or select the next operation.

## Separate workflows

Create a separate supplied browser session and `BrowserSession` for each
independent workflow:

```csharp
await using var firstBrowser = factory.Create();
await using var firstSession = new BrowserSession(firstBrowser);

await using var secondBrowser = factory.Create();
await using var secondSession = new BrowserSession(secondBrowser);
```

The sessions are isolated and have independent operation history, page state,
and storage lifecycle. A wrapper can be disposed without disposing the supplied
browser session, so the caller controls the resource boundary explicitly.

## Cancellation and failures

Pass a `CancellationToken` to `StartAsync`, `ExecuteAsync`, or
`GetSnapshotAsync`. Caller cancellation is propagated. Browser and page
failures that can be represented as page state are returned through
`BrowserSnapshot.Error`; callers decide whether to retry, stop, or create a new
workflow.
