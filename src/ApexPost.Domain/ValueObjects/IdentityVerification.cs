namespace ApexPost.Domain.ValueObjects;

public sealed record IdentityVerification
{
	public string DocumentType { get; }
	public string MaskedDocumentIdentifier { get; }
	public bool IsSuccessful { get; }

	public IdentityVerification(
		string documentType,
		string documentIdentifier,
		bool isSuccessful)
	{
		if (string.IsNullOrWhiteSpace(documentType))
			throw new ArgumentException(
				"Document type is required.", nameof(documentType));

		ArgumentNullException.ThrowIfNull(documentIdentifier);

		var normalizedIdentifier = documentIdentifier.Trim();
		if (normalizedIdentifier.Length < 5 ||
			normalizedIdentifier.Any(char.IsControl))
		{
			throw new ArgumentException(
				"Document identifier must contain at least five non-control characters.",
				nameof(documentIdentifier));
		}

		DocumentType = documentType.Trim();
		MaskedDocumentIdentifier =
			new string('*', normalizedIdentifier.Length - 4)
			+ normalizedIdentifier[^4..];
		IsSuccessful = isSuccessful;
	}
}
