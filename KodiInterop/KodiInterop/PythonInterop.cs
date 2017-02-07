﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

using Smx.KodiInterop.Python;
using Smx.KodiInterop.Messages;

namespace Smx.KodiInterop
{
	public static class PythonInterop {
		#region Variables
		private const string LastResultVarName = "LastResult";

		public static string GetVariable(string variableName) {
			return EvalToResult(string.Format("Variables['{0}']", variableName));
		}

		public static void DestroyVariable(string variableName) {
			Eval(string.Format("del Variables['{0}']", variableName));
		}
		#endregion

		#region Escape
		public static string EscapeArgument(object argument, EscapeFlags escapeMethod = EscapeFlags.Quotes) {
			if (argument == null) {
				return "None";
			}

			//If it's a variable, return it's unquoted python name
			if (argument is PyVariable)
				return (argument as PyVariable).PyName;

			string text = argument.ToString();

			//Don't escape primitives
			if (
				argument is bool ||
				argument is int ||
				argument is uint ||
				argument is long ||
				argument is ulong ||
				argument is float ||
				argument is double
			) {
				return text;
			}

			if (escapeMethod.HasFlag(EscapeFlags.Quotes)) {
				text = Regex.Replace(text, "\r?\n", "\\n");
				text = '"' + text.Replace("\"", "\\\"") + '"';
			}
			if (escapeMethod.HasFlag(EscapeFlags.EscapeBuiltin)) {
				text = Regex.Replace(text, ",", "\\,");
			}
			if (escapeMethod.HasFlag(EscapeFlags.RawString)) {
				text = "r'" + text + "'";
			}

			return text;
		}

		public static List<string> EscapeArguments(IEnumerable<object> arguments, EscapeFlags escapeMethod = EscapeFlags.Quotes) {
			List<string> textArguments = new List<string>();

			int i = arguments.Count();
			if (
				i > 0 &&
				escapeMethod.HasFlag(EscapeFlags.StripNullItems)
			) {
				List<object> argumentsList = arguments.ToList();

				int nulls = 0;
				for (i = i - 1; i >= 0; --i) {
					// Found the end of the null series
					if (argumentsList[i] != null) {
						argumentsList.RemoveRange(i + 1, nulls);
						arguments = argumentsList;
						break;
					}
					nulls++;
				}
			}

			foreach (object argument in arguments) {
				textArguments.Add(EscapeArgument(argument, escapeMethod));
			}

			return textArguments;
		}

		public static List<string> EscapeArguments(EscapeFlags escapeMethod = EscapeFlags.Quotes, params object[] arguments) {
			return EscapeArguments(arguments, escapeMethod);
		}
		#endregion

		#region FunctionCall
		public static string CallFunction(string module, string functionName, List<string> arguments) {
			return EvalToResult(string.Format("{0}.{1}({2})", module, functionName, string.Join(",", arguments.ToArray())));
		}

		public static string CallFunction(PythonFunction pythonFunction, List<object> arguments = null) {
			if (arguments == null)
				arguments = new List<object>();
			List<string> textArguments = EscapeArguments(arguments);
			return CallFunction(
				pythonFunction.Module,
				pythonFunction.Function,
				textArguments
			);
		}

		public static string CallFunction(PyModule module, string functionName, List<object> arguments) {
			return CallFunction(new PythonFunction(module, functionName), arguments);
		}

		public static string CallBuiltin(string builtinName, List<string> arguments) {
			return CallFunction(PyModule.Xbmc, "executebuiltin", new List<object> {
				//Kodi builtins shouldn't have quotes, so we pass a single parameter with the joined parameters
				string.Format("{0}({1})",
					builtinName,
					string.Join(",", arguments.Select(a => EscapeArgument(a, EscapeFlags.EscapeBuiltin)).ToArray())
				)
			});
		}

		public static string CallBuiltin(string builtinName) {
			return CallBuiltin(builtinName, new List<string> { });
		}

		public static string CallBuiltin(string builtinName, List<object> arguments) {
			List<string> textArguments = EscapeArguments(arguments, EscapeFlags.None);
			return CallBuiltin(builtinName, textArguments);
		}
		#endregion

		#region Eval
		public static string Eval(string code) {
			PythonEvalMessage msg = new PythonEvalMessage {
				Code = code
			};

			string replyString = KodiBridge.SendMessage(msg);
			PythonEvalReply reply = JsonConvert.DeserializeObject<PythonEvalReply>(replyString);
			return reply.Result;
		}

		public static string EvalToVar(string variableName, string code) {
			Console.WriteLine(variableName + " = " + code);
			return Eval(string.Format("Variables['{0}'] = {1}", variableName, code));
		}

		public static string EvalToVar(string variableName, string codeFormat, List<object> arguments, EscapeFlags escapeMethod) {
			List<string> textArguments = EscapeArguments(arguments, escapeMethod);
			return EvalToVar(variableName, string.Format(codeFormat, textArguments.ToArray()));
		}

		public static string EvalToResult(string code) {
			return EvalToVar(LastResultVarName, code);
		}
		#endregion
	}
}
