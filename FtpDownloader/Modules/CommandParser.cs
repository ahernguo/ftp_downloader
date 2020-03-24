using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ahern.General {

	/// <summary>解析命令</summary>
	internal static class CommandParser {

		/// <summary>將命令參數依照 /{命令}={數值} 解出</summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public static IDictionary<string, string> Parse(string[] args) {
			/* 輪巡轉換並回傳 */
			return args.Select(
				arg => {
					/* 檢查是否符合 /{命令}={數值} 形式 */
					if (Regex.IsMatch(arg, @"\/\w+=.+", RegexOptions.IgnoreCase)) {
						/* 取出命令 */
						var cmdMatch = Regex.Match(arg, @"(?<=\/)\w+", RegexOptions.IgnoreCase);
						/* 取出數值 */
						var valMatch = Regex.Match(arg, @"(?<==).+", RegexOptions.IgnoreCase);
						/* 回傳 */
						return new string[] {
							cmdMatch.Value.ToLower(), valMatch.Value
						};
					} else {
						/* 若不符合就直接回傳 */
						return new string[] {
							arg, string.Empty
						};
					}
				}
			).ToDictionary(
				c => c[0], c => c[1]
			);
		}

	}
}
