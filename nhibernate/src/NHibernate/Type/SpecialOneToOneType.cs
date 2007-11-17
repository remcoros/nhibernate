namespace NHibernate.Type
{
	/// <summary> 
	/// A one-to-one association that maps to specific formula(s)
	/// instead of the primary key column of the owning entity. 
	/// </summary>
	public class SpecialOneToOneType : OneToOneType
	{
		public SpecialOneToOneType(System.Type referencedEntityName, ForeignKeyDirection foreignKeyType, 
			string uniqueKeyPropertyName, bool lazy, bool unwrapProxy, string entityName, string propertyName)
			: base(referencedEntityName, foreignKeyType, uniqueKeyPropertyName, lazy, propertyName)
		{
		}

		public override int GetColumnSpan(Engine.IMapping mapping)
		{
			return GetIdentifierOrUniqueKeyType(mapping).GetColumnSpan(mapping);
		}

		public override SqlTypes.SqlType[] SqlTypes(Engine.IMapping mapping)
		{
			return GetIdentifierOrUniqueKeyType(mapping).SqlTypes(mapping);
		}

		public override bool UseLHSPrimaryKey
		{
			get { return false; }
		}

		public override object Hydrate(System.Data.IDataReader rs, string[] names, Engine.ISessionImplementor session, object owner)
		{
			return GetIdentifierOrUniqueKeyType(session.Factory).NullSafeGet(rs, names, session, owner);
		}
	}
}
