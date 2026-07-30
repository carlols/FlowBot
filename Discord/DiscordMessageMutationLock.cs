namespace FlowBot;

public sealed class DiscordMessageMutationLock
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, Entry> _entries = [];

    public async Task<IDisposable> AcquireAsync(ulong messageId)
    {
        Entry entry;

        lock (_gate)
        {
            if (!_entries.TryGetValue(messageId, out entry!))
            {
                entry = new Entry();
                _entries.Add(messageId, entry);
            }

            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync();
            return new Lease(this, messageId, entry);
        }
        catch
        {
            RemoveReference(messageId, entry);
            throw;
        }
    }

    private void Release(ulong messageId, Entry entry)
    {
        entry.Semaphore.Release();
        RemoveReference(messageId, entry);
    }

    private void RemoveReference(ulong messageId, Entry entry)
    {
        lock (_gate)
        {
            entry.ReferenceCount--;

            if (entry.ReferenceCount == 0)
            {
                _entries.Remove(messageId);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int ReferenceCount { get; set; }
    }

    private sealed class Lease(
        DiscordMessageMutationLock owner,
        ulong messageId,
        Entry entry) : IDisposable
    {
        private DiscordMessageMutationLock? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release(messageId, entry);
        }
    }
}