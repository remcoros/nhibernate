using System;
using System.Collections;
using System.Collections.Specialized;
using System.Data;
using NHibernate.Engine;
using NHibernate.Loader;
using NHibernate.Persister.Collection;
using NHibernate.Type;

namespace NHibernate.Collection
{
	/// <summary>
	/// A persistent wrapper for a <see cref="IDictionary" />. Underlying collection
	/// is a <see cref="Hashtable" />.
	/// </summary>
	[Serializable]
	public class Map : PersistentCollection, IDictionary
	{
		protected IDictionary map;

		protected override ICollection Snapshot( ICollectionPersister persister )
		{
			Hashtable clonedMap = new Hashtable( map.Count );
			foreach( DictionaryEntry e in map )
			{
				clonedMap[ e.Key ] = persister.ElementType.DeepCopy( e.Value );
			}
			return clonedMap;
		}

		public override ICollection GetOrphans( object snapshot )
		{
			IDictionary sn = ( IDictionary ) snapshot;
			ArrayList result = new ArrayList( sn.Values.Count );
			result.AddRange( sn.Values );
			PersistentCollection.IdentityRemoveAll( result, map.Values, Session );
			return result;
		}

		public override bool EqualsSnapshot( IType elementType )
		{
			IDictionary xmap = ( IDictionary ) GetSnapshot();
			if( xmap.Count != this.map.Count )
			{
				return false;
			}
			foreach( DictionaryEntry entry in map )
			{
				if( elementType.IsDirty( entry.Value, xmap[ entry.Key ], Session ) )
				{
					return false;
				}
			}
			return true;
		}

		public override bool IsWrapper( object collection )
		{
			return map == collection;
		}

		internal Map( ) : base( )
		{
		}

		/// <summary>
		/// Construct an uninitialized Map.
		/// </summary>
		/// <param name="session">The ISession the Map should be a part of.</param>
		internal Map( ISessionImplementor session ) : base( session )
		{
		}

		/// <summary>
		/// Construct an initialized Map based off the values from the existing IDictionary.
		/// </summary>
		/// <param name="session">The ISession the Map should be a part of.</param>
		/// <param name="map">The IDictionary that contains the initial values.</param>
		internal Map( ISessionImplementor session, IDictionary map ) : base( session )
		{
			this.map = map;
			SetInitialized();
			IsDirectlyAccessible = true;
		}

		public override void BeforeInitialize( ICollectionPersister persister )
		{
			if( persister.HasOrdering )
			{
				// if this Persister has an Ordering then use the ListDictionary because
				// it maintains items in the Dictionary in the same order as they were 
				// added.
				this.map = new ListDictionary();
			}
			else
			{
				this.map = new Hashtable();
			}
		}

		public int Count
		{
			get
			{
				Read();
				return map.Count;
			}
		}

		public bool IsSynchronized
		{
			get { return false; }
		}

		public bool IsFixedSize
		{
			get { return false; }
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		public object SyncRoot
		{
			get { return this; }
		}

		public ICollection Keys
		{
			get
			{
				Read();
				return map.Keys;
			}
		}

		public ICollection Values
		{
			get
			{
				Read();
				return map.Values;
			}

		}

		public IEnumerator GetEnumerator()
		{
			Read();
			return map.GetEnumerator();

		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			Read();
			return map.GetEnumerator();

		}

		/// <summary></summary>
		IDictionaryEnumerator IDictionary.GetEnumerator()
		{
			Read();
			return map.GetEnumerator();
		}

		public void CopyTo( Array array, int index )
		{
			Read();
			map.CopyTo( array, index );
		}

		public void Add( object key, object value )
		{
			Write();
			map.Add( key, value );
		}

		public bool Contains( object key )
		{
			Read();
			return map.Contains( key );
		}

		public object this[ object key ]
		{
			get
			{
				Read();
				return map[ key ];
			}
			set
			{
				Write();
				map[ key ] = value;
			}
		}

		public void Remove( object key )
		{
			Write();
			map.Remove( key );
		}

		public void Clear()
		{
			Write();
			map.Clear();
		}

		public override bool Empty
		{
			get { return map.Count == 0; }
		}

		public override string ToString()
		{
			Read();
			return map.ToString();
		}

		public override object ReadFrom( IDataReader rs, ICollectionPersister role, ICollectionAliases descriptor, object owner )
		{
			object element = role.ReadElement(rs, owner, descriptor.SuffixedElementAliases, Session);
			object index = role.ReadIndex( rs, descriptor.SuffixedIndexAliases, Session );

			map[ index ] = element;
			return element;
		}

		public override IEnumerable Entries()
		{
			// hibernate has a call to map.entrySet() - we don't need to do
			// that because .net provides an IEnumerable from a IDictionary
			// while java has no way to get an Iterator from a Map - so they
			// convert it to Set via entrySet()
			return map;
		}

		/// <summary>
		/// Initializes this Map from the cached values.
		/// </summary>
		/// <param name="persister">The CollectionPersister to use to reassemble the Map.</param>
		/// <param name="disassembled">The disassembled Map.</param>
		/// <param name="owner">The owner object.</param>
		public override void InitializeFromCache( ICollectionPersister persister, object disassembled, object owner )
		{
			BeforeInitialize( persister );
			object[ ] array = ( object[ ] ) disassembled;
			for( int i = 0; i < array.Length; i += 2 )
			{
				map[ persister.IndexType.Assemble( array[ i ], Session, owner ) ] =
					persister.ElementType.Assemble( array[ i + 1 ], Session, owner );
			}
			SetInitialized();
		}

		public override object Disassemble( ICollectionPersister persister )
		{
			object[ ] result = new object[map.Count*2];
			int i = 0;
			foreach( DictionaryEntry e in map )
			{
				result[ i++ ] = persister.IndexType.Disassemble( e.Key, Session );
				result[ i++ ] = persister.ElementType.Disassemble( e.Value, Session );
			}
			return result;
		}

		public override ICollection GetDeletes( IType elemType, bool indexIsFormula )
		{
			IList deletes = new ArrayList();
			foreach( DictionaryEntry e in ( ( IDictionary ) GetSnapshot() ) )
			{
				object key = e.Key;
				if( e.Value != null && map[ key ] == null )
				{
					deletes.Add( indexIsFormula ? e.Value : key );
				}
			}
			return deletes;
		}

		public override bool NeedsInserting( object entry, int i, IType elemType )
		{
			IDictionary sn = ( IDictionary ) GetSnapshot();
			DictionaryEntry e = ( DictionaryEntry ) entry;
			return ( e.Value != null && sn[ e.Key ] == null );
		}

		public override bool NeedsUpdating( object entry, int i, IType elemType )
		{
			IDictionary sn = ( IDictionary ) GetSnapshot();
			DictionaryEntry e = ( DictionaryEntry ) entry;
			object snValue = sn[ e.Key ];
			return ( e.Value != null && snValue != null && elemType.IsDirty( snValue, e.Value, Session ) );
		}

		public override object GetIndex( object entry, int i )
		{
			return ( ( DictionaryEntry ) entry ).Key;
		}

		public override object GetElement(object entry)
		{
			return ( ( DictionaryEntry ) entry ).Value;
		}

		public override object GetSnapshotElement(object entry, int i)
		{
			IDictionary sn = ( IDictionary ) GetSnapshot();
			return sn[ ( ( DictionaryEntry ) entry ).Key ];
		}

		public override bool EntryExists( object entry, int i )
		{
			return ( ( DictionaryEntry ) entry ).Value != null;
		}
	}
}