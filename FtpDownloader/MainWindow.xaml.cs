using Ahern.General;
using System;
using System.Windows;

namespace Ahern.GUI {

	/// <summary>下載器之互動邏輯</summary>
	public partial class MainWindow : Window {

		#region Fields
		/// <summary>紀錄傳入的下載器</summary>
		private IDownloader rDner;
		#endregion

		#region Constructor
		public MainWindow(IDownloader context) {
			/* 初始化介面 */
			InitializeComponent();
			/* 設定文本 */
			rDner = context;
			this.DataContext = rDner;
			/* 註冊事件 */
			rDner.DownloadFinished += Downloader_DownloadFinished;
		}
		#endregion

		#region Event Handles
		private void Downloader_DownloadFinished(object sender, EventArgs e) {
			this.Dispatcher.InvokeShutdown();
		}
		#endregion

		#region Overrides
		protected override void OnContentRendered(EventArgs e) {
			base.OnContentRendered(e);
			/* 開始下載 */
			rDner.StartDownload();
		}
		#endregion
	}
}
