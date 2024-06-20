using System.Diagnostics;

namespace VeraCryptMountHelper;

internal class Program
{
    private static string? _veraCryptExecutablePath;

    static void Main(string[] args)
    {
        Console.Title = "VeraCrypt Mount Helper. Powered by VeraCrypt.";

        var configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocsEncryptorConfig.txt");
        var configured = false;

        if (File.Exists(configFilePath))
        {
            _veraCryptExecutablePath = ReadPathFromConfigFile(configFilePath);           

            do
            {
                Console.WriteLine($"VeraCrypt executable file is set to \"{_veraCryptExecutablePath}\".");
                Console.Write("Press Enter to confirm or 2 to edit the path: ");
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    if (!string.IsNullOrEmpty(_veraCryptExecutablePath) && File.Exists(_veraCryptExecutablePath))
                    {                        
                        configured = true;
                    }
                    else
                    {
                        Console.WriteLine("\nInvalid path. Please, try again.");
                        configured = false;
                    }
                }
                else if (key.KeyChar == '2')
                {
                    Console.WriteLine();
                    Console.WriteLine("You selected edit file.");
                    PromptAndSetVeraCryptExecutablePath(configFilePath);                    
                }
                else
                {
                    Console.WriteLine("Invalid selection.");
                    continue;
                }
            }
            while (!configured);
        }
        else
        {
            if (VeraCryptPathAutomaticallyConfigured(configFilePath))
            {
                _veraCryptExecutablePath = ReadPathFromConfigFile(configFilePath);
                Console.WriteLine($"VeraCrypt executable file is automatically set to \"{_veraCryptExecutablePath}\".");
                Console.WriteLine("Press ENTER to continue.");
                Console.ReadLine();
            }
            else
            {
                Console.WriteLine("Could not find VeraCrypt executable file automatically.");
                PromptAndSetVeraCryptExecutablePath(configFilePath);
            }
        }

        Console.Clear();               

        string? encryptedVolumePath;
        do
        {
            encryptedVolumePath = GetEncryptedVolumePath();

            if (string.IsNullOrEmpty(encryptedVolumePath))
            {
                Console.WriteLine("The path cannot be empty.");
            }
            else if (!File.Exists(encryptedVolumePath))
            {
                Console.WriteLine("Volume file not found.");
                encryptedVolumePath = null;
            }
        }
        while (string.IsNullOrEmpty(encryptedVolumePath) || !File.Exists(encryptedVolumePath));

        var driveLetter = GetAvailableDriveLetter();
        if (string.IsNullOrEmpty(driveLetter))
        {
            Console.WriteLine("Unable to find an available drive in the system to mount the file.");
            return;
        }

        Console.WriteLine("VeraCrypt requires administrative privileges.");
        Console.WriteLine("You may see a User Account Control (UAC) prompt after you enter the password.");
        Console.WriteLine();
        Console.Write("Enter the password for the VeraCrypt volume: ");
        var password = ReadPassword();

        var processStartInfo = new ProcessStartInfo()
        {
            FileName = _veraCryptExecutablePath,
            Arguments = $"/v \"{encryptedVolumePath}\" /l {driveLetter} /p \"{password}\" /q /s /m rm",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process
        {
            StartInfo = processStartInfo
        };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Console.WriteLine("The specified executable is not a valid. Please, restart the program and configure a VeraCrypt executable.\n");
            Console.WriteLine(ex.Message);
            _veraCryptExecutablePath = null;

            return;
        }

        process.WaitForExit();

        if (process.ExitCode == 0 && Directory.Exists($"{driveLetter}:\\"))
        {
            Console.WriteLine($"Volume mounted successfully on drive {driveLetter}:\\");
            Process.Start("explorer.exe", $"{driveLetter}:\\");
        }
        else
        {
            Console.WriteLine("VeraCrypt encountered an error. Possible reasons include:");
            Console.WriteLine("- VeraCrypt was not run with administrative privileges.");
            Console.WriteLine("- Incorrect password for the file.");
            Console.WriteLine("- Provided file is not a VeraCrypt volume.");

            return;
        }

        Console.WriteLine("Press any key to unmount the volume and exit...");
        Console.ReadKey();

        var unmountProcessInfo = new ProcessStartInfo
        {
            FileName = _veraCryptExecutablePath,
            Arguments = $"/d {driveLetter} /q /s",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var unmountProcess = new Process
        {
            StartInfo = unmountProcessInfo
        };

        unmountProcess.Start();
        unmountProcess.WaitForExit();

        if (unmountProcess.ExitCode == 0)
        {
            Console.WriteLine($"Volume unmounted successfully from drive {driveLetter}:\\");
        }
        else
        {
            Console.WriteLine("Failed to unmount the volume.");
            Console.WriteLine(unmountProcess.StandardError.ReadToEnd());
        }
    }

    private static bool VeraCryptPathAutomaticallyConfigured(string configFilePath)
    {
        string[] exeNames = ["VeraCrypt-x64.exe", "VeraCrypt.exe"];

        string[] commonPaths = [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        ];

        foreach (var path in commonPaths)
        {
            var fullPath = exeNames
                .Select(exeName => Path.Combine(path, "VeraCrypt", exeName))
                .FirstOrDefault(File.Exists);

            if (fullPath != null)
            {
                File.WriteAllText(configFilePath, fullPath.Trim().Trim('"'));
                return true;
            }
        }

        return false;
    }

    private static void PromptAndSetVeraCryptExecutablePath(string configFilePath)
    {
        Console.Write("Paste the path to the VeraCrypt executable or drag and drop the file onto the console window and press enter: ");
        _veraCryptExecutablePath = Console.ReadLine()?.Trim().Trim('"');
        UpdateConfigFile(configFilePath, _veraCryptExecutablePath);
    }

    private static string GetAvailableDriveLetter()
    {
        var reservedDriveLetters = new[] { 'A', 'B' };
        var usedDriveLetters = DriveInfo.GetDrives().Select(d => d.Name[0]).Concat(reservedDriveLetters);
        var availableDriveLetter = Enumerable.Range('A', 26)
                                             .Select(i => (char)i)
                                             .Except(usedDriveLetters)
                                             .FirstOrDefault();

        if (availableDriveLetter == default(char))
        {
            return string.Empty;
        }

        return availableDriveLetter.ToString();
    }

    private static string GetEncryptedVolumePath()
    {
        Console.Write("Paste the path to your encrypted volume or drag and drop the file onto the console window and press enter: ");
        var encryptedVolumePath = Console.ReadLine()?.Trim().Trim('"');

        return encryptedVolumePath;
    }

    private static string ReadPassword()
    {
        var password = string.Empty;
        ConsoleKeyInfo info;
        do
        {
            info = Console.ReadKey(true);
            if (info.Key != ConsoleKey.Enter && info.Key != ConsoleKey.Backspace)
            {
                password += info.KeyChar;
                Console.Write('*');
            }
            else if (info.Key == ConsoleKey.Backspace)
            {
                if (!string.IsNullOrEmpty(password))
                {
                    // Removes the last character from the password string
                    password = password[..^1];
                    Console.Write("\b \b");
                }
            }
        }
        while (info.Key != ConsoleKey.Enter);

        Console.WriteLine();

        return password;
    }

    private static string ReadPathFromConfigFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var path = File.ReadAllText(filePath)?.Trim();

                return path;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading from configuration file: {ex.Message}");
        }

        return null;
    }

    private static void UpdateConfigFile(string filePath, string path)
    {
        try
        {
            File.WriteAllText(filePath, path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating configuration file: {ex.Message}");
        }
    }
}