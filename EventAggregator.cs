using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TSqlCop
{
	public interface IListener<in TMessage>
    {
        void Handle(TMessage message);
    }

    public interface IEventSubscriptionManager
    {
        IEventSubscriptionManager AddListener(object listener, bool? holdStrongReference = null);

        IEventSubscriptionManager AddListener<T>(IListener<T> listener, bool? holdStrongReference = null);

        IEventSubscriptionManager RemoveListener(object listener);
    }

    public interface IEventPublisher
    {
        void SendMessage<TMessage>(TMessage message, Action<Action> marshal = null);

        void SendMessage<TMessage>(Action<Action> marshal = null)
            where TMessage : new();
    }

    public interface IEventAggregator : IEventPublisher, IEventSubscriptionManager
    {
    }

    public class EventAggregator : IEventAggregator
    {
	    readonly ListenerWrapperCollection listeners;
	    readonly Config config;

        public EventAggregator()
            : this(new Config())
        {
        }

        public EventAggregator(Config config)
        {
            this.config = config;
            listeners = new ListenerWrapperCollection();
        }

        public void SendMessage<TMessage>(TMessage message, Action<Action> marshal = null)
        {
            if (marshal == null)
                marshal = config.DefaultThreadMarshaller;

            call<IListener<TMessage>>(message, marshal);
        }

        public void SendMessage<TMessage>(Action<Action> marshal = null)
            where TMessage : new()
        {
            SendMessage(new TMessage(), marshal);
        }

        void call<TListener>(object message, Action<Action> marshaller)
            where TListener : class
        {
            var listenerCalledCount = 0;
            marshaller(() =>
                {
                    foreach (var o in listeners.Where(o => o.Handles<TListener>() || o.HandlesMessage(message)))
                    {
	                    o.TryHandle<TListener>(message, out var wasThisOneCalled);
                        if (wasThisOneCalled)
                            listenerCalledCount++;
                    }
                });

            var wasAnyListenerCalled = listenerCalledCount > 0;

            if (!wasAnyListenerCalled)
	            config.OnMessageNotPublishedBecauseZeroListeners(message);
        }

        public IEventSubscriptionManager AddListener(object listener, bool? holdStrongReference = null)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));

            var holdRef = config.HoldReferences;
            if (holdStrongReference.HasValue)
                holdRef = holdStrongReference.Value;
            var supportMessageInheritance = config.SupportMessageInheritance;
            listeners.AddListener(listener, holdRef, supportMessageInheritance);

            return this;
        }

        public IEventSubscriptionManager AddListener<T>(IListener<T> listener, bool? holdStrongReference)
        {
            AddListener((object) listener, holdStrongReference);
            return this;
        }

        public IEventSubscriptionManager RemoveListener(object listener)
        {
            listeners.RemoveListener(listener);
            return this;
        }

        class ListenerWrapperCollection : IEnumerable<ListenerWrapper>
        {
	        readonly List<ListenerWrapper> listeners = new List<ListenerWrapper>();
	        readonly object sync = new object();

            public void RemoveListener(object listener)
            {
	            lock (sync)
                    if (tryGetListenerWrapperByListener(listener, out var listenerWrapper))
                        listeners.Remove(listenerWrapper);
            }

            void removeListenerWrapper(ListenerWrapper listenerWrapper)
            {
                lock (sync)
                    listeners.Remove(listenerWrapper);
            }

            public IEnumerator<ListenerWrapper> GetEnumerator()
            {
                lock (sync)
                    return listeners.ToList().GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            bool containsListener(object listener)
            {
	            return tryGetListenerWrapperByListener(listener, out _);
            }

            bool tryGetListenerWrapperByListener(object listener, out ListenerWrapper listenerWrapper)
            {
                lock (sync)
                    listenerWrapper = listeners.SingleOrDefault(x => x.ListenerInstance == listener);

                return listenerWrapper != null;
            }

            public void AddListener(object listener, bool holdStrongReference, bool supportMessageInheritance)
            {
                lock (sync)
                {

                    if (containsListener(listener))
                        return;

                    var listenerWrapper = new ListenerWrapper(listener, removeListenerWrapper, holdStrongReference, supportMessageInheritance);
                    if (listenerWrapper.Count == 0)
                        throw new ArgumentException("IListener<T> is not implemented", nameof(listener));
                    listeners.Add(listenerWrapper);
                }
            }
        }

        #region IReference

        interface IReference
        {
            object Target { get; }
        }

        class WeakReferenceImpl : IReference
        {
	        readonly WeakReference reference;

            public WeakReferenceImpl(object listener)
            {
                reference = new WeakReference(listener);
            }

            public object Target => reference.Target;
        }

        class StrongReferenceImpl : IReference
        {
	        readonly object target;

            public StrongReferenceImpl(object target)
            {
                this.target = target;
            }

            public object Target => target;
        }

        #endregion

        class ListenerWrapper
        {
            const string HANDLE_METHOD_NAME = "Handle";

            readonly Action<ListenerWrapper> onRemoveCallback;
            readonly List<HandleMethodWrapper> handlers = new List<HandleMethodWrapper>();
            readonly IReference reference;

            public ListenerWrapper(object listener, Action<ListenerWrapper> onRemoveCallback, bool holdReferences, bool supportMessageInheritance)
            {
                this.onRemoveCallback = onRemoveCallback;

                if (holdReferences)
                    reference = new StrongReferenceImpl(listener);
                else
                    reference = new WeakReferenceImpl(listener);

                var listenerInterfaces = TypeHelper.GetBaseInterfaceType(listener.GetType())
                                                   .Where(w => TypeHelper.DirectlyClosesGeneric(w, typeof (IListener<>)));

                foreach (var listenerInterface in listenerInterfaces)
                {
                    var messageType = TypeHelper.GetFirstGenericType(listenerInterface);
                    var handleMethod = TypeHelper.GetMethod(listenerInterface, HANDLE_METHOD_NAME);

                    var handler = new HandleMethodWrapper(handleMethod, listenerInterface, messageType,supportMessageInheritance );
                    handlers.Add(handler);
                }
            }

            public object ListenerInstance => reference.Target;

            public bool Handles<TListener>() where TListener : class
            {
                return handlers.Aggregate(false, (current, handler) => current | handler.Handles<TListener>());
            }

            public bool HandlesMessage(object message)
            {
                return message != null && handlers.Aggregate(false, (current, handler) => current | handler.HandlesMessage(message));
            }

            public void TryHandle<TListener>(object message, out bool wasHandled)
                where TListener : class
            {
                var target = reference.Target;
                wasHandled = false;
                if (target == null)
                {
                    onRemoveCallback(this);
                    return;
                }

                foreach (var handler in handlers)
                {
                    var thisOneHandled = false;
                    handler.TryHandle<TListener>(target, message, out thisOneHandled);
                    wasHandled |= thisOneHandled;
                }
            }

            public int Count => handlers.Count;
        }

        class HandleMethodWrapper
        {
            readonly Type listenerInterface;
            readonly Type messageType;
            readonly MethodInfo handlerMethod;
            readonly bool supportMessageInheritance;
            readonly Dictionary<Type, bool> supportedMessageTypes = new Dictionary<Type, bool>();

            public HandleMethodWrapper(MethodInfo handlerMethod, Type listenerInterface, Type messageType, bool supportMessageInheritance)
            {
                this.handlerMethod = handlerMethod;
                this.listenerInterface = listenerInterface;
                this.messageType = messageType;
                this.supportMessageInheritance = supportMessageInheritance;
                supportedMessageTypes[messageType] = true;
            }

            public bool Handles<TListener>() where TListener : class => listenerInterface == typeof (TListener);

            public bool HandlesMessage(object message)
            {
                if (message == null)
	                return false;

                var type = message.GetType();
                var previousMessageType = supportedMessageTypes.TryGetValue(type, out var handled);
                if (!previousMessageType && supportMessageInheritance)
                {
                    handled = TypeHelper.IsAssignableFrom(messageType, type);
                    supportedMessageTypes[type] = handled;
                }
                return handled;
            }

            public void TryHandle<TListener>(object target, object message, out bool wasHandled)
                where TListener : class
            {
                wasHandled = false;
                if (target == null)
	                return;

                if (!Handles<TListener>() && !HandlesMessage(message))
	                return;

                handlerMethod.Invoke(target, new[] {message});
                wasHandled = true;
            }
        }

        internal static class TypeHelper
        {
            internal static IEnumerable<Type> GetBaseInterfaceType(Type type)
            {
                if (type == null)
                    return new Type[0];

                var interfaces = type.GetInterfaces().ToList();

                foreach (var @interface in interfaces.ToArray())
	                interfaces.AddRange(GetBaseInterfaceType(@interface));

                if (type.IsInterface)
	                interfaces.Add(type);

                return interfaces.Distinct();
            }

            internal static bool DirectlyClosesGeneric(Type type, Type openType)
            {
                if (type == null)
                    return false;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == openType)
	                return true;

                return false;
            }

            internal static Type GetFirstGenericType<T>() where T : class
            {
                return GetFirstGenericType(typeof (T));
            }

            internal static Type GetFirstGenericType(Type type)
            {
                var messageType = type.GetGenericArguments().First();
                return messageType;
            }

            internal static MethodInfo GetMethod(Type type, string methodName)
            {
                var handleMethod = type.GetMethod(methodName);
                return handleMethod;
            }

            internal static bool IsAssignableFrom(Type type, Type specifiedType)
            {
                return type.IsAssignableFrom(specifiedType);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public class Config
        {
	        Action<object> onMessageNotPublishedBecauseZeroListeners = msg =>
                {
                    /* TODO: possibly Trace message?*/
                };

            public Action<object> OnMessageNotPublishedBecauseZeroListeners
            {
                get => onMessageNotPublishedBecauseZeroListeners;
                set => onMessageNotPublishedBecauseZeroListeners = value;
            }

            Action<Action> defaultThreadMarshaller = action => action();

            public Action<Action> DefaultThreadMarshaller
            {
                get => defaultThreadMarshaller;
                set => defaultThreadMarshaller = value;
            }

            public bool HoldReferences { get; set; }

            public bool SupportMessageInheritance { get; set; }
        }
    }
}