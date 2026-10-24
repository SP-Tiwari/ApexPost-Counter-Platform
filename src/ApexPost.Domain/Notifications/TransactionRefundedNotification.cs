namespace ApexPost.Domain.Notifications;

public sealed record TransactionRefundedNotification(string Reason)
	: DomainNotification;
