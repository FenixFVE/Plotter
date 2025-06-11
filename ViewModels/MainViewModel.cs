using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WpfPlotApp.Models;
using ScottPlot;

namespace WpfPlotApp.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private PlotPoint? _selectedPoint;
    private PlotFunction? _selectedFunction;
    private int _pointCounter = 1;
    private int _functionCounter = 1;
    private double _minX = -10;
    private double _maxX = 10;
    private double _minY = -10;
    private double _maxY = 10;
    private int _precision = 2;
    private bool _lockX = false;
    private bool _lockY = false;
    #pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isLoadingProject = false;
    #pragma warning restore CS0414

    public MainViewModel()
    {
        Points = new ObservableCollection<PlotPoint>();
        SelectedPoints = new ObservableCollection<PlotPoint>();
        Functions = new ObservableCollection<PlotFunction>();
        SelectedFunctions = new ObservableCollection<PlotFunction>();
        
        // Добавляем несколько начальных точек и функций
        AddInitialPoints();
        AddInitialFunctions();
    }

    public ObservableCollection<PlotPoint> Points { get; }
    public ObservableCollection<PlotPoint> SelectedPoints { get; }
    public ObservableCollection<PlotFunction> Functions { get; }
    public ObservableCollection<PlotFunction> SelectedFunctions { get; }

    public PlotPoint? SelectedPoint
    {
        get => _selectedPoint;
        set
        {
            if (SetProperty(ref _selectedPoint, value))
            {
                OnPropertyChanged(nameof(HasSelectedPoint));
                OnPropertyChanged(nameof(HasSelectedPoints));
                OnPropertyChanged(nameof(SelectedPointsCount));
            }
        }
    }

    public PlotFunction? SelectedFunction
    {
        get => _selectedFunction;
        set
        {
            if (SetProperty(ref _selectedFunction, value))
            {
                OnPropertyChanged(nameof(HasSelectedFunction));
                OnPropertyChanged(nameof(HasSelectedFunctions));
                OnPropertyChanged(nameof(SelectedFunctionsCount));
            }
        }
    }

    public bool HasSelectedPoint => SelectedPoint is not null;
    public bool HasSelectedPoints => SelectedPoints.Count > 0;
    public int SelectedPointsCount => SelectedPoints.Count;
    
    public bool HasSelectedFunction => SelectedFunction is not null;
    public bool HasSelectedFunctions => SelectedFunctions.Count > 0;
    public int SelectedFunctionsCount => SelectedFunctions.Count;

    // Свойство для настройки точности
    public int Precision
    {
        get => _precision;
        set
        {
            if (SetProperty(ref _precision, Math.Max(0, Math.Min(10, value))))
            {
                OnPropertyChanged(nameof(FormatString));
            }
        }
    }

    // Строка форматирования для динамической точности
    public string FormatString => $"F{Precision}";



    // Свойства для управления масштабом графика
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

    // Свойства для блокировки осей
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

    public void AddPoint(double x = 0, double y = 0)
    {
        var point = new PlotPoint(GenerateLetterName(_pointCounter++), x, y);
        Points.Add(point);
        SelectedPoint = point;
        ClearSelection();
        AddToSelection(point);
    }

    private string GenerateLetterName(int index)
    {
        string result = "";
        while (index > 0)
        {
            index--; // Делаем индекс начинающимся с 0 для правильного преобразования
            result = (char)('A' + index % 26) + result;
            index /= 26;
        }
        return result;
    }

    public void RemoveSelectedPoint()
    {
        if (SelectedPoint is not null)
        {
            Points.Remove(SelectedPoint);
            SelectedPoints.Remove(SelectedPoint);
            SelectedPoint = Points.FirstOrDefault();
            OnPropertyChanged(nameof(HasSelectedPoints));
            OnPropertyChanged(nameof(SelectedPointsCount));
        }
    }

    public void RemoveSelectedPoints()
    {
        var pointsToRemove = SelectedPoints.ToList();
        foreach (var point in pointsToRemove)
        {
            Points.Remove(point);
        }
        ClearSelection();
        SelectedPoint = Points.FirstOrDefault();
    }

    // Методы для работы с множественным выбором
    public void AddToSelection(PlotPoint point)
    {
        if (!SelectedPoints.Contains(point))
        {
            SelectedPoints.Add(point);
            OnPropertyChanged(nameof(HasSelectedPoints));
            OnPropertyChanged(nameof(SelectedPointsCount));
        }
    }

    public void RemoveFromSelection(PlotPoint point)
    {
        if (SelectedPoints.Remove(point))
        {
            OnPropertyChanged(nameof(HasSelectedPoints));
            OnPropertyChanged(nameof(SelectedPointsCount));
        }
    }

    public void ToggleSelection(PlotPoint point)
    {
        if (SelectedPoints.Contains(point))
        {
            RemoveFromSelection(point);
        }
        else
        {
            AddToSelection(point);
        }
    }

    public void ClearSelection()
    {
        SelectedPoints.Clear();
        OnPropertyChanged(nameof(HasSelectedPoints));
        OnPropertyChanged(nameof(SelectedPointsCount));
    }

    public void SelectSingle(PlotPoint point)
    {
        ClearSelection();
        AddToSelection(point);
        SelectedPoint = point;
    }

    // Групповые операции
    public void MoveSelectedPoints(double deltaX, double deltaY)
    {
        foreach (var point in SelectedPoints)
        {
            point.X += deltaX;
            point.Y += deltaY;
        }
    }

    public void SetSelectedPointsColor(Color color)
    {
        foreach (var point in SelectedPoints)
        {
            point.Color = color;
        }
    }

    public void SetSelectedPointsSize(int size)
    {
        foreach (var point in SelectedPoints)
        {
            point.MarkerSize = size;
        }
    }

    public void SetSelectedPointsShape(MarkerShape shape)
    {
        foreach (var point in SelectedPoints)
        {
            point.MarkerShape = shape;
        }
    }

    public void SetSelectedPointsX(double x)
    {
        foreach (var point in SelectedPoints)
        {
            point.X = x;
        }
    }

    public void SetSelectedPointsY(double y)
    {
        foreach (var point in SelectedPoints)
        {
            point.Y = y;
        }
    }

    // Методы для изменения порядка точек
    public void MovePointUp(PlotPoint point)
    {
        var index = Points.IndexOf(point);
        if (index > 0)
        {
            Points.Move(index, index - 1);
            // Уведомляем о изменении порядка
            OnPropertyChanged(nameof(Points));
        }
    }

    public void MovePointDown(PlotPoint point)
    {
        var index = Points.IndexOf(point);
        if (index >= 0 && index < Points.Count - 1)
        {
            Points.Move(index, index + 1);
            // Уведомляем о изменении порядка
            OnPropertyChanged(nameof(Points));
        }
    }

    public bool CanMovePointUp(PlotPoint point)
    {
        return Points.IndexOf(point) > 0;
    }

    public bool CanMovePointDown(PlotPoint point)
    {
        var index = Points.IndexOf(point);
        return index >= 0 && index < Points.Count - 1;
    }

    // Методы для работы с функциями
    public void AddFunction(string expression = "x")
    {
        var function = new PlotFunction(GenerateFunctionName(_functionCounter++), expression);
        Functions.Add(function);
        SelectedFunction = function;
        ClearFunctionSelection();
        AddToFunctionSelection(function);
    }

    private string GenerateFunctionName(int index)
    {
        return $"f{index}";
    }

    public void RemoveSelectedFunction()
    {
        if (SelectedFunction is not null)
        {
            Functions.Remove(SelectedFunction);
            SelectedFunctions.Remove(SelectedFunction);
            SelectedFunction = Functions.FirstOrDefault();
            OnPropertyChanged(nameof(HasSelectedFunctions));
            OnPropertyChanged(nameof(SelectedFunctionsCount));
        }
    }

    public void RemoveSelectedFunctions()
    {
        var functionsToRemove = SelectedFunctions.ToList();
        foreach (var function in functionsToRemove)
        {
            Functions.Remove(function);
        }
        ClearFunctionSelection();
        SelectedFunction = Functions.FirstOrDefault();
    }

    // Методы для работы с множественным выбором функций
    public void AddToFunctionSelection(PlotFunction function)
    {
        if (!SelectedFunctions.Contains(function))
        {
            SelectedFunctions.Add(function);
            OnPropertyChanged(nameof(HasSelectedFunctions));
            OnPropertyChanged(nameof(SelectedFunctionsCount));
        }
    }

    public void RemoveFromFunctionSelection(PlotFunction function)
    {
        if (SelectedFunctions.Remove(function))
        {
            OnPropertyChanged(nameof(HasSelectedFunctions));
            OnPropertyChanged(nameof(SelectedFunctionsCount));
        }
    }

    public void ToggleFunctionSelection(PlotFunction function)
    {
        if (SelectedFunctions.Contains(function))
        {
            RemoveFromFunctionSelection(function);
        }
        else
        {
            AddToFunctionSelection(function);
        }
    }

    public void ClearFunctionSelection()
    {
        SelectedFunctions.Clear();
        OnPropertyChanged(nameof(HasSelectedFunctions));
        OnPropertyChanged(nameof(SelectedFunctionsCount));
    }

    public void SelectSingleFunction(PlotFunction function)
    {
        ClearFunctionSelection();
        AddToFunctionSelection(function);
        SelectedFunction = function;
    }

    // Групповые операции с функциями
    public void SetSelectedFunctionsColor(Color color)
    {
        foreach (var function in SelectedFunctions)
        {
            function.Color = color;
        }
    }

    public void SetSelectedFunctionsLineWidth(int width)
    {
        foreach (var function in SelectedFunctions)
        {
            function.LineWidth = width;
        }
    }

    public void SetSelectedFunctionsPointCount(int count)
    {
        foreach (var function in SelectedFunctions)
        {
            function.PointCount = count;
        }
    }

    public void SetSelectedFunctionsVisible(bool visible)
    {
        foreach (var function in SelectedFunctions)
        {
            function.IsVisible = visible;
        }
    }

    // Методы для изменения порядка функций
    public void MoveFunctionUp(PlotFunction function)
    {
        var index = Functions.IndexOf(function);
        if (index > 0)
        {
            Functions.Move(index, index - 1);
            OnPropertyChanged(nameof(Functions));
        }
    }

    public void MoveFunctionDown(PlotFunction function)
    {
        var index = Functions.IndexOf(function);
        if (index >= 0 && index < Functions.Count - 1)
        {
            Functions.Move(index, index + 1);
            OnPropertyChanged(nameof(Functions));
        }
    }

    public bool CanMoveFunctionUp(PlotFunction function)
    {
        return Functions.IndexOf(function) > 0;
    }

    public bool CanMoveFunctionDown(PlotFunction function)
    {
        var index = Functions.IndexOf(function);
        return index >= 0 && index < Functions.Count - 1;
    }

    private void AddInitialPoints()
    {
        Points.Add(new PlotPoint("A", 1, 2));
        Points.Add(new PlotPoint("B", 3, 4));
        Points.Add(new PlotPoint("C", -1, -2));
        _pointCounter = 4;
    }

    private void AddInitialFunctions()
    {
        Functions.Add(new PlotFunction("f1", "sin(x)"));
        Functions.Add(new PlotFunction("f2", "f1(x) + x^2"));  // Вложенная функция: f2 использует f1
        Functions.Add(new PlotFunction("f3", "f1(A.X) + f2(x/2)"));  // Сложная вложенная функция
        _functionCounter = 4;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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

    // Методы для сохранения и загрузки состояния программы
    public async Task<bool> SaveToFileAsync(string filePath)
    {
        try
        {
            var projectData = CreateProjectData();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var json = JsonSerializer.Serialize(projectData, options);
            await File.WriteAllTextAsync(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveToFileAsync: ошибка сохранения: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> LoadFromFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;

            var json = await File.ReadAllTextAsync(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var projectData = JsonSerializer.Deserialize<ProjectData>(json, options);
            if (projectData != null)
            {
                LoadProjectData(projectData);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadFromFileAsync: ошибка загрузки: {ex.Message}");
            return false;
        }
    }

    private ProjectData CreateProjectData()
    {
        var projectData = new ProjectData
        {
            PlotSettings = new PlotSettings
            {
                Precision = Precision,
                MinX = MinX,
                MaxX = MaxX,
                MinY = MinY,
                MaxY = MaxY,
                LockX = LockX,
                LockY = LockY
            }
        };

        // Сохраняем точки в том же порядке, что и в списке
        foreach (var point in Points)
        {
            projectData.Points.Add(new PlotPointData
            {
                Name = point.Name,
                X = point.X,
                Y = point.Y,
                XExpression = point.XExpression,
                YExpression = point.YExpression,
                Color = new ColorData(point.Color),
                MarkerSize = point.MarkerSize,
                MarkerShape = point.MarkerShape.ToString(),
                LockName = point.LockName,
                LockX = point.LockX,
                LockY = point.LockY,
                LockSize = point.LockSize,
                LockShape = point.LockShape,
                LockColor = point.LockColor,
                LockXExpression = point.LockXExpression,
                LockYExpression = point.LockYExpression
            });
        }

        // Сохраняем функции в том же порядке, что и в списке
        foreach (var function in Functions)
        {
            projectData.Functions.Add(new PlotFunctionData
            {
                Name = function.Name,
                Expression = function.Expression,
                IsVisible = function.IsVisible,
                MinX = function.MinX,
                MaxX = function.MaxX,
                MinY = function.MinY,
                MaxY = function.MaxY,
                AutoScale = function.AutoScale,
                Color = new ColorData(function.Color),
                LineWidth = function.LineWidth,
                PointCount = function.PointCount,
                ArgumentCount = function.ArgumentCount,
                ArgumentNames = function.ArgumentNames,
                IsImplicitCurve = function.IsImplicitCurve,
                PerformanceMode = function.PerformanceMode,
                LockName = function.LockName,
                LockExpression = function.LockExpression,
                LockMinX = function.LockMinX,
                LockMaxX = function.LockMaxX,
                LockMinY = function.LockMinY,
                LockMaxY = function.LockMaxY,
                LockColor = function.LockColor,
                LockLineWidth = function.LockLineWidth,
                LockPointCount = function.LockPointCount,
                LockArgumentCount = function.LockArgumentCount,
                LockArgumentNames = function.LockArgumentNames,
                IsExpanded = function.IsExpanded
            });
        }

        return projectData;
    }

    private void LoadProjectData(ProjectData projectData)
    {
        System.Diagnostics.Debug.WriteLine("LoadProjectData: НАЧАЛО загрузки проекта");
        _isLoadingProject = true;
        
        try
        {
            // Загружаем настройки графика напрямую в поля, избегая событий PropertyChanged
            _precision = projectData.PlotSettings.Precision;
            _minX = projectData.PlotSettings.MinX;
            _maxX = projectData.PlotSettings.MaxX;
            _minY = projectData.PlotSettings.MinY;
            _maxY = projectData.PlotSettings.MaxY;
            _lockX = projectData.PlotSettings.LockX;
            _lockY = projectData.PlotSettings.LockY;

        // Очищаем текущие данные
        Points.Clear();
        Functions.Clear();
        SelectedPoints.Clear();
        SelectedFunctions.Clear();
        SelectedPoint = null;
        SelectedFunction = null;

        // Загружаем точки в том же порядке
        foreach (var pointData in projectData.Points)
        {
            var point = new PlotPoint(pointData.Name, pointData.X, pointData.Y)
            {
                XExpression = pointData.XExpression,
                YExpression = pointData.YExpression,
                Color = pointData.Color.ToScottPlotColor(),
                MarkerSize = pointData.MarkerSize,
                LockName = pointData.LockName,
                LockX = pointData.LockX,
                LockY = pointData.LockY,
                LockSize = pointData.LockSize,
                LockShape = pointData.LockShape,
                LockColor = pointData.LockColor,
                LockXExpression = pointData.LockXExpression,
                LockYExpression = pointData.LockYExpression
            };

            // Восстанавливаем форму маркера
            if (Enum.TryParse<MarkerShape>(pointData.MarkerShape, out var shape))
            {
                point.MarkerShape = shape;
            }

            Points.Add(point);
        }

        // Загружаем функции в том же порядке
        foreach (var functionData in projectData.Functions)
        {
            var function = new PlotFunction(functionData.Name, functionData.Expression)
            {
                IsVisible = functionData.IsVisible,
                MinX = functionData.MinX,
                MaxX = functionData.MaxX,
                MinY = functionData.MinY,
                MaxY = functionData.MaxY,
                AutoScale = functionData.AutoScale,
                Color = functionData.Color.ToScottPlotColor(),
                LineWidth = functionData.LineWidth,
                PointCount = functionData.PointCount,
                ArgumentCount = functionData.ArgumentCount,
                ArgumentNames = functionData.ArgumentNames,
                IsImplicitCurve = functionData.IsImplicitCurve,
                PerformanceMode = functionData.PerformanceMode,
                LockName = functionData.LockName,
                LockExpression = functionData.LockExpression,
                LockMinX = functionData.LockMinX,
                LockMaxX = functionData.LockMaxX,
                LockMinY = functionData.LockMinY,
                LockMaxY = functionData.LockMaxY,
                LockColor = functionData.LockColor,
                LockLineWidth = functionData.LockLineWidth,
                LockPointCount = functionData.LockPointCount,
                LockArgumentCount = functionData.LockArgumentCount,
                LockArgumentNames = functionData.LockArgumentNames,
                IsExpanded = functionData.IsExpanded
            };

            Functions.Add(function);
        }

        // Обновляем счетчики для генерации новых имен
        _pointCounter = Points.Count + 1;
        _functionCounter = Functions.Count + 1;

            // Уведомляем об изменениях
            OnPropertyChanged(nameof(HasSelectedPoint));
            OnPropertyChanged(nameof(HasSelectedPoints));
            OnPropertyChanged(nameof(SelectedPointsCount));
            OnPropertyChanged(nameof(HasSelectedFunction));
            OnPropertyChanged(nameof(HasSelectedFunctions));
            OnPropertyChanged(nameof(SelectedFunctionsCount));
        }
        finally
        {
            _isLoadingProject = false;
            System.Diagnostics.Debug.WriteLine("LoadProjectData: КОНЕЦ загрузки проекта");
        }
    }
} 