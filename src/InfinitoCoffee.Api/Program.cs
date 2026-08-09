using InfinitoCoffee.Api.Extensions;
using InfinitoCoffee.Api.Realtime;
using InfinitoCoffee.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors(ApiServiceCollectionExtensions.DevelopmentCorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health", ApiServiceCollectionExtensions.CreateHealthCheckOptions());
app.MapHub<OrdersHub>("/hubs/orders");
app.MapHub<PickupHub>("/hubs/pickup");

app.Run();

public partial class Program;
