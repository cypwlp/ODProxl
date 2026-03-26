using ODProxl.EntityModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.ViewModels.Pages
{
    public class OnnxModelMSPageViewModel : BindableBase, INavigationAware
    {

        #region INavigationAware Implementation
        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
          return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {

        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
       
        }
        #endregion

        #region 字段
        private readonly string _defaultBaseUrl= "http://129.204.149.106:8080/OnnxModels";
        #endregion

        #region 屬性
        public DelegateCommand<FileSystemItem>? ShowDetailsCommand { get; }
        public DelegateCommand? SearchCommand { get; }
        #endregion
    }
}
