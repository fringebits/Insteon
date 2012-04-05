using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Insteon.Mayhem.Widgets
{
    public partial class SwitchLincAnimation : UserControl
    {
        public SwitchLincAnimation()
        {
            InitializeComponent();
        }

        public string Step1
        {
            get { return step1TextBlock.Text; }
            set { step1TextBlock.Text = value; }
        }

        public string Step2
        {
            get { return step2TextBlock.Text; }
            set { step2TextBlock.Text = value; }
        }
    }
}
