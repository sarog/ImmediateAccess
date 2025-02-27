﻿using ImmediateAccessTray.Properties;
using ImmediateAccess;
using System;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;
using System.ServiceProcess;
using System.Security.Principal;
using System.Linq;
using System.Net.NetworkInformation;
using System.IO;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ImmediateAccessTray
{
    public partial class TrayWindow : Form
    {
        private Process nConsoleProc;
        private ServiceController IAS;
        private bool CurrentlyUpdatingStatuses = false;
        public TrayWindow()
        {
            InitializeComponent();
            Icon = Resources.Icon;
            if (!IsElevated()) pbShieldIcon.Image = new Icon(SystemIcons.Shield, 16, 16).ToBitmap();
        }
        /// <summary>
        /// This method restarts the current process as an administrator.
        /// </summary>
        private void RunAsAdmin()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                string trayArgument = " ForceTray";
                if (Program.TrayIcon == null) trayArgument = "";
                psi.Arguments = "ElevatedStartStopService" + trayArgument;
                psi.FileName = Application.ExecutablePath;
                psi.Verb = "RunAs";
                Process.Start(psi);
                if (Program.TrayIcon == null)
                {
                    Program.TrayWindow.Close();
                }
                else
                {
                    Program.TrayIcon.ExitThread();
                }
            }
            catch (Exception)
            {
                DialogResult result = MessageBox.Show(this, "Cannot start or stop the Immediate Access service, access was denied.", "User Account Control", MessageBoxButtons.RetryCancel, MessageBoxIcon.Information);
                if (result == DialogResult.Retry) RunAsAdmin();
            }
        }
        /// <summary>
        /// This method refreshes all of the status's.
        /// </summary>
        private void RefreshAllStatus()
        {
            Task.Run(() =>
            {
                try
                {
                    if (CurrentlyUpdatingStatuses) return;
                    CurrentlyUpdatingStatuses = true;
                    Invoke(new Action(() =>
                    {
                        lblNetLocation.ForeColor =
                        lblNetStatus.ForeColor =
                        lblVpnStatus.ForeColor =
                        lblServicePolicy.ForeColor =
                        lblServiceStatus.ForeColor =
                        Color.Orange;
                        lblNetLocation.Text =
                        lblNetStatus.Text =
                        lblVpnStatus.Text =
                        lblServicePolicy.Text =
                        lblServiceStatus.Text =
                        "Refreshing...";
                    }));
                    RefreshServiceStatus();
                    if (RefreshPolicyStatus())
                    {
                        Task[] tasks = new Task[3];
                        tasks[0] = RefreshVPNStatus();
                        tasks[1] = RefreshNetworkStatus();
                        tasks[2] = RefreshNetworkLocation();
                        Task.WaitAll(tasks);
                    }
                    else
                    {
                        Invoke(new Action(() =>
                        {
                            lblVpnStatus.Text =
                            lblNetStatus.Text =
                            lblNetLocation.Text =
                            "Unknown";
                        }));
                    }
                    CurrentlyUpdatingStatuses = false;
                }
                catch (Exception)
                {
                    CurrentlyUpdatingStatuses = false;
                }
            });
        }
        /// <summary>
        /// This method refreshes the network location label.
        /// </summary>
        private async Task RefreshNetworkLocation()
        {
            bool result = await TestNetwork.IsProbeAvailable();
            Invoke(new Action(() =>
            {
                lblNetLocation.ForeColor = Color.Black;
                if (result)
                {
                    lblNetLocation.Text = "Internal";
                }
                else
                {
                    lblNetLocation.Text = "External";
                }
            }));
        }
        /// <summary>
        /// This method refreshes the network status label.
        /// </summary>
        private async Task RefreshNetworkStatus()
        {
            bool result = await TestNetwork.IsProbeAvailable(true);
            Invoke(new Action(() =>
            {
                if (result)
                {
                    lblNetStatus.ForeColor = Color.DarkGreen;
                    lblNetStatus.Text = "Connected";
                }
                else
                {
                    lblNetStatus.ForeColor = Color.DarkRed;
                    lblNetStatus.Text = "Not Connected";
                }
            }));
        }
        /// <summary>
        /// This method refreshes the VPN status label.
        /// </summary>
        private async Task RefreshVPNStatus()
        {
            string profile = await VpnControl.IsConnected();
            Invoke(new Action(() =>
            {
                lblVpnStatus.ForeColor = Color.Black;
                if (profile == null)
                {
                    lblVpnStatus.Text = "Not Connected";
                }
                else
                {
                    lblVpnStatus.Text = "Connected";
                }
            }));
        }
        /// <summary>
        /// This method triggers a refresh of all the status's on the main tab.
        /// </summary>
        /// <returns>bool: Returns true if the service is enabled.</returns>
        private bool RefreshPolicyStatus()
        {
            PolicyReader.ReadPolicies();
            bool result = PolicyReader.IsServiceEnabled();
            Invoke(new Action(() => {
                if (result)
                {
                    lblServicePolicy.ForeColor = Color.DarkGreen;
                    lblServicePolicy.Text = "Active";
                }
                else
                {
                    lblServicePolicy.ForeColor = Color.Red;
                    lblServicePolicy.Text = "De-Activated";
                }
            }));
            return result;
        }
        /// <summary>
        /// This action will refresh all of the status's on the main tab.
        /// </summary>
        private Action DelegateRefreshServiceStatus = new Action(() =>
        {
            Label lblServiceStatus = Program.TrayWindow.lblServiceStatus;
            try
            {
                ServiceController IAS = Program.TrayWindow.IAS;
                IAS.Refresh();
                if (IAS.Status == ServiceControllerStatus.Running)
                {
                    lblServiceStatus.ForeColor = Color.DarkGreen;
                    lblServiceStatus.Text = "Running";
                }
                if (IAS.Status == ServiceControllerStatus.Stopped)
                {
                    lblServiceStatus.ForeColor = Color.Red;
                    lblServiceStatus.Text = "Stopped";
                }
                if (IAS.Status == ServiceControllerStatus.StartPending)
                {
                    lblServiceStatus.ForeColor = Color.DarkOrange;
                    lblServiceStatus.Text = "Starting...";
                }
                if (IAS.Status == ServiceControllerStatus.StopPending)
                {
                    lblServiceStatus.ForeColor = Color.DarkOrange;
                    lblServiceStatus.Text = "Stopping...";
                }
            }
            catch (Exception)
            {
                lblServiceStatus.ForeColor = Color.Red;
                lblServiceStatus.Text = "Not Installed";
            }
        });
        /// <summary>
        /// This method triggers a refresh of the service status.
        /// </summary>
        private void RefreshServiceStatus()
        {
            if (InvokeRequired)
            {
                Invoke(DelegateRefreshServiceStatus);
            }
            else
            {
                DelegateRefreshServiceStatus.Invoke();
            }
        }
        /// <summary>
        /// This method launches the nConsole and makes it a child of the current window.
        /// </summary>
        private void SetupNConsole()
        {
            tpLogs.Hide();
            Process proc = Process.GetCurrentProcess();
            string name = proc.MainModule.ModuleName;
            string path = proc.MainModule.FileName.Replace(name, "");
            string nConPath = Path.Combine(path, "ImmediateAccessNConsole.exe");
            nConsoleProc = new Process();
            nConsoleProc.StartInfo = new ProcessStartInfo()
            {
                FileName = nConPath,
                WindowStyle = ProcessWindowStyle.Minimized,
                Arguments = "WatchPID " + Process.GetCurrentProcess().Id
            };
            nConsoleProc.Start();
            while (nConsoleProc.MainWindowHandle.ToInt32() == 0x0) Thread.Sleep(10);
            PInvoke.SetParent(new HWND(nConsoleProc.MainWindowHandle), (HWND)tpLogs.Handle);
            PInvoke.SetWindowLong(new HWND(nConsoleProc.MainWindowHandle), WINDOW_LONG_PTR_INDEX.GWL_STYLE,  (int)WINDOW_STYLE.WS_VISIBLE);
            PInvoke.SetWindowPos(new HWND(nConsoleProc.MainWindowHandle), new HWND(IntPtr.Zero), 0, 0, tpLogs.Width, tpLogs.Height, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
            tpLogs.Show();
        }
        /// <summary>
        /// This method checks if the current user has administrator rights.
        /// </summary>
        /// <returns>bool: True if process has administrator rights.</returns>
        private bool IsElevated()
        {
            return WindowsIdentity.GetCurrent().Groups.Contains(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        }
        /// <summary>
        /// This event fires whenever the tab has changed.
        /// </summary>
        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab == tpLogs)
            {
                SetupNConsole();
            }
            else
            {
                if (nConsoleProc != null && !nConsoleProc.HasExited) nConsoleProc.Kill();
            }
        }
        /// <summary>
        /// This event fires whenever the website link is clicked.
        /// </summary>
        private void lblWebsite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(lblWebsite.Text);
        }
        /// <summary>
        /// This event fires whenever the toggle service button is pressed.
        /// </summary>
        private async void btnToggleService_Click(object sender, EventArgs e)
        {
            if (!IsElevated())
            {
                RunAsAdmin();
                return;
            }
            btnToggleService.Enabled = false;
            await Task.Run(() => {
                RefreshServiceStatus();
                if (IAS.Status == ServiceControllerStatus.Running)
                {
                    IAS.Stop();
                    RefreshServiceStatus();
                    IAS.WaitForStatus(ServiceControllerStatus.Stopped);
                }
                else
                {
                    IAS.Start();
                    RefreshServiceStatus();
                    IAS.WaitForStatus(ServiceControllerStatus.Running);
                }
                RefreshServiceStatus();
            });
            btnToggleService.Enabled = true;
        }
        /// <summary>
        /// This event fires whenever the form loads.
        /// </summary>
        private void Tray_Load(object sender, EventArgs e)
        {
            IAS = new ServiceController("ImmediateAccess");
            Assembly selfAssem = Assembly.GetExecutingAssembly();
            lblVersion.Text = selfAssem.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            lblAuthor.Text = selfAssem.GetCustomAttribute<AssemblyCompanyAttribute>().Company;
            lblWebsite.Text = selfAssem.GetCustomAttribute<AssemblyMetadataAttribute>().Value;
            tbDescription.Text = selfAssem.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            RefreshAllStatus();
            if (Program.Arguments.Contains("ElevatedStartStopService")) btnToggleService_Click(null, null);
        }
        /// <summary>
        /// This event fires whenever the network address has changed on the PC.
        /// </summary>
        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            RefreshAllStatus();
        }
        /// <summary>
        /// This event fires whenever the the refresh button is pushed.
        /// </summary>
        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshAllStatus();
        }
        /// <summary>
        /// This event fires whenever the help cursor is clicked on pbLogo, this in turn closes the entire program.
        /// </summary>
        private void pbLogo_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            Application.Exit();
        }
        /// <summary>
        /// This event fires when the tpLogs tab re-sizes and in turn resizes the nConsole.
        /// </summary>
        private void tpLogs_Resize(object sender, EventArgs e)
        {
            if (nConsoleProc != null && !nConsoleProc.HasExited)
            {
                PInvoke.SetWindowPos(new HWND(nConsoleProc.MainWindowHandle), new HWND(IntPtr.Zero), 0, 0, tpLogs.Width, tpLogs.Height,
                    SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
                    SET_WINDOW_POS_FLAGS.SWP_NOMOVE
                );
            }
        }
        /// <summary>
        /// This event fires when the main form is closing and kills the child nConsole if it exists.
        /// </summary>
        private void TrayWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (nConsoleProc != null && !nConsoleProc.HasExited) nConsoleProc.Kill();
        }
    }
}