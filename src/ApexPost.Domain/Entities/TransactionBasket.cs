using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class TransactionBasket
{
	private readonly Dictionary<TaxCategory, decimal> _taxRates;
	private readonly List<TransactionLineItem> _lineItems = [];

	public string Currency { get; }
	public TransactionPaymentStatus PaymentStatus { get; private set; } =
		TransactionPaymentStatus.Open;

	public IReadOnlyList<TransactionLineItem> LineItems =>
		_lineItems.AsReadOnly();

	public Money Subtotal =>
		new(_lineItems.Sum(lineItem => lineItem.Subtotal.Amount), Currency);

	public Money Tax =>
		new(
			_lineItems.Sum(lineItem =>
				lineItem.Subtotal.Amount * _taxRates[lineItem.TaxCategory]),
			Currency);

	public Money Total => new(Subtotal.Amount + Tax.Amount, Currency);

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

	public void Pay()
	{
		EnsureOpen();

		if (_lineItems.Count == 0)
			throw new InvalidOperationException(
				"An empty transaction basket cannot be paid.");

		PaymentStatus = TransactionPaymentStatus.Paid;
	}

	public void Void()
	{
		EnsureOpen();
		PaymentStatus = TransactionPaymentStatus.Voided;
	}

	public void Refund()
	{
		if (PaymentStatus != TransactionPaymentStatus.Paid)
		{
			throw new InvalidOperationException(
				$"A transaction can only be refunded when its status is Paid; current status is '{PaymentStatus}'.");
		}

		PaymentStatus = TransactionPaymentStatus.Refunded;
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
