using ApexPost.Domain;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Notifications;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class TransactionBasket
{
	private readonly Dictionary<TaxCategory, decimal> _taxRates;
	private readonly List<TransactionLineItem> _lineItems = [];
	private readonly List<DomainNotification> _notifications = [];
	private TransactionPaymentStatus _paymentStatus =
		TransactionPaymentStatus.Open;

	public string Currency { get; }
	public TransactionPaymentStatus PaymentStatus
	{
		get
		{
			lock (DomainMutationLock.Gate)
				return _paymentStatus;
		}
		private set => _paymentStatus = value;
	}

	public IReadOnlyList<TransactionLineItem> LineItems =>
		GetLineItems();

	public IReadOnlyList<DomainNotification> Notifications
	{
		get
		{
			lock (DomainMutationLock.Gate)
				return _notifications.ToArray();
		}
	}

	public Money Subtotal =>
		GetSubtotal();

	public Money Tax =>
		GetTax();

	public Money Total
	{
		get
		{
			lock (DomainMutationLock.Gate)
				return new(Subtotal.Amount + Tax.Amount, Currency);
		}
	}

	public Money TotalFor(TransactionItemType itemType)
	{
		if (!Enum.IsDefined(itemType))
			throw new ArgumentOutOfRangeException(nameof(itemType));

		lock (DomainMutationLock.Gate)
			return GetTotalFor(itemType);
	}

	internal (Money RetailSales, Money ServiceSales)? GetPaidSalesTotals()
	{
		lock (DomainMutationLock.Gate)
		{
			if (PaymentStatus != TransactionPaymentStatus.Paid)
				return null;

			return (
				GetTotalFor(TransactionItemType.Goods),
				GetTotalFor(TransactionItemType.Service));
		}
	}

	public TransactionBasket(
		string currency,
		IReadOnlyDictionary<TaxCategory, decimal> taxRates)
	{
		ArgumentNullException.ThrowIfNull(taxRates);

		Currency = new Money(0m, currency).Currency;
		_taxRates = new Dictionary<TaxCategory, decimal>();

		foreach (var (taxCategory, taxRate) in taxRates)
		{
			if (!Enum.IsDefined(taxCategory))
				throw new ArgumentOutOfRangeException(nameof(taxRates));

			if (taxRate < 0m)
				throw new ArgumentOutOfRangeException(
					nameof(taxRates), "Tax rates cannot be negative.");

			_taxRates.Add(taxCategory, taxRate);
		}
	}

	public void AddLineItem(TransactionLineItem lineItem)
	{
		ArgumentNullException.ThrowIfNull(lineItem);

		lock (DomainMutationLock.Gate)
		{
			EnsureOpen();

			if (!string.Equals(lineItem.UnitPrice.Currency, Currency, StringComparison.Ordinal))
				throw new ArgumentException(
					"Transaction line currency must match the basket currency.",
					nameof(lineItem));

			if (!_taxRates.ContainsKey(lineItem.TaxCategory))
				throw new InvalidOperationException(
					$"No tax rate is configured for category '{lineItem.TaxCategory}'.");

			_lineItems.Add(lineItem);
		}
	}

	public void Pay()
	{
		lock (DomainMutationLock.Gate)
		{
			EnsureOpen();

			if (_lineItems.Count == 0)
				throw new InvalidOperationException(
					"An empty transaction basket cannot be paid.");

			PaymentStatus = TransactionPaymentStatus.Paid;
		}
	}

	internal void Void(string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
			throw new ArgumentException(
				"A reason is required to void a transaction.", nameof(reason));

		lock (DomainMutationLock.Gate)
		{
			EnsureOpen();
			_notifications.Add(
				new TransactionVoidedNotification(reason.Trim()));
			PaymentStatus = TransactionPaymentStatus.Voided;
		}
	}

	internal void Refund(string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
			throw new ArgumentException(
				"A reason is required to refund a transaction.", nameof(reason));

		lock (DomainMutationLock.Gate)
		{
			if (PaymentStatus != TransactionPaymentStatus.Paid)
			{
				throw new InvalidOperationException(
					$"A transaction can only be refunded when its status is Paid; current status is '{PaymentStatus}'.");
			}

			_notifications.Add(
				new TransactionRefundedNotification(reason.Trim()));
			PaymentStatus = TransactionPaymentStatus.Refunded;
		}
	}

	private IReadOnlyList<TransactionLineItem> GetLineItems()
	{
		lock (DomainMutationLock.Gate)
			return _lineItems.AsReadOnly();
	}

	private Money GetSubtotal()
	{
		lock (DomainMutationLock.Gate)
			return new(_lineItems.Sum(lineItem => lineItem.Subtotal.Amount), Currency);
	}

	private Money GetTax()
	{
		lock (DomainMutationLock.Gate)
			return new(
				_lineItems.Sum(lineItem =>
					lineItem.Subtotal.Amount * _taxRates[lineItem.TaxCategory]),
				Currency);
	}

	private Money GetTotalFor(TransactionItemType itemType)
	{
		var subtotal = _lineItems
			.Where(lineItem => lineItem.Type == itemType)
			.Sum(lineItem => lineItem.Subtotal.Amount);
		var tax = _lineItems
			.Where(lineItem => lineItem.Type == itemType)
			.Sum(lineItem =>
				lineItem.Subtotal.Amount * _taxRates[lineItem.TaxCategory]);

		return new Money(subtotal + tax, Currency);
	}

	private void EnsureOpen()
	{
		if (PaymentStatus != TransactionPaymentStatus.Open)
		{
			throw new InvalidOperationException(
				$"This operation is only valid for an Open transaction; current status is '{PaymentStatus}'.");
		}
	}
}
