using System.Windows;

namespace Ahern.GUI {

	/// <summary>主介面之進入點</summary>
	public partial class App : Application {
		private void Application_Startup(object sender, StartupEventArgs e) {
			var context = new Ftp.FtpDownloader(e.Args);
			var wind = new MainWindow(context);
			wind.Show();
		}
	}
}
