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
		: this(
			documentType,
			MaskDocumentIdentifier(documentIdentifier),
			isSuccessful,
			true)
	{
	}

	private IdentityVerification(
		string documentType,
		string maskedDocumentIdentifier,
		bool isSuccessful,
		bool isMasked)
	{
		if (string.IsNullOrWhiteSpace(documentType))
			throw new ArgumentException(
				"Document type is required.", nameof(documentType));

		if (!isMasked ||
			string.IsNullOrWhiteSpace(maskedDocumentIdentifier) ||
			maskedDocumentIdentifier.Length < 5 ||
			maskedDocumentIdentifier.Any(char.IsControl) ||
			maskedDocumentIdentifier[..^4].Any(character => character != '*'))
		{
			throw new ArgumentException(
				"Masked document identifier must contain a mask and four trailing characters.",
				nameof(maskedDocumentIdentifier));
		}

		DocumentType = documentType.Trim();
		MaskedDocumentIdentifier = maskedDocumentIdentifier;
		IsSuccessful = isSuccessful;
	}

	public static IdentityVerification FromMaskedIdentifier(
		string documentType,
		string maskedDocumentIdentifier,
		bool isSuccessful) =>
		new(documentType, maskedDocumentIdentifier, isSuccessful, true);

	private static string MaskDocumentIdentifier(string documentIdentifier)
	{
		ArgumentNullException.ThrowIfNull(documentIdentifier);

		var normalizedIdentifier = documentIdentifier.Trim();
		if (normalizedIdentifier.Length < 5 ||
			normalizedIdentifier.Any(char.IsControl))
		{
			throw new ArgumentException(
				"Document identifier must contain at least five non-control characters.",
				nameof(documentIdentifier));
		}

		return new string('*', normalizedIdentifier.Length - 4)
			+ normalizedIdentifier[^4..];
	}
}
