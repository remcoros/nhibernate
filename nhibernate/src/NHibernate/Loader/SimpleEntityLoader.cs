using System;
using System.Collections;
using System.Data;
using System.Text;

using NHibernate.Collection;
using NHibernate.Engine;
using NHibernate.Persister;
using NHibernate.SqlCommand;
using NHibernate.Type;
using NHibernate.Util;

namespace NHibernate.Loader 
{
	/// <summary>
	/// Loads entity instances one instance per select (ie without outerjoin fetching)
	/// </summary>
	public class SimpleEntityLoader : Loader, IUniqueEntityLoader 
	{
		private ILoadable[] persister;
		private IType[] idType;
		private SqlString sqlString;
		private LockMode[] lockMode;
		
		private string[] NoSuffix = new string[] { String.Empty };

		public SimpleEntityLoader(ILoadable persister, SqlString sqlString, LockMode lockMode, Dialect.Dialect d) : base(d)
		{
			this.persister = new ILoadable[] { persister };
			this.idType = new IType[] { persister.IdentifierType };
			this.sqlString = sqlString;
			this.lockMode = new LockMode[] {lockMode};
			PostInstantiate();
		}

		protected internal override SqlString SqlString 
		{
			get { return sqlString; }
			set { sqlString = value; }
		}

		protected override ILoadable[] Persisters 
		{
			get { return persister; }
			set { persister = value; }
		}

		protected override CollectionPersister CollectionPersister 
		{
			get { return null; }
		}

		protected override string[] Suffixes 
		{
			get { return NoSuffix; }
			set { throw new NotSupportedException( "A SimpleEntityLoader has no Suffixes" ); }
		}

		public object Load(ISessionImplementor session, object id, object obj) 
		{
			IList list = LoadEntity(session, new object[] { id }, idType, obj, id, false);
			if (list.Count==1) 
			{
				return list[0];
				//return ( (object[]) list[0] )[0];
			} 
			else if (list.Count==0) 
			{
				return null;
			} 
			else 
			{
				throw new HibernateException("More than one row with the given identifier was found: " + id + ", for class: " + persister[0].ClassName );
			}
		}

		protected override LockMode[] GetLockModes(IDictionary lockModes) {
			return lockMode;
		}

		protected override Object GetResultColumnOrRow(
			Object[] row,
			IDataReader rs,
			ISessionImplementor session) {
		
			return row[0];
		}

	}
}
