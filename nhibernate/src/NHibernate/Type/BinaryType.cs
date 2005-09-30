using System;
using System.Data;
using System.IO;
using System.Text;
using NHibernate.SqlTypes;
using NHibernate.Util;
using Environment = NHibernate.Cfg.Environment;

namespace NHibernate.Type
{
	/// <summary>
	/// BinaryType.
	/// </summary>
	public class BinaryType : MutableType
	{
		/// <summary></summary>
		internal BinaryType() : this( new BinarySqlType() )
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sqlType"></param>
		internal BinaryType( BinarySqlType sqlType ) : base( sqlType )
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="cmd"></param>
		/// <param name="value"></param>
		/// <param name="index"></param>
		public override void Set( IDbCommand cmd, object value, int index )
		{
			//TODO: research into byte streams
			//if ( Cfg.Environment.UseStreamsForBinary ) {
			// Is this really necessary?
			// How do we do????

			//TODO: st.setBinaryStream( index, new ByteArrayInputStream( (byte[]) value ), ( (byte[]) value ).length );
			//}
			//else {
			//Need to set DbType in parameter????
			( ( IDataParameter ) cmd.Parameters[ index ] ).Value = ( byte[ ] ) value;
			//}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="rs"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		public override object Get( IDataReader rs, int index )
		{
			if( Environment.UseStreamsForBinary )
			{
				// Is this really necessary?
				// see http://msdn.microsoft.com/library/en-us/cpguide/html/cpconobtainingblobvaluesfromdatabase.asp?frame=true 
				// for a how to on reading binary/blob values from a db...
				MemoryStream outputStream = new MemoryStream( 2048 );
				byte[ ] buffer = new byte[2048];
				long fieldOffset = 0;

				try
				{
					while( true )
					{
						long amountRead = rs.GetBytes( index, fieldOffset, buffer, 0, buffer.Length );

						fieldOffset += amountRead;
						outputStream.Write( buffer, 0, ( int ) amountRead );

						if( amountRead < buffer.Length )
						{
							break;
						}
					}
					outputStream.Close();
				}
				catch( IOException ioe )
				{
					throw new HibernateException( "IOException occurred reading a binary value", ioe );
				}

				return outputStream.ToArray();

			}
			else
			{
				//TODO: not sure if this will work with all dbs
				return ( byte[ ] ) rs[ index ];
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="rs"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public override object Get( IDataReader rs, string name )
		{
			return Get( rs, rs.GetOrdinal( name ) );
		}

		/// <summary></summary>
		public override System.Type ReturnedClass
		{
			get { return typeof( byte[ ] ); }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public override bool Equals( object x, object y )
		{
			if( x == y )
			{
				return true;
			}
			if( x == null || y == null )
			{
				return false;
			}

			return ArrayHelper.Equals( ( byte[ ] ) x, ( byte[ ] ) y );
		}

		public override string Name
		{
			get { return "Byte[]"; }
		}

		public override string ToString( object val )
		{
			byte[ ] bytes = ( byte[ ] ) val;
			StringBuilder buf = new StringBuilder();
			for( int i = 0; i < bytes.Length; i++ )
			{
				string hexStr = ( bytes[ i ] - byte.MinValue ).ToString( "x" ); //Why no ToBase64?
				if( hexStr.Length == 1 )
				{
					buf.Append( '0' );
				}
				buf.Append( hexStr );
			}
			return buf.ToString();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public override object DeepCopyNotNull( Object value )
		{
			byte[ ] bytes = ( byte[ ] ) value;
			byte[ ] result = new byte[bytes.Length];
			Array.Copy( bytes, 0, result, 0, bytes.Length );
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xml"></param>
		/// <returns></returns>
		public override object FromStringValue( string xml )
		{
			if( xml == null )
			{
				return null;
			}
			
			if( xml.Length % 2 != 0 )
			{
				throw new ArgumentException(
					"The string is not a valid xml representation of a binary content.",
					"xml");
			}

			byte[ ] bytes = new byte[xml.Length / 2];
			for( int i = 0; i < bytes.Length; i++ )
			{
				string hexStr = xml.Substring( i * 2, (i + 1) * 2 );
				bytes[ i ] = ( byte ) ( byte.MinValue
					+ byte.Parse( hexStr, System.Globalization.NumberStyles.HexNumber ) );
					
			}

			return bytes;
		}
	}
}