﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using BookmarkManager.Models;
using BookmarkManager.MVVM;
using BookmarkManager.Services;
using BookmarkManager.Views;
using Microsoft.Win32;

namespace BookmarkManager.ViewModels
{
    public class MainViewModel : NotificationObject
    {
        private const string WindowTitleBase = "BookmarkManager";


        private bool _bookmarkStorageLoaded;
        public bool BookmarkStorageLoaded
        {
            get => _bookmarkStorageLoaded;
            set => SetProperty(ref _bookmarkStorageLoaded, value);
        }

        private bool _categorySelected;
        public bool CategorySelected
        {
            get => _categorySelected;
            set => SetProperty(ref _categorySelected, value);
        }


        private string _categoryText;
        public string CategoryText
        {
            get => _categoryText;
            set => SetProperty(ref _categoryText, value);
        }

        private string _urlText;
        public string UrlText
        {
            get => _urlText;
            set => SetProperty(ref _urlText, value);
        }

        private string _mainWindowTitle = "BookmarkManager";
        public string MainWindowTitle
        {
            get => _mainWindowTitle;
            set => SetProperty(ref _mainWindowTitle, value);
        }

        private string _selectedCategory;
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    RefreshCategory();
                }
            }
        }

        private Bookmark _selectedBookmark;
        public Bookmark SelectedBookmark
        {
            get => _selectedBookmark;
            set
            {
                if (SetProperty(ref _selectedBookmark, value))
                {
                    if (value != null)
                    {

                    }
                }
            }
        }

        private BookmarkStorage _currentBookmarkStorage;
        public BookmarkStorage CurrentBookmarkStorage
        {
            get => _currentBookmarkStorage;
            set
            {
                if (SetProperty(ref _currentBookmarkStorage, value))
                {
                    BookmarkStorageLoaded = (value != null);
                    DisplayingBookmarks.Clear();
                }
            }
        }

        private string _currentFileName;
        public string CurrentFileName
        {
            get => _currentFileName;
            set
            {
                SetProperty(ref _currentFileName, value);
                MainWindowTitle = WindowTitleBase + " - " + _currentFileName;
            }
        }

        private bool _bookmarkStorageModified;

        public ObservableCollection<Bookmark> DisplayingBookmarks { get; set; } = new ObservableCollection<Bookmark>();
        public Configuration Config { get; set; }

        public Command AddCategoryCommand { get; }
        public Command AddLinkCommand { get; }
        public Command NewDbCommand { get; }
        public Command OpenDbCommand { get; }
        public Command SaveDbCommand { get; }
        public Command OpenInDefaultBrowserCommand { get; }
        public Command OpenInTorBrowserCommand { get; }
        public Command OpenAllCommand { get; }
        public Command DeleteBookmarkCommand { get; }
        public Command OpenSettingsWindowCommand { get; }


        public MainViewModel()
        {
            AddCategoryCommand = new Command(AddCategory);
            AddLinkCommand = new Command(AddLink);
            NewDbCommand = new Command(NewDb);
            OpenDbCommand = new Command(OpenDb);
            SaveDbCommand = new Command(SaveDb);
            OpenInDefaultBrowserCommand = new Command(OpenInDefaultBrowser);
            OpenInTorBrowserCommand = new Command(OpenInTorBrowser);
            OpenAllCommand = new Command(OpenAll);
            DeleteBookmarkCommand= new Command(DeleteBookmark);
            OpenSettingsWindowCommand = new Command(OpenSettingsWindow);

            Config = Configuration.LoadFromFile();
            if (string.IsNullOrEmpty(Config.TorBrowserPath))
                TryFindTorBrowser();

            if (Config.StartMinimized)
                Config.MainWindowState = WindowState.Minimized;

            CheckCommandLineArgs();
        }

        private void TryFindTorBrowser()
        {
            const string torSubPath = "\\Tor Browser\\Browser\\firefox.exe";
            var desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var torPath = desktopFolder + torSubPath;
            if (File.Exists(torPath))
            {
                Config.TorBrowserPath = torPath;
                Config.SaveConfig();
            }
        }

        private void OpenSettingsWindow()
        {
            var settingsWindow = new SettingsView();
            settingsWindow.DataContext = Config;
            var result = settingsWindow.ShowDialog();
            if (result != null && result == true)
            {
                Config.SaveConfig();
            }
        }

        private void OpenAll()
        {
            if (SelectedCategory == null) return;
            foreach (var bookmark in DisplayingBookmarks)
            {
                System.Diagnostics.Process.Start(bookmark.Url);
            }
        }

        private void DeleteBookmark()
        {
            if (SelectedBookmark == null) return;
            CurrentBookmarkStorage.Bookmarks.Remove(SelectedBookmark);

            RefreshCategory();
            SaveCurrentBookmarkStorage();
        }

        private void OpenInDefaultBrowser()
        {
            if (SelectedBookmark == null) return;
            System.Diagnostics.Process.Start(SelectedBookmark.Url);
        }

        private void OpenInTorBrowser()
        {
            if (SelectedBookmark == null) return;
            if (!File.Exists(Config.TorBrowserPath))
            {
                MessageBox.Show($"Please set path to Tor Browser in File -> Settings", "Tor Browser not found", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Config.TorBrowserPath, 
                    Arguments = SelectedBookmark.Url, 
                    UseShellExecute = true, 
                    WorkingDirectory = Path.GetDirectoryName(Config.TorBrowserPath)
                };

                System.Diagnostics.Process.Start(startInfo);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error opening link in Tor Browser - {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckCommandLineArgs()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length <= 1) return;

            if (File.Exists(args[1]))
            {
                OpenBookmarkDb(args[1]);
            }
        }

        private void NewDb()
        {
            //todo: check unsaved changes

            CurrentFileName = "";
            CurrentBookmarkStorage = new BookmarkStorage();
            _bookmarkStorageModified = true;

            SaveCurrentBookmarkStorage("Choose filename for new database");
        }

        private void OpenDb()
        {
            var openFileDialog = new OpenFileDialog()
            {
                Filter = "Bookmarks DB file (*.xml)|*.xml",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
            if (openFileDialog.ShowDialog() == true)
            {
                OpenBookmarkDb(openFileDialog.FileName);
            }
        }

        private void OpenBookmarkDb(string fileName)
        {
            CurrentFileName = fileName;
            CurrentBookmarkStorage = BookmarkStorage.LoadFromFile(CurrentFileName);
            if (CurrentBookmarkStorage.Categories.Count > 0)
            {
                SelectedCategory = CurrentBookmarkStorage.Categories[0];
            }
        }

        private void SaveDb()
        {
            SaveCurrentBookmarkStorage();
        }

        private void SaveCurrentBookmarkStorage(string header = "")
        {
            if (string.IsNullOrEmpty(CurrentFileName))
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Bookmarks DB file (*.xml)|*.xml",
                    InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                if (header != "") saveFileDialog.Title = header;

                if (saveFileDialog.ShowDialog() == true)
                {
                    CurrentFileName = saveFileDialog.FileName;
                    CurrentBookmarkStorage.SaveStorage(CurrentFileName);
                    _bookmarkStorageModified = false;
                }
            }
            else
            {
                CurrentBookmarkStorage.SaveStorage(CurrentFileName);
                _bookmarkStorageModified = false;
            }
        }

        private void AddLink()
        {
            var title = WebPageParser.GetPageTitle(UrlText);
            CurrentBookmarkStorage.Bookmarks.Add(new Bookmark(UrlText, title, DateTime.Now, "", SelectedCategory));
            RefreshCategory();
            SaveCurrentBookmarkStorage();

            UrlText = "";
        }

        private void AddCategory()
        {
            CurrentBookmarkStorage.Categories.Add(CategoryText);
            SaveCurrentBookmarkStorage();

            CategoryText = "";
        }

        private void RefreshCategory()
        {
            if (_selectedCategory != null)
            {
                //todo: find more elegant way
                DisplayingBookmarks = new ObservableCollection<Bookmark>(CurrentBookmarkStorage.Bookmarks.Where(b => b.Category == _selectedCategory));
                RaisePropertyChanged("DisplayingBookmarks");
                CategorySelected = true;
            }
            else
            {
                CategorySelected = false;
            }
        }
    }
}
