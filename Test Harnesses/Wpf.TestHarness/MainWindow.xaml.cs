﻿namespace Wpf.TestHarness
{
    using System;
    using System.Windows;
    using Simple.Wpf.Themes;

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ThemeControl.Scope = ReplWindow;
            ThemeControl.ItemsSource = new[]
            {
                new Theme("Default theme", null), 
                new Theme("Blue theme", new Uri("/Simple.Wpf.Terminal.Themes;component/BlueTheme.xaml", UriKind.Relative)), 
                new Theme("Dark theme", new Uri("/Simple.Wpf.Terminal.Themes;component/DarkTheme.xaml", UriKind.Relative)), 
                new Theme("Wingding theme", new Uri("/Wpf.TestHarness;component/Themes/WingdingTheme.xaml", UriKind.Relative)), 
            };
        }
    }
}
