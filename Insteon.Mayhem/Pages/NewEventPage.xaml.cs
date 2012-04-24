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
using System.ComponentModel;
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
        private InsteonEventConfig config = null;
        private Timer timeout = null;

        public NewEventPage()
        {
            InitializeComponent();
            InsteonService.GetAvailableGroupCompleted += InsteonService_GetAvailableGroupCompleted;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            config = UIHelper.FindParent<InsteonEventConfig>(this);
            InsteonService.BeginGetAvailableGroup();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            addButton.IsEnabled = false;
            busyWidget.Visibility = Visibility.Visible;
            if (helpBubble.IsVisible)
                helpBubble.Visibility = Visibility.Hidden;

            EnterLinkMode();
        }

        private void EnterLinkMode()
        {
            InsteonService.Network.Controller.DeviceLinked += PlmDevice_DeviceLinked;
            if (!InsteonService.Network.Controller.TryEnterLinkMode(InsteonLinkMode.Responder, config.DataItem.Group))
            {
                SetError("Sorry, there was a problem communicating with the INSTEON controller.\r\n\r\nIf this problem persists, please try unplugging your INSTEON controller from the wall and plugging it back in.");
                return;
            }

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
            busyWidget.Visibility = Visibility.Hidden;
//          busyIcon.Visibility = Visibility.Hidden;
        }

        private void OnDeviceLinked(string address)
        {
            timeout.Stop();
            InsteonService.Network.Controller.DeviceLinked -= PlmDevice_DeviceLinked;

            config.DataItem.Device = address;
            config.DataItem.DeviceStatus = InsteonDeviceStatus.On;
            config.CanSave = true;

            PageFrame frame = UIHelper.FindParent<PageFrame>(this);
            if (frame != null)
                frame.SetPage(new ManageEventPage());
        }

        private void PlmDevice_DeviceLinked(object sender, InsteonDeviceEventArgs data)
        {
            this.Dispatcher.BeginInvoke(new Action(() => OnDeviceLinked(data.Device.Address.ToString())), null);
        }

        private void InsteonService_GetAvailableGroupCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.UserState != null)
            {
                config.DataItem.Group = (byte)e.UserState;
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    captionTextBlock.Text = string.Empty;
//                  busyIcon.Visibility = Visibility.Hidden;
                    animation.Visibility = Visibility.Visible;
                    addButton.Visibility = Visibility.Visible;
                }), null);
            }
            else if (e.Error != null)
            {
                this.Dispatcher.BeginInvoke(new Action(() => SetError(e.Error.Message)), null);
            }
        }

        private void timeout_Elapsed(object sender, ElapsedEventArgs e)
        {
            timeout.Stop();
            InsteonService.Network.Controller.DeviceLinked -= PlmDevice_DeviceLinked;

            InsteonService.Network.Controller.TryCancelLinkMode();

            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                helpBubble.Visibility = Visibility.Visible;
                busyWidget.Visibility = Visibility.Hidden;
                addButton.IsEnabled = true;
            }), null);
        }
    }
}
