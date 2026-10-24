using System.Collections.ObjectModel;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Notifications;

namespace ApexPost.Domain.Entities;

public sealed class TransactionBasketSnapshot
{
	public Guid Id { get; }
	public string Currency { get; }
	public IReadOnlyDictionary<TaxCategory, decimal> TaxRates { get; }
	public IReadOnlyList<TransactionLineItem> LineItems { get; }
	public TransactionPaymentStatus PaymentStatus { get; }
	public IReadOnlyList<DomainNotification> Notifications { get; }

	internal TransactionBasketSnapshot(
		Guid id,
		string currency,
		IReadOnlyDictionary<TaxCategory, decimal> taxRates,
		IEnumerable<TransactionLineItem> lineItems,
		TransactionPaymentStatus paymentStatus,
		IEnumerable<DomainNotification> notifications)
	{
		Id = id;
		Currency = currency;
		TaxRates = new ReadOnlyDictionary<TaxCategory, decimal>(
			new Dictionary<TaxCategory, decimal>(taxRates));
		LineItems = Array.AsReadOnly(lineItems.ToArray());
		PaymentStatus = paymentStatus;
		Notifications = Array.AsReadOnly(notifications.ToArray());
	}
}
