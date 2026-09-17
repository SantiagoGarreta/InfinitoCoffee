using System.Net;
using System.Net.Http.Json;
using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class AuthorizationEndpointTests
{
    public static IEnumerable<object[]> PrivateEndpointMatrix()
    {
        var allRoles = Enum.GetValues<UserRole>();
        var endpoints = new[]
        {
            Endpoint("orders-create", HttpStatusCode.Created, UserRole.Administrator, UserRole.Cashier),
            Endpoint("orders-get-by-id", HttpStatusCode.OK, allRoles),
            Endpoint("orders-active", HttpStatusCode.OK, allRoles),
            Endpoint("orders-start-preparation", HttpStatusCode.OK, allRoles),
            Endpoint("orders-mark-ready", HttpStatusCode.OK, allRoles),
            Endpoint("orders-deliver", HttpStatusCode.OK, allRoles),
            Endpoint("orders-cancel", HttpStatusCode.OK, allRoles),
            Endpoint("products-get-all", HttpStatusCode.OK, UserRole.Administrator, UserRole.Cashier),
            Endpoint("products-get-by-id", HttpStatusCode.OK, UserRole.Administrator, UserRole.Cashier),
            Endpoint("products-create", HttpStatusCode.Created, UserRole.Administrator),
            Endpoint("products-update", HttpStatusCode.OK, UserRole.Administrator),
            Endpoint("products-activate", HttpStatusCode.OK, UserRole.Administrator),
            Endpoint("products-deactivate", HttpStatusCode.OK, UserRole.Administrator),
            Endpoint("categories-get-all", HttpStatusCode.OK, UserRole.Administrator, UserRole.Cashier),
            Endpoint("categories-get-by-id", HttpStatusCode.OK, UserRole.Administrator, UserRole.Cashier),
            Endpoint("categories-create", HttpStatusCode.Created, UserRole.Administrator),
            Endpoint("categories-update", HttpStatusCode.OK, UserRole.Administrator),
            Endpoint("categories-activate", HttpStatusCode.OK, UserRole.Administrator),
            Endpoint("categories-deactivate", HttpStatusCode.OK, UserRole.Administrator)
        };

        foreach (var endpoint in endpoints)
        {
            yield return [endpoint.Name, null!, HttpStatusCode.Unauthorized];
            foreach (var role in allRoles)
            {
                yield return
                [
                    endpoint.Name,
                    (UserRole?)role,
                    endpoint.AllowedRoles.Contains(role) ? endpoint.SuccessStatus : HttpStatusCode.Forbidden
                ];
            }
        }
    }

    [Theory]
    [MemberData(nameof(PrivateEndpointMatrix))]
    public async Task PrivateEndpoint_EnforcesApprovedRoleMatrix(
        string endpointName,
        UserRole? role,
        HttpStatusCode expectedStatus)
    {
        await using var api = new ApiTestContext();
        var resources = await SeedResourcesAsync(api);
        if (role.HasValue)
        {
            await api.AuthenticateAsync(role.Value);
        }

        var before = await CaptureMutationSnapshotAsync(api);
        using var request = CreateRequest(endpointName, resources);
        var response = role.HasValue && IsMutable(request.Method)
            ? await api.SendWithCsrfAsync(request)
            : await api.Client.SendAsync(request);

        if (expectedStatus is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(response, expectedStatus);
            Assert.Null(response.Headers.Location);
        }
        else
        {
            Assert.Equal(expectedStatus, response.StatusCode);
        }
        if (expectedStatus is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            && IsMutable(request.Method))
        {
            Assert.Equal(before, await CaptureMutationSnapshotAsync(api));
        }
    }

    private static EndpointDefinition Endpoint(
        string name,
        HttpStatusCode successStatus,
        params UserRole[] allowedRoles)
    {
        return new EndpointDefinition(name, successStatus, allowedRoles);
    }

    private static async Task<TestResources> SeedResourcesAsync(ApiTestContext api)
    {
        return await api.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(dbContext, "Coffee");
            var nextCategory = await TestDataSeeder.AddCategoryAsync(dbContext, "Tea");
            var inactiveCategory = await TestDataSeeder.AddCategoryAsync(dbContext, "Inactive category", isActive: false);
            var product = await TestDataSeeder.AddProductAsync(dbContext, category.Id, "Latte");
            var inactiveProduct = await TestDataSeeder.AddProductAsync(
                dbContext,
                category.Id,
                "Inactive product",
                isActive: false);
            var pendingForStart = await TestDataSeeder.AddOrderAsync(
                dbContext,
                "AUTH-START",
                OrderStatus.Pending,
                DateTime.UtcNow.AddMinutes(-4));
            var preparing = await TestDataSeeder.AddOrderAsync(
                dbContext,
                "AUTH-READY",
                OrderStatus.Preparing,
                DateTime.UtcNow.AddMinutes(-3));
            var ready = await TestDataSeeder.AddOrderAsync(
                dbContext,
                "AUTH-DELIVER",
                OrderStatus.Ready,
                DateTime.UtcNow.AddMinutes(-2));
            var pendingForCancel = await TestDataSeeder.AddOrderAsync(
                dbContext,
                "AUTH-CANCEL",
                OrderStatus.Pending,
                DateTime.UtcNow.AddMinutes(-1));

            return new TestResources(
                category.Id,
                nextCategory.Id,
                inactiveCategory.Id,
                product.Id,
                inactiveProduct.Id,
                pendingForStart.Id,
                preparing.Id,
                ready.Id,
                pendingForCancel.Id);
        });
    }

    private static HttpRequestMessage CreateRequest(string endpointName, TestResources resources)
    {
        return endpointName switch
        {
            "orders-create" => JsonRequest(HttpMethod.Post, "/api/orders", new
            {
                orderNumber = "AUTH-CREATE",
                notes = (string?)null,
                items = new[] { new { productId = resources.ProductId, quantity = 1, notes = (string?)null } }
            }),
            "orders-get-by-id" => new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{resources.PendingForStartId}"),
            "orders-active" => new HttpRequestMessage(HttpMethod.Get, "/api/orders/active"),
            "orders-start-preparation" => EmptyPost($"/api/orders/{resources.PendingForStartId}/start-preparation"),
            "orders-mark-ready" => EmptyPost($"/api/orders/{resources.PreparingOrderId}/mark-ready"),
            "orders-deliver" => EmptyPost($"/api/orders/{resources.ReadyOrderId}/deliver"),
            "orders-cancel" => EmptyPost($"/api/orders/{resources.PendingForCancelId}/cancel"),
            "products-get-all" => new HttpRequestMessage(HttpMethod.Get, "/api/products"),
            "products-get-by-id" => new HttpRequestMessage(HttpMethod.Get, $"/api/products/{resources.ProductId}"),
            "products-create" => JsonRequest(HttpMethod.Post, "/api/products", new
            {
                name = "Authorization product",
                description = "Authorization matrix",
                price = 200m,
                categoryId = resources.CategoryId
            }),
            "products-update" => JsonRequest(HttpMethod.Put, $"/api/products/{resources.ProductId}", new
            {
                name = "Updated authorization product",
                description = "Updated",
                price = 250m,
                categoryId = resources.NextCategoryId
            }),
            "products-activate" => EmptyPost($"/api/products/{resources.InactiveProductId}/activate"),
            "products-deactivate" => EmptyPost($"/api/products/{resources.ProductId}/deactivate"),
            "categories-get-all" => new HttpRequestMessage(HttpMethod.Get, "/api/product-categories"),
            "categories-get-by-id" => new HttpRequestMessage(HttpMethod.Get, $"/api/product-categories/{resources.CategoryId}"),
            "categories-create" => JsonRequest(HttpMethod.Post, "/api/product-categories", new { name = "Authorization category" }),
            "categories-update" => JsonRequest(
                HttpMethod.Put,
                $"/api/product-categories/{resources.CategoryId}",
                new { name = "Updated authorization category" }),
            "categories-activate" => EmptyPost($"/api/product-categories/{resources.InactiveCategoryId}/activate"),
            "categories-deactivate" => EmptyPost($"/api/product-categories/{resources.CategoryId}/deactivate"),
            _ => throw new ArgumentOutOfRangeException(nameof(endpointName), endpointName, "Unknown endpoint case.")
        };
    }

    private static HttpRequestMessage JsonRequest<T>(HttpMethod method, string uri, T value)
    {
        return new HttpRequestMessage(method, uri) { Content = JsonContent.Create(value) };
    }

    private static HttpRequestMessage EmptyPost(string uri)
    {
        return new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(new { }) };
    }

    private static bool IsMutable(HttpMethod method)
    {
        return method == HttpMethod.Post
            || method == HttpMethod.Put
            || method == HttpMethod.Patch
            || method == HttpMethod.Delete;
    }

    private static Task<DatabaseSnapshot> CaptureMutationSnapshotAsync(ApiTestContext api)
    {
        return api.ExecuteDbContextAsync(async dbContext => new DatabaseSnapshot(
            string.Join('|', await dbContext.Orders.AsNoTracking()
                .OrderBy(order => order.Id)
                .Select(order => order.Id + ":" + order.Status)
                .ToArrayAsync()),
            string.Join('|', await dbContext.Products.AsNoTracking()
                .OrderBy(product => product.Id)
                .Select(product => product.Id + ":" + product.Name + ":" + product.Price + ":" + product.CategoryId + ":" + product.IsActive)
                .ToArrayAsync()),
            string.Join('|', await dbContext.ProductCategories.AsNoTracking()
                .OrderBy(category => category.Id)
                .Select(category => category.Id + ":" + category.Name + ":" + category.IsActive)
                .ToArrayAsync())));
    }

    private sealed record EndpointDefinition(
        string Name,
        HttpStatusCode SuccessStatus,
        IReadOnlyCollection<UserRole> AllowedRoles);

    private sealed record DatabaseSnapshot(string Orders, string Products, string Categories);

    private sealed record TestResources(
        Guid CategoryId,
        Guid NextCategoryId,
        Guid InactiveCategoryId,
        Guid ProductId,
        Guid InactiveProductId,
        Guid PendingForStartId,
        Guid PreparingOrderId,
        Guid ReadyOrderId,
        Guid PendingForCancelId);
}
