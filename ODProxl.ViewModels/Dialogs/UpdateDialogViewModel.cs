using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.ViewModels.Dialogs
{
    public class UpdateDialogViewModel : BindableBase, IDialogAware
    {
        #region IDialogAware Implementation
        public DialogCloseListener RequestClose { get; set; }

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
 
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {

        }
        #endregion
    }
}
