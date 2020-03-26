using System.Linq;
using System.Windows;

namespace Ahern.GUI {

	/// <summary>主介面之進入點</summary>
	public partial class App : Application {
		private void Application_Startup(object sender, StartupEventArgs e) {
			/* 若呼叫啟動參數列表，顯示之 */
			if (e.Args.Contains("/?")) {
				MessageBox.Show(
					Ftp.FtpDownloader.Help(),
					"Help /?",
					MessageBoxButton.OK,
					MessageBoxImage.Information
				);
				/* 關閉視窗 */
				this.Dispatcher.InvokeShutdown();
			} else {
				/* 建立 FTP 下載器並啟動之 */
				var context = new Ftp.FtpDownloader(e.Args);
				var wind = new MainWindow(context);
				wind.Show();
			}
		}
	}
}
