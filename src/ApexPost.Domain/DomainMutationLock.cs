namespace ApexPost.Domain;

internal static class DomainMutationLock
{
	internal static object Gate { get; } = new();
}
