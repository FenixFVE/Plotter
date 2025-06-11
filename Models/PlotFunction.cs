using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ScottPlot;

namespace WpfPlotApp.Models;

public class PlotFunction : INotifyPropertyChanged
{
    private string _name = "f";
    private string _expression = "x";
    private bool _isVisible = true;
    private double _minX = -10;
    private double _maxX = 10;
    private double _minY = -10;
    private double _maxY = 10;
    private bool _autoScale = false;
    private Color _color = Colors.Blue;
    private int _lineWidth = 2;
    private int _pointCount = 100;
    private int _argumentCount = 1;
    private string _argumentNames = "x";
    private bool _isImplicitCurve = false;
    private bool _performanceMode = false;
    
    // Свойства блокировки
    private bool _lockName = false;
    private bool _lockExpression = false;
    private bool _lockMinX = false;
    private bool _lockMaxX = false;
    private bool _lockMinY = false;
    private bool _lockMaxY = false;
    private bool _lockColor = false;
    private bool _lockLineWidth = false;
    private bool _lockPointCount = false;
    private bool _lockArgumentCount = false;
    private bool _lockArgumentNames = false;
    
    // Свойство для сворачивания/разворачивания
    private bool _isExpanded = false;

    public PlotFunction(string name, string expression)
    {
        Name = name;
        Expression = expression;
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Expression
    {
        get => _expression;
        set => SetProperty(ref _expression, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public double MinX
    {
        get => _minX;
        set => SetProperty(ref _minX, value);
    }

    public double MaxX
    {
        get => _maxX;
        set => SetProperty(ref _maxX, value);
    }

    public double MinY
    {
        get => _minY;
        set => SetProperty(ref _minY, value);
    }

    public double MaxY
    {
        get => _maxY;
        set => SetProperty(ref _maxY, value);
    }

    public bool AutoScale
    {
        get => _autoScale;
        set => SetProperty(ref _autoScale, value);
    }

    public Color Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    public int LineWidth
    {
        get => _lineWidth;
        set => SetProperty(ref _lineWidth, Math.Max(1, Math.Min(10, value)));
    }

    public int PointCount
    {
        get => _pointCount;
        set => SetProperty(ref _pointCount, Math.Max(10, Math.Min(10000, value)));
    }

    public int ArgumentCount
    {
        get => _argumentCount;
        set => SetProperty(ref _argumentCount, Math.Max(1, Math.Min(5, value)));
    }

    public string ArgumentNames
    {
        get => _argumentNames;
        set => SetProperty(ref _argumentNames, value);
    }

    public bool IsImplicitCurve
    {
        get => _isImplicitCurve;
        set => SetProperty(ref _isImplicitCurve, value);
    }

    public bool PerformanceMode
    {
        get => _performanceMode;
        set => SetProperty(ref _performanceMode, value);
    }

    // Свойства блокировки полей
    public bool LockName
    {
        get => _lockName;
        set => SetProperty(ref _lockName, value);
    }

    public bool LockExpression
    {
        get => _lockExpression;
        set => SetProperty(ref _lockExpression, value);
    }

    public bool LockMinX
    {
        get => _lockMinX;
        set => SetProperty(ref _lockMinX, value);
    }

    public bool LockMaxX
    {
        get => _lockMaxX;
        set => SetProperty(ref _lockMaxX, value);
    }

    public bool LockMinY
    {
        get => _lockMinY;
        set => SetProperty(ref _lockMinY, value);
    }

    public bool LockMaxY
    {
        get => _lockMaxY;
        set => SetProperty(ref _lockMaxY, value);
    }

    public bool LockColor
    {
        get => _lockColor;
        set => SetProperty(ref _lockColor, value);
    }

    public bool LockLineWidth
    {
        get => _lockLineWidth;
        set => SetProperty(ref _lockLineWidth, value);
    }

    public bool LockPointCount
    {
        get => _lockPointCount;
        set => SetProperty(ref _lockPointCount, value);
    }

    public bool LockArgumentCount
    {
        get => _lockArgumentCount;
        set => SetProperty(ref _lockArgumentCount, value);
    }

    public bool LockArgumentNames
    {
        get => _lockArgumentNames;
        set => SetProperty(ref _lockArgumentNames, value);
    }

    // Свойство для управления сворачиванием
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
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