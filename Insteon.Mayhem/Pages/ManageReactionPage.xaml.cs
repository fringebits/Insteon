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
    public partial class ManageReactionPage : UserControl
    {
        public ManageReactionPage()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            InsteonReactionConfig config = UIHelper.FindParent<InsteonReactionConfig>(this);
            if (config != null)
                deviceTextBlock.Text = config.DataItem.Device;

            deviceStateComboBox.SelectedIndex = (int)config.DataItem.DeviceStatus;
        }

        private void deviceStateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InsteonReactionConfig config = UIHelper.FindParent<InsteonReactionConfig>(this);
            if (config != null)
                config.DataItem.DeviceStatus = (InsteonDeviceStatus)deviceStateComboBox.SelectedIndex;
        }
    }
}
