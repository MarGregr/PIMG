using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Linq;
using System.Net;

namespace pi.api.Functions;

/// <summary>
/// Obsługa ulubionych stacji
/// </summary>
public class FavoritePoolsFunction
{
    private readonly ILogger<FavoritePoolsFunction> _logger;
    private readonly NpgsqlDataSource _dataSource;

    public FavoritePoolsFunction(ILogger<FavoritePoolsFunction> logger, NpgsqlDataSource dataSource)
    {
        _logger = logger;
        _dataSource = dataSource;
    }

    // GET /api/favorites/pools
    [Function("GetFavoritePools")]
    [Authorize]
    public async Task<IActionResult> GetFavoritePools(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "favorites/pools")] HttpRequest req)
    {
        var userId = GetUserId(req);
        if (string.IsNullOrEmpty(userId))
            return new UnauthorizedResult();

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, pool_id FROM favorite_pools WHERE user_id = @userId";
        cmd.Parameters.AddWithValue("userId", userId);

        var favorites = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            favorites.Add(new { id = reader.GetInt64(0), poolId = reader.GetInt64(1) });

        return new OkObjectResult(favorites);
    }

    // POST /api/favorites/pools/{poolId}
    [Function("AddFavoritePool")]
    [Authorize]
    public async Task<IActionResult> AddFavoritePool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "favorites/pools/{poolId:long}")] HttpRequest req,
        long poolId)
    {
        var userId = GetUserId(req);
        if (string.IsNullOrEmpty(userId))
            return new UnauthorizedResult();

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO favorite_pools (pool_id, user_id)
            VALUES (@poolId, @userId)
            ON CONFLICT (pool_id, user_id) DO NOTHING
            RETURNING id
            """;
        cmd.Parameters.AddWithValue("poolId", poolId);
        cmd.Parameters.AddWithValue("userId", userId);

        var id = await cmd.ExecuteScalarAsync();
        if (id is null)
            return new ConflictObjectResult(new { message = "Pool już jest w ulubionych." });

        return new CreatedResult($"/api/favorites/pools/{poolId}", new { id, poolId });
    }

    // DELETE /api/favorites/pools/{id}
    [Function("DeleteFavoritePool")]
    [Authorize]
    public async Task<IActionResult> DeleteFavoritePool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "favorites/pools/{poolId:long}")] HttpRequest req,
        long poolId)
    {
        var userId = GetUserId(req);
        if (string.IsNullOrEmpty(userId))
            return new UnauthorizedResult();

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM favorite_pools WHERE pool_id = @poolId AND user_id = @userId";
        cmd.Parameters.AddWithValue("poolId", poolId);
        cmd.Parameters.AddWithValue("userId", userId);

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0 ? new NoContentResult() : new NotFoundResult();
    }

    private string GetUserId(HttpRequest req)
    {
        var user = req.HttpContext.User;
        //Odczytanie identyfikatora zalogowanego użytkownika
        return user.Claims.FirstOrDefault(c => c.Type == @"http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value ?? string.Empty;
    }
}
