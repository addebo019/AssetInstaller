using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace AssetInstaller
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            TrainzUtil trainzUtil = new TrainzUtil();

            // Check if Trainz installation was found on the system
            if (trainzUtil.ProductInstallPath is null)
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

            // Check if Nvidia GPU is installed on the system
            if (SystemGpuInfo.IsNvidia)
            {
                DialogResult result = MessageBox.Show("Auf diesem System wurde eine Nvidia-Grafikkarte erkannt!\n\nEs kann zu Fehlern bei Texturen kommen, wenn die hardwarebeschleunigte Texturkompression in Trainz während der Installation aktiviert ist. Bitte stellen Sie sicher, dass diese Einstellung im Content Manager ausgeschaltet ist, bevor Sie mit der Installation fortfahren.\n\nSind Sie sicher, dass Sie fortfahren möchten? ", "Warnung!", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                if (result == DialogResult.No)
                {
                    return;
                }
            }

            Application.Run(new InstallerForm(trainzUtil));
        }
    }
}
