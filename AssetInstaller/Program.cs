using AssetInstaller.Utils;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Windows.Forms;

namespace AssetInstaller
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            TrainzUtil trainzUtil = new TrainzUtil(args.Length > 0 ? args[0] : RegistryUtils.FindTrainzInstallation());

            // Check if Trainz installation was found on the system and show folder picker if not
            if (trainzUtil.ProductInstallPath is null)
            {
                using (CommonOpenFileDialog dialog = new CommonOpenFileDialog())
                {
                    dialog.Title = "Bitte Installationsverzeichnis auswählen...";
                    dialog.IsFolderPicker = true;

                    CommonFileDialogResult result = dialog.ShowDialog();

                    if (result == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
                    {
                        trainzUtil.ProductInstallPath = dialog.FileName;
                    }
                }
            }

            // Check if installation path exists and contains TrainzUtil.exe
            if (!File.Exists(Path.Combine(trainzUtil.ProductInstallPath, "bin", "TrainzUtil.exe")))
            {
                MessageBox.Show("Trainz Simulator 2009 konnte auf diesem System nicht gefunden werden. Bitte installieren Sie das Spiel neu und versuchen Sie es erneut.", "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }

            // Check if Trainz is not running
            if (Process.GetProcessesByName("trainz").Length != 0)
            {
                MessageBox.Show("Trainz muss geschlossen werden, um mit der Installation fortfahren zu können. Bitte schließen Sie das Spiel und versuchen Sie es erneut.", "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }

            // Check if we have write permissions to installation path
            if (!FileUtils.DirectoryHasPermission(trainzUtil.ProductInstallPath, FileSystemRights.WriteData))
            {
                RestartWithElevatedPrivileges(new string[] { trainzUtil.ProductInstallPath });
                return;
            }

            // Check if Nvidia GPU is installed on the system
            if (SystemGpuInfo.IsNvidia && !File.Exists(".lastinstall"))
            {
                DialogResult result = MessageBox.Show("Auf diesem System wurde eine Nvidia-Grafikkarte erkannt!\n\nEs kann zu Fehlern bei Texturen kommen, wenn die hardwarebeschleunigte Texturkompression in Trainz während der Installation aktiviert ist. Bitte stellen Sie sicher, dass diese Einstellung im Content Manager ausgeschaltet ist, bevor Sie mit der Installation fortfahren.\n\nSind Sie sicher, dass Sie fortfahren möchten? ", "Warnung!", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                if (result == DialogResult.No)
                {
                    return;
                }
            }

            Application.Run(new InstallerForm(trainzUtil));
        }

        static void RestartWithElevatedPrivileges(string[] args)
        {
            ProcessStartInfo proc = new ProcessStartInfo();
            proc.WorkingDirectory = Environment.CurrentDirectory;
            proc.FileName = Application.ExecutablePath;
            proc.UseShellExecute = true;
            proc.Verb = "runas";

            foreach (string arg in args)
            {
                proc.Arguments += String.Format("\"{0}\" ", arg);
            }

            try
            {
                Process process = Process.Start(proc);
                process.WaitForExit();
            }
            catch
            {
                MessageBox.Show("Die Installation kann ohne Administratorrechte nicht fortgesetzt werden.", "Fehler!", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
        }
    }
}
