using System.Collections.Concurrent;
using System.Reflection;

namespace ApexPost.Application.Validation;

// Declarative rules for application request records, checked by
// RequestValidator. Apply them to positional record members with the
// property: target, for example [property: NotBlank].
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public abstract class RequestRuleAttribute : Attribute
{
	// Returns an error message, or null when the value is valid.
	public abstract string? Validate(object? value, string memberName);
}

public sealed class NotBlankAttribute : RequestRuleAttribute
{
	public override string? Validate(object? value, string memberName) =>
		value is string text && !string.IsNullOrWhiteSpace(text)
			? null
			: $"{memberName} is required.";
}

public sealed class GreaterThanZeroAttribute : RequestRuleAttribute
{
	public override string? Validate(object? value, string memberName) =>
		value switch
		{
			int number when number > 0 => null,
			decimal number when number > 0m => null,
			_ => $"{memberName} must be greater than zero."
		};
}

public sealed class DefinedEnumAttribute : RequestRuleAttribute
{
	public override string? Validate(object? value, string memberName) =>
		value is Enum enumValue && Enum.IsDefined(enumValue.GetType(), enumValue)
			? null
			: $"{memberName} has an unsupported value.";
}

public static class RequestValidator
{
	private sealed record PropertyRule(
		PropertyInfo Property,
		RequestRuleAttribute Rule);

	// Reflection runs once per request type; later validations reuse the
	// cached property/rule pairs.
	private static readonly ConcurrentDictionary<Type, PropertyRule[]> Rules = new();

	public static void Validate<T>(T request)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(request);

		var errors = new List<string>();
		foreach (var (property, rule) in Rules.GetOrAdd(typeof(T), Discover))
		{
			if (rule.Validate(property.GetValue(request), property.Name) is { } error)
				errors.Add(error);
		}

		if (errors.Count > 0)
			throw new ArgumentException(string.Join(" ", errors), typeof(T).Name);
	}

	public static void ValidateAll<T>(IEnumerable<T> requests)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(requests);
		foreach (var request in requests)
			Validate(request);
	}

	private static PropertyRule[] Discover(Type type) =>
		type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
			.SelectMany(property => property
				.GetCustomAttributes<RequestRuleAttribute>(inherit: true)
				.Select(rule => new PropertyRule(property, rule)))
			.ToArray();
}
