namespace ApexPost.Web.Security;

// Endpoint metadata read by ApiKeyAndIpRestrictionMiddleware: the endpoint is
// for integration partners and needs a valid X-API-Key from an allowed IP.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class RequireApiKeyAttribute : Attribute
{
}

// Endpoint metadata read by AdminIpRestrictionMiddleware: the endpoint is an
// administration surface, reachable only from AdminSecurity:AllowedIpAddresses
// when that allowlist is configured.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class RequireAdminNetworkAttribute : Attribute
{
}
