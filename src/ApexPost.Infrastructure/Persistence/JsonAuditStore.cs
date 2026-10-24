using System.Text.Json;
using ApexPost.Application.Auditing;

namespace ApexPost.Infrastructure.Persistence;

public sealed class JsonAuditStore : IAuditStore, IDisposable
{
	private readonly string _filePath;
	private readonly SemaphoreSlim _writeLock = new(1, 1);
	private readonly JsonSerializerOptions _serializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public JsonAuditStore(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			throw new ArgumentException(
				"Audit file path is required.", nameof(filePath));
		}

		_filePath = Path.GetFullPath(filePath);
	}

	public void Dispose() => _writeLock.Dispose();

	public async Task AppendAsync(
		AuditRecord record,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(record);

		var json = JsonSerializer.SerializeToUtf8Bytes(
			record, _serializerOptions);
		Array.Resize(ref json, json.Length + 1);
		json[^1] = (byte)'\n';

		await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var directory = Path.GetDirectoryName(_filePath)
				?? throw new InvalidOperationException(
					"Audit file path must include a directory.");
			Directory.CreateDirectory(directory);

			await using var stream = new FileStream(
				_filePath,
				FileMode.Append,
				FileAccess.Write,
				FileShare.Read,
				4096,
				FileOptions.Asynchronous | FileOptions.SequentialScan);
			await stream.WriteAsync(json, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			_writeLock.Release();
		}
	}
}
