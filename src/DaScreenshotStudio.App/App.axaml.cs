using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace DaScreenshotStudio.App;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            if (desktop.Args is [var scene, ..] && File.Exists(scene))
            {
                window.Open(scene);
            }

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
