using ApexPost.Domain.Events;

namespace ApexPost.Application.Events;

// Observer hub for committed domain events. Subscribers (such as simulated
// notification senders) attach to the C# events. A failing subscriber is
// reported through the error callback and never undoes or fails the
// already-saved operation, nor stops other subscribers.
public sealed class DomainEventPublisher(
	Action<Exception, DomainEvent>? onSubscriberError = null)
{
	public event EventHandler<TransactionCompletedEvent>? TransactionCompleted;
	public event EventHandler<StockLowEvent>? StockLow;
	public event EventHandler<MoneyOrderIssuedEvent>? MoneyOrderIssued;
	public event EventHandler<HighValueMoneyOrderIssuedEvent>? HighValueMoneyOrderIssued;
	public event EventHandler<TransactionVoidedEvent>? TransactionVoided;
	public event EventHandler<TransactionRefundedEvent>? TransactionRefunded;

	public void Publish(IEnumerable<DomainEvent> domainEvents)
	{
		ArgumentNullException.ThrowIfNull(domainEvents);

		foreach (var domainEvent in domainEvents)
		{
			switch (domainEvent)
			{
				case TransactionCompletedEvent completed:
					Raise(TransactionCompleted, completed);
					break;
				case StockLowEvent stockLow:
					Raise(StockLow, stockLow);
					break;
				case MoneyOrderIssuedEvent issued:
					Raise(MoneyOrderIssued, issued);
					break;
				case HighValueMoneyOrderIssuedEvent highValue:
					Raise(HighValueMoneyOrderIssued, highValue);
					break;
				case TransactionVoidedEvent voided:
					Raise(TransactionVoided, voided);
					break;
				case TransactionRefundedEvent refunded:
					Raise(TransactionRefunded, refunded);
					break;
				default:
					throw new ArgumentException(
						$"Unsupported domain event '{domainEvent?.GetType().Name}'.",
						nameof(domainEvents));
			}
		}
	}

	private void Raise<TEvent>(EventHandler<TEvent>? handlers, TEvent domainEvent)
		where TEvent : DomainEvent
	{
		if (handlers is null)
			return;

		foreach (var handler in handlers.GetInvocationList()
			.Cast<EventHandler<TEvent>>())
		{
			try
			{
				handler(this, domainEvent);
			}
			catch (Exception exception)
			{
				onSubscriberError?.Invoke(exception, domainEvent);
			}
		}
	}
}
