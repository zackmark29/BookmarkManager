﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Xml.Serialization;
using Prism.Mvvm;

namespace BookmarkManager.Models
{
    [Serializable]
    public class BookmarkStorage : BindableBase
    {
        public ObservableCollection<Bookmark> Bookmarks { get; set; } = new ObservableCollection<Bookmark>();

        public ObservableCollection<string> Categories { get; set; } = new ObservableCollection<string>();



        public void SaveStorage(string fileName)
        {
            var formatter = new XmlSerializer(typeof(BookmarkStorage));
            try
            {
                using (var fs = new FileStream(fileName, FileMode.Create))
                {
                    formatter.Serialize(fs, this);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "File saving error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static BookmarkStorage LoadFromFile(string fileName)
        {
            if (!File.Exists(fileName)) return null;

            try
            {
                var formatter = new XmlSerializer(typeof(BookmarkStorage));
                using (var fs = new FileStream(fileName, FileMode.Open))
                {
                    return ((BookmarkStorage) formatter.Deserialize(fs));
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "File loading error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }
}
