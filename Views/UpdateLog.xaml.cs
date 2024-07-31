using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CallRecording.Views
{
    /// <summary>
    /// UpdateLog.xaml 的交互逻辑
    /// </summary>
    public partial class UpdateLog : Window
    {
        public UpdateLog()
        {
            InitializeComponent();
            InitializeAsync();
        }

        public async void InitializeAsync()
        {
            await webView.EnsureCoreWebView2Async(null);
            webView.CoreWebView2.Navigate($"http://my.52shell.ltd/#/Soft/getUpdateLog?skey=2706a699-8246-4ffc-afb9-1d904e1dbe4f");

        }
    }

}
