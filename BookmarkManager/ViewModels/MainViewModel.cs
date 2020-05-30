﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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

        #region Public properties

        private bool _hasUnsavedChanges;
        private bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set
            {
                SetProperty(ref _hasUnsavedChanges, value);
                if (_hasUnsavedChanges)
                {
                    MainWindowTitle = "* " + MainWindowTitle;
                }
                else
                {
                    if (MainWindowTitle.StartsWith("* "))
                        MainWindowTitle = MainWindowTitle.Remove(0, 2);
                }
            }
        }

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
                MainWindowTitle = (!string.IsNullOrEmpty(_currentFileName)) ? 
                                WindowTitleBase + " - " + _currentFileName : 
                                WindowTitleBase;
            }
        }

        public ObservableCollection<Bookmark> DisplayingBookmarks { get; set; } = new ObservableCollection<Bookmark>();
        public Configuration Config { get; set; }

        #endregion

        #region Commands

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
        public Command ShowMainWindowCommand { get; }
        public Command AddBookmarkFromClipboardCommand { get; }
        public Command ExitCommand { get; }
        public Command OpenAboutWindowCommand { get; }
        public Command DeleteCategoryCommand { get; }

        #endregion

        #region Constructor

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
            DeleteBookmarkCommand = new Command(DeleteBookmark);
            OpenSettingsWindowCommand = new Command(OpenSettingsWindow);
            ShowMainWindowCommand = new Command(ShowMainWindow);
            AddBookmarkFromClipboardCommand = new Command(AddBookmarkFromClipboard);
            ExitCommand = new Command(Exit);
            OpenAboutWindowCommand = new Command(OpenAboutWindow);
            DeleteCategoryCommand = new Command(DeleteCategory);

            Config = Configuration.LoadFromFile();
            if (string.IsNullOrEmpty(Config.TorBrowserPath))
                TryFindTorBrowser();

            if (Config.StartMinimized)
                Config.MainWindowState = WindowState.Minimized;

            CheckCommandLineArgs();
        }

        #endregion

        #region Private methods

        private void CheckCommandLineArgs()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length <= 1) return;

            if (File.Exists(args[1]))
            {
                OpenBookmarkDb(args[1]);
            }
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

        #endregion

        #region Opening windows commands

        private void ShowMainWindow()
        {
            if (Application.Current.MainWindow?.WindowState == WindowState.Minimized)
                Application.Current.MainWindow.WindowState = WindowState.Normal;

            Application.Current.MainWindow?.Activate();
        }

        private void Exit()
        {
            Application.Current.Shutdown();
        }

        private void OpenAboutWindow()
        {
            var aboutWindow = new AboutView();
            aboutWindow.ShowDialog();
        }

        private void OpenSettingsWindow()
        {
            var settingsWindow = new SettingsView { DataContext = Config };
            var result = settingsWindow.ShowDialog();
            if (result != null && result == true)
            {
                SystemStartup.SetAutorunState(Config.RunOnWidowsStart);
                Config.SaveConfig();
            }
        }

        #endregion

        #region Saving/loading

        private void NewDb()
        {
            //todo: check unsaved changes

            CurrentFileName = "";
            CurrentBookmarkStorage = new BookmarkStorage();
            HasUnsavedChanges = true;

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
            CurrentBookmarkStorage = BookmarkStorage.LoadFromFile(fileName);
            if (CurrentBookmarkStorage == null)
            {
                CurrentFileName = "";
                return;
            }

            if (CurrentBookmarkStorage.Categories.Count > 0)
            {
                CurrentFileName = fileName;
                SelectedCategory = CurrentBookmarkStorage.Categories[0];
                HasUnsavedChanges = false;
            }
        }

        private void SaveDb()
        {
            SaveCurrentBookmarkStorage();
        }

        public void SaveCurrentBookmarkStorage(string header = "")
        {
            if (!HasUnsavedChanges) return;

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
                    CurrentBookmarkStorage?.SaveStorage(CurrentFileName);
                    HasUnsavedChanges = false;
                }
            }
            else
            {
                CurrentBookmarkStorage?.SaveStorage(CurrentFileName);
                HasUnsavedChanges = false;
            }
        }

        #endregion

        #region Working with bookmarks

        private void AddLink()
        {
            if (string.IsNullOrWhiteSpace(UrlText)) return;

            var rawTitle = WebPageParser.GetPageTitle(UrlText);
            var title = Regex.Replace(rawTitle, @"\r\n?|\n", "");
            CurrentBookmarkStorage.Bookmarks.Add(new Bookmark(UrlText, title, DateTime.Now, "", SelectedCategory));
            HasUnsavedChanges = true;
            RefreshCategory();
            SaveCurrentBookmarkStorage();

            UrlText = "";
        }

        private void AddBookmarkFromClipboard()
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (CheckUrlValid(text))
                {
                    UrlText = Clipboard.GetText();
                    AddLink();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckUrlValid(string source)
        {
            return Uri.TryCreate(source, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private void OpenAll()
        {
            if (SelectedCategory == null) return;
            foreach (var bookmark in DisplayingBookmarks)
            {
                Process.Start(bookmark.Url);
            }
        }

        private void DeleteBookmark()
        {
            if (SelectedBookmark == null) return;
            CurrentBookmarkStorage.Bookmarks.Remove(SelectedBookmark);
            HasUnsavedChanges = true;

            RefreshCategory();
            SaveCurrentBookmarkStorage();
        }

        internal void OpenInDefaultBrowser()
        {
            if (SelectedBookmark == null) return;
            Process.Start(SelectedBookmark.Url);
        }

        private void OpenInTorBrowser()
        {
            if (SelectedBookmark == null) return;
            if (!File.Exists(Config.TorBrowserPath))
            {
                MessageBox.Show("Please set path to Tor Browser in File -> Settings", "Tor Browser not found", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Config.TorBrowserPath,
                    Arguments = SelectedBookmark.Url,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(Config.TorBrowserPath) ?? ""
                };

                Process.Start(startInfo);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error opening link in Tor Browser - {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Working with categories

        private void AddCategory()
        {
            if (string.IsNullOrWhiteSpace(CategoryText)) return;

            CurrentBookmarkStorage.Categories.Add(CategoryText);
            HasUnsavedChanges = true;
            SaveCurrentBookmarkStorage();

            CategoryText = "";
        }

        private void RefreshCategory()
        {
            if (_selectedCategory != null)
            {
                //todo: find more elegant way
                DisplayingBookmarks = new ObservableCollection<Bookmark>(CurrentBookmarkStorage.Bookmarks.Where(b => b.Category == _selectedCategory));
                RaisePropertyChanged(nameof(DisplayingBookmarks));
                CategorySelected = true;
            }
            else
            {
                CategorySelected = false;
            }
        }

        private void DeleteCategory()
        {
            if (SelectedCategory == null) return;

            foreach (var bookmark in CurrentBookmarkStorage.Bookmarks.ToArray())
            {
                if (bookmark.Category == SelectedCategory)
                    CurrentBookmarkStorage.Bookmarks.Remove(bookmark);
            }
            CurrentBookmarkStorage.Categories.Remove(SelectedCategory);
            HasUnsavedChanges = true;

            RefreshCategory();
            SaveCurrentBookmarkStorage();
        }

        #endregion
    }
}
