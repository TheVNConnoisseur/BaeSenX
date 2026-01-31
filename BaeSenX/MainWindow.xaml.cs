using Microsoft.Win32;
using System.IO;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace BaeSenX
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string FilePath;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ComboBox_OptionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Disable the convert button until a file is selected
            Button_Convert.IsEnabled = false;

            if (ComboBox_OptionSelector.SelectedIndex != -1)
            {
                Button_Original_File.IsEnabled = true;
            }
            else
            {
                Button_Original_File.IsEnabled = false;
            }
        }

        private void Button_Original_File_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.FileName = "";
            ofd.Multiselect = false;
            switch (ComboBox_OptionSelector.SelectedIndex)
            {
                case 1:
                    ofd.Title = "Select the game's overall save data or one of its quick save files";
                    ofd.Filter = "Main save|common.dat|Quick save|no*";
                    break;
                case 4:
                    ofd.Title = "Select the game's compiled script file";
                    ofd.Filter = "Compiled script|bsxx.dat";
                    break;
                case 5:
                    ofd.Title = "Select the game's decompiled script file";
                    ofd.Filter = "Decompiled script|*.json";
                    break;
            }
            ofd.FilterIndex = 0;
            Nullable<bool> result = ofd.ShowDialog();

            if (result == true)
            {
                try
                {
                    Button_Convert.IsEnabled = true;
                    FilePath = ofd.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Button_Convert_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            switch (ComboBox_OptionSelector.SelectedIndex)
            {
                case 1:
                    sfd.Filter = "Main save|common.dat|Quick save|no*";
                    break;
                case 4:
                    sfd.Filter = "Decompiled script|*.json";
                    break;
                case 5:
                    sfd.Filter = "Compiled script|bsxx.dat";
                    break;
            }
            Nullable<bool> result = sfd.ShowDialog();

            if (result == true)
            {
                try
                {
                    switch (ComboBox_OptionSelector.SelectedIndex)
                    {
                        //When trying to update the checksum of a save file, it needs to first load the script file associated
                        //to the game. Then, it will recalculate the checksum based on the compiled opcodes of the script.
                        case 1:
                            OpenFileDialog ofd = new OpenFileDialog();
                            ofd.FileName = "bsxx.dat";
                            ofd.Multiselect = false;
                            ofd.Title = "Select the game's compiled script file";
                            ofd.Filter = "Compiled script|bsxx.dat";
                            ofd.FilterIndex = 0;
                            Nullable<bool> resultofd = ofd.ShowDialog();

                            if (resultofd == true)
                            {
                                try
                                {
                                    BSXScript ScriptToReference = new BSXScript(File.ReadAllBytes(ofd.FileName));
                                    Save SaveToEdit = new Save(File.ReadAllBytes(FilePath));
                                    SaveToEdit.SetUpdatedChecksum(ScriptToReference.GetRawList(0));
                                    File.WriteAllBytes(Path.GetFullPath(sfd.FileName), SaveToEdit.GetCompiledSave());
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            break;
                        case 4:
                            BSXScript ScriptObject = new BSXScript(File.ReadAllBytes(FilePath));
                            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
                            string json = JsonSerializer.Serialize(ScriptObject.Decompile(), options);
                            File.WriteAllText(Path.GetFullPath(sfd.FileName), json);
                            break;
                        case 5:
                            break;
                    }

                    Button_Convert.IsEnabled = false;
                    MessageBox.Show($"Process completed successfully.", "Conversion completed.", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}