using System;
using System.ComponentModel;
using System.Linq;

namespace FtpDownloader {

	/// <summary>操作權限</summary>
	[Flags]
	enum Permission {
		/// <summary>Execute，可執行此檔案或開啟此資料夾</summary>
		X = 0b0001,
		/// <summary>Write，可更改此檔案或資料夾之內容</summary>
		W = 0b0010,
		/// <summary>Read，可讀取此檔案或資料夾內容</summary>
		R = 0b0100
	}

	/// <summary>描述 FTP 之物件屬性</summary>
	interface IFtpObject {

		/// <summary>取得 List 的原始字串</summary>
		string OriginString { get; }
		/// <summary>取得物件名稱(非完整路徑)</summary>
		string Name { get; }
		/// <summary>取得物件的完整存取路徑</summary>
		string Uri { get; }
		/// <summary>取得物件的標記時間</summary>
		DateTime Time { get; }
		/// <summary>取得擁有者的權限</summary>
		Permission Owner { get; }
		/// <summary>取得群組使用者的權限</summary>
		Permission Group { get; }
		/// <summary>取得匿名使用者的權限</summary>
		Permission Others { get; }

	}

	/// <summary>描述 FTP 之檔案屬性</summary>
	class FtpFile : IFtpObject, INotifyPropertyChanged {

		#region Fields
		/// <summary>暫存是否完成下載之旗標</summary>
		private bool mIsDone;
		#endregion

		#region IFtpMode Implements
		/// <summary>取得 List 的原始字串</summary>
		public string OriginString { get; }
		/// <summary>取得物件名稱(非完整路徑)</summary>
		public string Name { get; }
		/// <summary>取得物件的完整存取路徑</summary>
		public string Uri { get; }
		/// <summary>取得物件的標記時間</summary>
		public DateTime Time { get; }
		/// <summary>取得擁有者的權限</summary>
		public Permission Owner { get; }
		/// <summary>取得群組使用者的權限</summary>
		public Permission Group { get; }
		/// <summary>取得匿名使用者的權限</summary>
		public Permission Others { get; }
		#endregion

		#region INotifyPropertyChanged Implements
		public event PropertyChangedEventHandler PropertyChanged;
		private void RaisePropChg(string name) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
		#endregion

		#region Properties
		/// <summary>取得檔案大小，單位為 kB</summary>
		public int FileSize { get; }
		/// <summary>取得此檔案所在的資料夾路徑</summary>
		public string DirectoryUri { get; }
		public bool IsFinished {
			get => mIsDone;
			set {
				mIsDone = value;
				RaisePropChg("IsFinished");
			}
		}
		#endregion

		#region Constructor
		internal FtpFile(string uri, string line) {
			/* 紀錄原始字串 */
			OriginString = line;
			/* 分割 */
			var split = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			/* 權限 */
			if (split[0][1] == 'r') Owner |= Permission.R;
			if (split[0][2] == 'w') Owner |= Permission.W;
			if (split[0][3] == 'x') Owner |= Permission.X;
			if (split[0][4] == 'r') Group |= Permission.R;
			if (split[0][5] == 'w') Group |= Permission.W;
			if (split[0][6] == 'x') Group |= Permission.X;
			if (split[0][7] == 'r') Others |= Permission.R;
			if (split[0][8] == 'w') Others |= Permission.W;
			if (split[0][9] == 'x') Others |= Permission.X;
			/* 檔案大小 */
			FileSize = int.Parse(split[4]);
			/* 日期 */
			var timeStr = string.Join(" ", split.Skip(5).Take(3));
			Time = DateTime.Parse(timeStr);
			/* 檔名 */
			Name = split[8];
			DirectoryUri = uri;
			Uri = uri.EndsWith("/") ? $"{uri}{Name}" : $"{uri}/{Name}";
		}
		#endregion

		#region Overrides
		/// <summary>取得此物件的描述字串</summary>
		/// <returns>描述文字</returns>
		public override string ToString() {
			return $"File, {Name}";
		}
		#endregion
	}

	/// <summary>描述 FTP 之資料夾屬性</summary>
	class FtpDirectoy : IFtpObject {

		#region IFtpMode Implements
		/// <summary>取得 List 的原始字串</summary>
		public string OriginString { get; }
		/// <summary>取得物件名稱(非完整路徑)</summary>
		public string Name { get; }
		/// <summary>取得物件的完整存取路徑</summary>
		public string Uri { get; }
		/// <summary>取得物件的標記時間</summary>
		public DateTime Time { get; }
		/// <summary>取得擁有者的權限</summary>
		public Permission Owner { get; }
		/// <summary>取得群組使用者的權限</summary>
		public Permission Group { get; }
		/// <summary>取得匿名使用者的權限</summary>
		public Permission Others { get; }
		#endregion

		#region Constructor
		internal FtpDirectoy(string uri, string line) {
			/* 紀錄原始字串 */
			OriginString = line;
			/* 分割 */
			var split = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			/* 權限 */
			if (split[0][1] == 'r') Owner |= Permission.R;
			if (split[0][2] == 'w') Owner |= Permission.W;
			if (split[0][3] == 'x') Owner |= Permission.X;
			if (split[0][4] == 'r') Group |= Permission.R;
			if (split[0][5] == 'w') Group |= Permission.W;
			if (split[0][6] == 'x') Group |= Permission.X;
			if (split[0][7] == 'r') Others |= Permission.R;
			if (split[0][8] == 'w') Others |= Permission.W;
			if (split[0][9] == 'x') Others |= Permission.X;
			/* 日期 */
			var timeStr = string.Join(" ", split.Skip(5).Take(3));
			Time = DateTime.Parse(timeStr);
			/* 資料夾名稱 */
			Name = split[8];
			Uri = uri.EndsWith("/") ? $"{uri}{Name}/" : $"{uri}/{Name}/";
		}
		#endregion

		#region Overrides
		/// <summary>取得此物件的描述字串</summary>
		/// <returns>描述文字</returns>
		public override string ToString() {
			return $"Directory, {Name}";
		}
		#endregion
	}

}
