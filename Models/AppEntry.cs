using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;

namespace AppOpenerAndTimer.Models;

/// <summary>
/// One installed app shown in the checklist, with its selection state and live timer.
/// </summary>
public class AppEntry : INotifyPropertyChanged
{
    public string PackageName { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    private string _status = "Not launched";
    public string Status
    {
        get => _status;
        set { if (_status != value) { _status = value; OnPropertyChanged(); } }
    }

    private string _elapsedText = "00:00";
    public string ElapsedText
    {
        get => _elapsedText;
        set { if (_elapsedText != value) { _elapsedText = value; OnPropertyChanged(); } }
    }

    private Color _timerColor = Colors.Gray;
    public Color TimerColor
    {
        get => _timerColor;
        set { if (_timerColor != value) { _timerColor = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
