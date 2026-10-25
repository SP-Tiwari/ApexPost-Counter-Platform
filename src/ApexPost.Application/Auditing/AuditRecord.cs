namespace ApexPost.Application.Auditing;

public sealed record AuditRecord(
	DateTimeOffset TimestampUtc,
	string Controller,
	string Action,
	string Description,
	string Outcome,
	int StatusCode,
	double DurationMilliseconds,
	IReadOnlyDictionary<string, string> RouteValues,
	string? StaffId,
	string? StaffRole,
	string? BranchCode,
	string? CorrelationId);
