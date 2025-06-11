using System.Collections.Generic;
using ScottPlot;

namespace WpfPlotApp.Models;

// Модели для сериализации состояния программы
public class ProjectData
{
    public string Version { get; set; } = "1.0";
    public PlotSettings PlotSettings { get; set; } = new();
    public List<PlotPointData> Points { get; set; } = new();
    public List<PlotFunctionData> Functions { get; set; } = new();
}

public class PlotSettings
{
    public int Precision { get; set; } = 2;
    public double MinX { get; set; } = -10;
    public double MaxX { get; set; } = 10;
    public double MinY { get; set; } = -10;
    public double MaxY { get; set; } = 10;
    public bool LockX { get; set; } = false;
    public bool LockY { get; set; } = false;
}

public class PlotPointData
{
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public string XExpression { get; set; } = string.Empty;
    public string YExpression { get; set; } = string.Empty;
    
    // Стиль
    public ColorData Color { get; set; } = new();
    public int MarkerSize { get; set; } = 5;
    public string MarkerShape { get; set; } = "FilledCircle";
    
    // Блокировки
    public bool LockName { get; set; } = false;
    public bool LockX { get; set; } = false;
    public bool LockY { get; set; } = false;
    public bool LockSize { get; set; } = false;
    public bool LockShape { get; set; } = false;
    public bool LockColor { get; set; } = false;
    public bool LockXExpression { get; set; } = false;
    public bool LockYExpression { get; set; } = false;
}

public class PlotFunctionData
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public double MinX { get; set; } = -10;
    public double MaxX { get; set; } = 10;
    public double MinY { get; set; } = -10;
    public double MaxY { get; set; } = 10;
    public bool AutoScale { get; set; } = false;
    
    // Стиль
    public ColorData Color { get; set; } = new();
    public int LineWidth { get; set; } = 2;
    public int PointCount { get; set; } = 1000;
    
    // Параметры функции
    public int ArgumentCount { get; set; } = 1;
    public string ArgumentNames { get; set; } = "x";
    public bool IsImplicitCurve { get; set; } = false;
    public bool PerformanceMode { get; set; } = false;
    
    // Блокировки
    public bool LockName { get; set; } = false;
    public bool LockExpression { get; set; } = false;
    public bool LockMinX { get; set; } = false;
    public bool LockMaxX { get; set; } = false;
    public bool LockMinY { get; set; } = false;
    public bool LockMaxY { get; set; } = false;
    public bool LockColor { get; set; } = false;
    public bool LockLineWidth { get; set; } = false;
    public bool LockPointCount { get; set; } = false;
    public bool LockArgumentCount { get; set; } = false;
    public bool LockArgumentNames { get; set; } = false;
    
    // Состояние UI
    public bool IsExpanded { get; set; } = false;
}

public class ColorData
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte A { get; set; } = 255;
    
    public ColorData() { }
    
    public ColorData(Color color)
    {
        R = color.R;
        G = color.G;
        B = color.B;
        A = color.A;
    }
    
    public Color ToScottPlotColor()
    {
        return new Color(R, G, B, A);
    }
} 