//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:2.0.50727.91
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace NHibernateExtensions.Caches.SysCache.Properties
{
	using System;

	/// <summary>
	///   A strongly-typed resource class, for looking up localized strings, etc.
	/// </summary>
	// This class was auto-generated by the StronglyTypedResourceBuilder
	// class via a tool like ResGen or Visual Studio.
	// To add or remove a member, edit your .ResX file then rerun ResGen
	// with the /str option, or rebuild your VS project.
	[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "2.0.0.0")]
	[DebuggerNonUserCode()]
	[CompilerGenerated()]
	internal class Resources
	{
		private static ResourceManager resourceMan;

		private static CultureInfo resourceCulture;

		[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
		internal Resources()
		{
		}

		/// <summary>
		///   Returns the cached ResourceManager instance used by this class.
		/// </summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		internal static ResourceManager ResourceManager
		{
			get
			{
				if (ReferenceEquals(resourceMan, null))
				{
					ResourceManager temp =
						new ResourceManager("NHibernateExtensions.Caches.SysCache.Properties.Resources", typeof(Resources).Assembly);
					resourceMan = temp;
				}
				return resourceMan;
			}
		}

		/// <summary>
		///   Overrides the current thread's CurrentUICulture property for all
		///   resource lookups using this strongly typed resource class.
		/// </summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		internal static CultureInfo Culture
		{
			get { return resourceCulture; }
			set { resourceCulture = value; }
		}

		/// <summary>
		///   Looks up a localized string similar to There are no configured connection strings..
		/// </summary>
		internal static string ConnectionStringNotConfigured
		{
			get { return ResourceManager.GetString("ConnectionStringNotConfigured", resourceCulture); }
		}

		/// <summary>
		///   Looks up a localized string similar to Named connection string, &apos;{0}&apos;, could not be found by configured connection string provider..
		/// </summary>
		internal static string NamedConnectionStringNotFound
		{
			get { return ResourceManager.GetString("NamedConnectionStringNotFound", resourceCulture); }
		}
	}
}