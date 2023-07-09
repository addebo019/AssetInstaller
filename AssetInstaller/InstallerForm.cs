using AssetInstaller.Utils;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AssetInstaller
{
    public partial class InstallerForm : Form
    {
        private readonly TrainzUtil trainzUtil;

        private bool promptOnClosing = false;
        private bool isCancelled = false;

        public InstallerForm(TrainzUtil trainzUtil)
        {
            this.trainzUtil = trainzUtil;
            InitializeComponent();
        }

        private void InstallerForm_Shown(object sender, EventArgs e)
        {
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Indeterminate);

            // Run the installer in a seperate thread
            Task.Run(async () =>
            {
                try
                {
                    await RunInstaller();
                }
                catch (Exception ex)
                {
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Error);
                    ShowMessageBoxAndClose(ex.Message, "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        }

        private void InstallerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (promptOnClosing)
            {
                e.Cancel = true;
                DialogResult result = MessageBox.Show(this, "Sind Sie sicher, dass Sie die Installation abbrechen möchten?", "Installation abbrechen?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                // Tell the installer to cancel the installation
                if (result == DialogResult.Yes)
                {
                    isCancelled = true;
                    label.Text = "Installation wird abgebrochen, bitte warten...";
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Paused);
                }
            }
        }

        private async Task RunInstaller()
        {
            long lastTimestamp = 0;

            // Get last install timestamp
            if (File.Exists(".lastinstall"))
            {
                using (TextReader reader = File.OpenText(".lastinstall"))
                {
                    lastTimestamp = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(reader.ReadLine())).DateTime.Ticks;
                }
            }

            List<string> updatedScripts = new List<string>();
            List<TrainzAsset> updatedAssets = new List<TrainzAsset>();

            // Get updated scripts
            if (Directory.Exists("scripts"))
            {
                updatedScripts = GetUpdatedScripts(lastTimestamp);
                updatedScripts.Sort();
            }

            // Get updated assets
            if (Directory.Exists(Path.Combine("UserData", "editing")))
            {
                updatedAssets = GetUpdatedAssets(lastTimestamp);
                updatedAssets.Sort(new CompareByUsername());
            }

            int count = 0;
            int totalCount = updatedScripts.Count > 0 ? updatedAssets.Count + 1 : updatedAssets.Count;

            // Check if we have any new scripts or assets to update
            if (totalCount == 0)
            {
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
                ShowMessageBoxAndClose("Es gibt nichts Neues zu installieren.", "Fertig!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            promptOnClosing = true;

            // Update UI
            Invoke((MethodInvoker)delegate
            {
                this.Text = "Installiere... 0%";

                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
                TaskbarManager.Instance.SetProgressValue(0, totalCount);

                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Minimum = 0;
                progressBar.Maximum = totalCount;
            });

            // Check if there are new scripts to be installed
            if (updatedScripts.Count > 0)
            {
                UpdateLabel("Installiere Scripts, dies kann eine Weile dauern...");

                // Check and close TADDaemon if still running
                if (Process.GetProcessesByName("TADDaemon").Length != 0)
                {
                    await CloseTrainzAssetDatabase();
                }

                foreach (string scriptFile in updatedScripts)
                {
                    string targetFilePath = Path.Combine(trainzUtil.ProductInstallPath, "scripts", Path.GetFileName(scriptFile));

                    try
                    {
                        File.Delete(targetFilePath);
                        File.Copy(scriptFile, targetFilePath);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Error);
                        ShowMessageBoxAndClose("Keine Berechtigung zum Kopieren von Dateien.", "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                // Wait for TADDaemon to start
                await trainzUtil.EchoAsync("TADDaemon started!");

                // Exit if the installation has been cancelled
                if (isCancelled)
                {
                    Environment.Exit(0);
                    return;
                }

                count++;
                UpdateProgress(count, totalCount);
            }

            // Now we install all new assets
            foreach (TrainzAsset asset in updatedAssets)
            {
                UpdateLabel("Installiere Asset \"" + asset.Username + "\" <" + asset.Kuid + ">...");

                try
                {
                    string editDirPath = await trainzUtil.OpenForEditAsync(asset.Kuid);

                    try
                    {
                        Directory.Delete(editDirPath, true);
                        FileUtils.CopyDirectory(asset.Path, editDirPath, true);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Error);
                        ShowMessageBoxAndClose("Keine Berechtigung zum Kopieren von Dateien.", "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch (TrainzUtil.TrainzException)
                {
                    await trainzUtil.InstallFromPathAsync(asset.Path);
                }
                finally
                {
                    try
                    {
                        await trainzUtil.CommitAssetAsync(asset.Kuid);
                    }
                    catch (TrainzUtil.TrainzException)
                    {
                        // TODO: await trainzUtil.RevertAssetAsync(asset.Kuid);
                    }
                }

                // Exit if the installation has been cancelled
                if (isCancelled)
                {
                    Environment.Exit(0);
                    return;
                }

                count++;
                UpdateProgress(count, totalCount);
            }

            UpdateLabel("Fertigstellung der Installation...");

            // Copy settings if folder exists
            if (Directory.Exists(Path.Combine("UserData", "settings")))
            {
                foreach (string settingsFile in Directory.GetFiles(Path.Combine("UserData", "settings")))
                {
                    string targetFilePath = Path.Combine(trainzUtil.ProductInstallPath, "UserData", "settings", Path.GetFileName(settingsFile));

                    try
                    {
                        File.Delete(targetFilePath);
                        File.Copy(settingsFile, targetFilePath);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Error);
                        ShowMessageBoxAndClose("Keine Berechtigung zum Kopieren von Dateien.", "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            string globalModulePath = Path.Combine(trainzUtil.ProductInstallPath, "UserData", "settings", "globalmodule.txt");
            string tempFilePath = Path.GetTempFileName();

            // Patch the globalmodule.txt file to enable legacy mode
            // For this we will need to read the file and go through each line to find the line containing "legacy-support-mode"
            // Then we can replace the value 0 with 1 in the second line after the line containing "legacy-support-mode"
            using (StreamWriter writer = new StreamWriter(tempFilePath))
            {
                using (StreamReader reader = new StreamReader(globalModulePath))
                {
                    string line;
                    int i = 0, lineNumber = 0;

                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (line.Contains("legacy-support-mode"))
                        {
                            lineNumber = i + 2;
                        }

                        if (i == lineNumber)
                        {
                            line = line.Replace("0", "1");
                        }

                        await writer.WriteLineAsync(line);
                        i++;
                    }
                }
            }

            try
            {
                File.Delete(globalModulePath);
                File.Move(tempFilePath, globalModulePath);
            }
            catch (UnauthorizedAccessException)
            {
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Error);
                ShowMessageBoxAndClose("Keine Berechtigung zum Kopieren von Dateien.", "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            List<string> failedAssets = new List<string>();

            // Commit any remaining assets left open for editing
            foreach (string directory in Directory.GetDirectories(Path.Combine(trainzUtil.ProductInstallPath, "UserData", "editing")))
            {
                string configFile = Path.Combine(directory, "config.txt");

                if (!File.Exists(configFile))
                {
                    continue;
                }

                string kuid = ReadKuidFromConfig(configFile);
                string username = ReadUsernameFromConfig(configFile);

                // Try to commit the asset five times
                for (int i = 1; i <= 5; i++)
                {
                    try
                    {
                        await trainzUtil.CommitAssetAsync(kuid);
                    }
                    catch (TrainzUtil.TrainzException ex)
                    {
                        Console.Error.WriteLine("Failed to commit asset \"" + username + "\" <" + kuid + ">: " + ex.Message);
                    }

                    // Check if directory still exists
                    if (!Directory.Exists(directory))
                    {
                        break;
                    }
                    else if (i == 5)
                    {
                        failedAssets.Add(kuid);
                    }
                }
            }

            // Write the current timestamp as last install
            using (StreamWriter file = FileUtils.HiddenStreamWriter.Open(".lastinstall"))
            {
                file.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString());
            }

            // Show a message box if there are failed assets
            if (failedAssets.Count > 0)
            {
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Error);
                ShowMessageBox("Einige Assets konnten nicht eingebunden werden. Bitte überprüfen Sie die Assets unter \"Geöffnet zum Bearbeiten\" im Content Manager bevor Sie Trainz starten.", "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Finish it off
            UpdateLabel("Installation abgeschlossen!");
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
            ShowMessageBoxAndClose("Die Installation ist erfolgreich abgeschlossen.\n\nScripts: " + updatedScripts.Count + ", Assets: " + updatedAssets.Count, "Fertig!", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task CloseTrainzAssetDatabase()
        {
            Process process = new Process();

            // Stop the process from opening a new window
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Setup executable and parameters
            process.StartInfo.FileName = "taskkill";
            process.StartInfo.Arguments = "/IM TADDaemon.exe";

            // Go
            process.Start();

            // Wait for TADDaemon to be killed
            await Task.Run(() =>
            {
                Process tadDaemon = Process.GetProcessesByName("TADDaemon").FirstOrDefault();

                if (tadDaemon != null)
                {
                    tadDaemon.WaitForExit();
                    tadDaemon.Close();
                }
            });

            // Clean up
            process.Close();
        }

        private void UpdateLabel(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => UpdateLabel(text)));
            }
            else
            {
                label.Text = text;
            }
        }

        private void UpdateProgress(int value, int totalCount)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => UpdateProgress(value, totalCount)));
            }
            else
            {
                this.Text = "Installiere... " + (int)Math.Round((double)(100 * value) / totalCount) + "%";
                TaskbarManager.Instance.SetProgressValue(value, totalCount);
                progressBar.Value = value;
            }
        }

        private void ShowMessageBox(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => ShowMessageBox(message, caption, buttons, icon)));
            }
            else
            {
                FlashWindow.Flash(this);
                MessageBox.Show(this, message, caption, buttons, icon);
            }
        }

        private void ShowMessageBoxAndClose(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => ShowMessageBoxAndClose(message, caption, buttons, icon)));
            }
            else
            {
                FlashWindow.Flash(this);
                DialogResult result = MessageBox.Show(this, message, caption, buttons, icon);

                if (result == DialogResult.OK)
                {
                    Environment.Exit(0);
                }
            }
        }

        private List<string> GetUpdatedScripts(long lastTimestamp)
        {
            List<string> updated = new List<string>();

            foreach (string fileName in Directory.GetFiles("scripts"))
            {
                if (File.GetLastWriteTimeUtc(fileName).Ticks > lastTimestamp)
                {
                    updated.Add(Path.GetFullPath(fileName));
                }
            }

            return updated;
        }

        private List<TrainzAsset> GetUpdatedAssets(long lastTimestamp)
        {
            // First we build an index of kuid -> directory
            Dictionary<string, Tuple<string, string>> changedDirs = new Dictionary<string, Tuple<string, string>>();

            foreach (string directory in Directory.GetDirectories(Path.Combine("UserData", "editing")))
            {
                if (ContainsFilesNewerThan(directory, lastTimestamp))
                {
                    string configFile = Path.Combine(directory, "config.txt");

                    if (!File.Exists(configFile))
                    {
                        continue;
                    }

                    string kuid = ReadKuidFromConfig(configFile);
                    string username = ReadUsernameFromConfig(configFile);

                    if (!changedDirs.ContainsKey(kuid))
                    {
                        changedDirs.Add(kuid, Tuple.Create(Path.GetFullPath(directory), username));
                    }
                }
            }

            // Now it is time to sort the whole thing by asset username
            List<TrainzAsset> updated = new List<TrainzAsset>();

            foreach (string kuid in changedDirs.Keys)
            {
                updated.Add(new TrainzAsset(kuid, changedDirs[kuid].Item1, changedDirs[kuid].Item2));
            }

            return updated;
        }

        private string ReadKuidFromConfig(string configFile)
        {
            using (TextReader reader = File.OpenText(configFile))
            {
                while (reader.Peek() >= 0)
                {
                    string line = reader.ReadLine().TrimEnd();

                    if (String.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (line.Split().First().Equals("kuid"))
                    {
                        return line.Split('<')[1].Split('>')[0];
                    }
                }
            }

            return null;
        }

        private string ReadUsernameFromConfig(string configFile)
        {
            using (TextReader reader = File.OpenText(configFile))
            {
                while (reader.Peek() >= 0)
                {
                    string line = reader.ReadLine().TrimEnd();

                    if (String.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (line.Split().First().Equals("username") || line.Split().First().Equals("asset-filename"))
                    {
                        string key = line.Split().First();

                        if (line.Substring(key.Length).Length > 0)
                        {
                            return line.Split('"')[1].Split('"')[0];
                        }
                    }
                }
            }

            return null;
        }

        private bool ContainsFilesNewerThan(string directory, long lastTimestamp)
        {
            // Go through each file in directory and compare last write time with lastTimestamp
            foreach (string fileName in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (File.GetLastWriteTimeUtc(fileName).Ticks > lastTimestamp)
                {
                    return true;
                }
            }

            return false;
        }

        class TrainzAsset
        {
            public string Kuid { set; get; }
            public string Path { set; get; }
            public string Username { set; get; }

            public TrainzAsset(string kuid, string path, string username)
            {
                this.Kuid = kuid;
                this.Path = path;
                this.Username = username;
            }
        }

        class CompareByUsername : IComparer<TrainzAsset>
        {
            public int Compare(TrainzAsset x, TrainzAsset y)
            {
                return String.Compare(x.Username.ToLower(), y.Username.ToLower());
            }
        }
    }
}
