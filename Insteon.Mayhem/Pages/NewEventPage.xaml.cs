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
using System.IO;
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
            Window window = Window.GetWindow(this);
            window.Cursor = Cursors.AppStarting;

            if (helpBubble.IsVisible)
            {
                helpBubble.Visibility = Visibility.Hidden;
                UIHelper.RefreshElement(helpBubble);
            }

            byte group = 0;
            string message = null;
            if (!InsteonService.TryGetAvailableGroup(out group, out message))
            {
                SetError(message);
                window.Cursor = Cursors.Arrow;
                return;
            }
            else
            {
                PageFrame frame = UIHelper.FindParent<PageFrame>(this);
                if (frame != null)
                    frame.UpdateStatus();
            }

            InsteonEventConfig config = UIHelper.FindParent<InsteonEventConfig>(this);
            config.DataItem.Group = group;

            if (!InsteonService.Network.Controller.TryEnterLinkMode(InsteonLinkMode.Responder, config.DataItem.Group))
            {
                SetError("Sorry, there was a problem communicating with the INSTEON controller.\r\n\r\nIf this problem persists, please try unplugging your INSTEON controller from the wall and plugging it back in.");
                window.Cursor = Cursors.Arrow;
                return;
            }
            window.Cursor = Cursors.Arrow;

            InsteonService.Network.Controller.DeviceLinked += PlmDevice_DeviceLinked;
            
            timeout = new Timer(1000);
            timeout.Elapsed += timeout_Elapsed;
            timeout.AutoReset = false;
            timeout.Start();
        }

        private void SetError(string message)
        {
            captionTextBlock.Text = message;
            captionTextBlock.Visibility = Visibility.Visible;
            animation.Visibility = Visibility.Hidden;
            addButton.Visibility = Visibility.Hidden;
            helpBubble.Visibility = Visibility.Hidden;
            UIHelper.RefreshElement(helpBubble);
        }

        private void timeout_Elapsed(object sender, ElapsedEventArgs e)
        {
            timeout.Stop();
            InsteonService.Network.Controller.DeviceLinked -= PlmDevice_DeviceLinked;

            InsteonService.Network.Controller.TryCancelLinkMode();
            this.Dispatcher.BeginInvoke(new Action(() => helpBubble.Visibility = Visibility.Visible), null);
        }

        private void OnDeviceLinked(string address)
        {
            timeout.Stop();
            InsteonService.Network.Controller.DeviceLinked -= PlmDevice_DeviceLinked;

            InsteonEventConfig config = UIHelper.FindParent<InsteonEventConfig>(this);
            config.DataItem.Device = address;
            config.DataItem.DeviceStatus = InsteonDeviceStatus.On;
            config.CanSave = true;

            PageFrame frame = UIHelper.FindParent<PageFrame>(this);
            if (frame != null)
                frame.SetPage(new ManageEventPage());
        }

        private void PlmDevice_DeviceLinked(object sender, InsteonDeviceEventArgs data)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.OnDeviceLinked(data.Device.Address.ToString())), null);
        }
    }
}
