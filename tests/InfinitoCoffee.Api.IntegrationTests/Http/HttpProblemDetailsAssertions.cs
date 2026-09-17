using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

internal static class HttpProblemDetailsAssertions
{
    public static async Task<ProblemDetails> AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal((int)expectedStatus, problemDetails.Status);
        Assert.False(string.IsNullOrWhiteSpace(problemDetails.Title));
        Assert.False(string.IsNullOrWhiteSpace(problemDetails.Detail));
        Assert.True(problemDetails.Extensions.ContainsKey("traceId"));

        return problemDetails;
    }
}
