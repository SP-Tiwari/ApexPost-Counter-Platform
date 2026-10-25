
namespace ApexPost.Domain.Notifications;

public sealed record HighValueMoneyOrderNotification(
	Money Amount,
	Money Threshold) : DomainNotification;
