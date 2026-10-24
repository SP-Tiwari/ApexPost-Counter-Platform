namespace ApexPost.Application.Auditing;

public interface IAuditStore
{
	Task AppendAsync(
		AuditRecord record,
		CancellationToken cancellationToken = default);
}
