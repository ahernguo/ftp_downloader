using System;
using System.ComponentModel;

namespace Ahern.General {

	/// <summary>描述下載器之功能</summary>
	public interface IDownloader : INotifyPropertyChanged {

		#region Window Title
		/// <summary>取得欲顯示於視窗的標題</summary>
		string Caption { get; }
		#endregion

		#region Progress
		/// <summary>取得當前的進度百分比</summary>
		double Progress { get; }
		/// <summary>取得當前已下載的大小</summary>
		string CurrentSize { get; }
		/// <summary>取得所需下載的大小</summary>
		string MaximumSize { get; }
		/// <summary>取得當前的下載狀態資訊</summary>
		string Info { get; }
		#endregion

		#region File List
		/// <summary>取得欲下載的檔案集合</summary>
		WpfObservableCollection<IRemoteObject> Files { get; }
		#endregion

		#region Events
		/// <summary>所有下載已完成之事件</summary>
		event EventHandler DownloadFinished;
		#endregion

		#region Actions
		/// <summary>開始進行下載之動作</summary>
		void StartDownload();
		#endregion
	}

	/// <summary>下載器之基底類別</summary>
	internal abstract class DownloaderBase : IDownloader {

		#region Fields
		/// <summary>暫存進度百分比</summary>
		private double mProg;
		/// <summary>暫存當前已下載的大小</summary>
		private string mCurSz;
		/// <summary>暫存所需下載的大小</summary>
		private string mMaxSz;
		/// <summary>暫存下載訊息</summary>
		private string mInfo;
		#endregion

		#region INotifyPropertyChanged Implements
		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void RaisePropChg(string name) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
		#endregion

		#region IDownloader Implements

		#region Properties

		#region Window Title
		/// <summary>取得欲顯示於視窗的標題</summary>
		public string Caption { get; protected set; }
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
		/// <summary>取得下載資訊</summary>
		public string Info {
			get => mInfo;
			set {
				mInfo = value;
				RaisePropChg("Info");
			}
		}
		#endregion

		#region File List
		/// <summary>取得欲下載的檔案集合</summary>
		public WpfObservableCollection<IRemoteObject> Files { get; }
		#endregion

		#endregion

		#region Events
		/// <summary>所有下載已完成之事件</summary>
		public event EventHandler DownloadFinished;

		protected virtual void RaiseDone() {
			DownloadFinished?.Invoke(this, null);
		}
		#endregion

		#region Methods
		/// <summary>開始進行下載之動作</summary>
		public abstract void StartDownload();
		#endregion

		#endregion

		#region Constructor
		public DownloaderBase() {
			/* 初始化數值 */
			mProg = 0.0;
			mCurSz = "0 KB";
			mMaxSz = "0 KB";
			/* 初始化物件 */
			Files = new WpfObservableCollection<IRemoteObject>();
		}
		#endregion
	}
}
