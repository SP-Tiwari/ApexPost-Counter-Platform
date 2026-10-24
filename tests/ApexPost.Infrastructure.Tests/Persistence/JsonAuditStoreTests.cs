using System.Text.Json;
using ApexPost.Application.Auditing;
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Infrastructure.Tests.Persistence;

public sealed class JsonAuditStoreTests : IAsyncLifetime
{
	private static readonly JsonSerializerOptions AuditJsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};
	private string _directory = null!;
	private string _filePath = null!;

	public Task InitializeAsync()
	{
		_directory = Path.Combine(
			Path.GetTempPath(), $"ApexPost-Audit-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_directory);
		_filePath = Path.Combine(_directory, "audit-events.jsonl");
		return Task.CompletedTask;
	}

	public Task DisposeAsync()
	{
		if (Directory.Exists(_directory))
			Directory.Delete(_directory, recursive: true);

		return Task.CompletedTask;
	}

	[Fact]
	public async Task AppendAsync_PreservesPreviouslyWrittenRecords()
	{
		using var store = new JsonAuditStore(_filePath);
		var first = CreateRecord("First");
		var second = CreateRecord("Second");

		await store.AppendAsync(first);
		var firstLine = await File.ReadAllTextAsync(_filePath);
		await store.AppendAsync(second);

		var lines = await File.ReadAllLinesAsync(_filePath);
		Assert.Equal(2, lines.Length);
		Assert.Equal(firstLine.TrimEnd('\r', '\n'), lines[0]);
		Assert.Equal("First", Deserialize(lines[0]).Action);
		Assert.Equal("Second", Deserialize(lines[1]).Action);
	}

	[Fact]
	public async Task AppendAsync_ConcurrentCallsWriteCompleteRecords()
	{
		using var store = new JsonAuditStore(_filePath);
		const int recordCount = 100;

		await Task.WhenAll(Enumerable.Range(0, recordCount).Select(index =>
			store.AppendAsync(CreateRecord($"Action-{index}"))));

		var lines = await File.ReadAllLinesAsync(_filePath);
		Assert.Equal(recordCount, lines.Length);
		var records = lines.Select(Deserialize).ToArray();
		Assert.Equal(
			recordCount,
			records.Select(record => record.Action)
				.Distinct(StringComparer.Ordinal).Count());
	}

	[Fact]
	public async Task AppendAsync_CanceledBeforeWriteDoesNotCreateAuditFile()
	{
		using var store = new JsonAuditStore(_filePath);
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => store.AppendAsync(
				CreateRecord("Canceled"),
				cancellation.Token));

		Assert.False(File.Exists(_filePath));
	}

	private static AuditRecord CreateRecord(string action) =>
		new(
			DateTimeOffset.UtcNow,
			"AuditProbe",
			action,
			"Succeeded",
			null,
			null,
			null);

	private static AuditRecord Deserialize(string json) =>
		JsonSerializer.Deserialize<AuditRecord>(
			json, AuditJsonOptions)
		?? throw new InvalidDataException("Audit record could not be read.");
}
