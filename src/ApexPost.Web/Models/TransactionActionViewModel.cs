using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;

public sealed class TransactionActionViewModel
{
	[Required]
	[StringLength(500)]
	public string Reason { get; set; } = string.Empty;
}
