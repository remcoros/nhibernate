using System;
using System.Collections;
using System.Globalization;
using System.Text;
using Iesi.Collections;
using NHibernate.Hql;
using NHibernate.Util;

namespace NHibernate.SqlCommand
{
	public sealed class Template
	{
		private static readonly ISet Keywords = new HashedSet();
		private static readonly ISet BeforeTableKeywords = new HashedSet();
		private static readonly ISet FunctionKeywords = new HashedSet();
		
		static Template()
		{
			Keywords.Add("and");
			Keywords.Add("or");
			Keywords.Add("not");
			Keywords.Add("like");
			Keywords.Add("is");
			Keywords.Add("in");
			Keywords.Add("between");
			Keywords.Add("null");
			Keywords.Add("select");
			Keywords.Add("distinct");
			Keywords.Add("from");
			Keywords.Add("join");
			Keywords.Add("inner");
			Keywords.Add("outer");
			Keywords.Add("left");
			Keywords.Add("right");
			Keywords.Add("on");
			Keywords.Add("where");
			Keywords.Add("having");
			Keywords.Add("group");
			Keywords.Add("order");
			Keywords.Add("by");
			Keywords.Add("desc");
			Keywords.Add("asc");
			Keywords.Add("limit");
			Keywords.Add("any");
			Keywords.Add("some");
			Keywords.Add("exists");
			Keywords.Add("all");

			BeforeTableKeywords.Add("from");
			BeforeTableKeywords.Add("join");

			FunctionKeywords.Add("as");
			FunctionKeywords.Add("leading");
			FunctionKeywords.Add("trailing");
			FunctionKeywords.Add("from");
			FunctionKeywords.Add("case");
			FunctionKeywords.Add("when");
			FunctionKeywords.Add("then");
			FunctionKeywords.Add("else");
			FunctionKeywords.Add("end");
		}

		public static readonly string Placeholder = "$PlaceHolder$";

		private Template() { }
		
		public static string RenderWhereStringTemplate(string sqlWhereString, Dialect.Dialect dialect)
		{
			return RenderWhereStringTemplate(sqlWhereString, Placeholder, dialect);
		}

		public static string RenderWhereStringTemplate(string sqlWhereString, string placeholder, Dialect.Dialect dialect)
		{
			//TODO: make this a bit nicer
			string symbols = new StringBuilder()
				.Append("=><!+-*/()',|&`")
				.Append(ParserHelper.Whitespace)
				.Append(dialect.OpenQuote)
				.Append(dialect.CloseQuote)
				.ToString();
			StringTokenizer tokens = new StringTokenizer(sqlWhereString, symbols, true);

			StringBuilder result = new StringBuilder();
			bool quoted = false;
			bool quotedIdentifier = false;
			bool beforeTable = false;
			bool inFromClause = false;
			bool afterFromTable = false;

			IEnumerator tokensEnum = tokens.GetEnumerator();
			bool hasMore = tokensEnum.MoveNext();
			string nextToken = hasMore ? (string) tokensEnum.Current : null;
			while (hasMore)
			{
				string token = nextToken;
				string lcToken = token.ToLower(CultureInfo.InvariantCulture);
				hasMore = tokensEnum.MoveNext();
				nextToken = hasMore ? (string) tokensEnum.Current : null;

				bool isQuoteCharacter = false;

				if (!quotedIdentifier && "'".Equals(token))
				{
					quoted = !quoted;
					isQuoteCharacter = true;
				}

				if (!quoted)
				{

					bool isOpenQuote;
					if ("`".Equals(token))
					{
						isOpenQuote = !quotedIdentifier;
						token = lcToken = isOpenQuote ?
							dialect.OpenQuote.ToString() :
							dialect.CloseQuote.ToString();
						quotedIdentifier = isOpenQuote;
						isQuoteCharacter = true;
					}
					else if (!quotedIdentifier && (dialect.OpenQuote == token[0]))
					{
						isOpenQuote = true;
						quotedIdentifier = true;
						isQuoteCharacter = true;
					}
					else if (quotedIdentifier && (dialect.CloseQuote == token[0]))
					{
						quotedIdentifier = false;
						isQuoteCharacter = true;
						isOpenQuote = false;
					}
					else
					{
						isOpenQuote = false;
					}

					if (isOpenQuote)
					{
						result.Append(placeholder).Append('.');
					}

				}

				bool quotedOrWhitespace = quoted ||
					quotedIdentifier ||
					isQuoteCharacter ||
					char.IsWhiteSpace(token[0]);

				if (quotedOrWhitespace)
				{
					result.Append(token);
				}
				else if (beforeTable)
				{
					result.Append(token);
					beforeTable = false;
					afterFromTable = true;
				}
				else if (afterFromTable)
				{
					if (!"as".Equals(lcToken))
						afterFromTable = false;
					result.Append(token);
				}
				else if (IsNamedParameter(token))
				{
					result.Append(token);
				}
				else if (
					IsIdentifier(token, dialect) &&
					!IsFunctionOrKeyword(lcToken, nextToken, dialect)
				)
				{
					result.Append(placeholder)
						.Append('.')
						.Append(dialect.QuoteForColumnName(token));
				}
				else
				{
					if (BeforeTableKeywords.Contains(lcToken))
					{
						beforeTable = true;
						inFromClause = true;
					}
					else if (inFromClause && ",".Equals(lcToken))
					{
						beforeTable = true;
					}
					result.Append(token);
				}

				if ( //Yuck:
						inFromClause &&
						Keywords.Contains(lcToken) && //"as" is not in Keywords
						!BeforeTableKeywords.Contains(lcToken)
				)
				{
					inFromClause = false;
				}

			}
			return result.ToString();
		}

