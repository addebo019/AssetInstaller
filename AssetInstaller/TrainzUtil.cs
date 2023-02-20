using AssetInstaller.Utils;
using CliWrap;
using CliWrap.Buffered;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AssetInstaller
{
    public class TrainzUtil
    {
        public string ProductInstallPath { get; set; }

        public TrainzUtil()
        {
            this.ProductInstallPath = RegistryUtils.FindTrainzInstallation();
        }

        private string ParseErrorMessage(string line)
        {
            string newLine = line.Substring(line.IndexOf('>') + 1);
            return newLine.Substring(newLine.IndexOf(':') + 1).Trim();
        }

        public async Task<BufferedCommandResult> RunCommandAsync(params string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(5));

            Command command = Cli.Wrap(ProductInstallPath + "\\bin\\TrainzUtil.exe").WithArguments(args).WithValidation(CommandResultValidation.None);
            BufferedCommandResult bufferedCommandResult = await command.ExecuteBufferedAsync(cts.Token);
            return bufferedCommandResult;
        }

        public async Task<string> InstallFromPathAsync(string assetPath)
        {
            BufferedCommandResult result = await RunCommandAsync("installfrompath", assetPath);
            string[] output = result.StandardOutput.Split('\n');

            foreach (string line in output)
            {
                if (line.StartsWith("-"))
                {
                    throw new TrainzException(ParseErrorMessage(line));
                }
                else if (line.StartsWith("+"))
                {
                    return line.Split('<')[1].Split('>')[0];
                }
            }

            return null;
        }

        public async Task<string> OpenForEditAsync(string assetKuid)
        {
            BufferedCommandResult result = await RunCommandAsync("edit", assetKuid);
            string[] output = result.StandardOutput.Split('\n');

            foreach (string line in output)
            {
                if (line.StartsWith("-"))
                {
                    throw new TrainzException(ParseErrorMessage(line));
                }
                else if (line.StartsWith("+"))
                {
                    return ParseErrorMessage(line);
                }
            }

            return null;
        }

        public async Task<bool> CommitAssetAsync(string assetKuid)
        {
            BufferedCommandResult result = await RunCommandAsync("commit", assetKuid);
            string[] output = result.StandardOutput.Split('\n');

            foreach (string line in output)
            {
                if (line.StartsWith("-"))
                {
                    throw new TrainzException(ParseErrorMessage(line));
                }
                else if (line.StartsWith("+"))
                {
                    return true;
                }
            }

            return false;
        }

        public async void RevertAssetAsync(string assetKuid)
        {
            BufferedCommandResult result = await RunCommandAsync("revert", assetKuid);
            string[] output = result.StandardOutput.Split('\n');

            foreach (string line in output)
            {
                if (line.StartsWith("-"))
                {
                    throw new TrainzException(ParseErrorMessage(line));
                }
            }
        }

        public async Task<bool> EchoAsync(string text)
        {
            BufferedCommandResult result = await RunCommandAsync("echo", text);
            string[] output = result.StandardOutput.Split('\n');

            foreach (string line in output)
            {
                if (line.Equals(text))
                {
                    return true;
                }
            }

            return false;
        }

        public class TrainzException : Exception
        {
            public TrainzException() { }

            public TrainzException(string message) : base(message) { }

            public TrainzException(string message, Exception innerException) : base(message, innerException) { }
        }
    }
}
