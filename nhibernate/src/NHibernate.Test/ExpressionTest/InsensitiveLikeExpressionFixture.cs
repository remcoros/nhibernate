using System;
using System.Data;
using System.Text;

using NHibernate.Engine;
using NExpression = NHibernate.Expression;
using NHibernate.SqlCommand;
using NHibernate.Type;

using NHibernate.DomainModel;
using NUnit.Framework;

namespace NHibernate.Test.ExpressionTest
{
	/// <summary>
	/// Summary description for InsensitiveLikeExpressionFixture.
	/// </summary>
	[TestFixture]
	public class InsensitiveLikeExpressionFixture : BaseExpressionFixture
	{
		[Test]
		public void InsentitiveLikeSqlStringTest() 
		{
			
			ISession session = factory.OpenSession();
			
			NExpression.Expression expression = NExpression.Expression.InsensitiveLike("Address", "12 Adress");

			SqlString sqlString = expression.ToSqlString(factoryImpl, typeof(Simple), "simple_alias");
			
			string expectedSql = "lower(simple_alias.address) like :simple_alias.address";
			Parameter[] expectedParams = new Parameter[1];

			ParameterLength firstParam = new ParameterLength();
			firstParam.DbType = DbType.String;
			firstParam.TableAlias = "simple_alias";
			firstParam.Name = "address";
			firstParam.Length = 200;

			expectedParams[0] = firstParam;

			CompareSqlStrings(sqlString, expectedSql, expectedParams);

			session.Close();
		}


	}
}
