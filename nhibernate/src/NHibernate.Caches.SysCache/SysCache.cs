#region License
//
//  SysCache - A cache provider for NHibernate using System.Web.Caching.Cache.
//
//  This library is free software; you can redistribute it and/or
//  modify it under the terms of the GNU Lesser General Public
//  License as published by the Free Software Foundation; either
//  version 2.1 of the License, or (at your option) any later version.
//
//  This library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//  Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public
//  License along with this library; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
#endregion

using System;
using System.Collections;
using System.Web;
using AspCache = System.Web.Caching; // clash with new NHibernate namespace below
using log4net;
using NHibernate.Cache;

namespace NHibernate.Caches.SysCache
{
	/// <summary>
	/// Pluggable cache implementation using the System.Web.Caching classes
	/// </summary>
	public class SysCache : ICache
	{
		private static readonly ILog log = LogManager.GetLogger( typeof( SysCache ) );
		private string _region;
		private AspCache.Cache _cache;
		private TimeSpan _expiration;
		private AspCache.CacheItemPriority _priority;
		private Hashtable _map;
		private static readonly TimeSpan _defaultExpiration = TimeSpan.FromSeconds( 300 );
		private static readonly string _cacheKeyPrefix = "NHibernate-Cache:";

		/// <summary>
		/// default constructor
		/// </summary>
		public SysCache() : this( null, null )
		{
		}

		/// <summary>
		/// constructor with no properties
		/// </summary>
		/// <param name="region"></param>
		public SysCache( string region ) : this( region, null )
		{
		}

		/// <summary>
		/// full constructor
		/// </summary>
		/// <param name="region"></param>
		/// <param name="properties">cache configuration properties</param>
		/// <remarks>
		/// There are two (2) configurable parameters:
		/// <ul>
		///		<li>expiration = number of seconds to wait before expiring each item</li>
		///		<li>priority = a numeric cost of expiring each item, where 1 is a low cost, 5 is the highest, and 3 is normal. Only values 1 through 5 are valid.</li>
		/// </ul>
		/// All parameters are optional. The defaults are an expiration of 300 seconds and the default priority of 3.
		/// </remarks>
		/// <exception cref="IndexOutOfRangeException">The "priority" property is not between 1 and 5</exception>
		/// <exception cref="ArgumentException">The "expiration" property could not be parsed.</exception>
		public SysCache( string region, IDictionary properties )
		{
			_region = region;
			_map = new Hashtable();
			_cache = HttpRuntime.Cache;
			Configure( properties );
		}

		private void Configure( IDictionary props )
		{
			if( props == null )
			{
				if( log.IsDebugEnabled )
				{
					log.Debug( "configuring cache with default values" );
				}
				_expiration = _defaultExpiration;
				_priority = AspCache.CacheItemPriority.Default;
			}
			else
			{
				if( props["priority"] != null )
				{
					int priority = Convert.ToInt32( props["priority"] );
					if( priority < 1 || priority > 5 )
					{
						if( log.IsWarnEnabled )
						{
							log.Warn( "priority value out of range: " + priority.ToString() );
						}
						throw new IndexOutOfRangeException( "priority must be between 1 and 5" );
					}
					_priority = (AspCache.CacheItemPriority)priority;
					if( log.IsDebugEnabled )
					{
						log.Debug( "new priority: " + _priority.ToString() );
					}
				}
				else
				{
					_priority = AspCache.CacheItemPriority.Default;
				}
				if( props["expiration"] != null )
				{
					try
					{
						int seconds = Convert.ToInt32( props["expiration"] );
						_expiration = TimeSpan.FromSeconds( seconds );
						if( log.IsDebugEnabled )
						{
							log.Debug( "new expiration value: " + seconds.ToString() );
						}
					}
					catch( Exception ex )
					{
						if( log.IsWarnEnabled )
						{
							log.Warn( "error parsing expiration value" );
						}
						throw new ArgumentException( "could not parse exception as a number of seconds", "expiration", ex );
					}
				}
				else
				{
					if( log.IsDebugEnabled )
					{
						log.Debug( "no expiration value given, using defaults" );
					}
					_expiration = _defaultExpiration;
				}
			}
		}

		private string GetCacheKey( object key )
		{
			return String.Concat( _cacheKeyPrefix, _region, ":", key.ToString(), "@", key.GetHashCode() );
		}

		/// <summary></summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public object Get( object key )
		{
			if( key == null )
			{
				return null;
			}
			string cacheKey = GetCacheKey( key );
			if( log.IsDebugEnabled )
			{
				log.Debug( String.Format( "Fetching object '{0}' from the cache.", cacheKey ) );
			}

			object obj = _cache.Get( cacheKey );
			if( obj == null )
			{
				return null;
			}

			DictionaryEntry de = ( DictionaryEntry ) obj;

			if( key.Equals( de.Key ) )
			{
				return de.Value;
			}
			else
			{
				return null;
			}
		}

		/// <summary></summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		public void Put( object key, object value )
		{
			if( key == null )
			{
				throw new ArgumentNullException( "key", "null key not allowed" );
			}
			if( value == null )
			{
				throw new ArgumentNullException( "value", "null value not allowed" );
			}
			string cacheKey = GetCacheKey( key );
			if( _cache[ cacheKey ] != null )
			{
				if( log.IsDebugEnabled )
				{
					log.Debug( String.Format("updating value of key '{0}' to '{1}'.", cacheKey, value.ToString() ) );
				}
				_cache[ cacheKey ] = new DictionaryEntry( key, value );
			}
			else
			{
				if( log.IsDebugEnabled )
				{
					log.Debug( String.Format("adding new data: key={0}&value={1}", cacheKey, value.ToString() ) );
				}
				_map.Add( cacheKey, value );
				_cache.Add(
					cacheKey, new DictionaryEntry( key, value ), null,
					DateTime.Now.Add(_expiration), AspCache.Cache.NoSlidingExpiration, _priority,
					new AspCache.CacheItemRemovedCallback( CacheItemRemoved )
				);
			}
		}

		/// <summary>
		/// make sure the Hashtable is in sync with the cache by using the callback.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <param name="reason"></param>
		public void CacheItemRemoved( string key, object value, AspCache.CacheItemRemovedReason reason )
		{
			_map.Remove( key );
		}

		/// <summary></summary>
		/// <param name="key"></param>
		public void Remove( object key )
		{
			if( key == null )
			{
				throw new ArgumentNullException( "key" );
			}
			string cacheKey = GetCacheKey( key );
			if( log.IsDebugEnabled )
			{
				log.Debug( "removing item with key: " + cacheKey );
			}
			_map.Remove( cacheKey ); // possibly not needed now that callbacks are used
			_cache.Remove( cacheKey );
		}

		/// <summary></summary>
		public void Clear()
		{
			ArrayList keys = new ArrayList( _map.Keys );
			foreach( object key in keys )
			{
				_cache.Remove( key.ToString() );
			}
			_map.Clear();
		}

		/// <summary></summary>
		public void Destroy()
		{
			Clear();
		}

		/// <summary></summary>
		/// <param name="key"></param>
		public void Lock( object key )
		{
			// Do nothing
		}

		/// <summary></summary>
		/// <param name="key"></param>
		public void Unlock( object key )
		{
			// Do nothing
		}

		/// <summary></summary>
		public long NextTimestamp()
		{
			return Timestamper.Next();
		}

		/// <summary></summary>
		public int Timeout
		{
			get { return Timestamper.OneMs * 60000; } // 60 seconds
		}

		/// <summary></summary>
		public string Region
		{
			set { _region = value; }
		}
	}
}