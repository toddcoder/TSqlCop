using System;

namespace TSqlCop
{
   public static class EventAggregatorExtensions
   {
      public static IDisposable AddListenerAction<T>(this IEventSubscriptionManager eventAggregator, Action<T> listener)
      {
         if (eventAggregator == null)
            throw new ArgumentNullException(nameof(eventAggregator));
         if (listener == null)
            throw new ArgumentNullException(nameof(listener));

         var delegateListener = new DelegateListener<T>(listener, eventAggregator);
         eventAggregator.AddListener(delegateListener);

         return delegateListener;
      }
   }

   public class DelegateListener<T> : IListener<T>, IDisposable
   {
      readonly Action<T> listener;
      readonly IEventSubscriptionManager eventSubscriptionManager;

      public DelegateListener(Action<T> listener, IEventSubscriptionManager eventSubscriptionManager)
      {
         this.listener = listener;
         this.eventSubscriptionManager = eventSubscriptionManager;
      }

      public void Handle(T message)
      {
         listener(message);
      }

      public void Dispose()
      {
         Dispose(true);
         GC.SuppressFinalize(this);
      }

      protected virtual void Dispose(bool disposing)
      {
         if (disposing)
            eventSubscriptionManager.RemoveListener(this);
      }
   }
}