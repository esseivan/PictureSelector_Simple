using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.WindowsAPICodePack.Dialogs;
using PictureSelector_Simple;
using st = EsseivaN.Tools.SettingManager_Fast;

namespace PictureSelector_Simple
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<ImageInfo> files { get; set; } = new ObservableCollection<ImageInfo>();
        public IEnumerable<string> filesPath { get => files.Select((x) => x.FileName); }
        public string SelectedFolder { get; private set; } = "Aucun fichier sélectionné";
        private string saveFileName = "pictureSelector.pssave";

        public ImageInfo SelectedItem
        {
            get
            {
                return (ImageInfo)listFiles.SelectedItem;
            }
        }

        // 1min auto save timer
        public System.Timers.Timer tmrAutoUpdate = new Timer(1000 * 60 * 1);

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();

            tmrAutoUpdate.AutoReset = true;
            tmrAutoUpdate.Elapsed += TmrAutoUpdate_Elapsed;
            tmrAutoUpdate.Start();

            DataContext = this;
        }

        private void Save()
        {
            if (!Directory.Exists(SelectedFolder))
                return;

            if (files.Count == 0)
                return;

            string savePath = Path.Combine(SelectedFolder, saveFileName);
            if (File.Exists(savePath))
                File.SetAttributes(savePath, FileAttributes.Normal);

            st.Save(savePath, files, false, false);

            File.SetAttributes(savePath, FileAttributes.Hidden);
        }

        private void Load()
        {
            string savePath = Path.Combine(SelectedFolder, saveFileName);

            if (!File.Exists(savePath))
                return;

            if (!st.Load(savePath, out List<ImageInfo> loadedData))
                return;

            foreach (var dataItem in loadedData)
            {
                var found = files.Where((x) => dataItem.Equals(x));
                if (found.Count() == 0) continue;
                found.ElementAt(0).Selected = dataItem.Selected;
            }
        }

        private void TmrAutoUpdate_Elapsed(object sender, ElapsedEventArgs e)
        {
            Save();
        }

        private void OpenFolder(string path)
        {
            if (!Directory.Exists(path))
                return;

            // Restart auto save timer
            tmrAutoUpdate.Stop();
            tmrAutoUpdate.Start();

            SelectedFolder = path;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedFolder"));

            files.Clear();

            DirectoryInfo di = new DirectoryInfo(path);
            foreach (FileInfo file in di.EnumerateFiles())
            {
                string mimeType = MimeMapping.GetMimeMapping(file.Name);
                if (mimeType == null) continue;
                if (!mimeType.StartsWith("image")) continue;

                files.Add(new ImageInfo(file.Name));
            }

            Load();
            Save();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("files"));

            if (files.Count > 0)
                listFiles.SelectedIndex = 0;
            listFiles.Focus();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog("Sélectionner le dossier contenant les images")
            {
                IsFolderPicker = true
            };
            CommonFileDialogResult result = dialog.ShowDialog();

            if (result == CommonFileDialogResult.Ok)
            {
                OpenFolder(dialog.FileName);
            }
        }

        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void listFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            imageControl.Source = null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));

            if (listFiles.SelectedIndex == -1)
                return;

            string fileName = (listFiles.SelectedItem as ImageInfo).ToString();
            string filePath = Path.Combine(SelectedFolder, fileName);

            // Get image
            if (!File.Exists(filePath))
                return;

            BitmapImage img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(filePath);
            img.EndInit();

            imageControl.Source = img;
        }

        private void ToggleSelected()
        {
            if (listFiles.SelectedIndex == -1)
                return;

            ImageInfo imgInfo = listFiles.SelectedItem as ImageInfo;
            imgInfo.Selected = !imgInfo.Selected;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                case Key.Space:
                    ToggleSelected();
                    break;

                default:
                    break;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = files.Where((x) => x.Selected);

            // Copy to output
            CommonOpenFileDialog ofd = new CommonOpenFileDialog("Sélectionner le dossier où copier les images sélectionnées")
            {
                IsFolderPicker = true,
            };
            if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string selFolder = ofd.FileName;
                foreach (var file in selectedFiles)
                {
                    string srcPath = Path.Combine(SelectedFolder, file.FileName);
                    string destPath = Path.Combine(selFolder, file.FileName);
                    File.Copy(srcPath, destPath, false);
                }
                Process.Start(ofd.FileName);
            }
        }

        private void Aide_Click(object sender, RoutedEventArgs e)
        {
            string helpStr = "1. Aller sous Fichier -> Ouvrir pour choisir un DOSSIER contenant les images à sélectionner\n\n" +
                "2. Sélectionner les images en utilisant les flèches Haut et Bas et la barre Espace\n\n" +
                "3. Aller sous Fichier -> Exporter pour choisir un DOSSIER où copier les images sélectionnées";
            System.Threading.Thread t = new System.Threading.Thread(() => MessageBox.Show(helpStr, "Aide", MessageBoxButton.OK, MessageBoxImage.Question));
            t.Start();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Save();
        }
    }

    public class ImageInfo : INotifyPropertyChanged
    {
        public string FileName { get; set; } = string.Empty;
        private bool _selected = false;

        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Selected"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ImageInfo(string fileName)
        {
            FileName = fileName;
        }

        public override string ToString()
        {
            return FileName;
        }

        public override bool Equals(object obj)
        {
            return obj is ImageInfo info &&
                   FileName == info.FileName;
        }

        public override int GetHashCode()
        {
            return 901043656 + EqualityComparer<string>.Default.GetHashCode(FileName);
        }
    }

    [ValueConversion(typeof(ImageInfo), typeof(SolidColorBrush))]
    public class SelectedImageColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is ImageInfo)) return new SolidColorBrush(Colors.Transparent);

            ImageInfo img = value as ImageInfo;
            return img.Selected ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Crimson);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ImageInfoEqualityComparer : IEqualityComparer<ImageInfo>
    {
        public bool Equals(ImageInfo x, ImageInfo y)
        {
            return string.Equals(x.FileName, y.FileName, StringComparison.InvariantCultureIgnoreCase);
        }

        public int GetHashCode(ImageInfo obj)
        {
            return 901043656 + EqualityComparer<string>.Default.GetHashCode(obj.FileName);
        }
    }
}
