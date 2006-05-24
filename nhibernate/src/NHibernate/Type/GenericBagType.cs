#if NET_2_0

using System;
using System.Collections.Generic;

using NHibernate.Collection;
using NHibernate.Collection.Generic;
using NHibernate.Engine;
using NHibernate.Persister.Collection;

namespace NHibernate.Type
{
	/// <summary>
	/// An <see cref="IType"/> that maps an <see cref="IList{T}"/> collection
	/// to the database using bag semantics.
	/// </summary>
	public class GenericBagType<T> : BagType
	{
		/// <summary>
		/// Initializes a new instance of a <see cref="GenericBagType{T}"/> class for
		/// a specific role.
		/// </summary>
		/// <param name="role">The role the persistent collection is in.</param>
		public GenericBagType( string role, string propertyRef )
			: base( role, propertyRef )
		{
		}

		/// <summary>
		/// Instantiates a new <see cref="IPersistentCollection"/> for the bag.
		/// </summary>
		/// <param name="session">The current <see cref="ISessionImplementor"/> for the bag.</param>
		public override IPersistentCollection Instantiate( ISessionImplementor session, ICollectionPersister persister )
		{
			return new PersistentGenericBag<T>( session );
		}

		public override System.Type ReturnedClass
		{
			get { return typeof( IList<T> ); }
		}

		/// <summary>
		/// Wraps an <see cref="IList&lt;T&gt;"/> in a <see cref="PersistentGenericBag&lt;T&gt;"/>.
		/// </summary>
		/// <param name="session">The <see cref="ISessionImplementor"/> for the collection to be a part of.</param>
		/// <param name="collection">The unwrapped <see cref="IList&lt;T&gt;"/>.</param>
		/// <returns>
		/// An <see cref="PersistentGenericBag&lt;T&gt;"/> that wraps the non NHibernate <see cref="IList&lt;T&gt;"/>.
		/// </returns>
		public override IPersistentCollection Wrap( ISessionImplementor session, object collection )
		{
			return new PersistentGenericBag<T>( session, ( IList<T> ) collection );
		}
		
		//TODO: Add() & Clear() methods - need to see if these should be refactored back into
		// their own version of Copy or a DoCopy.  The Copy() method used to be spread out amongst
		// the various collections, but since they all had common code Add() and Clear() were made
		// virtual since that was where most of the logic was.  A different/better way might be to
		// have a Copy on the base collection that handles the standard checks and then a DoCopy
		// that performs the actual copy.

		public override object Instantiate()
		{
			return new List<T>();
		}
	}
}
#endif