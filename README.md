Simple.Wpf.FSharp.Repl
======================

A simple F# REPL engine for use in a WPF application. Mimics the F# Interactive console application inside a WPF user control. Currently based on the open source F# 3.1 Interactive process.

The control is available as a nuget [package](https://www.nuget.org/packages/Simple.Wpf.FSharp.Repl/)

Example usages of the control, with styles applied dynamically:

![alt text](https://raw.github.com/oriches/Simple.Wpf.FSharp.Repl/master/Readme%20Images/examples.png "Example usage using 2 different themes")


The repo contains 2 test harnesses, one code-behind and the other an MVVM implementation, these are detailed below:

### Code behind implementation

```
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ReplWindow.DataContext = new ReplWindowController("let ollie = 1337;;").ViewModel;
    }
}
```
XAML:
```
<v:ReplWindow x:Name="ReplWindow" />
```

### MVVM implementation

```
public sealed class MainViewModel
{
    private readonly IReplWindowController _controller;

    public MainViewModel()
    {
        _controller = new ReplWindowController("let ollie = 1337;;");
    }

    public IReplWindowViewModel Content { get { return _controller.ViewModel; } }
}
```

XAML:
```
<v:ReplWindow x:Name="ReplWindow"
              Grid.Row="1"
              DataContext="{Binding Path=Content, Mode=OneWay}" />
```


### Other documentation

There is a set of blog posts which detail the journey of creating this control & nuget package - [part 1](http://awkwardcoder.blogspot.co.uk/2013/12/simple-f-repl-in-wpf-part-1.html), [part 2](http://awkwardcoder.blogspot.co.uk/2013/12/simple-f-repl-in-wpf-part-2.html), [part 3](http://awkwardcoder.blogspot.co.uk/2013/12/simple-f-repl-in-wpf-part-3.html) & [part 4] (http://awkwardcoder.blogspot.co.uk/2013/12/simple-f-repl-in-wpf-part-4.html).
