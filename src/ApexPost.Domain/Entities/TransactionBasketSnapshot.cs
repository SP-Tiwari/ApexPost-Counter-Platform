using System.Collections.ObjectModel;

namespace ApexPost.Domain.Entities;

public sealed class TransactionBasketSnapshot
{
	public Guid Id { get; }
	public string Currency { get; }
	public Money Subtotal { get; }
	public Money Tax { get; }
	public Money Total { get; }
	public IReadOnlyDictionary<TaxCategory, decimal> TaxRates { get; }
	public IReadOnlyList<TransactionLineItem> LineItems { get; }
	public BranchCode? BranchCode { get; }
	public TransactionPaymentStatus PaymentStatus { get; }
	public IReadOnlyList<DomainNotification> Notifications { get; }

	internal TransactionBasketSnapshot(
		Guid id,
		string currency,
		Money subtotal,
		Money tax,
		Money total,
		IReadOnlyDictionary<TaxCategory, decimal> taxRates,
		IEnumerable<TransactionLineItem> lineItems,
		BranchCode? branchCode,
		TransactionPaymentStatus paymentStatus,
		IEnumerable<DomainNotification> notifications)
	{
		Id = id;
		Currency = currency;
		Subtotal = subtotal;
		Tax = tax;
		Total = total;
		TaxRates = new ReadOnlyDictionary<TaxCategory, decimal>(
			new Dictionary<TaxCategory, decimal>(taxRates));
		LineItems = Array.AsReadOnly(lineItems.ToArray());
		BranchCode = branchCode;
		PaymentStatus = paymentStatus;
		Notifications = Array.AsReadOnly(notifications.ToArray());
	}
}
