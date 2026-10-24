using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Notifications;

public sealed record HighValueMoneyOrderNotification(
	Money Amount,
	Money Threshold) : DomainNotification;
