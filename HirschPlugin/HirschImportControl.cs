using System.Windows.Forms;

namespace Timex.HirschPlugin
{
    public partial class HirschImportControl : UserControl
    {
        public HirschImportControl()
        {
            InitializeComponent();
        }

        public string HirschConnectionString
        {
            get { return textBox1.Text; }
            set { textBox1.Text = value; }
        }

        public string TimexLogin
        {
            get { return textBox2.Text; }
            set { textBox2.Text = value; }
        }

        public string TimexPassword
        {
            get { return textBox3.Text; }
            set { textBox3.Text = value; }
        }

        public string SDKAddress
        {
            get { return textBox4.Text; }
            set { textBox4.Text = value; }
        }
    }
}