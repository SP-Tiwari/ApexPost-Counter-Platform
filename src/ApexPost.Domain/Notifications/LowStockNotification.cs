using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Notifications;

public sealed record LowStockNotification(
	BranchCode BranchCode,
	Sku ProductSku,
	int Quantity,
	int Threshold) : DomainNotification;
