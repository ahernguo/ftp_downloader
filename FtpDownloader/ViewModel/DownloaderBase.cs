using Ahern.General;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ahern.Ftp {

	/// <summary>FTP 下載器</summary>
	internal class FtpDownloader : IDownloader {

		#region Fields
		/// <summary>暫存進度百分比</summary>
		private double mProg;
		/// <summary>暫存當前已下載的大小</summary>
		private string mCurSz;
		/// <summary>暫存所需下載的大小</summary>
		private string mMaxSz;
		/// <summary>暫存 FTP 站台</summary>
		private readonly string mSite;
		/// <summary>暫存 FTP 登入的使用者</summary>
		private readonly string mUsr;
		/// <summary>暫存 FTP 登入的密碼</summary>
		private readonly string mPwd;
		/// <summary>操作 FTP 之物件</summary>
		private readonly FtpClient mFtp;
		/// <summary>檔案大小容量對應表，用於自動調配單位</summary>
		private readonly Dictionary<Unit, int> mUnits;
		/// <summary>指出當前是否要繼續下載的暫停旗標</summary>
		private readonly ManualResetEventSlim mPauseSign;
		/// <summary>下載的主執行緒取消物件</summary>
		private readonly CancellationTokenSource mCncSrc;
		#endregion

		#region Properties

		#region Window Title
		/// <summary>取得欲顯示於視窗的標題</summary>
		public string Caption { get; }
		#endregion

		#region Progress
		/// <summary>取得或設定當前的進度百分比</summary>
		public double Progress {
			get => mProg;
			set {
				mProg = value;
				RaisePropChg("Progress");
			}
		}
		/// <summary>取得當前已下載的大小</summary>
		public string CurrentSize {
			get => mCurSz;
			set {
				mCurSz = value;
				RaisePropChg("CurrentSize");
			}
		}
		/// <summary>取得所需下載的大小</summary>
		public string MaximumSize {
			get => mMaxSz;
			set {
				mMaxSz = value;
				RaisePropChg("MaximumSize");
			}
		}
		#endregion

		#region File List
		/// <summary>取得欲下載的檔案集合</summary>
		public WpfObservableCollection<IRemoteObject> Files { get; }
		#endregion

		#endregion

		#region INotifyPropertyChanged Implements
		public event PropertyChangedEventHandler PropertyChanged;
		private void RaisePropChg(string name) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
		#endregion

		#region Constructor
		/// <summary>初始化下載器</summary>
		/// <param name="args">使用者傳入的引數</param>
		public FtpDownloader(string[] args) {
			/* 解析參數 */
			var map = CommandParser.Parse(args);
			//如果有不認識的，直接噴出去
			var unknown = map.FirstOrDefault(kvp => string.IsNullOrEmpty(kvp.Value));
			if (!string.IsNullOrEmpty(unknown.Key)) {
				throw new Exception($"Unrecognized command: '{unknown.Key}'");
			}
			//依序取出參數
			if (!map.TryGetValue("site", out mSite)) {
				throw new Exception("Must contains '/site' command");
			}
			if (!map.TryGetValue("user", out mUsr)) {
				throw new Exception("Must contains '/user' command");
			}
			if (!map.TryGetValue("pwd", out mPwd)) {
				throw new Exception("Must contains '/pwd' command");
			}
			/* 初始化數值 */
			mProg = 0.0;
			mCurSz = "0 KB";
			mMaxSz = "0 KB";
			/* 初始化物件 */
			Files = new WpfObservableCollection<IRemoteObject>();
			mUnits = Enum.GetValues(typeof(Unit))
				.Cast<Unit>()
				.ToDictionary(u => u, u => (int)u);
			mFtp = new FtpClient(mSite, mUsr, mPwd);
			mPauseSign = new ManualResetEventSlim();
			mCncSrc = new CancellationTokenSource();
			/* 啟動執行緒 */
			Task.Factory.StartNew(
				DownloadProcess,
				mCncSrc.Token,
				TaskCreationOptions.LongRunning,
				TaskScheduler.Current
			);
		}
		#endregion

		#region Utilities
		/// <summary>計算當前的容量大小，自動調配單位</summary>
		/// <param name="curSz">當前的容量大小</param>
		/// <param name="curUnit">當前的單位</param>
		/// <returns>自動調配後的字串</returns>
		private string ToChunkSize(ref double curSz, ref Unit curUnit) {
			/* 如果大小已大於 1024，則取得下一個單位並重新計算大小 */
			if (curSz > 1024.0 && curUnit < Unit.GB) {
				/* 找出此單位的下一個等級 */
				var unitValue = (int)curUnit;
				var kvp = mUnits.FirstOrDefault(p => p.Value > unitValue);
				/* 賦值並重新計算 */
				curUnit = kvp.Key;
				curSz /= 1024.0;
			}
			/* 組合字串並回傳 */
			return $"{curSz:F2} {curUnit.ToString()}";
		}
		#endregion

		#region Threads
		private void DownloadProcess() {
			var cncToken = Task.Factory.CancellationToken;
			var step = 0;
			try {
				/* 一直跑，直到完成或取消工作 */
				while (!cncToken.IsCancellationRequested && step < 100) {
					switch (step) {
						case 0: /* 取出所有檔案 */
							var files = new List<FtpFile>();
							FindAllFiles(files);
							break;
						default:
							break;
					}
				}
			} catch (Exception ex) {
				MessageBox.Show(
					ex.Message,
					"Exception",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
			}
		}
		#endregion

		#region Methods
		/// <summary>確保字串後方帶有斜線</summary>
		/// <param name="data">欲檢查的字串</param>
		/// <returns>帶有斜線的字串</returns>
		private string EnsureSlash(string data) {
			return data.EndsWith("/") ? data : $"{data}/";
		}

		/// <summary>列出所有 FTP 檔案</summary>
		/// <param name="files">欲儲存檔案的集合</param>
		/// <param name="subDir">子目錄</param>
		private void FindAllFiles(IList<FtpFile> files, string subDir = null) {
			/* 列出此資料夾所有的物件 */
			var objs = mFtp.ListObjects(subDir);
			/* 取出檔案並加入集合 */
			var fObjs = objs.Where(o => o is FtpFile);
			foreach (FtpFile fObj in fObjs) {
				files.Add(fObj);
			}
			/* 取出資料夾並遞迴 */
			var dObjs = objs.Where(o => o is FtpDirectoy);
			foreach (FtpDirectoy dObj in dObjs) {
				var subName = string.IsNullOrEmpty(subDir) ? dObj.Name : $"{EnsureSlash(subDir)}{dObj.Name}";
				FindAllFiles(files, subName);
			}
		}
		#endregion
	}

}
