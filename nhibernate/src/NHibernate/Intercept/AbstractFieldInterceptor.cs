using System;
using Iesi.Collections.Generic;
using NHibernate.Engine;
using NHibernate.Proxy;

namespace NHibernate.Intercept
{
	[Serializable]
	public abstract class AbstractFieldInterceptor : IFieldInterceptor
	{
		public static readonly object InvokeImplementation = new object();

		[NonSerialized]
		private ISessionImplementor session;
		private ISet<string> uninitializedFields;
		private readonly ISet<string> unwrapProxyFieldNames;
		private readonly ISet<string> loadedUnwrapProxyFieldNames = new HashedSet<string>();
		private readonly string entityName;

		[NonSerialized]
		private bool initializing;
		private bool isDirty;

		protected internal AbstractFieldInterceptor(ISessionImplementor session, ISet<string> uninitializedFields, ISet<string> unwrapProxyFieldNames, string entityName)
		{
			this.session = session;
			this.uninitializedFields = uninitializedFields;
			this.unwrapProxyFieldNames = unwrapProxyFieldNames;
			this.entityName = entityName;
		}

		#region IFieldInterceptor Members

		public bool IsDirty
		{
			get { return isDirty; }
		}

		public void SetSession(ISessionImplementor session)
		{
			this.session = session;
		}

		public bool IsInitialized
		{
			get { return uninitializedFields == null || uninitializedFields.Count == 0; }
		}

		public bool IsInitializedField(string field)
		{
			if (unwrapProxyFieldNames != null && unwrapProxyFieldNames.Contains(field))
			{
				return loadedUnwrapProxyFieldNames.Contains(field);
			}
			return uninitializedFields == null || !uninitializedFields.Contains(field);
		}

		public void MarkDirty()
		{
			isDirty = true;
		}

		public void ClearDirty()
		{
			isDirty = false;
		}

		#endregion

		public ISet<string> UninitializedFields
		{
			get { return uninitializedFields; }
		}

		public string EntityName
		{
			get { return entityName; }
		}

		public bool Initializing
		{
			get { return initializing; }
		}

		// NH Specific: Hibernate only deals with lazy properties here, we deal with 
		// both lazy properties and with no-proxy. 
		public object Intercept(object target, string fieldName, object value)
		{
			if (initializing)
				return InvokeImplementation;

			if (session == null)
			{
				throw new LazyInitializationException("entity with lazy properties is not associated with a session");
			}
			if (!session.IsOpen || !session.IsConnected)
			{
				throw new LazyInitializationException("session is not connected");
			}

			if (uninitializedFields != null && uninitializedFields.Contains(fieldName))
			{
				return InitializeField(fieldName, target);
			}
			if (value is INHibernateProxy && unwrapProxyFieldNames != null && unwrapProxyFieldNames.Contains(fieldName))
			{
				return InitializeOrGetAssociation((INHibernateProxy)value, fieldName);
			}
			return InvokeImplementation;
		}

		private object InitializeOrGetAssociation(INHibernateProxy value, string fieldName)
		{
			if(value.HibernateLazyInitializer.IsUninitialized)
			{
				value.HibernateLazyInitializer.Initialize();
				value.HibernateLazyInitializer.Unwrap = true; // means that future Load/Get from the session will get the implementation
				loadedUnwrapProxyFieldNames.Add(fieldName);
			}
			return value.HibernateLazyInitializer.GetImplementation(session);
		}

		private object InitializeField(string fieldName, object target)
		{
			object result;
			initializing = true;
			try
			{
				var lazyPropertyInitializer = ((ILazyPropertyInitializer) session.Factory.GetEntityPersister(entityName));
				result = lazyPropertyInitializer.InitializeLazyProperty(fieldName, target, session);
			}
			finally
			{
				initializing = false;
			}
			uninitializedFields = null; //let's assume that there is only one lazy fetch group, for now!
			return result;
		}
	}
}
