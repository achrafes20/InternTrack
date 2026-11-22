namespace InternTrack.Infrastructure.Services.Email;

/// <summary>Réveille le processeur de l'outbox dès qu'un e-mail est validé en base (sans attendre le prochain cycle).</summary>
public sealed class EmailOutboxSignal
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Notify()
    {
        try
        {
            if (_semaphore.CurrentCount == 0)
                _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Déjà signalé.
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        => _semaphore.WaitAsync(timeout, cancellationToken);
}
