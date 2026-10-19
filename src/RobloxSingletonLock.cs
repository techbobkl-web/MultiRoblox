namespace MultiRoblox;

/// <summary>
/// Holds the named kernel objects the Roblox client uses to enforce "one client at a time".
///
/// On startup RobloxPlayerBeta.exe creates the mutex "ROBLOX_singletonMutex" (newer builds also
/// use the event "ROBLOX_singletonEvent") and exits if another instance already has it. If we
/// create and hold them first, every client that starts afterwards can run side by side.
///
/// Nothing here touches the Roblox process or its files: these are ordinary named objects in the
/// user's own session namespace (no "Global\" prefix, matching what the client creates).
/// Disposing releases them and restores normal single-instance behaviour.
///
/// A mutex is owned by the thread that acquired it, so call TryAcquire/Release from one thread
/// (the UI thread).
/// </summary>
internal sealed class RobloxSingletonLock : IDisposable
{
    public const string MutexName = "ROBLOX_singletonMutex";
    public const string EventName = "ROBLOX_singletonEvent";

    public enum Result
    {
        Acquired,
        /// <summary>Some other process owns the mutex: another multi-instance tool, or a launcher setting.</summary>
        HeldByOtherProgram,
        Failed,
    }

    private Mutex? _mutex;
    private EventWaitHandle? _event;

    public bool IsHeld => _mutex != null;

    public string? LastError { get; private set; }

    public Result TryAcquire()
    {
        if (IsHeld)
        {
            return Result.Acquired;
        }

        LastError = null;
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(initiallyOwned: false, MutexName, out _);
            bool owned;
            try
            {
                owned = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // The previous owner (e.g. a crashed client) died holding it; we own it now.
                owned = true;
            }

            if (!owned)
            {
                mutex.Dispose();
                return Result.HeldByOtherProgram;
            }

            _mutex = mutex;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException)
        {
            mutex?.Dispose();
            LastError = ex.Message;
            return Result.Failed;
        }

        try
        {
            _event = new EventWaitHandle(false, EventResetMode.ManualReset, EventName, out _);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException)
        {
            // Older clients only check the mutex, so holding that alone is still useful.
            _event = null;
        }

        return Result.Acquired;
    }

    public void Release()
    {
        _event?.Dispose();
        _event = null;

        if (_mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not owned by this thread any more; closing the handle below still frees it.
            }

            _mutex.Dispose();
            _mutex = null;
        }
    }

    public void Dispose() => Release();
}
