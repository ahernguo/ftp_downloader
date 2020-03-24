using Ahern.General;
using System.Windows;

namespace Ahern.GUI {

	/// <summary>下載器之互動邏輯</summary>
	public partial class MainWindow : Window {

		#region Constructor
		public MainWindow(IDownloader context) {
			/* 初始化介面 */
			InitializeComponent();
			/* 設定文本 */
			this.DataContext = context;
		}
		#endregion

	}
}
