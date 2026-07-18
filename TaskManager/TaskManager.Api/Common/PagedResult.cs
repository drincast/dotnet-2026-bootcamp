namespace TaskManager.Api.Common
{
    /// <summary>
    /// Record para el manejo de paginación para resultados
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="Page"></param>
    /// <param name="PageSize"></param>
    /// <param name="TotalItems"></param>
    /// <param name="Items"></param>
    public sealed record PagedResult<T>(
        int Page,
        int PageSize,
        int TotalItems,
        IReadOnlyList<T> Items)
    {
        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
    }
}
