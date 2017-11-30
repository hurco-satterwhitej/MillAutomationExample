﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Security;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using RESTclient;
using WcfDataService;
using WcfDataServices;

namespace WinMaxDataServiceExample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            subscribedSids = new List<SidConstants.SID>
                {
            SidConstants.SID.SID_RT_LAMP_FEED_HOLD,
            SidConstants.SID.SID_RT_TOOL_IN_SPINDLE,
            //SidConstants.SID.SID_WINMAX_RUNNING_LOCAL_FEED,
            SidConstants.SID.SID_RT_SPINDLE_OVERRIDE_POT,
            SidConstants.SID.SID_RT_FEED_OVERRIDE_POT,
            SidConstants.SID.SID_RT_RAPID_OVERRIDE_POT,
           // SidConstants.SID.SID_RT_SPINDLE_SPEED,
            SidConstants.SID.SID_RT_PROGRAM_RUNNING,
            SidConstants.SID.SID_RT_PART_COUNT,
            SidConstants.SID.SID_RT_SERVO_POWER,
            SidConstants.SID.SID_RT_EMERGENCY_STOP,
            SidConstants.SID.SID_WINMAX_MACHINE_MODE_CHANGED,
            //SidConstants.SID.SID_WINMAX_NC_POUND_VARIABLE_NUMBER,
            SidConstants.SID.SID_WINMAX_RUN_PROGRAM_NAME, 
           // SidConstants.SID.SID_WINMAX_RUN_PROGRAM_BLOCK_NUMBER,
            SidConstants.SID.SID_UI_BULK_LAST_NOTIFICATION,
            //SidConstants.SID.SID_UI_BULK_MACHINE_POSITION,
            //SidConstants.SID.SID_WCF_BULK_TOOL_DATA,
            //SidConstants.SID.SID_UI_BULK_CURRENT_PART_SETUP
            SidConstants.SID.SID_CURRENT_PROGRAM_STATUS,
            SidConstants.SID.SID_RT_WAITING_ON_REMOTE_PROGRAM_START
                };
            MachineStatus = new MachineStatus();
            DataContext = this;
        }
        private Boolean AutoScroll = true;

        private void ScrollViewer_ScrollChanged(Object sender, ScrollChangedEventArgs e)
        {
          // User scroll event : set or unset autoscroll mode
          if (e.ExtentHeightChange == 0)
          {   // Content unchanged : user scroll event
            if (ScrollViewer.VerticalOffset == ScrollViewer.ScrollableHeight)
            {   // Scroll bar is in bottom
              // Set autoscroll mode
              AutoScroll = true;
            }
            else
            {   // Scroll bar isn't in bottom
              // Unset autoscroll mode
              AutoScroll = false;
            }
          }

          // Content scroll event : autoscroll eventually
          if (AutoScroll && e.ExtentHeightChange != 0)
          {   // Content changed and autoscroll mode set
            // Autoscroll
            ScrollViewer.ScrollToVerticalOffset(ScrollViewer.ExtentHeight);
          }
        }

        private readonly List<SidConstants.SID> subscribedSids;
        private RestClient Client;
        /// <summary>
        /// The machine status.
        /// </summary>
        public MachineStatus MachineStatus { get; set; }
        private bool ab;
        public String ProgramPath { get; set; }

        /// <summary>
        /// Initializes the notification service client.
        /// </summary>
        private void InitializeClient(string addressText)
        {
            IPAddress ipAddress;
            if (!IPAddress.TryParse(addressText, out ipAddress))
            {
              Messages.Text += "Invalid Machine Address entered.";
                return;
            }

            Client = new RestClient(ipAddress.ToString(), "VendorID", "Password"); //create local service
            Client.SidUpdated += this.OnNotificationReceived;
            ab = false;
           
       

          HeartbeatTimer = new Timer(PingWinmax, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
          try
          {
              Client.Connect();
          }
          catch (Exception e)
          {
            Messages.Text += "Failed to Connect:\n" + e.Message + "\n" + e.StackTrace;
          }
        }
        public Stream GenerateStreamFromString(string s)
        {
          MemoryStream stream = new MemoryStream();
          StreamWriter writer = new StreamWriter(stream);
          writer.Write(s);
          writer.Flush();
          stream.Position = 0;
          return stream;
        }

        private Timer HeartbeatTimer;
        private void PingWinmax(object state)
        {
            try
            {
                if (Client != null)
                {
                    Client.GetSID("SID_RT_SERVO_POWER");
                }
            }
            catch (Exception e)
            {
                HeartbeatTimer.Dispose();
                HeartbeatTimer = null;
            }
        }

      private DateTime LastMessage = DateTime.Now;
      private TimeSpan maxTimeSpan = TimeSpan.FromMilliseconds(0);
      private StringBuilder MessageBuffer = new StringBuilder();
        /// <summary>
        /// Handles the NotificatedReceived event of NotificationServiceCallback.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="eventArgs">Event arguments.</param>
        /// 
        /// 
        private void OnNotificationReceived(object sender, SIDUpdate update)
        {
          MessageBuffer.Clear();
          NotificationReceivedEventArgs eventArgs = new NotificationReceivedEventArgs(update.SID, update.SIDValue); //convert to our type;
          if (eventArgs.Sid == SidConstants.SID.SID_UI_BULK_MACHINE_POSITION)
          {
            var s = new DataContractJsonSerializer(typeof(MachinePosition));
            MachinePosition pos = (MachinePosition)s.ReadObject(GenerateStreamFromString(eventArgs.Value));
            MessageBuffer.AppendFormat("Timestep:{6}ms\nMachine Position:\n\tX:{0}\n\tY:{1}\n\tZ:{2}\n\tA:{3}\n\tB:{4}\n\tC:{5}\n", pos.dMachinePosition[0],
              pos.dMachinePosition[1], pos.dMachinePosition[2], pos.dMachinePosition[4], pos.dMachinePosition[5], pos.dMachinePosition[6],(DateTime.Now - LastMessage).TotalMilliseconds);
            LastMessage = DateTime.Now;
           Debug.WriteLine(MessageBuffer);
          }
          else if (eventArgs.Sid == SidConstants.SID.SID_CURRENT_PROGRAM_STATUS)
          {
              double val = 0;
              double.TryParse(eventArgs.Value, out val);
              if (val > 1 && val <3) //completed success
              {
                  Dispatcher.BeginInvoke(new Action(() => {  LoadProgram(this, null); }));
              }
          }
          else if (eventArgs.Sid == SidConstants.SID.SID_RT_WAITING_ON_REMOTE_PROGRAM_START)
          {
              //double val = 0;
              //double.TryParse(eventArgs.Value, out val);
              //if (val > 3 && val < 5) // auto_prep
              {

                  Dispatcher.BeginInvoke(new Action(() => {  StartCycle(this, null); }));
              }
          }   
          else
          {
              double doubleValue;
              MessageBuffer.AppendFormat("The SID {0} has new value {1}\n", eventArgs.Sid, eventArgs.Value);


          }
          Dispatcher.Invoke(new Action(() => { Messages.Text = MessageBuffer.ToString(); }));
        }


        /// <summary>
        /// Handles the click event of the subscribe button.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SubscribeButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                UnsubscribeSidsIfNeeded();
                InitializeClient(Address.Text);
              Client.BeginSubscribe();
                foreach (SidConstants.SID subscribedSid in subscribedSids)
                {
                    Client.Subscribe(subscribedSid.ToString());
                }
              Client.EndSubscribe();
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR:\n\n" + ex.Message, "Error", MessageBoxButton.OK);
            }
        }

        /// <summary>
        /// Unsubscribes previously subscribed SIDs if the client was previously opened.
        /// </summary>
        private void UnsubscribeSidsIfNeeded()
        {
            if (Client != null && Client.IsConnected == true)
            {
                foreach (uint subscribedSid in subscribedSids)
                {
                    Client.Unsubscribe(subscribedSid.ToString());
                }
            }
        }

        private void StartCycle(object sender, EventArgs e)
        {
            Client.SetSID("SID_RT_START_CYCLE_BUTTON", 1);
        }
        private void LoadProgram(object sender, EventArgs e)
        {
            if (Program.Text.Trim() == "")
            { return; }
            BulkRemoteCommandDataTypeBox rcrdatabox = new BulkRemoteCommandDataTypeBox();
            UnmanagedDataTypes.RemoteCommandInfoType rcrdata = new UnmanagedDataTypes.RemoteCommandInfoType();
            rcrdata.dwCmdId = 42; // LOAD PROGRAM
            rcrdata.sValue = new byte[200*5];
            rcrdata.dValue = new double[10];

            rcrdata.dValue[0] = 0;
            rcrdata.dValue[1] = 1;
            rcrdata.dValue[2] = 0;
            rcrdata.dValue[3] = 0;
            rcrdata.dValue[4] = 0;
            rcrdata.dValue[5] = 0;
            if (ab)
            {
                byte[] pathdata = Encoding.ASCII.GetBytes(Program2.Text);
                Array.Copy(pathdata, 0, rcrdata.sValue, 0, Math.Min(200, pathdata.Length));
            }
            else
            {
                byte[] pathdata = Encoding.ASCII.GetBytes(Program.Text);
                Array.Copy(pathdata, 0, rcrdata.sValue, 0, Math.Min(200, pathdata.Length));
            }
           // Array.Copy(pathdata, 0, rcrdata.sValue, 0, Math.Min(200,pathdata.Length));
            rcrdatabox.BulkStruct = rcrdata;
            BulkWrapper wrap = new BulkWrapper();
            wrap.bulk = rcrdatabox;
            Client.SetSID("SID_WINMAX_BULK_RCRID", wrap);
            ab = !ab;
  
        }
        private void Browse(object sender, EventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".HWM";
            dlg.Filter = "All files (*.*)|*.*";


            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();


            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                string filename = dlg.FileName;
                Program.Text = filename;
            }
        }
       
    }
}
