using System.Security.Cryptography;
using System.Text;

namespace Avand.Application.Api;

/// <summary>Resolves the customer from the authenticated credential. Body IDs and retry keys never grant authority.</summary>
internal static class CustomerAccess
{
    public static RouteGroupBuilder MapCustomerGroup(this WebApplication app, string prefix)
    {
        var customers = Customers(app.Configuration);
        return app.MapGroup(prefix).AddEndpointFilter(async (context, next) =>
        {
            var request = context.HttpContext.Request;
            var authenticated = customers.FirstOrDefault(customer => Matches(request, customer.Token));
            if (authenticated.Id == Guid.Empty)
                return Results.Unauthorized();

            context.HttpContext.Items[typeof(CustomerAccess)] = authenticated.Id;
            return await next(context);
        });
    }

    public static Guid Customer(HttpContext context) => (Guid)context.Items[typeof(CustomerAccess)]!;

    /// <summary>Fails startup when any customer, provider or service credential is reused.</summary>
    public static void RequireDistinctCredentials(IConfiguration configuration)
    {
        var tokens = Customers(configuration).Select(x => x.Token)
            .Append(configuration["V01:CallbackToken"])
            .Append(configuration["V01:LedgerToken"])
            .Append(configuration["V02:LedgerToken"])
            .Where(token => !string.IsNullOrWhiteSpace(token)).ToArray();
        if (tokens.Distinct(StringComparer.Ordinal).Count() != tokens.Length)
            throw new InvalidOperationException("Customer, provider and service credentials must be distinct.");
    }

    public static bool Matches(HttpRequest request, string? token) =>
        !string.IsNullOrWhiteSpace(token) && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(request.Headers.Authorization.ToString()), Encoding.UTF8.GetBytes("Bearer " + token));

    private static (Guid Id, string? Token)[] Customers(IConfiguration configuration) =>
        configuration.GetSection("V01:Customers").GetChildren()
            .Select(section => (Id: Guid.Parse(section.Key), Token: section.Value))
            .ToArray();
}
