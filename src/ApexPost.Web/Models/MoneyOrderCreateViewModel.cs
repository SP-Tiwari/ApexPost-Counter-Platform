using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;

public sealed class MoneyOrderCreateViewModel
{
	[Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
	public decimal Amount { get; set; }

	[Required]
	[StringLength(100)]
	public string Sender { get; set; } = string.Empty;

	[Required]
	[StringLength(100)]
	public string Recipient { get; set; } = string.Empty;

	[Range(typeof(decimal), "0", "79228162514264337593543950335")]
	public decimal Fee { get; set; }

	[StringLength(50)]
	public string? DocumentType { get; set; }

	[StringLength(100)]
	public string? DocumentIdentifier { get; set; }

	public bool DocumentCheckedInPerson { get; set; }

	public bool IdentityCheckPassed { get; set; }
}

public sealed record MoneyOrderListItemViewModel(
	decimal Amount,
	string Currency,
	string Sender,
	string Recipient,
	decimal Fee,
	string? DocumentType,
	string? MaskedDocumentIdentifier,
	bool? IdentityCheckPassed);
