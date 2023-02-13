using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PDFSecuReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Settings window constructor
        public MainWindow()
        {
            InitializeComponent();

            //Check if settings exists, load them if they do
            if (App.SettingsFile.Exists)
            {
                using FileStream fileStream = App.SettingsFile.Open(FileMode.Open, FileAccess.Read);
                AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(fileStream);

                if (settings == null)
                    return;

                //Get the preferred reader from the settings file
                ComboboxPDFViewer.SelectedIndex = (int)settings.PrefferedReader;
            }
        }

        //Handle for the save settings button
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //Create appdata directory if it doesn't exists
                if (!App.AppDataDirectory.Exists)
                {
                    App.AppDataDirectory.Create();
                }

                //Create app settings
                AppSettings settings = new();
                settings.UseHWAcceleratedVideo = false;

                //Get preferred reader from the UI (user selection)
                settings.PrefferedReader = ComboboxPDFViewer.SelectedIndex switch
                {
                    0 => ReaderTypes.READER_ADOBE_READER,
                    1 => ReaderTypes.READER_MICROSOFT_EDGE,
                    2 => ReaderTypes.READER_SUMATRA_PDF,
                    _ => ReaderTypes.READER_ADOBE_READER,
                };

                //Write settings file
                using StreamWriter streamWriter = new(App.SettingsFile.FullName, false);
                streamWriter.Write(JsonSerializer.Serialize(settings));
                streamWriter.Flush();

                streamWriter.Close();

                Close();
            }
            catch
            {
                MessageBox.Show("Error!", "Cannot create configuration file!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
