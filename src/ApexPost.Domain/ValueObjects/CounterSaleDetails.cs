namespace ApexPost.Domain.ValueObjects;

// Where, by whom and how a counter sale was taken. The payment method is a
// configured name only; no card or account data is ever recorded here.
public sealed record CounterSaleDetails
{
	// C# 14 field keyword: each init accessor validates and trims in place,
	// so no private backing fields or separate assignments are needed.
	public string CounterTerminalId
	{
		get;
		init => field = Required(value, "Counter terminal ID");
	}

	public string ClerkStaffId
	{
		get;
		init => field = Required(value, "Clerk staff ID");
	}

	public string PaymentMethod
	{
		get;
		init => field = Required(value, "Payment method");
	}

	public CounterSaleDetails(
		string counterTerminalId,
		string clerkStaffId,
		string paymentMethod)
	{
		CounterTerminalId = counterTerminalId;
		ClerkStaffId = clerkStaffId;
		PaymentMethod = paymentMethod;
	}

	private static string Required(string value, string description) =>
		string.IsNullOrWhiteSpace(value)
			? throw new ArgumentException($"{description} is required.")
			: value.Trim();
}
