using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;

public sealed class StaffSignInViewModel
{
	[Required]
	[StringLength(128)]
	public string StaffId { get; set; } = string.Empty;

	[Required]
	[StringLength(256, MinimumLength = 1)]
	[DataType(DataType.Password)]
	public string Password { get; set; } = string.Empty;

	public string? ReturnUrl { get; set; }
}
