namespace ApexPost.Domain.Notifications;

public sealed record TransactionVoidedNotification(string Reason)
	: DomainNotification;
