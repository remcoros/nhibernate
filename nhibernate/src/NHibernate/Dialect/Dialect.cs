using System;
using System.Collections;
using System.Data;
using System.Text;
using log4net;
using NHibernate.Dialect.Function;
using NHibernate.Dialect.Lock;
using NHibernate.Engine;
using NHibernate.Mapping;
using NHibernate.Persister.Entity;
using NHibernate.SqlCommand;
using NHibernate.SqlTypes;
using NHibernate.Type;
using NHibernate.Util;
using Environment=NHibernate.Cfg.Environment;
using System.Collections.Generic;

namespace NHibernate.Dialect
{
	/// <summary>
	/// Represents a dialect of SQL implemented by a particular RDBMS. Sublcasses
	/// implement NHibernate compatibility with different systems.
	/// </summary>
	/// <remarks>
	/// Subclasses should provide a public default constructor that <c>Register()</c>
	/// a set of type mappings and default Hibernate properties.
	/// </remarks>
	public abstract class Dialect
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(Dialect));

		private readonly TypeNames typeNames = new TypeNames();
		private readonly IDictionary<string, string> properties = new Dictionary<string, string>();
		private readonly IDictionary<string, ISQLFunction> sqlFunctions;

		private static readonly IDictionary<string, ISQLFunction> standardAggregateFunctions =
			CollectionHelper.CreateCaseInsensitiveHashtable<ISQLFunction>();

		/// <summary></summary>
		protected const string DefaultBatchSize = "15";

		/// <summary></summary>
		protected const string NoBatch = "0";

		/// <summary></summary>
		static Dialect()
		{
			standardAggregateFunctions["count"] = new CountQueryFunctionInfo();
			standardAggregateFunctions["avg"] = new AvgQueryFunctionInfo();
			standardAggregateFunctions["max"] = new ClassicAggregateFunction("max",false);
			standardAggregateFunctions["min"] = new ClassicAggregateFunction("min",false);
			standardAggregateFunctions["sum"] = new SumQueryFunctionInfo();
		}

		/// <summary>
		/// The base constructor for Dialect.
		/// </summary>
		/// <remarks>
		/// Every subclass should override this and call Register() with every <see cref="DbType"/> except
		/// <see cref="DbType.Object"/>, <see cref="DbType.SByte"/>, <see cref="DbType.UInt16"/>, <see cref="DbType.UInt32"/>, 
		/// <see cref="DbType.UInt64"/>, <see cref="DbType.VarNumeric"/>.
		/// 
		/// <para>
		/// The Default properties for this Dialect should also be set - such as whether or not to use outer-joins
		/// and what the batch size should be.
		/// </para>
		/// </remarks>
		protected Dialect()
		{
			log.Info("Using dialect: " + this);
			sqlFunctions = CollectionHelper.CreateCaseInsensitiveHashtable(standardAggregateFunctions);
			// standard sql92 functions (can be overridden by subclasses)
			RegisterFunction("substring", new SQLFunctionTemplate(NHibernateUtil.String, "substring(?1, ?2, ?3)"));
			RegisterFunction("locate", new SQLFunctionTemplate(NHibernateUtil.Int32, "locate(?1, ?2, ?3)"));
			RegisterFunction("trim", new AnsiTrimFunction());
			RegisterFunction("length", new StandardSQLFunction("length", NHibernateUtil.Int32));
			RegisterFunction("bit_length", new StandardSQLFunction("bit_length", NHibernateUtil.Int32));
			RegisterFunction("coalesce", new StandardSQLFunction("coalesce"));
			RegisterFunction("nullif", new StandardSQLFunction("nullif"));
			RegisterFunction("abs", new StandardSQLFunction("abs"));
			RegisterFunction("mod", new StandardSQLFunction("mod", NHibernateUtil.Int32));
			RegisterFunction("sqrt", new StandardSQLFunction("sqrt", NHibernateUtil.Double));
			RegisterFunction("upper", new StandardSQLFunction("upper"));
			RegisterFunction("lower", new StandardSQLFunction("lower"));
			RegisterFunction("cast", new CastFunction());
			RegisterFunction("extract", new AnsiExtractFunction());
			RegisterFunction("concat", new VarArgsSQLFunction(NHibernateUtil.String, "(", "||", ")"));

			// the syntax of current_timestamp is extracted from H3.2 tests 
			// - test\hql\ASTParserLoadingTest.java
			// - test\org\hibernate\test\hql\HQLTest.java
			RegisterFunction("current_timestamp", new NoArgSQLFunction("current_timestamp", NHibernateUtil.DateTime, true));
			RegisterFunction("sysdate", new NoArgSQLFunction("sysdate", NHibernateUtil.DateTime, false));

			//map second/minute/hour/day/month/year to ANSI extract(), override on subclasses
			RegisterFunction("second", new SQLFunctionTemplate(NHibernateUtil.Int32, "extract(second from ?1)"));
			RegisterFunction("minute", new SQLFunctionTemplate(NHibernateUtil.Int32, "extract(minute from ?1)"));
			RegisterFunction("hour", new SQLFunctionTemplate(NHibernateUtil.Int32, "extract(hour from ?1)"));
			RegisterFunction("day", new SQLFunctionTemplate(NHibernateUtil.Int32, "extract(day from ?1)"));
			RegisterFunction("month", new SQLFunctionTemplate(NHibernateUtil.Int32, "extract(month from ?1)"));
			RegisterFunction("year", new SQLFunctionTemplate(NHibernateUtil.Int32, "extract(year from ?1)"));

			RegisterFunction("str", new SQLFunctionTemplate(NHibernateUtil.String, "cast(?1 as char)"));
		}


		/// <summary>
		/// Characters used for quoting sql identifiers
		/// </summary>
		public const string PossibleQuoteChars = "`'\"[";

		/// <summary></summary>
		public const string PossibleClosedQuoteChars = "`'\"]";

		/// <summary>
		/// Get the name of the database type associated with the given 
		/// <see cref="SqlTypes.SqlType"/>,
		/// </summary>
		/// <param name="sqlType">The SqlType</param>
		/// <returns>The database type name used by ddl.</returns>
		public virtual string GetTypeName(SqlType sqlType)
		{
			if (sqlType.LengthDefined)
			{
				string resultWithLength = typeNames.Get(sqlType.DbType, sqlType.Length, sqlType.Precision, sqlType.Scale);
				if (resultWithLength != null) return resultWithLength;
			}

			string result = typeNames.Get(sqlType.DbType);
			if (result == null)
			{
				throw new HibernateException(string.Format("No default type mapping for SqlType {0}", sqlType));
			}

			return result;
		}

		/// <summary>
		/// Get the name of the database type associated with the given
		/// <see cref="SqlType"/>.
		/// </summary>
		/// <param name="sqlType">The SqlType </param>
		/// <param name="length">The datatype length </param>
		/// <param name="precision">The datatype precision </param>
		/// <param name="scale">The datatype scale </param>
		/// <returns>The database type name used by ddl.</returns>
		public virtual string GetTypeName(SqlType sqlType, int length, int precision, int scale)
		{
			string result = typeNames.Get(sqlType.DbType, length, precision, scale);
			if (result == null)
			{
				throw new HibernateException(string.Format("No type mapping for SqlType {0} of length {1}", sqlType, length));
			}
			return result;
		}

		public virtual string GetCastTypeName(SqlType sqlType)
		{
			return GetTypeName(sqlType, Column.DefaultLength, Column.DefaultPrecision, Column.DefaultScale);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="function"></param>
		protected void RegisterFunction(string name, ISQLFunction function)
		{
			sqlFunctions[name] = function;
		}

		/// <summary>
		/// Subclasses register a typename for the given type code and maximum
		/// column length. <c>$l</c> in the type name will be replaced by the column
		/// length (if appropriate)
		/// </summary>
		/// <param name="code">The typecode</param>
		/// <param name="capacity">Maximum length of database type</param>
		/// <param name="name">The database type name</param>
		protected void RegisterColumnType(DbType code, int capacity, string name)
		{
			typeNames.Put(code, capacity, name);
		}

		/// <summary>
		/// Suclasses register a typename for the given type code. <c>$l</c> in the 
		/// typename will be replaced by the column length (if appropriate).
		/// </summary>
		/// <param name="code">The typecode</param>
		/// <param name="name">The database type name</param>
		protected void RegisterColumnType(DbType code, string name)
		{
			typeNames.Put(code, name);
		}


		/// <summary>
		/// Does this dialect support the <c>ALTER TABLE</c> syntax?
		/// </summary>
		public virtual bool HasAlterTable
		{
			get { return true; }
		}

		/// <summary>
		/// Do we need to drop constraints before dropping tables in the dialect?
		/// </summary>
		public virtual bool DropConstraints
		{
			get { return true; }
		}

		/// <summary>
		/// Do we need to qualify index names with the schema name?
		/// </summary>
		public virtual bool QualifyIndexName
		{
			get { return true; }
		}

		/// <summary>
		/// How we seperate the queries when we use multiply queries.
		/// </summary>
		public virtual string MultipleQueriesSeparator
		{
			get { return ";"; }
		}

		#region Lock acquisition support
		/// <summary> 
		/// Get a strategy instance which knows how to acquire a database-level lock
		/// of the specified mode for this dialect. 
		/// </summary>
		/// <param name="lockable">The persister for the entity to be locked. </param>
		/// <param name="lockMode">The type of lock to be acquired. </param>
		/// <returns> The appropriate locking strategy. </returns>
		public virtual ILockingStrategy GetLockingStrategy(ILockable lockable, LockMode lockMode)
		{
			return new SelectLockingStrategy(lockable, lockMode);
		}

		/// <summary> 
		/// Given a lock mode, determine the appropriate for update fragment to use. 
		/// </summary>
		/// <param name="lockMode">The lock mode to apply. </param>
		/// <returns> The appropriate for update fragment. </returns>
		public virtual string GetForUpdateString(LockMode lockMode)
		{
			if (lockMode == LockMode.Upgrade)
			{
				return ForUpdateString;
			}
			else if (lockMode == LockMode.UpgradeNoWait)
			{
				return ForUpdateNowaitString;
			}
			else if (lockMode == LockMode.Force)
			{
				return ForUpdateNowaitString;
			}
			else
			{
				return string.Empty;
			}
		}

		/// <summary>
		/// Get the string to append to SELECT statements to acquire locks
		/// for this dialect.
		/// </summary>
		/// <value>The appropriate <c>FOR UPDATE</c> clause string.</value>
		public virtual string ForUpdateString
		{
			get { return " for update"; }
		}

		/// <summary> Is <tt>FOR UPDATE OF</tt> syntax supported? </summary>
		/// <value> True if the database supports <tt>FOR UPDATE OF</tt> syntax; false otherwise. </value>
		public virtual bool ForUpdateOfColumns
		{
			// by default we report no support
			get { return false; }
		}

		/// <summary> 
		/// Does this dialect support <tt>FOR UPDATE</tt> in conjunction with outer joined rows?
		/// </summary>
		/// <value> True if outer joined rows can be locked via <tt>FOR UPDATE</tt>. </value>
		public virtual bool SupportsOuterJoinForUpdate
		{
			get { return true; }
		}

		/// <summary> 
		/// Get the <tt>FOR UPDATE OF column_list</tt> fragment appropriate for this
		/// dialect given the aliases of the columns to be write locked.
		///  </summary>
		/// <param name="aliases">The columns to be write locked. </param>
		/// <returns> The appropriate <tt>FOR UPDATE OF column_list</tt> clause string. </returns>
		public virtual string GetForUpdateString(string aliases)
		{
			// by default we simply return the ForUpdateString result since
			// the default is to say no support for "FOR UPDATE OF ..."
			return ForUpdateString;
		}

		/// <summary>
		/// Retrieves the <c>FOR UPDATE NOWAIT</c> syntax specific to this dialect
		/// </summary>
		/// <value>The appropriate <c>FOR UPDATE NOWAIT</c> clause string.</value>
		public virtual string ForUpdateNowaitString
		{
			// by default we report no support for NOWAIT lock semantics
			get { return ForUpdateString; }
		}

		/// <summary> 
		/// Get the <tt>FOR UPDATE OF column_list NOWAIT</tt> fragment appropriate
		/// for this dialect given the aliases of the columns to be write locked.
		/// </summary>
		/// <param name="aliases">The columns to be write locked. </param>
		/// <returns> The appropriate <tt>FOR UPDATE colunm_list NOWAIT</tt> clause string. </returns>
		public virtual string GetForUpdateNowaitString(string aliases)
		{
			return GetForUpdateString(aliases);
		}

		/// <summary> 
		/// Modifies the given SQL by applying the appropriate updates for the specified
		/// lock modes and key columns.
		/// </summary>
		/// <param name="sql">the SQL string to modify </param>
		/// <param name="aliasedLockModes">a map of lock modes indexed by aliased table names. </param>
		/// <param name="keyColumnNames">a map of key columns indexed by aliased table names. </param>
		/// <returns> the modified SQL string. </returns>
		/// <remarks>
		/// The behavior here is that of an ANSI SQL <tt>SELECT FOR UPDATE</tt>.  This
		/// method is really intended to allow dialects which do not support
		/// <tt>SELECT FOR UPDATE</tt> to achieve this in their own fashion.
		/// </remarks>
		public virtual SqlString ApplyLocksToSql(SqlString sql, IDictionary aliasedLockModes, IDictionary keyColumnNames)
		{
			return sql.Append(new ForUpdateFragment(this, aliasedLockModes, keyColumnNames).ToSqlStringFragment());
		}

		/// <summary> 
		/// Some dialects support an alternative means to <tt>SELECT FOR UPDATE</tt>,
		/// whereby a "lock hint" is appends to the table name in the from clause.
		///  </summary>
		/// <param name="lockMode">The lock mode to apply </param>
		/// <param name="tableName">The name of the table to which to apply the lock hint. </param>
		/// <returns> The table with any required lock hints. </returns>
		public virtual string AppendLockHint(LockMode lockMode, string tableName)
		{
			return tableName;
		}

		#endregion

		/// <summary>
		/// Does this dialect support subselects?
		/// </summary>
		public virtual bool SupportsSubSelects
		{
			get { return true; }
		}

		/// <summary>
		/// Does this dialect support the <c>UNIQUE</c> column syntax?
		/// </summary>
		public virtual bool SupportsUnique
		{
			get { return true; }
		}

		/// <summary>
		/// The syntax used to add a column to a table. Note this is deprecated
		/// </summary>
		public virtual string AddColumnString
		{
			get { throw new NotSupportedException("No add column syntax supported by Dialect"); }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parentTable"></param>
		/// <param name="constraintName"></param>
		/// <param name="foreignKey"></param>
		/// <param name="referencedTable"></param>
		/// <param name="primaryKey"></param>
		/// <returns></returns>
		public virtual string GetAddForeignKeyConstraintString(string parentTable, string constraintName, string[] foreignKey,
			                                                       string referencedTable, string[] primaryKey)
		{
			return new StringBuilder(30)
				.Append(" add constraint ")
				.Append(constraintName)
				.Append(" foreign key (")
				.Append(string.Join(StringHelper.CommaSpace, foreignKey))
				.Append(") references ")
				.Append(referencedTable)
				.ToString();
		}

		/// <summary>
		/// The syntax used to drop a foreign key constraint from a table.
		/// </summary>
		/// <param name="constraintName">The name of the foreign key constraint to drop.</param>
		/// <returns>
		/// The SQL string to drop the foreign key constraint.
		/// </returns>
		public virtual string GetDropForeignKeyConstraintString(string constraintName)
		{
			return " drop constraint " + constraintName;
		}

		/// <summary>
		/// The syntax that is used to check if a constraint does not exists before creating it
		/// </summary>
		/// <param name="table">The table.</param>
		/// <param name="name">The name.</param>
		/// <returns></returns>
		public virtual string GetIfNotExistsCreateConstraint(Table table, string name)
		{
			return "";
		}

		/// <summary>
		/// The syntax that is used to close the if for a constraint exists check, used
		/// for dialects that requires begin/end for ifs
		/// </summary>
		/// <param name="table">The table.</param>
		/// <param name="name">The name.</param>
		/// <returns></returns>
		public virtual string GetIfNotExistsCreateConstraintEnd(Table table, string name)
		{
			return "";
		}



		/// <summary>
		/// The syntax that is used to check if a constraint exists before dropping it
		/// </summary>
		/// <param name="table">The table.</param>
		/// <param name="name">The name.</param>
		/// <returns></returns>
		public virtual string GetIfExistsDropConstraint(Table table, string name)
		{
			return "";
		}

		/// <summary>
		/// The syntax that is used to close the if for a constraint exists check, used
		/// for dialects that requires begin/end for ifs
		/// </summary>
		/// <param name="table">The table.</param>
		/// <param name="name">The name.</param>
		/// <returns></returns>
		public virtual string GetIfExistsDropConstraintEnd(Table table, string name)
		{
			return "";
		}

		/// <summary>
		/// The syntax used to add a primary key constraint to a table
		/// </summary>
		/// <param name="constraintName"></param>
		public virtual string GetAddPrimaryKeyConstraintString(string constraintName)
		{
			return " add constraint " + constraintName + " primary key ";
		}

		/// <summary>
		/// The syntax used to drop a primary key constraint from a table.
		/// </summary>
		/// <param name="constraintName">The name of the primary key constraint to drop.</param>
		/// <returns>
		/// The SQL string to drop the primary key constraint.
		/// </returns>
		public virtual string GetDropPrimaryKeyConstraintString(string constraintName)
		{
			return " drop constraint " + constraintName;
		}

		/// <summary>
		/// The syntax used to drop an index constraint from a table.
		/// </summary>
		/// <param name="constraintName">The name of the index constraint to drop.</param>
		/// <returns>
		/// The SQL string to drop the primary key constraint.
		/// </returns>
		public virtual string GetDropIndexConstraintString(string constraintName)
		{
			return " drop constraint " + constraintName;
		}

		/// <summary>
		/// The keyword used to specify a nullable column
		/// </summary>
		public virtual string NullColumnString
		{
			get { return String.Empty; }
		}

		/// <summary>
		/// Does this dialect support identity column key generation?
		/// </summary>
		public virtual bool SupportsIdentityColumns
		{
			get { return false; }
		}

		/// <summary>
		/// Does this dialect support sequences?
		/// </summary>
		public virtual bool SupportsSequences
		{
			get { return false; }
		}

		/// <summary>
		/// Generate SQL to get the identifier of an inserted row.
		/// If the returned value is not null, the caller will prepare a statement from it,
		/// set SQL parameters just as it would for insertSQL, and execute it as a query
		/// which is expected to return the identifier of the inserted row.
		/// If the returned value is null, the caller will execute insertSQL as an update
		/// and then execute IdentitySelectString as a query.
		/// The default implementation (in this class) returns <see langword="null" />.
		/// </summary>
		/// <param name="insertSql">a parameterized SQL statement to insert a row into a table.</param>
		/// <param name="identityColumn">The column for which the identity generator was specified.</param>
		/// <param name="tableName">The name of the table the row is being inserted in.</param>
		/// <returns>a SQL statement that has the same effect as insertSQL
		/// and also gets the identifier of the inserted row.
		/// Return <see langword="null" /> if this dialect doesn't support this feature.
		/// </returns>
		public virtual SqlString AddIdentitySelectToInsert(SqlString insertSql, string identityColumn, string tableName)
		{
			return null;
		}

		/// <summary>
		/// The syntax that returns the identity value of the last insert, if native
		/// key generation is supported
		/// </summary>
		public virtual string GetIdentitySelectString(string identityColumn, string tableName)
		{
			throw new MappingException("Dialect does not support identity key generation");
		}

		/// <summary>
		/// The keyword used to specify an identity column, if native key generation is supported
		/// </summary>
		public virtual string IdentityColumnString
		{
			get { throw new MappingException("Dialect does not support identity key generation"); }
		}

		/// <summary>
		/// The keyword used to insert a generated value into an identity column (or null)
		/// </summary>
		public virtual string IdentityInsertString
		{
			get { return null; }
		}

		/// <summary>
		/// The keyword used to insert a row without specifying any column values
		/// </summary>
		public virtual string NoColumnsInsertString
		{
			get { return "values ( )"; }
		}

		/// <summary>
		/// The syntax that fetches the next value of a sequence, if sequences are supported.
		/// </summary>
		/// <param name="sequenceName">The name of the sequence</param>
		/// <returns></returns>
		public virtual string GetSequenceNextValString(string sequenceName)
		{
			throw new MappingException("Dialect does not support sequences");
		}

		/// <summary>
		/// The syntax used to create a sequence, if sequences are supported
		/// </summary>
		/// <param name="sequenceName"></param>
		/// <returns></returns>
		public virtual string GetCreateSequenceString(string sequenceName)
		{
			throw new MappingException("Dialect does not support sequences");
		}

		/// <summary>
		/// The syntax used to drop a sequence, if sequences are supported
		/// </summary>
		/// <param name="sequenceName"></param>
		/// <returns></returns>
		public virtual string GetDropSequenceString(string sequenceName)
		{
			throw new MappingException("Dialect does not support sequences");
		}

		private static Dialect InstantiateDialect(string dialectName)
		{
			try
			{
				return (Dialect) Activator.CreateInstance(ReflectHelper.ClassForName(dialectName));
			}
			catch (Exception e)
			{
				throw new HibernateException("Could not instantiate dialect class " + dialectName, e);
			}
		}

		/// <summary>
		/// Get de <see cref="Dialect"/> from a property bag (prop name <see cref="Environment.Dialect"/>)
		/// </summary>
		/// <param name="props">The property bag.</param>
		/// <returns>An instance of <see cref="Dialect"/>.</returns>
		/// <exception cref="ArgumentNullException">When <paramref name="props"/> is null.</exception>
		/// <exception cref="HibernateException">When the property bag don't contains de property <see cref="Environment.Dialect"/>.</exception>
		public static Dialect GetDialect(IDictionary props)
		{
			if (props == null)
				throw new ArgumentNullException("props");
			string dialectName = (string)props[Environment.Dialect];
			if (dialectName == null)
			{
				throw new HibernateException("The dialect was not set. Set the property 'dialect'.");
			}

			return InstantiateDialect(dialectName);
		}

		/// <summary>
		/// Retrieve a set of default Hibernate properties for this database.
		/// </summary>
		public IDictionary<string, string> DefaultProperties
		{
			get { return properties; }
		}

		/// <summary> Command used to create a table. </summary>
		public virtual string CreateTableString
		{
			get { return "create table"; }
		}

		/// <summary> 
		/// Slight variation on <see cref="CreateTableString"/>.
		/// The command used to create a multiset table. 
		/// </summary>
		/// <remarks>
		/// Here, we have the command used to create a table when there is no primary key and
		/// duplicate rows are expected.
		/// <p/>
		/// Most databases do not care about the distinction; originally added for
		/// Teradata support which does care.
		/// </remarks>
		public virtual string CreateMultisetTableString
		{
			get { return CreateTableString; }
		}

		/// <summary>
		/// Completely optional cascading drop clause
		/// </summary>
		protected virtual string CascadeConstraintsString
		{
			get { return String.Empty; }
		}

		/// <summary>
		/// Create an <c>JoinFragment</c> for this dialect
		/// </summary>
		/// <returns></returns>
		public virtual JoinFragment CreateOuterJoinFragment()
		{
			return new ANSIJoinFragment();
		}

		/// <summary>
		/// Create an <c>CaseFragment</c> for this dialect
		/// </summary>
		/// <returns></returns>
		public virtual CaseFragment CreateCaseFragment()
		{
			return new ANSICaseFragment(this);
		}

		/// <summary>
		/// The name of the SQL function that transforms a string to lowercase
		/// </summary>
		public virtual string LowercaseFunction
		{
			get { return "lower"; }
		}

		/// <summary>
		/// Does this Dialect have some kind of <c>LIMIT</c> syntax?
		/// </summary>
		/// <value>False, unless overridden.</value>
		public virtual bool SupportsLimit
		{
			get { return false; }
		}

		/// <summary>
		/// Does this Dialect support an offset?
		/// </summary>
		public virtual bool SupportsLimitOffset
		{
			get { return SupportsLimit; }
		}

		/// <summary>
		/// Add a <c>LIMIT</c> clause to the given SQL <c>SELECT</c>
		/// </summary>
		/// <param name="querySqlString">A Query in the form of a SqlString.</param>
		/// <param name="hasOffset">Offset of the first row is not zero</param>
		/// <returns>A new SqlString that contains the <c>LIMIT</c> clause.</returns>
		public virtual SqlString GetLimitString(SqlString querySqlString, bool hasOffset)
		{
			throw new NotSupportedException("Paged Queries not supported");
		}

		/// <summary>
		/// Add a <c>LIMIT</c> clause to the given SQL <c>SELECT</c>
		/// </summary>
		/// <param name="querySqlString">A Query in the form of a SqlString.</param>
		/// <param name="offset">Offset of the first row to be returned by the query (zero-based)</param>
		/// <param name="limit">Maximum number of rows to be returned by the query</param>
		/// <returns>A new SqlString that contains the <c>LIMIT</c> clause.</returns>
		public virtual SqlString GetLimitString(SqlString querySqlString, int offset, int limit)
		{
			return GetLimitString(querySqlString, offset > 0);
		}

		/// <summary>
		/// Can parameters be used for a statement containing a LIMIT?
		/// </summary>
		public virtual bool SupportsVariableLimit
		{
			get { return SupportsLimit; }
		}

		/// <summary>
		/// Does the <c>LIMIT</c> clause specify arguments in the "reverse" order
		/// limit, offset instead of offset, limit?
		/// </summary>
		/// <value>False, unless overridden.</value>
		/// <remarks>Inheritors should return true if the correct order is limit, offset</remarks>
		public virtual bool BindLimitParametersInReverseOrder
		{
			get { return false; }
		}

		/// <summary>
		/// Does the <c>LIMIT</c> clause come at the start of the 
		/// <c>SELECT</c> statement rather than at the end?
		/// </summary>
		/// <value>false, unless overridden</value>
		public virtual bool BindLimitParametersFirst
		{
			get { return false; }
		}

		/// <summary>
		/// Does the <c>LIMIT</c> clause take a "maximum" row number
		/// instead of a total number of returned rows?
		/// </summary>
		/// <returns>false, unless overridden</returns>
		public virtual bool UseMaxForLimit
		{
			get { return false; }
		}

		/// <summary>
		/// The opening quote for a quoted identifier.
		/// </summary>
		public virtual char OpenQuote
		{
			get { return '"'; }
		}

		/// <summary>
		/// The closing quote for a quoted identifier.
		/// </summary>
		public virtual char CloseQuote
		{
			get { return '"'; }
		}

		/// <summary>
		/// Whether this dialect has an identity clause added to the data type or a
		/// completely seperate identity data type.
		/// </summary>
		public virtual bool HasDataTypeInIdentityColumn
		{
			get { return true; }
		}

		/// <summary>
		/// Aggregate SQL functions as defined in general. This is
		/// a case-insensitive hashtable!
		/// </summary>
		/// <remarks>
		/// The results of this method should be integrated with the 
		/// specialization's data.
		/// </remarks>
		public virtual IDictionary<string, ISQLFunction> Functions
		{
			get { return sqlFunctions; }
		}

		/// <summary>
		/// Return SQL needed to drop the named table. May (and should) use
		/// some form of "if exists" clause, and cascade constraints.
		/// </summary>
		/// <param name="tableName"></param>
		/// <returns></returns>
		public virtual string GetDropTableString(string tableName)
		{
			StringBuilder buf = new StringBuilder("drop table ");
			if (SupportsIfExistsBeforeTableName)
			{
				buf.Append("if exists ");
			}

			buf.Append(tableName).Append(CascadeConstraintsString);

			if (SupportsIfExistsAfterTableName)
			{
				buf.Append(" if exists");
			}
			return buf.ToString();
		}

		/// <summary>
		/// Does the dialect support the syntax 'drop table if exists NAME'
		/// </summary>
		protected virtual bool SupportsIfExistsBeforeTableName
		{
			get { return false; }
		}

		/// <summary>
		/// Does the dialect support the syntax 'drop table NAME if exists'
		/// </summary>
		protected virtual bool SupportsIfExistsAfterTableName
		{
			get { return false; }
		}

		/// <summary>
		/// Checks to see if the name has been quoted.
		/// </summary>
		/// <param name="name">The name to check if it is quoted</param>
		/// <returns>true if name is already quoted.</returns>
		/// <remarks>
		/// The default implementation is to compare the first character
		/// to Dialect.OpenQuote and the last char to Dialect.CloseQuote
		/// </remarks>
		public virtual bool IsQuoted(string name)
		{
			return (name[0] == OpenQuote && name[name.Length - 1] == CloseQuote);
		}

		/// <summary>
		/// Unquotes and unescapes an already quoted name
		/// </summary>
		/// <param name="quoted">Quoted string</param>
		/// <returns>Unquoted string</returns>
		/// <remarks>
		/// <p>
		/// This method checks the string <c>quoted</c> to see if it is 
		/// quoted.  If the string <c>quoted</c> is already enclosed in the OpenQuote
		/// and CloseQuote then those chars are removed.
		/// </p>
		/// <p>
		/// After the OpenQuote and CloseQuote have been cleaned from the string <c>quoted</c>
		/// then any chars in the string <c>quoted</c> that have been escaped by doubling them
		/// up are changed back to a single version.
		/// </p>
		/// <p>
		/// The following quoted values return these results
		/// "quoted" = quoted
		/// "quote""d" = quote"d
		/// quote""d = quote"d 
		/// </p>
		/// <p>
		/// If this implementation is not sufficient for your Dialect then it needs to be overridden.
		/// MsSql2000Dialect is an example of where UnQuoting rules are different.
		/// </p>
		/// </remarks>
		public virtual string UnQuote(string quoted)
		{
			string unquoted;

			if (IsQuoted(quoted))
			{
				unquoted = quoted.Substring(1, quoted.Length - 2);
			}
			else
			{
				unquoted = quoted;
			}

			unquoted = unquoted.Replace(new string(OpenQuote, 2), OpenQuote.ToString());

			if (OpenQuote != CloseQuote)
			{
				unquoted = unquoted.Replace(new string(CloseQuote, 2), CloseQuote.ToString());
			}

			return unquoted;
		}

		/// <summary>
		/// Unquotes an array of Quoted Names.
		/// </summary>
		/// <param name="quoted">strings to Unquote</param>
		/// <returns>an array of unquoted strings.</returns>
		/// <remarks>
		/// This use UnQuote(string) for each string in the quoted array so
		/// it should not need to be overridden - only UnQuote(string) needs
		/// to be overridden unless this implementation is not sufficient.
		/// </remarks>
		public virtual string[] UnQuote(string[] quoted)
		{
			string[] unquoted = new string[quoted.Length];

			for (int i = 0; i < quoted.Length; i++)
			{
				unquoted[i] = UnQuote(quoted[i]);
			}

			return unquoted;
		}


		/// <summary>
		/// Quotes a name.
		/// </summary>
		/// <param name="name">The string that needs to be Quoted.</param>
		/// <returns>A QuotedName </returns>
		/// <remarks>
		/// <p>
		/// This method assumes that the name is not already Quoted.  So if the name passed
		/// in is <c>"name</c> then it will return <c>"""name"</c>.  It escapes the first char
		/// - the " with "" and encloses the escaped string with OpenQuote and CloseQuote. 
		/// </p>
		/// </remarks>
		protected virtual string Quote(string name)
		{
			string quotedName = name.Replace(OpenQuote.ToString(), new string(OpenQuote, 2));

			// in some dbs the Open and Close Quote are the same chars - if they are 
			// then we don't have to escape the Close Quote char because we already
			// got it.
			if (OpenQuote != CloseQuote)
			{
				quotedName = name.Replace(CloseQuote.ToString(), new string(CloseQuote, 2));
			}

			return OpenQuote + quotedName + CloseQuote;
		}

		/// <summary>
		/// Quotes a name for being used as a aliasname
		/// </summary>
		/// <remarks>Original implementation calls <see cref="QuoteForTableName"/></remarks>
		/// <param name="aliasName">Name of the alias</param>
		/// <returns>A Quoted name in the format of OpenQuote + aliasName + CloseQuote</returns>
		/// <remarks>
		/// <p>
		/// If the aliasName is already enclosed in the OpenQuote and CloseQuote then this 
		/// method will return the aliasName that was passed in without going through any
		/// Quoting process.  So if aliasName is passed in already Quoted make sure that 
		/// you have escaped all of the chars according to your DataBase's specifications.
		/// </p>
		/// </remarks>
		public virtual string QuoteForAliasName(string aliasName)
		{
			return IsQuoted(aliasName) ?
			       aliasName :
			       Quote(aliasName);
		}

		/// <summary>
		/// Quotes a name for being used as a columnname
		/// </summary>
		/// <remarks>Original implementation calls <see cref="QuoteForTableName"/></remarks>
		/// <param name="columnName">Name of the column</param>
		/// <returns>A Quoted name in the format of OpenQuote + columnName + CloseQuote</returns>
		/// <remarks>
		/// <p>
		/// If the columnName is already enclosed in the OpenQuote and CloseQuote then this 
		/// method will return the columnName that was passed in without going through any
		/// Quoting process.  So if columnName is passed in already Quoted make sure that 
		/// you have escaped all of the chars according to your DataBase's specifications.
		/// </p>
		/// </remarks>
		public virtual string QuoteForColumnName(string columnName)
		{
			return IsQuoted(columnName) ?
			       columnName :
			       Quote(columnName);
		}

		/// <summary>
		/// Quotes a name for being used as a tablename
		/// </summary>
		/// <param name="tableName">Name of the table</param>
		/// <returns>A Quoted name in the format of OpenQuote + tableName + CloseQuote</returns>
		/// <remarks>
		/// <p>
		/// If the tableName is already enclosed in the OpenQuote and CloseQuote then this 
		/// method will return the tableName that was passed in without going through any
		/// Quoting process.  So if tableName is passed in already Quoted make sure that 
		/// you have escaped all of the chars according to your DataBase's specifications.
		/// </p>
		/// </remarks>
		public virtual string QuoteForTableName(string tableName)
		{
			return IsQuoted(tableName) ?
			       tableName :
			       Quote(tableName);
		}

		/// <summary>
		/// Quotes a name for being used as a schemaname
		/// </summary>
		/// <param name="schemaName">Name of the schema</param>
		/// <returns>A Quoted name in the format of OpenQuote + schemaName + CloseQuote</returns>
		/// <remarks>
		/// <p>
		/// If the schemaName is already enclosed in the OpenQuote and CloseQuote then this 
		/// method will return the schemaName that was passed in without going through any
		/// Quoting process.  So if schemaName is passed in already Quoted make sure that 
		/// you have escaped all of the chars according to your DataBase's specifications.
		/// </p>
		/// </remarks>
		public virtual string QuoteForSchemaName(string schemaName)
		{
			return IsQuoted(schemaName) ?
			       schemaName :
			       Quote(schemaName);
		}

		public virtual int MaxAliasLength
		{
			get { return 10; }
		}

		/// <summary>
		/// Gives the best resolution that the database can use for storing
		/// date/time values, in ticks.
		/// </summary>
		/// <remarks>
		/// <para>
		/// For example, if the database can store values with 100-nanosecond
		/// precision, this property is equal to 1L. If the database can only
		/// store values with 1-millisecond precision, this property is equal
		/// to 10000L (number of ticks in a millisecond).
		/// </para>
		/// <para>
		/// Used in TimestampType.
		/// </para>
		/// </remarks>
		public virtual long TimestampResolutionInTicks
		{
			get { return 1L; } // Maximum precision (one tick)
		}

		// union subclass support ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

		/// <summary> 
		/// Given a <see cref="DbType"/> type code, determine an appropriate
		/// null value to use in a select clause.
		/// </summary>
		/// <param name="sqlType">The <see cref="DbType"/> type code. </param>
		/// <returns> The appropriate select clause value fragment. </returns>
		/// <remarks>
		/// One thing to consider here is that certain databases might
		/// require proper casting for the nulls here since the select here
		/// will be part of a UNION/UNION ALL.
		/// </remarks>
		public virtual string GetSelectClauseNullString(SqlType sqlType)
		{
			return "null";
		}

		/// <summary> 
		/// Does this dialect support UNION ALL, which is generally a faster variant of UNION? 
		/// True if UNION ALL is supported; false otherwise.
		/// </summary>
		public virtual bool SupportsUnionAll
		{
			get { return false; }
		}

		#region Agregate function redefinition

		protected class CountQueryFunctionInfo : ClassicAggregateFunction
		{
			public CountQueryFunctionInfo() : base("count",true)
			{
			}

			public override IType ReturnType(IType columnType, IMapping mapping)
			{
				return NHibernateUtil.Int64;
			}
		}

		protected class AvgQueryFunctionInfo : ClassicAggregateFunction
		{
			public AvgQueryFunctionInfo() : base("avg",false)
			{
			}

			public override IType ReturnType(IType columnType, IMapping mapping)
			{
				SqlType[] sqlTypes;
				try
				{
					sqlTypes = columnType.SqlTypes(mapping);
				}
				catch (MappingException me)
				{
					throw new QueryException(me);
				}

				if (sqlTypes.Length != 1)
				{
					throw new QueryException("multi-column type can not be in avg()");
				}
				return NHibernateUtil.Double;
			}
		}

		protected class SumQueryFunctionInfo : ClassicAggregateFunction
		{
			public SumQueryFunctionInfo() : base("sum",false)
			{
			}

			//H3.2 behavior
			public override IType ReturnType(IType columnType, IMapping mapping)
			{
				SqlType[] sqlTypes;
				try
				{
					sqlTypes = columnType.SqlTypes(mapping);
				}
				catch (MappingException me)
				{
					throw new QueryException(me);
				}

				if (sqlTypes.Length != 1)
				{
					throw new QueryException("multi-column type can not be in sum()");
				}

				SqlType sqlType = sqlTypes[0];

				// TODO: (H3.2 for nullable types) First allow the actual type to control the return value. (the actual underlying sqltype could actually be different)

				// finally use the sqltype if == on Hibernate types did not find a match.
				switch (sqlType.DbType)
				{
					case DbType.Single:
					case DbType.Double:
						return NHibernateUtil.Double;

					case DbType.SByte:
					case DbType.Int16:
					case DbType.Int32:
					case DbType.Int64:
						return NHibernateUtil.Int64;

					case DbType.Byte:
					case DbType.UInt16:
					case DbType.UInt32:
					case DbType.UInt64:
						return NHibernateUtil.UInt64;

					default:
						return columnType;
				}
			}
		}

		#endregion
	}
}