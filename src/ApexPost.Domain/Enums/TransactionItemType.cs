namespace ApexPost.Domain.Enums;

public enum TransactionItemType
{
	Goods,
	Service,
	// The money-order principal: collected in the basket total but untaxed
	// and not counted as retail or service sales. The fee is a Service line.
	MoneyOrder
}
