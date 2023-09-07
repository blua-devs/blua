using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace bLua.JSON
{
    public static class bLuaJSONConverter
	{
		enum TokenType
		{
			Boolean,
			String,
			Number,
			BeginTable,
			EndTable,
			BeginArray,
			EndArray,
			EndOfFile,
			KeySeparator,
			ValueSeparator,
			Null,
			None
		}

		struct Token
		{
			public TokenType type;
			public string value;


			public Token(TokenType _type)
			{
				type = _type;
				value = string.Empty;
			}
			public Token(TokenType _type, string _value)
			{
				type = _type;
				value = _value;
			}
		}

		const string nullString = "null";
		const string boolTrue = "true";
		const string boolFalse = "false";
		const char beginTable = '{';
		const char endTable = '}';
		const char beginArray = '[';
		const char endArray = ']';
		const char valueSeparator = ',';
		const char keySeparator = ':';


		public static string BLuaTableToJSON(bLuaValue _value)
		{
			if (!_value.IsTable() || !IsCompatible(_value))
			{
				return nullString;
			}

			StringBuilder sb = new StringBuilder();
			BLuaValueToJSON(sb, _value);
			return sb.ToString();
		}

        public static bLuaValue JSONToBLuaTable(bLuaInstance _instance, string _json)
        {
			if (_instance == null || string.IsNullOrWhiteSpace(_json) || _json.Length == 0)
			{
				// Invalid JSON
				return bLuaValue.CreateNil();
			}

			if (!_json.StartsWith(beginTable) && !_json.StartsWith(beginArray))
			{
				// Not a table or array (you cannot JSON convert bLuaValues that are not a table/array)
				return bLuaValue.CreateNil();
            }

			bool isArray = _json.StartsWith(beginArray);
			if (_json.StartsWith(beginTable) || _json.StartsWith(beginArray))
			{
				_json = _json.Substring(1, _json.Length - 1); // Cut off the { or [ at the beginning of the table or array
            }

			StringReader sr = new StringReader(_json);
			bLuaValue table = JSONToBLuaTable(_instance, sr, isArray);

			return table;
        }

        public static string ToJson(this bLuaValue _value)
        {
            return BLuaTableToJSON(_value);
        }

        public static bLuaValue ToBLuaTable(this string _json, bLuaInstance _instance)
        {
            return JSONToBLuaTable(_instance, _json);
        }

		#region bLuaValue to JSON
		static void BLuaValueToJSON(StringBuilder _sb, bLuaValue _value)
		{
			if (_value.IsTable())
			{
				BLuaTableToJSON(_sb, _value);
            }
			else
			{
				switch (_value.Type)
				{
					case DataType.Boolean:
						_sb.Append(_value.Boolean ? boolTrue : boolFalse);
						break;
					case DataType.String:
						_sb.Append(StringToJSON(_value.String ?? ""));
						break;
					case DataType.Number:
						_sb.Append(_value.Number.ToString("r"));
						break;
					default:
						_sb.Append(nullString);
						break;
				}
			}
		}

		static void BLuaTableToJSON(StringBuilder _sb, bLuaValue _table)
		{
			if (_table.Length > 0)
			{
				_sb.Append(beginArray);
				bLuaValue[] arrayEntries = _table.List().ToArray();
				for (int i = 0; i < _table.Length; i++)
				{
					if (i > 0)
					{
						_sb.Append(valueSeparator);
					}

					if (IsCompatible(arrayEntries[i]))
					{
						BLuaValueToJSON(_sb, arrayEntries[i]);
					}
					else
					{
						_sb.Append(nullString);
					}
                }
				_sb.Append(endArray);
			}
			else if (_table.Pairs().Count > 0)
			{
				_sb.Append(beginTable);
				bLuaValue.Pair[] pairs = _table.Pairs().ToArray();
				for (int i = 0; i < pairs.Length; i++)
				{
					if (pairs[i].Key.Type == DataType.String)
					{
						if (i > 0)
						{
							_sb.Append(valueSeparator);
						}

						BLuaValueToJSON(_sb, pairs[i].Key);
						_sb.Append(keySeparator);
					}
					else
					{
						continue;
					}

					if (IsCompatible(pairs[i].Value))
					{
						BLuaValueToJSON(_sb, pairs[i].Value);
					}
					else
					{
						_sb.Append(nullString);
					}
				}
				_sb.Append(endTable);
			}
			else
			{
				_sb.Append(beginTable);
				_sb.Append(nullString);
				_sb.Append(endTable);
			}
		}
		#endregion // bLuaValue to JSON

		#region JSON to bLuaValue
        /// <summary> This is called when the StringReader reads a BeginTable token, and will fill a new table with all of the containing values. </summary>
        static bLuaValue JSONToBLuaTable(bLuaInstance _instance, StringReader _sr, bool _isArray = false)
		{
			bLuaValue table = bLuaValue.CreateTable(_instance);

			int arrayIndex = 1;
			string key = string.Empty;
			Token token = new Token(TokenType.None);
			while (token.type != TokenType.EndOfFile)
            {
				token = GetNextToken(_sr);

				switch (token.type)
				{
					case TokenType.EndOfFile:
						return table;
					case TokenType.ValueSeparator:
						if (_isArray)
                        {
							arrayIndex++;
						}
						else
                        {
							key = string.Empty;
                        }
						break;
					case TokenType.BeginTable:
						if (_isArray)
						{
							table.Set(arrayIndex, JSONToBLuaTable(_instance, _sr));
						}
						else if (!string.IsNullOrEmpty(key))
						{
							table.Set(key, JSONToBLuaTable(_instance, _sr));
						}
						break;
					case TokenType.BeginArray:
						if (_isArray)
                        {
							table.Set(arrayIndex, JSONToBLuaTable(_instance, _sr, true));
                        }
						else if (!string.IsNullOrEmpty(key))
                        {
							table.Set(key, JSONToBLuaTable(_instance, _sr, true));
                        }
						break;
					case TokenType.EndTable:
					case TokenType.EndArray:
						return table;
					case TokenType.Boolean:
						if (_isArray)
                        {
							table.Set(arrayIndex, TokenToBool(token));
                        }
						else if (!string.IsNullOrEmpty(key))
                        {
							table.Set(key, TokenToBool(token));
                        }
						break;
					case TokenType.String:
						if (_isArray)
                        {
							table.Set(arrayIndex, TokenToBool(token));
                        }
						else if (!string.IsNullOrEmpty(key))
						{
							table.Set(key, TokenToString(token));
						}
						else
						{
							key = TokenToString(token);
						}
						break;
					case TokenType.Number:
						if (_isArray)
                        {
							table.Set(arrayIndex, TokenToNumber(token));
                        }
						else if (!string.IsNullOrEmpty(key))
						{
							table.Set(key, TokenToNumber(token));
						}
						break;
					case TokenType.Null:
						if (_isArray)
						{
							table.Set(arrayIndex, bLuaValue.CreateNil());
						}
						else if (!string.IsNullOrEmpty(key))
                        {
							table.Set(key, bLuaValue.CreateNil());
                        }
						break;
				}
			}

			return table;
		}

		static bool TokenToBool(Token _token)
        {
			return _token.value == boolTrue;
		}

		static string TokenToString(Token _token)
		{
			return JSONToString(_token.value);
		}

		static double TokenToNumber(Token _token)
        {
			return double.Parse(_token.value);
		}

		static Token GetNextToken(StringReader _sr)
        {
			int i = _sr.Peek();
			if (i == -1)
            {
				return new Token(TokenType.EndOfFile);
            }
			char c = (char)_sr.Read();

			if (char.IsWhiteSpace(c))
            {
				// Skip white space
				return GetNextToken(_sr);
			}
			else if (c == '"')
            {
				string s = string.Empty;
				c = (char)_sr.Read();
				while (c != '"' && c != endTable && c != endArray)
				{
					s += c;

					i = _sr.Peek();
					if (i == -1)
					{
						break;
					}
					else if ((char)i == '"')
					{
						_sr.Read(); // Consume the ending quotation marks
						break;
					}
					c = (char)_sr.Read();
				}
				return new Token() { type = TokenType.String, value = s };
			}
			else if (c.IsJSONNumber())
            {
				string n = string.Empty;
				while (c.IsJSONNumber() && c != endTable && c != endArray)
                {
					n += c;

					i = _sr.Peek();
					if (i == -1)
                    {
						break;
                    }
					else if (!((char)i).IsJSONNumber())
                    {
						break;
                    }
					c = (char)_sr.Read();
				}
				return new Token() { type = TokenType.Number, value = n };
            }
			else if (c == beginTable)
            {
				return new Token(TokenType.BeginTable, beginTable.ToString());
            }
			else if (c == endTable)
			{
				return new Token(TokenType.EndTable, endTable.ToString());
			}
			else if (c == beginArray)
            {
				return new Token(TokenType.BeginArray, beginArray.ToString());
            }
			else if (c == endArray)
            {
				return new Token(TokenType.EndArray, endArray.ToString());
            }
			else if (c == keySeparator)
            {
				return new Token(TokenType.KeySeparator);
            }
			else if (c == valueSeparator)
            {
				return new Token(TokenType.ValueSeparator);
			}
			else if (char.IsLetter(c))
			{
				string s = string.Empty;
				while (char.IsLetter(c))
				{
					s += c;

					if (s == boolTrue)
					{
						return new Token(TokenType.Boolean, boolTrue);
					}
					else if (s == boolFalse)
					{
						return new Token(TokenType.Boolean, boolFalse);
					}
					else if (s == nullString)
                    {
						return new Token(TokenType.Null, nullString);
                    }

					if (s.Length > boolTrue.Length && s.Length > boolFalse.Length && s.Length > nullString.Length)
                    {
						break;
                    }

					i = _sr.Peek();
					if (i == -1)
					{
						break;
					}
					c = (char)_sr.Read();
				}
			}

			return new Token(TokenType.None);
        }

		static bool IsJSONNumber(this char _c)
        {
			return char.IsDigit(_c) || _c == '.';
        }
		#endregion // JSON to bLuaValue

		static string StringToJSON(string _string)
		{
			_string = _string.Replace("\b", @"\b");
			_string = _string.Replace("\f", @"\f");
			_string = _string.Replace("\n", @"\n");
			_string = _string.Replace("\r", @"\r");
			_string = _string.Replace("\t", @"\t");
			_string = _string.Replace(@"\", @"\\");
			_string = _string.Replace(@"/", @"\/");
			_string = _string.Replace("\"", "\\\"");
			return "\"" + _string + "\"";
		}

		static string JSONToString(string _json)
		{
			if (string.IsNullOrEmpty(_json))
            {
				return string.Empty;
            }

			_json = _json.Replace(@"\b", "\b");
			_json = _json.Replace(@"\f", "\f");
			_json = _json.Replace(@"\n", "\n");
			_json = _json.Replace(@"\r", "\r");
			_json = _json.Replace(@"\t", "\t");
			_json = _json.Replace(@"\\", @"\");
			_json = _json.Replace(@"\/", @"/");
			_json = _json.Replace("\\\"", "\"");
			return _json;
		}

		static bool IsCompatible(bLuaValue _value)
		{
			return _value.IsNil()
				|| _value.Type == DataType.Boolean
				|| _value.Type == DataType.String
				|| _value.Type == DataType.Number
				|| _value.Type == DataType.Table;
		}
	}
} // bLua.JSON namespace
