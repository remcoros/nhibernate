using System;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.Type;
using NHibernate.Util;

namespace NHibernate.SqlCommand
{
	/// <summary>
	/// An immutable Parameter that later will be converted into an IDbParameter
	/// for an IDbCommand.
	/// </summary>
	[Serializable]
	public class Parameter : ICloneable
	{
		private readonly string _name;
		private readonly SqlType _sqlType;

		/// <summary>
		/// Initializes a new instance of <see cref="Parameter"/> class.
		/// </summary>
		/// <param name="name">The name of the parameter.</param>
		public Parameter(string name)
			: this( name, null, null )
		{
		}

		/// <summary>
		/// Initializes a new instance of <see cref="Parameter"/> class.
		/// </summary>
		/// <param name="name">The name of the parameter.</param>
		/// <param name="sqlType">The <see cref="SqlType"/> to create the parameter for.</param>
		public Parameter(string name, SqlType sqlType)
			: this( name, null, sqlType )
		{
		}

		/// <summary>
		/// Initializes a new instance of <see cref="Parameter"/> class.
		/// </summary>
		/// <param name="name">The name of the parameter.</param>
		/// <param name="tableAlias">The Alias to use for the table.</param>
		/// <param name="sqlType">The <see cref="SqlType"/> to create the parameter for.</param>
		public Parameter(string name, string tableAlias, SqlType sqlType)
		{
			if( tableAlias!=null && tableAlias.Length>0 )
			{
				_name = tableAlias + StringHelper.Dot + name;
			}
			else
			{
				_name = name;
			}
			_sqlType = sqlType;
		}

		/// <summary>
		/// Gets the name of the Parameter.
		/// </summary>
		/// <value>The name of the Parameter.</value>
		/// <remarks>
		/// The Parameter name is not used by anything except to compare equality
		/// and to generate a debug string.
		/// </remarks>
		public string Name
		{
			get { return _name; }
		}

		/// <summary>
		/// Gets the <see cref="SqlType"/> that defines the specifics of 
		/// the IDbDataParameter.
		/// </summary>
		/// <value>
		/// The <see cref="SqlType"/> that defines the specifics of 
		/// the IDbDataParameter.
		/// </value>
		public SqlType SqlType
		{
			get { return _sqlType; }
		}


		/// <summary>
		/// Generates an Array of Parameters for the columns that make up the IType
		/// </summary>
		/// <param name="columnNames">The names of the Columns that compose the IType</param>
		/// <param name="type">The IType to turn into Parameters</param>
		/// <param name="factory"></param>
		/// <returns>An Array of <see cref="Parameter"/> objects</returns>
		public static Parameter[ ] GenerateParameters( IMapping factory, string[ ] columnNames, IType type )
		{
			return Parameter.GenerateParameters( factory, null, columnNames, type );
		}


		/// <summary>
		/// Generates an Array of Parameters for the columns that make up the IType
		/// </summary>
		/// <param name="factory">The SessionFactory to use to get the DbTypes.</param>
		/// <param name="tableAlias">The Alias for the Table.</param>
		/// <param name="columnNames">The names of the Columns that compose the IType</param>
		/// <param name="type">The IType to turn into Parameters</param>
		/// <returns>An Array of <see cref="Parameter"/> objects</returns>
		public static Parameter[ ] GenerateParameters( IMapping factory, string tableAlias, string[ ] columnNames, IType type )
		{
			SqlType[ ] sqlTypes = type.SqlTypes( factory );

			Parameter[ ] parameters = new Parameter[sqlTypes.Length];

			for( int i = 0; i < sqlTypes.Length; i++ )
			{
				parameters[ i ] = new Parameter( columnNames[i], tableAlias, sqlTypes[i] );
			}

			return parameters;
		}

		#region Object Members

		/// <summary>
		/// Determines wether this instance and the specified object 
		/// are the same Type and have the same values.
		/// </summary>
		/// <param name="obj">An object to compare to this instance.</param>
		/// <returns>
		/// <c>true</c> if the object is a Parameter (not a subclass) and has the
		/// same values.
		/// </returns>
		/// <remarks>
		/// Each subclass needs to implement their own <c>Equals</c>. 
		/// </remarks>
		public override bool Equals( object obj )
		{
			// Step1: Perform an equals test
			if( obj == this )
			{
				return true;
			}

			// Step	2: Instance of check
			Parameter rhs = obj as Parameter;
			if( rhs == null )
			{
				return false;
			}
		
			//Step 3: Check each important field
			return
				this.SqlType.Equals(rhs.SqlType) &&
				this.Name.Equals(rhs.Name);
		}

		/// <summary>
		/// Gets a hash code based on the SqlType, Name, and TableAlias properties.
		/// </summary>
		/// <returns>
		/// An <see cref="Int32"/> value for the hash code.
		/// </returns>
		public override int GetHashCode()
		{
			int hashCode = 0;

			unchecked
			{
				if( _sqlType != null )
				{
					hashCode += _sqlType.GetHashCode();
				}
				if( _name != null )
				{
					hashCode += _name.GetHashCode();
				}

				return hashCode;
			}
		}

		/// <summary></summary>
		public override string ToString()
		{
			return ":" + _name;
		}

		#endregion

		#region ICloneable Members

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public Parameter Clone()
		{
			Parameter paramClone = ( Parameter ) this.MemberwiseClone();

			return paramClone;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		object ICloneable.Clone()
		{
			return Clone();
		}

		#endregion
	}

}