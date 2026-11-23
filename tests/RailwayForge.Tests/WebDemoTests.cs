using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RailwayForge.WebDemo;

namespace RailwayForge.Tests;

public sealed class WebDemoTests
{
    [Fact]
    public async Task WebDemo_EndToEnd_Pipeline_ProblemDetails_And_AppendOnly_History()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        // 1. Serves interactive HTML dashboard
        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("RailwayForge", await home.Content.ReadAsStringAsync());

        // 2. Happy path -> 201 Created, 4 green steps
        var okResp = await client.PostAsJsonAsync("/api/orders",
            new OrderRequest("alice@forge.io", "RF-PRO-100", 2, "tok_live_1234"));
        Assert.Equal(HttpStatusCode.Created, okResp.StatusCode);

        // 3. Multi-field ValidationBag failure -> 400 VALIDATION_MULTIPLE
        var valResp = await client.PostAsJsonAsync("/api/orders",
            new OrderRequest("bad-email", "", 0, ""));
        Assert.Equal(HttpStatusCode.BadRequest, valResp.StatusCode);
        Assert.Contains("VALIDATION_MULTIPLE", await valResp.Content.ReadAsStringAsync());

        // 4. Unknown SKU -> 404 NOT_FOUND
        var nfResp = await client.PostAsJsonAsync("/api/orders",
            new OrderRequest("alice@forge.io", "NO-SUCH-SKU", 1, "tok_live_1234"));
        Assert.Equal(HttpStatusCode.NotFound, nfResp.StatusCode);
        Assert.Contains("NOT_FOUND", await nfResp.Content.ReadAsStringAsync());

        // 5. Out of stock -> 409 CONFLICT
        var conflictResp = await client.PostAsJsonAsync("/api/orders",
            new OrderRequest("alice@forge.io", "RF-EMPTY-0", 1, "tok_live_1234"));
        Assert.Equal(HttpStatusCode.Conflict, conflictResp.StatusCode);
        Assert.Contains("CONFLICT", await conflictResp.Content.ReadAsStringAsync());

        // 6. Unauthorized token -> 401 UNAUTHORIZED
        var unauthResp = await client.PostAsJsonAsync("/api/orders",
            new OrderRequest("alice@forge.io", "RF-CELL-24", 1, "bad_token"));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResp.StatusCode);
        Assert.Contains("UNAUTHORIZED", await unauthResp.Content.ReadAsStringAsync());

        // 7. Payment gateway outage -> 503 UNAVAILABLE
        var unavailResp = await client.PostAsJsonAsync("/api/orders",
            new OrderRequest("alice@forge.io", "RF-CELL-24", 1, "tok_live_1234", SimulateGatewayOutage: true));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailResp.StatusCode);
        Assert.Contains("UNAVAILABLE", await unavailResp.Content.ReadAsStringAsync());

        // 8. Verify append-only execution history invariant (6 runs: 1 ok, 5 failed; every trace valid!)
        var histJson = await client.GetStringAsync("/api/history");
        using var doc = JsonDocument.Parse(histJson);
        Assert.Equal(6, doc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("succeeded").GetInt32());
        Assert.Equal(5, doc.RootElement.GetProperty("failed").GetInt32());

        foreach (var entry in doc.RootElement.GetProperty("entries").EnumerateArray())
        {
            bool succeeded = entry.GetProperty("succeeded").GetBoolean();
            var steps = entry.GetProperty("steps").EnumerateArray().ToList();
            Assert.NotEmpty(steps);
            if (succeeded)
            {
                Assert.Equal(4, steps.Count);
                Assert.All(steps, s => Assert.Equal("Ok", s.GetProperty("track").GetString()));
            }
            else
            {
                Assert.Equal("Error", steps[^1].GetProperty("track").GetString());
                for (int i = 0; i < steps.Count - 1; i++)
                    Assert.Equal("Ok", steps[i].GetProperty("track").GetString());
            }
        }

        // 9. Restock restores RF-PRO-100 back to 12
        var restockResp = await client.PostAsync("/api/inventory/restock", null);
        Assert.Equal(HttpStatusCode.OK, restockResp.StatusCode);
        var inv = await restockResp.Content.ReadFromJsonAsync<List<SkuItem>>();
        Assert.Equal(12, Assert.Single(inv!, i => i.Sku == "RF-PRO-100").Stock);
    }
}
