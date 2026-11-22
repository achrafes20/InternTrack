using Microsoft.EntityFrameworkCore;

namespace InternTrack.Application.Common.Models;

public class PaginatedList<T>
{
    public const int MaxPageSize = 100;

    public IReadOnlyCollection<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalPages { get; }
    public int TotalCount { get; }

    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PaginatedList(IReadOnlyCollection<T> items, int count, int pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        TotalCount = count;
        Items = items;
    }

    /// <summary>Borne les paramètres de pagination fournis par le client (anti-DoS, pas d'OFFSET négatif).</summary>
    public static (int PageNumber, int PageSize) Normalize(int pageNumber, int pageSize)
        => (Math.Max(1, pageNumber), Math.Clamp(pageSize, 1, MaxPageSize));

    public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        (pageNumber, pageSize) = Normalize(pageNumber, pageSize);

        var count = await source.CountAsync(cancellationToken);
        var items = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PaginatedList<T>(items, count, pageNumber, pageSize);
    }
}
