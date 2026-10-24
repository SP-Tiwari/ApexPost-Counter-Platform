using System.Text.Json;
using System.Text.Json.Serialization;
using ApexPost.Application.Security;

namespace ApexPost.Infrastructure.Persistence;

public sealed class JsonStaffCredentialRepository : IStaffCredentialRepository
{
	private const int CurrentSchemaVersion = 1;
	private readonly string _filePath;
	private readonly JsonSerializerOptions _serializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		RespectNullableAnnotations = true,
		RespectRequiredConstructorParameters = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
	};

	public JsonStaffCredentialRepository(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
			throw new ArgumentException(
				"Credential file path is required.", nameof(filePath));

		_filePath = Path.GetFullPath(filePath);
	}

	public async Task<StaffCredential?> FindAsync(
		string staffId,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new ArgumentException("Staff ID is required.", nameof(staffId));

		await using var stream = new FileStream(
			_filePath,
			FileMode.Open,
			FileAccess.Read,
			FileShare.Read | FileShare.Delete,
			4096,
			FileOptions.Asynchronous | FileOptions.SequentialScan);

		PersistedCredentialDocument document;
		try
		{
			document = await JsonSerializer.DeserializeAsync<PersistedCredentialDocument>(
				stream,
				_serializerOptions,
				cancellationToken).ConfigureAwait(false)
				?? throw new InvalidDataException(
					"Credential document cannot be null.");
		}
		catch (JsonException exception)
		{
			throw new InvalidDataException(
				"Credential document is malformed.", exception);
		}

		if (document.SchemaVersion != CurrentSchemaVersion)
			throw new InvalidDataException(
				$"Unsupported credential schema version '{document.SchemaVersion}'.");

		if (document.Credentials is null ||
			document.Credentials.Any(credential => credential is null))
		{
			throw new InvalidDataException(
				"Credential document contains null records.");
		}

		var credentials = document.Credentials.Select(ToCredential).ToArray();
		if (credentials.Select(credential => credential.StaffId)
			.Distinct(StringComparer.Ordinal).Count() != credentials.Length)
		{
			throw new InvalidDataException(
				"Credential records contain duplicate staff IDs.");
		}

		foreach (var credential in credentials)
		{
			if (string.IsNullOrWhiteSpace(credential.StaffId) ||
				!string.Equals(
					credential.StaffId,
					credential.StaffId.Trim(),
					StringComparison.Ordinal))
			{
				throw new InvalidDataException(
					"Credential record contains an invalid staff ID.");
			}

			StaffPasswordHashVerifier.Validate(credential);
		}

		return credentials.SingleOrDefault(credential =>
			string.Equals(
				credential.StaffId, staffId.Trim(), StringComparison.Ordinal));
	}

	private static StaffCredential ToCredential(
		PersistedStaffCredential credential) =>
		new(
			credential.StaffId,
			credential.HashVersion,
			credential.Iterations,
			credential.Salt,
			credential.PasswordHash);
}

internal sealed class PersistedCredentialDocument
{
	public required int SchemaVersion { get; set; }
	public required List<PersistedStaffCredential> Credentials { get; set; }
}

internal sealed class PersistedStaffCredential
{
	public required string StaffId { get; set; }
	public required int HashVersion { get; set; }
	public required int Iterations { get; set; }
	public required string Salt { get; set; }
	public required string PasswordHash { get; set; }
}
