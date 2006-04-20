#if NET_2_0

using System;
using System.Collections.Generic;
using System.Text;

namespace NHibernate.Test.GenericTest.MapGeneric
{
	public class A
	{
		private int? _id;
		private string _name;
		private IDictionary<string, B> _items;

		public A() { }

		public int? Id
		{
			get { return _id; }
			set { _id = value; }
		}

		public string Name
		{
			get { return _name; }
			set { _name = value; }
		}

		public IDictionary<string,B> Items
		{
			get { return _items; }
			set { _items = value; }
		}

	}
}
#endif