namespace ApexPost.Web.Models;

public sealed record MoneyOrderListItemViewModel(
	decimal Amount,
	string Currency,
	string Sender,
	string Recipient,
	decimal Fee,
	string? DocumentType,
	string? MaskedDocumentIdentifier,
	bool? IdentityCheckPassed,
	MoneyOrderStatus Status);
