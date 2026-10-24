namespace ApexPost.Application.Security;

public sealed record StaffCredential(
	string StaffId,
	int Version,
	int Iterations,
	string Salt,
	string PasswordHash);
