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

	}
}
