using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;
using static PDFSecuReader.Configuration;

namespace PDFSecuReader
{
    //Reader types enum
    public enum ReaderTypes
    {
        READER_ADOBE_READER,
        READER_MICROSOFT_EDGE,
        READER_SUMATRA_PDF
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        //Global paths
        public const string APP_DATA_NAME = "PDFSecuReader";
        public const string SETTINGS_FILE_NAME = "settings.json";
        public const string SANDBOX_CONFIG_FILE_NAME = "temp.wsb";

        public static DirectoryInfo AppDataDirectory;
        public static FileInfo SettingsFile;

        //Main app entry point
        public App()
        {
            //Get appdata directory
            string appdataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            AppDataDirectory = new(Path.Combine(appdataPath, APP_DATA_NAME));

            //Get the settings file
            SettingsFile = new FileInfo(Path.Combine(AppDataDirectory.FullName, SETTINGS_FILE_NAME));
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //Open PDF file
            //If args length == 1 => open PDF file
            //Else open settings
            if (e.Args.Length == 1) //Open PDF file
            {
                //Check PDF file
                FileInfo pdfFile = new(e.Args[0]);
                if (!pdfFile.Exists)
                {
                    MessageBox.Show("Error!", "PDF file " + pdfFile.Name + " does not exists!", MessageBoxButton.OK, MessageBoxImage.Error);
                    Current.Shutdown();
                }

                //Check if settings exists
                if (AppDataDirectory.Exists && SettingsFile.Exists) //Open PDF in Windows Sandbox
                {
                    //Try to open the PDF
                    try
                    {
                        OpenPDF(pdfFile);
                    }
                    catch
                    {
                        MessageBox.Show("Error!", "Error opening PDF file!", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    //Open settings -> first run
                    OpenSettingsWindow();

                    //Try to open pdf
                    try
                    {
                        OpenPDF(pdfFile);
                    }
                    catch
                    {
                        MessageBox.Show("Error!", "Error opening PDF file!", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else //Open settings
            {
                OpenSettingsWindow();
            }

            //Close application
            Current.Shutdown();
        }

        //Open settings window
        private static void OpenSettingsWindow()
        {
            MainWindow window = new();
            window.ShowDialog();
        }

        //Open PDF routine
        public void OpenPDF(FileInfo fileInfo)
        {
            //Check if sandbox is already running and kill it
            try
            {
                foreach (var process in Process.GetProcessesByName("WindowsSandboxClient"))
                {
                    process.Kill();
                    Thread.Sleep(1000);
                }
            }
            catch
            {
                MessageBox.Show("Cannot close the running Windows Sandbox!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
                return;
            }

            //Create the PDF in Map/PDF folder if it doesn't exist
            DirectoryInfo mapPDFDir = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "Map", "PDF"));
            if (!mapPDFDir.Exists)
            {
                try
                {
                    mapPDFDir.Create();
                }
                catch
                {
                    MessageBox.Show("Cannot create PDF mapped folder!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Current.Shutdown();
                    return;
                }
            }

            //Clear the PDF files from the Map/PDF folder
            //If it's not working a file is opened somewhere, and we must clear it before continuing
            FileInfo[] files = mapPDFDir.GetFiles();
            foreach (FileInfo file in files)
            {
                try
                {
                    if (file.IsReadOnly) file.IsReadOnly = false;
                    file.Delete();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    //Not critical, it will be cleared next time
                }
            }

            //Copy PDF file to Map/PDF folder
            FileInfo[] remainingFiles = mapPDFDir.GetFiles();
            int fileCopies = 0;
            foreach (FileInfo file in remainingFiles)
            {
                if (file.Name.StartsWith("PDFSR_"))
                {
                    fileCopies++;
                }
            }

            //Try to copy base file
            bool fileCopied = false;
            try
            {
                fileInfo.CopyTo(Path.Combine(AppContext.BaseDirectory, "Map", "PDF", fileInfo.Name), true);
                fileCopied = true;
            }
            catch
            {
                //Do nothing, we try to create a copy
            }

            //If the base file was not copied, make a copy
            if (!fileCopied)
            {
                try
                {
                    string fileName = "PDFSR_" + (fileCopies + 1).ToString() + "_" + fileInfo.Name;
                    fileInfo.CopyTo(Path.Combine(AppContext.BaseDirectory, "Map", "PDF", fileName), true);

                    fileInfo = new FileInfo(Path.Combine(AppContext.BaseDirectory, "Map", "PDF", fileName));
                }
                catch
                {
                    MessageBox.Show("Cannot create copy for PDF file", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    Current.Shutdown();
                    return;
                }
            }

            //Open settings
            //Path: AppData -> Roaming -> PDFSecuReader
            using FileStream fileStream = SettingsFile.Open(FileMode.Open, FileAccess.Read);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(fileStream);

            //If no settings -> use defaults
            if (settings == null)
            {
                MessageBox.Show("Warning!", "Cannot open the settings file, the default settings will be used!" + Environment.NewLine
                     + "If you want to create new settings, open the settings shortcut from application folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                settings = new AppSettings()
                {
                    UseHWAcceleratedVideo = false,
                    PrefferedReader = ReaderTypes.READER_ADOBE_READER
                };
            }

            fileStream.Close();

            //Create the sandbox .wsb file needed for sandbox configurations
            //Path: AppData -> Roaming -> PDFSecuReader
            FileInfo sandboxConfigFile = new(Path.Combine(AppDataDirectory.FullName, SANDBOX_CONFIG_FILE_NAME));
            using FileStream sandboxFileStream = sandboxConfigFile.Open(FileMode.OpenOrCreate, FileAccess.Write);

            //Create commands
            //Create a "reader_run.cmd" file that will be executed by the Windows Sandbox at startup
            //First, reader_run.cmd hides the taskbar through Windows Registry edit
            //Then it runs the application in the preferred reader 
            List<LogonCommand> commands = new();
            switch (settings.PrefferedReader)
            {
                //Adobe reader run command + hide taskbar
                case ReaderTypes.READER_ADOBE_READER:
                    //Write to reader_run.cmd the commands
                    //Open with adobe reader
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Map", "reader_run.cmd"),
                            @"powershell -command ""&{$p='HKCU:SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3';$v=(Get-ItemProperty -Path $p).Settings;$v[8]=3;&Set-ItemProperty -Path $p -Name Settings -Value $v;&Stop-Process -f -ProcessName explorer}""" + "\n" +
                            "REG IMPORT C:\\MappedFolder\\final_reg.reg\n" + @"C:\MappedFolder\Adobe_Reader_9.0_Lite_ENG.exe /s ""C:\\MappedFolder\\PDF\\" + fileInfo.Name + "\"\n"
                        );
                    commands.Add(new LogonCommand() { Command = @"cmd.exe /C C:\MappedFolder\reader_run.cmd" });
                    break;

                //Microsoft edge run command + hide taskbar
                case ReaderTypes.READER_MICROSOFT_EDGE:
                    //Write to reader_run.cmd the commands
                    //Open with MS Edge
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Map", "reader_run.cmd"),
                            @"powershell -command ""&{$p='HKCU:SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3';$v=(Get-ItemProperty -Path $p).Settings;$v[8]=3;&Set-ItemProperty -Path $p -Name Settings -Value $v;&Stop-Process -f -ProcessName explorer}""" + "\n" +
                            @"cmd.exe /C start /MAX msedge ""C:\\MappedFolder\\PDF\\" + fileInfo.Name + "\""
                        );
                    commands.Add(new LogonCommand() { Command = @"cmd.exe /C C:\MappedFolder\reader_run.cmd" });
                    break;

                //SumatraPDF run command + hide taskbar
                case ReaderTypes.READER_SUMATRA_PDF:
                    //Write to reader_run.cmd the commands
                    //Open with SumatraPDF
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Map", "reader_run.cmd"),
                            @"powershell -command ""&{$p='HKCU:SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3';$v=(Get-ItemProperty -Path $p).Settings;$v[8]=3;&Set-ItemProperty -Path $p -Name Settings -Value $v;&Stop-Process -f -ProcessName explorer}""" + "\n" +
                            @"cmd.exe /C start /MAX C:\MappedFolder\SumatraPDF.exe ""C:\\MappedFolder\\PDF\\" + fileInfo.Name + "\""
                        );
                    commands.Add(new LogonCommand() { Command = @"cmd.exe /C C:\MappedFolder\reader_run.cmd" });
                    break;
                default:
                    break;
            }

            //Create a configuration object for the XML .wsb file
            Configuration confObject = new()
            {
                VGpu = settings.UseHWAcceleratedVideo ? "Enable" : "Disable",
                Networking = "Disable",
                ProtectedClient = "Enable",
                MappedFolders = new MappedFolder[]
                {
                    //Map utils folder (run cmd) from host READ-ONLY
                    new MappedFolder()
                    {
                        HostFolder = Path.Combine(AppContext.BaseDirectory, "Map"),
                        SandboxFolder = @"C:\MappedFolder",
                        ReadOnly = true //readonly to protect the host system
                    }
                },
                LogonCommand = commands
            };

            //Serialize settings into the XML .wsb file
            XmlSerializer xmlSerializer = new(typeof(Configuration));
            xmlSerializer.Serialize(sandboxFileStream, confObject);

            //Open the sandbox process
            Process sandboxProcess = new();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = @"/C start " + sandboxConfigFile.FullName;
            sandboxProcess.StartInfo = startInfo;
            sandboxProcess.Start();

            //Close application
            Current.Shutdown();
        }
    }
}
