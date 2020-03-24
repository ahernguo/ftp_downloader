using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Threading;

namespace Ahern.General {

	/// <summary>代表在加入和移除項目時，或重新整理整份清單時提供通知的動態資料集合。於通知過程中調用對應的 <see cref="Dispatcher"/></summary>
	/// <typeparam name="T">集合中項目的類型</typeparam>
	/// <remarks>修改範本: https://stackoverflow.com/questions/23108045/how-to-make-observablecollection-thread-safe/23108315#23108315 </remarks>
	public class WpfObservableCollection<T> : ObservableCollection<T> {
	
		#region Overrides
		/// <summary>加入、移除、變更或移動項目者或重新整理整個清單</summary>
		public override event NotifyCollectionChangedEventHandler CollectionChanged;

		/// <summary>引發 <see cref="CollectionChanged"/> 事件</summary>
		/// <param name="e"><see cref="CollectionChanged"/> 所引發事件的引數</param>
		protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
			/* 檢視目前是否有人訂閱此事件 */
			var chgEvt = this.CollectionChanged;
			if (chgEvt != null) {
				/* 輪詢每個訂閱者，若訂閱者為 Dispatcher 則進行 Invoke */
				Array.ForEach(
					chgEvt.GetInvocationList(), //取得訂閱清單
					act => {
						if (act is NotifyCollectionChangedEventHandler notifyEvHdl) {
							if (notifyEvHdl.Target is DispatcherObject dispObj) {
								if (dispObj.Dispatcher is Dispatcher disp) {
									if (!disp.CheckAccess()) {  //當前執行緒不能直接操作，需進行 Invoke
										disp.BeginInvoke(
											new Action(() => {
												/* 重新 new 只能使用 Reset (by 跳出來的 Exception) */
												notifyEvHdl.Invoke(
													this,
													new NotifyCollectionChangedEventArgs(
														NotifyCollectionChangedAction.Reset
													)
												);
											}),
											DispatcherPriority.DataBind,
											null
										);
									} else {    //當前執行緒可直接操作，直接執行原本動作即可
										notifyEvHdl.Invoke(this, e);
									}
								}
							}
						}
					}
				);
			}
		}
		#endregion

	}
}
