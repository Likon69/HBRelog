/*
Copyright 2012 HighVoltz

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using HighVoltz.HBRelog;

namespace HighVoltz.HBRelog.Honorbuddy
{
    public class HonorbuddyManager : IBotManager
    {
        object _lockObject = new object();
        public bool IsRunning { get; private set; }
        public bool StartupSequenceIsComplete { get; private set; }
        public event EventHandler<ProfileEventArgs> OnStartupSequenceIsComplete;
        CharacterProfile _profile;
        public CharacterProfile Profile
        {
            get { return _profile; }
            private set { _profile = value; Settings = value.Settings.HonorbuddySettings; }
        }
        public HonorbuddySettings Settings { get; private set; }
        public Process BotProcess { get; private set; }

        public HonorbuddyManager(CharacterProfile profile)
        {
            Profile = profile;
        }

        public void SetSettings(HonorbuddySettings settings)
        {
            Settings = settings;
        }

        Timer _hbCloseTimer;
        int _windowCloseAttempt = 0;
        bool _isExiting;
        public void CloseBotProcess()
        {
            if (!_isExiting && BotProcess != null && !BotProcess.HasExited)
            {
                _isExiting = true;
                Profile.Log("Attempting to close Honorbuddy");
                BotProcess.CloseMainWindow();
                _windowCloseAttempt++;
                _hbCloseTimer = new Timer(state =>
                {
                    if (!((Process)state).HasExited)
                    {
                        if (_windowCloseAttempt++ < 15)
                            ((Process)state).CloseMainWindow();
                        else if (_windowCloseAttempt >= 15)
                        {
                            Profile.Log("Killing Honorbuddy");
                            ((Process)state).Kill();
                        }
                    }
                    else
                    {
                        _isExiting = false;
                        Profile.Log("Successfully closed Honorbuddy");
                        BotProcess = null;
                        _windowCloseAttempt = 0;
                        _hbCloseTimer.Dispose();
                    }
                }, BotProcess, 1000, 1000);
            }
        }

        bool _pluginIsUptodate = false;
        public void Start()
        {
            if (File.Exists(Settings.HonorbuddyPath))
            {
                // remove internet zone restrictions from Honorbuddy.exe if it exists
                Utility.UnblockFileIfZoneRestricted(Settings.HonorbuddyPath);
                // check if we need to copy over plugin.
                if (!_pluginIsUptodate)
                {
                    using (var reader = new StreamReader(
                        Assembly.GetExecutingAssembly().
                        GetManifestResourceStream("HighVoltz.HBRelog.HBPlugin.HBRelogHelper.cs")))
                    {
                        string pluginString = reader.ReadToEnd();
                        // copy the HBPlugin over to the Honorbuddy plugin folder if it doesn't exist.
                        // or length doesn't match with the version in resource.
                        string pluginPath = Path.Combine(Path.GetDirectoryName(Settings.HonorbuddyPath),
                                            "Plugins\\HBRelogHelper.cs");
                        FileInfo fi = new FileInfo(pluginPath);
                        if (!fi.Exists || fi.Length != pluginString.Length)
                        {
                            File.WriteAllText(pluginPath, pluginString);
                        }
                    }
                    _pluginIsUptodate = true;
                }
                IsRunning = true;
                StartHonorbuddy();
            }
            else
                throw new InvalidOperationException(string.Format("path to honorbuddy.exe does not exist: {0}", Settings.HonorbuddyPath));
        }

        bool _waitingToStart = false;

        void StartHonorbuddy()
        {
            _waitingToStart = true;
        }

        DateTime _hbStartupTimeStamp;
        public void Pulse()
        {
            lock (_lockObject)
            {
                if (IsRunning)
                {
                    var gameProc = Profile.TaskManager.WowManager.GameProcess;
                    if (gameProc == null || gameProc.HasExited)
                    {
                        Stop();
                        return;
                    }
                    if (_isExiting)
                        return;
                    if (_waitingToStart)
                    {  // we need to delay starting honorbuddy for a few seconds if another instance from same path was started a few seconds ago
                        if (HBStartupManager.CanStart(Settings.HonorbuddyPath))
                        {
                            Profile.Log("starting {0}", Profile.Settings.HonorbuddySettings.HonorbuddyPath);
                            Profile.Status = "Starting Honorbuddy";
                            StartupSequenceIsComplete = false;
                            // Build the bot's command-line arguments.
                            // The original Honorbuddy bot parsed /loadprofile, /botname, and
                            // /autostart. CopilotBuddy (the WPF/.NET 10 port) only parses
                            // /pid (in MainWindow.xaml.cs) and /customclass (in RoutineManager.cs).
                            // Passing /loadprofile and /botname to CopilotBuddy is a no-op — the
                            // botbase and profile are persisted via the bot's own UI
                            // (cmbBotSelector.SelectedIndex → CharacterSettings.Instance.SelectedBotIndex,
                            // profile via Honorbuddy profile loader). So we only forward the args
                            // that CopilotBuddy actually understands.
                            string hbArgs = string.Format("/pid={0} /autostart {1}{2}{3}",
                                Profile.TaskManager.WowManager.GameProcess.Id,
                                !string.IsNullOrEmpty(Settings.CustomClass) ? string.Format("/customclass=\"{0}\" ", Settings.CustomClass) : string.Empty,
                                !string.IsNullOrEmpty(Settings.HonorbuddyProfile) ? string.Format("/loadprofile=\"{0}\" ", Settings.HonorbuddyProfile) : string.Empty,
                                !string.IsNullOrEmpty(Settings.BotBase) ? string.Format("/botname=\"{0}\" ", Settings.BotBase) : string.Empty);
                            var hbWorkingDirectory = Path.GetDirectoryName(Settings.HonorbuddyPath);
                            var procStartI = new ProcessStartInfo(Settings.HonorbuddyPath, hbArgs)
                            {
                                WorkingDirectory = hbWorkingDirectory
                            };
                            BotProcess = Process.Start(procStartI);
                            _hbStartupTimeStamp = DateTime.Now;
                            _waitingToStart = false;
                        }
                        else
                            return;
                    }
                    // restart wow hb if it has exited
                    if (BotProcess == null || BotProcess.HasExited)
                    {
                        Profile.Log("Honorbuddy process was terminated. Restarting");
                        Profile.Status = "Honorbuddy has exited.";
                        BotProcess = null;
                        StartupSequenceIsComplete = false;
                        IsRunning = false;
                        return;
                    }
                    // return if hb isn't ready for input.
                    if (!BotProcess.WaitForInputIdle(0))
                        return;

                    // check if it's taking Honorbuddy too long to connect.
                    if (!StartupSequenceIsComplete && DateTime.Now - _hbStartupTimeStamp > TimeSpan.FromMinutes(1))
                    {
                        Profile.Log("Closing Honorbuddy because it took too long to attach");
                        Stop();
                    }
                    if (!HBIsResponding || HBHasCrashed)
                    {
                        if (!HBIsResponding) // we need to kill the process if it's not responding. 
                        {
                            Profile.Log("Honorbuddy is not responding.. So lets restart it");
                            Profile.Status = "Honorbuddy isn't responding. restarting";
                        }
                        else// otherwise nicely close the window instead so it can logout serverside.
                        {
                            Profile.Log("Honorbuddy has crashed.. So lets restart it");
                            Profile.Status = "Honorbuddy has crashed. restarting";
                        }
                        Stop();
                    }
                }
            }
        }

        public void Stop()
        {
            // try to aquire lock, if fail then kill process anyways.
            bool lockAquried = Monitor.TryEnter(_lockObject, 500);
            if (IsRunning)
            {
                if (BotProcess != null && !BotProcess.HasExited)
                    CloseBotProcess();
                IsRunning = false;
                StartupSequenceIsComplete = false;
            }
            // Cancel any pending startup-complete timer so a delayed "ready" signal
            // from a previous bot doesn't fire after we've moved on.
            if (_startupCompleteTimer != null)
            {
                _startupCompleteTimer.Dispose();
                _startupCompleteTimer = null;
            }
            if (lockAquried) // release lock if it was aquired
                Monitor.Exit(_lockObject);
        }

        public void SetStartupSequenceToComplete()
        {
            // The bot's WCF Init is called from HBRelogHelper.cs as soon as that
            // first plugin is compiled and its constructor runs. For WPF-based bots
            // (e.g. CopilotBuddy) this is too early — other plugins are still being
            // Roslyn-compiled by the DynamicLoader, and the bot has not yet logged
            // into the world. Fire the "startup complete" event after a grace period
            // so HBRelog does not log "WoW and HB startup sequence complete" while
            // the bot is still in the middle of compiling plugins and getting in-game.
            // Use a one-shot timer; subsequent calls (e.g. from a restart) are no-ops.
            if (_startupCompleteTimer != null)
                return;
            _startupCompleteTimer = new System.Threading.Timer(_ =>
            {
                StartupSequenceIsComplete = true;
                if (OnStartupSequenceIsComplete != null)
                    OnStartupSequenceIsComplete(this, new ProfileEventArgs(Profile));
            }, null, TimeSpan.FromSeconds(30), System.Threading.Timeout.InfiniteTimeSpan);
        }

        System.Threading.Timer _startupCompleteTimer;

        Stopwatch _hbRespondingSW = new Stopwatch();
        /// <summary>
        /// returns false if the WoW user interface is not responsive for 10+ seconds.
        /// </summary>
        public bool HBIsResponding
        {
            get
            {
                if (BotProcess != null && !BotProcess.HasExited && !BotProcess.Responding && StartupSequenceIsComplete)
                {
                    if (!_hbRespondingSW.IsRunning)
                        _hbRespondingSW.Start();
                    if (_hbRespondingSW.ElapsedMilliseconds >= 20000)
                        return false;
                }
                else if (_hbRespondingSW.IsRunning)
                    _hbRespondingSW.Start();
                return true;
            }
        }

        DateTime _crashTimeStamp = DateTime.Now;
        public bool HBHasCrashed
        {
            get
            {
                // check for crash every 10 seconds
                if (DateTime.Now - _crashTimeStamp >= TimeSpan.FromSeconds(10))
                {
                    _crashTimeStamp = DateTime.Now;
                    // For WPF-based bots (e.g. CopilotBuddy), the original "count windows
                    // matching the bot name" heuristic is unreliable because WPF creates
                    // multiple top-level windows (main + dispatcher + helper windows from
                    // MahApps.Metro / HwndSource hooks), which causes false-positive crash
                    // detection a few seconds after startup.
                    // Crash signal is now derived from `MainWindowHandle` being null/invalid
                    // AND a startup grace period (so we don't fire while the WPF window is
                    // still being constructed).
                    if (BotProcess == null || BotProcess.HasExited)
                        return true;
                    // Give the bot 30 seconds after launch to show its main window.
                    var uptime = DateTime.Now - _hbStartupTimeStamp;
                    if (uptime < TimeSpan.FromSeconds(30))
                        return false;
                    return BotProcess.MainWindowHandle == IntPtr.Zero;
                }
                return false;
            }
        }

        static public class HBStartupManager
        {
            static object _lockObject = new object();
            static Dictionary<string, DateTime> TimeStamps = new Dictionary<string, DateTime>();
            static public bool CanStart(string path)
            {
                string key = path.ToUpper();
                lock (_lockObject)
                {
                    if (TimeStamps.ContainsKey(key) &&
                        DateTime.Now - TimeStamps[key] < TimeSpan.FromSeconds(HBRelogManager.Settings.HBDelay))
                    {
                        return false;
                    }
                    TimeStamps[key] = DateTime.Now;
                }
                return true;
            }
        }
    }
}
