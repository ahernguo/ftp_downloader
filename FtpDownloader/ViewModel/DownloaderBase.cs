using Ahern.General;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ahern.Ftp {

	/// <summary>FTP 下載器</summary>
	internal class FtpDownloader : DownloaderBase {

		#region Fields
		/// <summary>暫存 FTP 站台</summary>
		private readonly string mSite;
		/// <summary>暫存 FTP 登入的使用者</summary>
		private readonly string mUsr;
		/// <summary>暫存 FTP 登入的密碼</summary>
		private readonly string mPwd;
		/// <summary>暫存本機下載的目標資料夾</summary>
		private readonly string mTarDir;
		/// <summary>暫存下載完畢後是否自動關閉視窗</summary>
		private readonly bool mAutoClose;
		/// <summary>操作 FTP 之物件</summary>
		private readonly FtpClient mFtp;
		/// <summary>檔案大小容量對應表，用於自動調配單位</summary>
		private readonly Dictionary<Unit, decimal> mUnits;
		/// <summary>指出當前是否要繼續下載的暫停旗標</summary>
		private readonly ManualResetEventSlim mPauseSign;
		/// <summary>指出是否可以開始進行下載之任務</summary>
		private readonly ManualResetEventSlim mStartSign;
		/// <summary>下載的主執行緒取消物件</summary>
		private readonly CancellationTokenSource mCncSrc;
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
			if (!map.TryGetValue("dir", out mTarDir)) {
				throw new Exception("Must contains '/dir' command");
			}
			if (map.TryGetValue("autoclose", out var autoclose)) {
				if (!bool.TryParse(autoclose, out mAutoClose)) {
					throw new Exception("Invalid value of '/AutoClose'. It must be 'true' or 'false'");
				}
			} else {
				mAutoClose = true;	//預設為自動關閉視窗
			}
			mTarDir = EnsureSlash(mTarDir.Replace("\"", string.Empty));
			/* 初始化數值 */
			Caption = $"Site: {mSite}";
			/* 初始化物件 */
			mUnits = Enum.GetValues(typeof(Unit))
				.Cast<Unit>()
				.ToDictionary(u => u, u => (decimal)u);
			mFtp = new FtpClient(mSite, mUsr, mPwd);
			mPauseSign = new ManualResetEventSlim();
			mStartSign = new ManualResetEventSlim();
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
		/// <param name="size">當前的容量大小</param>
		/// <returns>自動調配後的字串</returns>
		private string ToChunkSize(decimal size) {
			/* 嘗試找出比此範圍大的級距，如 1050 bytes 會找到 Units.KB (1024) */
			var match = mUnits.LastOrDefault(kvp => size > kvp.Value);
			if (match.Value > 0) {
				var sz = size / match.Value;
				return $"{sz:F2} {match.Key}";
			} else {
				throw new Exception("Can not find match unit");
			}
		}
		#endregion

		#region Threads
		private void DownloadProcess() {
			var cncToken = Task.Factory.CancellationToken;
			var step = 0;
			var idx = 0;
			var delay = 500;
			decimal curSz = 0;
			decimal maxSz = 0;
			decimal lastSz = 0;
			var files = new List<FtpFile>();
			var dirs = new List<FtpDirectoy>();
			var created = new List<string>();
			try {
				/* 卡住直到介面顯示完成 */
				Info = "Waiting...";
				mStartSign.Wait();
				SpinWait.SpinUntil(() => false, 500);
				/* 一直跑，直到完成或取消工作 */
				while (!cncToken.IsCancellationRequested && step < 100) {
					switch (step) {
						case 0: /* 取出所有檔案 */
							Info = "Searching files...";
							mFtp.ListAllObjects(files, dirs);
							step++;
							delay = 500;
							break;
						case 1:	/* 將檔案加入集合 */
							Info = "Pending files...";
							files.ForEach(f => Files.Add(f));
							step++;
							delay = 500;
							break;
						case 2:	/* 計算總大小 */
							Info = "Calculating size...";
							maxSz = files.Sum(f => f.FileSize);
							MaximumSize = ToChunkSize(maxSz);
							/* 如果 maxSz 有問題，噴出去取消動作 */
							if (maxSz <= 0) {
								throw new Exception("Calculate size failed.");
							}
							step++;
							delay = 500;
							break;
						case 3:	/* 準備下載 */
							Info = "Prepare to download...";
							/* 建立根目錄 */
							Directory.CreateDirectory(mTarDir);
							/* 建立各個子資料夾 */
							dirs.ForEach(
								dir => {
									var path = string.IsNullOrEmpty(dir.Directory) ?
										$"{mTarDir}{dir.Name}" :
										$"{mTarDir}{EnsureSlash(dir.Directory)}{dir.Name}";
									Directory.CreateDirectory(path);
								}
							);
							/* 註冊事件 */
							mFtp.ProgressUpdated += (sender, e) => {
								/* 如果已經下載好，更新 curSz 並更改 CheckBox 狀態 */
								if (e.IsFinished) {
									/* 累加總下載量 */
									curSz += e.FullSize;
									/* 更新當前大小與進度 */
									CurrentSize = ToChunkSize(curSz);
									Progress = (double)(curSz / maxSz) * 100.0;
								} else {
									/* 還沒下載好，不能更新 curSz，先用暫存的 */
									lastSz = curSz + e.CurrentSize;
									/* 更新當前大小與進度 */
									CurrentSize = ToChunkSize(lastSz);
									Progress = (double)(lastSz / maxSz) * 100.0;
								}
							};
							step++;
							delay = 500;
							break;
						case 4: /* 循環直至下載完畢 */
							var file = files[idx++];
							/* 更改訊息 */
							Info = $"Download: {file.Name}";
							var localFold = string.IsNullOrEmpty(file.Directory) ?
								mTarDir :
								$"{mTarDir}{EnsureSlash(file.Directory)}";
							mFtp.Download(file, localFold);
							/* 更新介面 */
							file.IsFinished = true;
							/* 如果已經都抓完，跳下一步 */
							if (idx.Equals(files.Count)) {
								step++;
							}
							delay = 10;
							break;
						case 5: /* 下載完成，等待一秒 */
							Info = "Finished";
							if (mAutoClose) {
								Task.Factory.StartNew(
									() => {
										SpinWait.SpinUntil(() => false, TimeSpan.FromSeconds(1));
										RaiseDone();
									}
								);
							}
							step = 100;	//離開
							break;
						default:
							break;
					}
					/* 體感延遲一下 */
					SpinWait.SpinUntil(() => false, delay);
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
		/// <summary>開始進行下載之動作</summary>
		public override void StartDownload() {
			mStartSign.Set();
		}

		/// <summary>確保文字以「\」結尾</summary>
		/// <param name="data">欲檢查的字串</param>
		/// <returns>帶有「\」結尾的字串</returns>
		private string EnsureSlash(string data) {
			return data.EndsWith(@"\") ? data : $@"{data}\";
		}
		#endregion

		#region Help Info
		/// <summary>取得 FTP 下載器之命令參數</summary>
		/// <returns>命令參數列表</returns>
		public static string Help() {
			var sb = new StringBuilder();
			sb.AppendLine("FTP Downloader (fdn) Usage:");
			sb.AppendLine("    fdn /Site /User /Pwd /Dir [/AutoClose]");
			sb.AppendLine();
			sb.AppendLine("** Commands are case insensitive **");
			sb.AppendLine("");
			sb.AppendLine("Commands:");
			sb.AppendLine("    /Site\tFTP 站點");
			sb.AppendLine("\t如 '/Site=192.168.1.1'");
			sb.AppendLine("    /User\tFTP 站點的登入帳號");
			sb.AppendLine("\t如 '/User=root'");
			sb.AppendLine("    /Pwd\tFTP 登入帳號的對應密碼(不接受空白密碼)");
			sb.AppendLine("\t如 '/Pwd=rootPassword'");
			sb.AppendLine("    /Dir\t下載後欲存放的本機資料夾，空白請加上雙引號");
			sb.AppendLine("\t如 '/Dir=\"D:\\Company Recipes\"'");
			sb.AppendLine();
			sb.AppendLine("Optionals:");
			sb.AppendLine("    /AutoClose  下載完畢是否自動關閉視窗(延遲一秒)");
			sb.AppendLine("\t數值必為 'true' 或 'false'。如 '/AutoCloase=True'");
			sb.AppendLine();
			return sb.ToString();
		}
		#endregion
	}

}