		public static string RenderOrderByStringTemplate(string sqlOrderByString, Dialect.Dialect dialect)
		{
			//TODO: make this a bit nicer
			string symbols = new StringBuilder()
				.Append("=><!+-*/()',|&`")
				.Append(ParserHelper.Whitespace)
				.Append(dialect.OpenQuote)
				.Append(dialect.CloseQuote)
				.ToString();
			StringTokenizer tokens = new StringTokenizer(sqlOrderByString, symbols, true);

			StringBuilder result = new StringBuilder();
			bool quoted = false;
			bool quotedIdentifier = false;

			IEnumerator tokensEnum = tokens.GetEnumerator();
			bool hasMore = tokensEnum.MoveNext();
			string nextToken = hasMore ? (string) tokensEnum.Current : null;
			while (hasMore)
			{
				string token = nextToken;
				string lcToken = token.ToLower(CultureInfo.InvariantCulture);
				hasMore = tokensEnum.MoveNext();
				nextToken = hasMore ? (string) tokensEnum.Current : null;

				bool isQuoteCharacter = false;

				if (!quotedIdentifier && "'".Equals(token))
				{
					quoted = !quoted;
					isQuoteCharacter = true;
				}

				if (!quoted)
				{

					bool isOpenQuote;
					if ("`".Equals(token))
					{
						isOpenQuote = !quotedIdentifier;
						token = lcToken = isOpenQuote ?
							dialect.OpenQuote.ToString() :
							dialect.CloseQuote.ToString();
						quotedIdentifier = isOpenQuote;
						isQuoteCharacter = true;
					}
					else if (!quotedIdentifier && (dialect.OpenQuote == token[0]))
					{
						isOpenQuote = true;
						quotedIdentifier = true;
						isQuoteCharacter = true;
					}
					else if (quotedIdentifier && (dialect.CloseQuote == token[0]))
					{
						quotedIdentifier = false;
						isQuoteCharacter = true;
						isOpenQuote = false;
					}
					else
					{
						isOpenQuote = false;
					}

					if (isOpenQuote)
					{
						result.Append(Placeholder).Append('.');
					}

				}

				bool quotedOrWhitespace = quoted ||
					quotedIdentifier ||
					isQuoteCharacter ||
					char.IsWhiteSpace(token[0]);

				if (quotedOrWhitespace)
				{
					result.Append(token);
				}
				else if (
					IsIdentifier(token, dialect) &&
					!IsFunctionOrKeyword(lcToken, nextToken, dialect)
				)
				{
					result.Append(Placeholder)
						.Append('.')
						.Append(dialect.QuoteForColumnName(token));
				}
				else
				{
					result.Append(token);
				}
			}
			return result.ToString();
		}

		private static bool IsNamedParameter(string token)
		{
			return token.StartsWith(":");
		}

		private static bool IsFunctionOrKeyword(string lcToken, string nextToken, Dialect.Dialect dialect)
		{
			return "(".Equals(nextToken) ||
				Keywords.Contains(lcToken) ||
				dialect.Functions.Contains(lcToken) ||
				// TODO H3: dialect.Keywords.Contains(lcToken) ||
				FunctionKeywords.Contains(lcToken);
		}

		private static bool IsIdentifier(string token, Dialect.Dialect dialect)
		{
			return token[0] == '`' || ( //allow any identifier quoted with backtick
				char.IsLetter(token[0]) && //only recognizes identifiers beginning with a letter
				token.IndexOf('.') < 0
			);
		}
	}
}