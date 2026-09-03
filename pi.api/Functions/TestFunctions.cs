using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace pi.api.Functions;

public class TestFunctions
{
  

    // GET /api/favorites/pools
    [Function("Test")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFavoritePools(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "test")] HttpRequest req)
    {
        return new OkObjectResult("test function ok");
    }
}
