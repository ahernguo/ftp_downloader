using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace FtpDownloader {

	/// <summary>存取遠端 FTP 站台之檔案、資料夾之互動元件</summary>
	internal class FtpClient : IDisposable {

		#region Definitions
		/// <summary>緩衝區的預設長度</summary>
		private const int BUFFER_LENGTH = 1024;
		#endregion

		#region Fields
		/// <summary>暫存使用者的登入資訊</summary>
		private readonly NetworkCredential mCred;
		/// <summary>讀取或上傳用的緩衝區</summary>
		private Lazy<byte[]> mBuf;
		/// <summary>讀取或上傳用的緩衝區數量</summary>
		private int mCnt;
		#endregion

		#region Properties
		/// <summary>取得 FTP 站點之名稱，如 "192.168.10.14"、"doc.adept.com"</summary>
		public string HostName { get; }
		#endregion

		#region Constructor
		public FtpClient(string hostName, string userName, string password) {
			HostName = DelSlash(hostName);
			mCred = new NetworkCredential(userName, password);
			mBuf = new Lazy<byte[]>(() => new byte[BUFFER_LENGTH]);
		}
		#endregion

		#region Utilities
		/// <summary>檢查字串結尾是否有斜線「/」，若沒有則補上之</summary>
		/// <param name="data">欲檢查之字串</param>
		/// <returns>含有斜線結尾的字串</returns>
		private string AddSlash(string data) {
			return data.EndsWith("/") ? data : $"{data}/";
		}

		/// <summary>檢查字串結尾是否有斜線「/」，有則移除之</summary>
		/// <param name="data">欲檢查之字串</param>
		/// <returns>不含斜線結尾的字串</returns>
		private string DelSlash(string data) {
			return data.EndsWith("/") ? data.TrimEnd('/') : data;
		}

		/// <summary>取得檔案或資料夾對應的 URI，會自動組裝「ftp://」與 <see cref="HostName"/></summary>
		/// <param name="subDir">子資料夾</param>
		/// <returns>FTP 路徑</returns>
		private string GetUri(string subDir) {
			/* 如果是空字串，表示指向跟目錄，直接回傳 */
			if (string.IsNullOrEmpty(subDir)) {
				return $"ftp://{HostName}";
			} else {
				/* 如果有子資料夾，組合之 */
				return $"ftp://{HostName}/{DelSlash(subDir)}";
			}
		}

		/// <summary>取得檔案或資料夾對應的 URI，會自動組裝「ftp://」與 <see cref="HostName"/></summary>
		/// <param name="subDir">子資料夾</param>
		/// <param name="fileName">檔案名稱</param>
		/// <returns>FTP 路徑</returns>
		private string GetUri(string subDir, string fileName) {
			/* 如果是空字串，表示指向跟目錄，直接回傳 */
			if (string.IsNullOrEmpty(subDir)) {
				return $"ftp://{HostName}/{fileName}";
			} else {
				/* 如果有子資料夾，組合之 */
				return $"ftp://{HostName}/{AddSlash(subDir)}/{fileName}";
			}
		}

		/// <summary>建立 FTP 存取器</summary>
		/// <param name="uri">欲進行操作的 FTP 路徑，如 "ftp://192.168.10.14"</param>
		/// <returns>FTP 存取器</returns>
		private FtpWebRequest GetRequest(string uri) {
			var ftp = WebRequest.Create(uri) as FtpWebRequest;
			ftp.UseBinary = true;
			ftp.UsePassive = true;
			ftp.KeepAlive = true;
			ftp.Credentials = mCred;
			return ftp;
		}

		/// <summary>執行 FTP 方法並取得其回應</summary>
		/// <param name="ftpReq">欲操作的 FTP 存取器</param>
		/// <param name="method">欲操作的方法，如 <see cref="WebRequestMethods.Ftp.DownloadFile"/></param>
		/// <returns>FTP 回應</returns>
		private FtpWebResponse GetResponse(FtpWebRequest ftpReq, string method) {
			/* 設定方法 */
			ftpReq.Method = method;
			/* 開啟串流並回傳 */
			return ftpReq.GetResponse() as FtpWebResponse;
		}

		/// <summary>執行 FTP 方法並取得其回應</summary>
		/// <param name="uri">欲進行操作的 FTP 路徑，如 "ftp://192.168.10.14"</param>
		/// <param name="method">欲操作的方法，如 <see cref="WebRequestMethods.Ftp.DownloadFile"/></param>
		/// <returns>FTP 回應</returns>
		private FtpWebResponse GetResponse(string uri, string method) {
			var ftp = WebRequest.Create(uri) as FtpWebRequest;
			ftp.UseBinary = true;
			ftp.UsePassive = true;
			ftp.KeepAlive = true;
			ftp.Credentials = mCred;
			ftp.Method = method;
			return ftp.GetResponse() as FtpWebResponse;
		}
		#endregion

		#region List
		/// <summary>取得目錄下的所有檔案與資料夾資訊</summary>
		/// <param name="subDir">(null)取得根目錄資訊  (others)取得子資料夾之資訊，如欲取得 "ftp://localhost/Recipe/MotionA" 則此處帶入 @"Recipe/MotionA"</param>
		/// <returns>檔案與資料夾資訊</returns>
		public IList<IFtpObject> ListObjects(string subDir = null) {
			/* 取得路徑 */
			var uri = GetUri(subDir);
			/* 設定方法並開啟回應 */
			List<IFtpObject> objects = null;
			using (var rsp = GetResponse(uri, WebRequestMethods.Ftp.ListDirectoryDetails)) {
				using (var sr = new StreamReader(rsp.GetResponseStream())) {
					/* 一口氣讀完 */
					var rspStr = sr.ReadToEnd();
					/* 拆解 */
					objects = rspStr
						.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(
							line => 'd'.Equals(line[0]) ? new FtpDirectoy(uri, line) : new FtpFile(uri, line) as IFtpObject
						).ToList();
				}
			}
			/* 回傳 */
			return objects;
		}
		#endregion

		#region Download
		/// <summary>下載檔案至指定的本機資料夾內</summary>
		/// <param name="file">欲下載的檔案資訊</param>
		/// <param name="localFold">欲存放的本機資料夾，如 @"D:\Recipe"</param>
		public void Download(FtpFile file, string localFold) {
			/* 檢查本機存放的目錄位置是否存在，不存在則建立之 */
			if (!Directory.Exists(localFold)) {
				Directory.CreateDirectory(localFold);
			}
			/* 組裝本地檔案路徑，將名稱給加到後方 */
			var path = localFold.EndsWith("\\") ? $"{localFold}{file.Name}" : $@"{localFold}\{file.Name}";
			/* 建立串流並直接寫入檔案 */
			using (var rsp = GetResponse(file.Uri, WebRequestMethods.Ftp.DownloadFile)) {
				using (var sr = rsp.GetResponseStream()) {
					/* 開啟本地檔案的串流 */
					using (var fs = new FileStream(path, FileMode.Create)) {
						mCnt = BUFFER_LENGTH;	//因為 while 條件，先讓他跑第一次
						while (mCnt >= BUFFER_LENGTH) {
							mCnt = sr.Read(mBuf.Value, 0, BUFFER_LENGTH);
							if (mCnt > 0) {
								fs.Write(mBuf.Value, 0, mCnt);
							}
						}
					}
				}
			}
			/* 取得該檔案的原始時間 */
			using (var rsp = GetResponse(file.Uri, WebRequestMethods.Ftp.GetDateTimestamp)) {
				/* 更改本地檔案的時間 */
				var time = rsp.LastModified;
				var fi = new FileInfo(path) {
					CreationTime = time,
					LastAccessTime = time,
					LastWriteTime = time
				};
			}
		}

		/// <summary>從 FTP 站台中下載所有的檔案及資料夾</summary>
		/// <param name="localFold">欲存放檔案的本地資料夾路徑</param>
		/// <param name="subDir">(null)下載整個 FTP 根目錄  (others)下載子資料夾，如欲下載 "ftp://localhost/Recipe/MotionA" 則此處帶入 @"Recipe/MotionA"</param>
		public void DownloadAll(string localFold, string subDir = null) {
			/* 先取得所有檔案與資料夾清單 */
			var objects = ListObjects(subDir);
			/* 先下載檔案到當前的資料夾 */
			var files = objects.Where(o => o is FtpFile);
			foreach (FtpFile file in files) {
				Download(file, localFold);
			}
			/* 遞迴下載資料夾 */
			var folds = objects.Where(o => o is FtpDirectoy);
			foreach (FtpDirectoy fold in folds) {
				/* 建立本機資料夾 */
				var tarFold = localFold.EndsWith("\\") ? $"{localFold}{fold.Name}" : $@"{localFold}\{fold.Name}";
				if (!Directory.Exists(tarFold)) {
					Directory.CreateDirectory(tarFold);
					/* 取得資料夾的原始時間 */
					using (var rsp = GetResponse(DelSlash(fold.Uri), WebRequestMethods.Ftp.GetDateTimestamp)) {
						/* 更改本地檔案的時間 */
						var time = rsp.LastModified;
						var di = new DirectoryInfo(tarFold) {
							CreationTime = time,
							LastAccessTime = time,
							LastWriteTime = time
						};
					}
				}
				/* 組裝 FTP 上的子資料夾目錄 */
				var tarUri = string.IsNullOrEmpty(subDir) ? fold.Name : $"{AddSlash(subDir)}{fold.Name}";
				/* 遞迴下載 */
				DownloadAll(tarFold, tarUri);
			}
		}
		#endregion

		#region Upload
		/// <summary>上傳檔案至指定的 FTP 站台內</summary>
		/// <param name="fileInfo">欲上傳的本機檔案路徑，如 @"D:\a.log"</param>
		/// <param name="subDir">(null)上傳至 FTP 根目錄  (others)上傳至子資料夾，如欲傳至 "ftp://localhost/Recipe/MotionA" 則此處帶入 @"Recipe/MotionA"</param>
		public void Upload(FileInfo fileInfo, string subDir = null) {
			/* 組裝路徑 */
			var ftpUri = GetUri(subDir, fileInfo.Name);
			/* 建立 FTP 存取器 */
			var ftp = GetRequest(ftpUri);
			/* 建立寫入串流 */
			using (var sr = ftp.GetRequestStream()) {
				/* 開啟本地檔案的串流 */
				using (var fs = fileInfo.OpenRead()) {
					mCnt = BUFFER_LENGTH;   //因為 while 條件，先讓他跑第一次
					while (mCnt >= BUFFER_LENGTH) {
						mCnt = fs.Read(mBuf.Value, 0, BUFFER_LENGTH);
						if (mCnt > 0) {
							sr.Write(mBuf.Value, 0, mCnt);
						}
					}
				}
			}
		}

		/// <summary>將本機的資料夾上傳至 FTP</summary>
		/// <param name="localFold">欲上傳的本機資料夾，如 @"D:\Recipe"</param>
		/// <param name="subDir">(null)上傳至 FTP 根目錄  (others)上傳至子資料夾，如欲傳至 "ftp://localhost/Recipe/MotionA" 則此處帶入 @"Recipe/MotionA"</param>
		public void UploadAll(string localFold, string subDir = null) {
			/* 先取得所有檔案和資料夾 */
			var dirInfo = new DirectoryInfo(localFold);
			var files = dirInfo.GetFiles();
			var dirs = dirInfo.GetDirectories();
			/* 先上傳當前的檔案至根目錄 */
			foreach (var fi in files) {
				Upload(fi, subDir);
			}
			/* 遞迴上傳資料夾 */
			foreach (var di in dirs) {
				/* 先建立資料夾，若已存在不會發生任何事 */
				MakeDirectory(di.Name, subDir);
				/* 遞迴上傳 */
				var tarUri = string.IsNullOrEmpty(subDir) ? di.Name : $"{AddSlash(subDir)}{di.Name}";
				UploadAll(di.FullName, tarUri);
			}
		}
		#endregion

		#region Delete
		/// <summary>刪除 FTP 內的檔案</summary>
		/// <param name="name">欲移除的檔案名稱，如 "520.log"</param>
		/// <param name="subDir">(null)該檔案位於 FTP 根目錄  (others)位於子資料夾，如欲刪除 "ftp://localhost/CASTEC/Logs/520.log" 檔案，則此處帶入 "CASTEC/Logs" (子資料夾)</param>
		public void DeleteFile(string name, string subDir = null) {
			/* 建立路徑 */
			var ftpUri = GetUri(subDir, name);
			/* 開啟回應並直接刪除 */
			using (var rsp = GetResponse(ftpUri, WebRequestMethods.Ftp.DeleteFile)) {
				/* 不需要做啥 */
				rsp.Close();
			}
		}

		/// <summary>刪除指定的檔案</summary>
		/// <param name="ftpFile">欲刪除的檔案資訊</param>
		public void DeleteFile(FtpFile ftpFile) {
			/* 開啟串流並直接移除 */
			using (var rsp = GetResponse(ftpFile.Uri, WebRequestMethods.Ftp.DeleteFile)) {
				/* 不需要做啥 */
				rsp.Close();
			}
		}
		#endregion

		#region Directory
		/// <summary>於指定的位置建立新資料夾</summary>
		/// <param name="name">欲建立的資料夾名稱，如 "Logs"</param>
		/// <param name="subDir">(null)於 FTP 根目錄建立資料夾  (others)建立至子資料夾，如欲建立 "ftp://localhost/CASTEC/Logs" 資料夾，則此處帶入 "CASTEC" (子資料夾)</param>
		public void MakeDirectory(string name, string subDir = null) {
			/* 建立路徑 */
			var ftpUri = GetUri(subDir, name);
			/* 開啟串流 */
			using (var rsp = GetResponse(ftpUri, WebRequestMethods.Ftp.MakeDirectory)) {
				/* 不需要做啥 */
				rsp.Close();
			}
		}

		/// <summary>於指定的位置移除資料夾</summary>
		/// <param name="name">欲移除的資料夾名稱，如 "Logs"</param>
		/// <param name="subDir">(null)該資料夾位於 FTP 根目錄  (others)位於子資料夾內，如欲刪除 "ftp://localhost/CASTEC/Logs" 資料夾，則此處帶入 "CASTEC" (子資料夾)</param>
		public void RemoveDirectory(string name, string subDir = null) {
			/* 建立路徑 */
			var ftpUri = GetUri(subDir, name);
			/* 開啟串流 */
			using (var rsp = GetResponse(ftpUri, WebRequestMethods.Ftp.RemoveDirectory)) {
				/* 不需要做啥 */
				rsp.Close();
			}
		}

		/// <summary>移除指定的資料夾</summary>
		/// <param name="ftpDir">欲移除的資料夾資訊</param>
		public void RemoveDirectory(FtpDirectoy ftpDir) {
			/* 開啟串流並直接移除 */
			using (var rsp = GetResponse(ftpDir.Uri, WebRequestMethods.Ftp.RemoveDirectory)) {
				/* 不需要做啥 */
				rsp.Close();
			}
		}
		#endregion

		#region IDisposable Support
		private bool disposedValue = false; // 偵測多餘的呼叫

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// TODO: 處置受控狀態 (受控物件)。
					if (mBuf.IsValueCreated) {
						Array.Clear(mBuf.Value, 0, BUFFER_LENGTH);
						mBuf = null;
					}
				}

				// TODO: 釋放非受控資源 (非受控物件) 並覆寫下方的完成項。
				// TODO: 將大型欄位設為 null。

				disposedValue = true;
			}
		}

		// TODO: 僅當上方的 Dispose(bool disposing) 具有會釋放非受控資源的程式碼時，才覆寫完成項。
		// ~FtpClient() {
		//   // 請勿變更這個程式碼。請將清除程式碼放入上方的 Dispose(bool disposing) 中。
		//   Dispose(false);
		// }

		// 加入這個程式碼的目的在正確實作可處置的模式。
		public void Dispose() {
			// 請勿變更這個程式碼。請將清除程式碼放入上方的 Dispose(bool disposing) 中。
			Dispose(true);
			// TODO: 如果上方的完成項已被覆寫，即取消下行的註解狀態。
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}
