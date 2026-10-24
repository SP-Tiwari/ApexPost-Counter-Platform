namespace ApexPost.Application.Auditing;

public sealed record AuditRecord(
	DateTimeOffset TimestampUtc,
	string Controller,
	string Action,
	string Outcome,
	string? StaffId,
	string? BranchCode,
	string? CorrelationId);
