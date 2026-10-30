using System.Text.Json;
using System.Text.Json.Serialization;
using ApexPost.Application.Security;

namespace ApexPost.Infrastructure.Persistence;

public sealed class JsonStaffCredentialRepository
	: IStaffCredentialRepository, IStaffCredentialProvisioning, IDisposable
{
	private const int CurrentSchemaVersion = 1;
	private readonly string _filePath;
	private readonly SemaphoreSlim _writeLock = new(1, 1);
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

	public void Dispose() => _writeLock.Dispose();

	public async Task<StaffCredential?> FindAsync(
		string staffId,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(staffId))
			throw new ArgumentException("Staff ID is required.", nameof(staffId));

		var credentials = await LoadAllAsync(cancellationToken)
			.ConfigureAwait(false);
		return credentials.SingleOrDefault(credential =>
			string.Equals(
				credential.StaffId, staffId.Trim(), StringComparison.Ordinal));
	}

	public async Task<IReadOnlySet<string>> GetStaffIdsAsync(
		CancellationToken cancellationToken = default) =>
		File.Exists(_filePath)
			? (await LoadAllAsync(cancellationToken).ConfigureAwait(false))
				.Select(credential => credential.StaffId)
				.ToHashSet(StringComparer.Ordinal)
			: new HashSet<string>(StringComparer.Ordinal);

	// Creates the credential file when it does not exist. The document is
	// written to a temporary file and moved into place, so readers never see
	// a partial file.
	public async Task<int> AddMissingAsync(
		IReadOnlyCollection<StaffCredential> credentials,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(credentials);
		foreach (var credential in credentials)
			StaffPasswordHashVerifier.Validate(credential);

		await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var existing = File.Exists(_filePath)
				? await LoadAllAsync(cancellationToken).ConfigureAwait(false)
				: [];
			var knownIds = existing
				.Select(credential => credential.StaffId)
				.ToHashSet(StringComparer.Ordinal);
			var added = credentials
				.Where(credential => knownIds.Add(credential.StaffId))
				.ToArray();
			if (added.Length == 0)
				return 0;

			var document = new PersistedCredentialDocument
			{
				SchemaVersion = CurrentSchemaVersion,
				Credentials = existing.Concat(added).Select(ToPersisted).ToList()
			};
			var directory = Path.GetDirectoryName(_filePath)
				?? throw new InvalidOperationException(
					"Credential file path must include a directory.");
			Directory.CreateDirectory(directory);
			var temporaryFile = Path.Combine(
				directory, $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
			try
			{
				await using (var stream = new FileStream(
					temporaryFile,
					FileMode.CreateNew,
					FileAccess.Write,
					FileShare.None,
					4096,
					FileOptions.Asynchronous))
				{
					await JsonSerializer.SerializeAsync(
						stream, document, _serializerOptions, cancellationToken)
						.ConfigureAwait(false);
				}

				File.Move(temporaryFile, _filePath, overwrite: true);
			}
			finally
			{
				if (File.Exists(temporaryFile))
					File.Delete(temporaryFile);
			}

			return added.Length;
		}
		finally
		{
			_writeLock.Release();
		}
	}

	private async Task<StaffCredential[]> LoadAllAsync(
		CancellationToken cancellationToken)
	{
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

		return credentials;
	}

	private static PersistedStaffCredential ToPersisted(
		StaffCredential credential) =>
		new()
		{
			StaffId = credential.StaffId,
			HashVersion = credential.Version,
			Iterations = credential.Iterations,
			Salt = credential.Salt,
			PasswordHash = credential.PasswordHash
		};

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
