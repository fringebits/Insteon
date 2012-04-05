// <copyright company="INSTEON">
// Copyright (c) 2012 All Right Reserved, http://www.insteon.net
//
// This source is subject to the Common Development and Distribution License (CDDL). 
// Please see the LICENSE.txt file for more information.
// All other rights reserved.
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY 
// KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// </copyright>
// <author>Dave Templin</author>
// <email>info@insteon.net</email>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Insteon.Network;

namespace Insteon.Mayhem
{
    public partial class NewEventPage : UserControl
    {
        private Timer timeout = null;

        public NewEventPage()
        {
            InitializeComponent();            
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            InsteonEventConfig config = UIHelper.FindParent<InsteonEventConfig>(this);
            try
            {
                config.DataItem.Group = InsteonService.GetAvailableGroup();
            }
            catch (OutOfMemoryException)
            {
                captionTextBlock.Text = "Sorry, no more devices can be added.\r\n\r\nIf there is another event or reaction that may no longer be needed, remove it and try again.";
                captionTextBlock.Visibility = Visibility.Visible;
                animation.Visibility = Visibility.Hidden;
                addButton.Visibility = Visibility.Hidden;
                return;
            }

            if (!InsteonService.Network.Controller.TryEnterLinkMode(InsteonLinkMode.Responder, config.DataItem.Group))
            {
                captionTextBlock.Text = "Sorry, there was a problem communicating with the INSTEON controller.\r\n\r\nIf this problem persists, please try unplugging your INSTEON controller and plugging it back in.";
                captionTextBlock.Visibility = Visibility.Visible;
                animation.Visibility = Visibility.Hidden;
                addButton.Visibility = Visibility.Hidden;
                return;
            }
            InsteonService.Network.Controller.DeviceLinked += PlmDevice_DeviceLinked;
            
            timeout = new Timer(1000);
            timeout.Elapsed += timeout_Elapsed;
            timeout.AutoReset = false;
            timeout.Start();
        }

        void timeout_Elapsed(object sender, ElapsedEventArgs e)
        {
            timeout.Stop();
            InsteonService.Network.Controller.DeviceLinked -= PlmDevice_DeviceLinked;

            InsteonService.Network.Controller.TryCancelLinkMode();
            this.Dispatcher.BeginInvoke(new Action(() => helpTextBlock.Visibility = Visibility.Visible), null);
        }

        private void OnDeviceLinked(string address)
        {
            timeout.Stop();
            InsteonService.Network.Controller.DeviceLinked -= PlmDevice_DeviceLinked;

            InsteonEventConfig config = UIHelper.FindParent<InsteonEventConfig>(this);
            config.DataItem.Device = address;
            config.DataItem.DeviceStatus = InsteonDeviceStatus.On;
            config.CanSave = true;

            Panel parent = this.VisualParent as Panel;
            parent.Children.Remove(this);
            UserControl page = new ManageEventPage();
            parent.Children.Add(page);
            parent.Height = page.Height;
        }

        private void PlmDevice_DeviceLinked(object sender, InsteonDeviceEventArgs data)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.OnDeviceLinked(data.Device.Address.ToString())), null);
        }
    }
}
