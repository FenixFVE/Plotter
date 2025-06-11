using System.ComponentModel;
using System.Runtime.CompilerServices;
using ScottPlot;

namespace WpfPlotApp.Models;

public class PlotPoint : INotifyPropertyChanged
{
    private double _x;
    private double _y;
    private string _name = "Точка";
    private int _markerSize = 12;
    private MarkerShape _markerShape = MarkerShape.FilledCircle;
    private Color _color = Colors.Red;
    
    // Свойства блокировки
    private bool _lockX = false;
    private bool _lockY = false;
    private bool _lockName = false;
    private bool _lockSize = false;
    private bool _lockShape = false;
    private bool _lockColor = false;
    private bool _lockXExpression = false;
    private bool _lockYExpression = false;
    
    // Свойство для сворачивания/разворачивания
    private bool _isExpanded = false;
    
    // Выражения для вычисления координат
    private string _xExpression = "";
    private string _yExpression = "";

    public PlotPoint(string name, double x, double y)
    {
        Name = name;
        X = x;
        Y = y;
    }

    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int MarkerSize
    {
        get => _markerSize;
        set => SetProperty(ref _markerSize, value);
    }

    public MarkerShape MarkerShape
    {
        get => _markerShape;
        set => SetProperty(ref _markerShape, value);
    }

    public Color Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    // Свойства блокировки полей
    public bool LockX
    {
        get => _lockX;
        set => SetProperty(ref _lockX, value);
    }

    public bool LockY
    {
        get => _lockY;
        set => SetProperty(ref _lockY, value);
    }

    public bool LockName
    {
        get => _lockName;
        set => SetProperty(ref _lockName, value);
    }

    public bool LockSize
    {
        get => _lockSize;
        set => SetProperty(ref _lockSize, value);
    }

    public bool LockShape
    {
        get => _lockShape;
        set => SetProperty(ref _lockShape, value);
    }

    public bool LockColor
    {
        get => _lockColor;
        set => SetProperty(ref _lockColor, value);
    }

    public bool LockXExpression
    {
        get => _lockXExpression;
        set => SetProperty(ref _lockXExpression, value);
    }

    public bool LockYExpression
    {
        get => _lockYExpression;
        set => SetProperty(ref _lockYExpression, value);
    }

    // Свойство для управления сворачиванием
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    // Выражения для координат
    public string XExpression
    {
        get => _xExpression;
        set => SetProperty(ref _xExpression, value);
    }

    public string YExpression
    {
        get => _yExpression;
        set => SetProperty(ref _yExpression, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
} 