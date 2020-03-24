using System.ComponentModel;

namespace Ahern.General {

	/// <summary>描述遠端物件</summary>
	public interface IRemoteObject : INotifyPropertyChanged {

		/// <summary>取得物件名稱</summary>
		string Name { get; }
		/// <summary>取得此物件是否已被下載</summary>
		bool IsFinished { get; }

	}
}
