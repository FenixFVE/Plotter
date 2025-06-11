using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WpfPlotApp.Models;
using WpfPlotApp.ViewModels;
using MathExpressionParser;
using IsolinePlotting;

namespace WpfPlotApp;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Dictionary<PlotPoint, Scatter> _pointToScatterMap;
    private readonly Dictionary<PlotFunction, ScottPlot.Plottables.Signal> _functionToSignalMap;
    private readonly Dictionary<PlotFunction, List<ScottPlot.Plottables.Scatter>> _functionToImplicitMap;
    private readonly Dictionary<string, List<List<IsolinePlotting.Point>>> _implicitCurveCache;
    private readonly MathParser _mathParser;
    private bool _isUpdatingFromPlot { get; set; } = false;
    private bool _isUpdatingSelection = false;
    private bool _isUpdatingAxisLimits = false;
    private bool _isUpdatingPrecision = false;
    private bool _isReorderingPoints = false;
    private bool _isReorderingFunctions = false;
    private bool _isUpdatingFromExpressions = false;
    private bool _isUpdatingAutoScale = false;
    private bool _isLoadingUI = false;
    private bool _isLoadingProject = false;
    private DispatcherTimer? _blinkTimer;
    private DispatcherTimer? _axisUpdateTimer;
    private bool _isBlinkVisible = true;

    public MainWindow()
    {
        InitializeComponent();

        //PlotControl.Interaction.IsEnabled = false;
        PlotControl.UserInputProcessor.IsEnabled = true;

        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _pointToScatterMap = new Dictionary<PlotPoint, Scatter>();
        _functionToSignalMap = new Dictionary<PlotFunction, ScottPlot.Plottables.Signal>();
        _functionToImplicitMap = new Dictionary<PlotFunction, List<ScottPlot.Plottables.Scatter>>();
        _implicitCurveCache = new Dictionary<string, List<List<IsolinePlotting.Point>>>();
        _mathParser = new MathParser();

        InitializePlot();
        SetupEventHandlers();
        UpdatePlotFromViewModel();
        UpdateAxisLockRules(); // Изначально применяем правила блокировки
        SetupBlinkTimer();
        SetupAxisUpdateTimer();
    }

    private void InitializePlot()
    {
        // Настройка графика
        PlotControl.Plot.Title("Редактор Графиков");
        PlotControl.Plot.XLabel("X");
        PlotControl.Plot.YLabel("Y");

        // Включаем сетку
        //PlotControl.Plot.Grid.MajorLineWidth = 1;
        //PlotControl.Plot.Grid.MinorLineWidth = 0.5f;

        /*
        ScottPlot.Plottables.FloatingAxis floatingX = new(PlotControl.Plot.Axes.Bottom);
        ScottPlot.Plottables.FloatingAxis floatingY = new(PlotControl.Plot.Axes.Left);
        //PlotControl.Plot.Axes.Frameless();
        PlotControl.Plot.Add.Plottable(floatingX);
        PlotControl.Plot.Add.Plottable(floatingY);
        */

        // Автоматическое масштабирование
        
        //PlotControl.Plot.Axes.SquareUnits();
        PlotControl.Plot.Axes.AutoScale();
        var vertical_line = PlotControl.Plot.Add.VerticalLine(0.0);
        vertical_line.Color = ScottPlot.Colors.Black;
        vertical_line.LineWidth = 0.5f;
        var horizontal_line = PlotControl.Plot.Add.HorizontalLine(0.0);
        horizontal_line.Color = ScottPlot.Colors.Black;
        horizontal_line.LineWidth = 0.5f;

        // Обработчики событий мыши для перетаскивания точек
        PlotControl.MouseMove += PlotControl_MouseMove;
        PlotControl.MouseDown += PlotControl_MouseDown;
        PlotControl.MouseUp += PlotControl_MouseUp;
    }

    private void SetupEventHandlers()
    {
        // Подписываемся на изменения в коллекции точек
        _viewModel.Points.CollectionChanged += Points_CollectionChanged;

        // Подписываемся на изменения в коллекции функций
        _viewModel.Functions.CollectionChanged += Functions_CollectionChanged;

        // Подписываемся на изменения выбранной точки
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        
        // Начальные значения масштаба будут установлены таймером
    }

    private void Points_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_isUpdatingFromPlot || _isReorderingPoints) return;

        if (e.NewItems is not null)
        {
            foreach (PlotPoint point in e.NewItems)
            {
                AddPointToPlot(point);
                point.PropertyChanged += Point_PropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (PlotPoint point in e.OldItems)
            {
                RemovePointFromPlot(point);
                point.PropertyChanged -= Point_PropertyChanged;
            }
        }

        PlotControl.Refresh();
    }

    private void Functions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_isUpdatingFromPlot) return;

        // Обрабатываем событие Move отдельно, чтобы избежать создания фантомных копий
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
        {
            // При перемещении не нужно ничего добавлять или удалять с графика
            // Только обновляем регистрацию функций в парсере для корректного порядка зависимостей
            System.Diagnostics.Debug.WriteLine($"Functions_CollectionChanged: перемещение функции, обновляем регистрацию");
            return;
        }

        if (e.NewItems is not null)
        {
            foreach (PlotFunction function in e.NewItems)
            {
                AddFunctionToPlot(function);
                function.PropertyChanged += Function_PropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (PlotFunction function in e.OldItems)
            {
                RemoveFunctionFromPlot(function);
                function.PropertyChanged -= Function_PropertyChanged;
            }
        }

        PlotControl.Refresh();
    }

    private void Function_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not PlotFunction function) return;
        if (_isLoadingUI || _isUpdatingSelection || _isReorderingFunctions || _isLoadingProject) return; // Защита от обновлений во время загрузки UI, изменения порядка или загрузки проекта
        
        // Очищаем кэш неявных кривых при изменении функций
        _implicitCurveCache.Clear();
        
        // Просто всегда обновляем всё при любом изменении функции
        UpdateAllFunctions();
        UpdateAllExpressions();
        StartBlinking();
    }

    private void Point_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("Point_PropertyChanged");
        if (sender is not PlotPoint point) return;
        if (_isLoadingUI || _isUpdatingSelection || _isLoadingProject) return; // Защита от обновлений во время загрузки
        System.Diagnostics.Debug.WriteLine("Point_PropertyChanged 2");
        
        // Очищаем кэш неявных кривых при изменении точек
        _implicitCurveCache.Clear();
        
        // Просто всегда обновляем всё при любом изменении точки
        UpdatePlotPointPosition(point);
        UpdateAllExpressions();
        UpdateAllFunctions();
        StartBlinking();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedPoint) || 
            e.PropertyName == nameof(MainViewModel.HasSelectedPoints) ||
            e.PropertyName == nameof(MainViewModel.SelectedPointsCount) ||
            e.PropertyName == nameof(MainViewModel.SelectedFunction) ||
            e.PropertyName == nameof(MainViewModel.HasSelectedFunctions) ||
            e.PropertyName == nameof(MainViewModel.SelectedFunctionsCount))
        {
            if (!_isReorderingPoints)
            {
                // Сначала остановим мигание для всех элементов, чтобы вернуть их в исходное состояние
                RestoreAllOpacity();
                HighlightSelectedElements();
                SyncListBoxSelection();
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.MinX) ||
                 e.PropertyName == nameof(MainViewModel.MaxX) ||
                 e.PropertyName == nameof(MainViewModel.MinY) ||
                 e.PropertyName == nameof(MainViewModel.MaxY))
        {
            // Обновляем границы графика при изменении свойств масштаба
            if (!_isUpdatingAxisLimits)
            {
                _isUpdatingAxisLimits = true;
                PlotControl.Plot.Axes.SetLimits(_viewModel.MinX, _viewModel.MaxX, _viewModel.MinY, _viewModel.MaxY);
                PlotControl.Refresh();
                _isUpdatingAxisLimits = false;
                
                // Обновляем функции с автомасштабированием при ручном изменении границ экрана
                if (!_isUpdatingAutoScale)
                {
                    UpdateFunctionsWithAutoScale();
                }
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.LockX) ||
                 e.PropertyName == nameof(MainViewModel.LockY))
        {
            // Обновляем правила блокировки осей
            UpdateAxisLockRules();
        }
        else if (e.PropertyName == nameof(MainViewModel.Precision))
        {
            // При изменении точности просто обновляем отображение без дополнительной логики
            // Все поля автоматически обновятся через MultiBinding с PrecisionConverter
        }
    }

    private void SyncListBoxSelection()
    {
        if (_isUpdatingSelection) return;
        
        _isUpdatingSelection = true;
        _isLoadingUI = true; // Защищаем от обновлений во время синхронизации UI
        
        // Синхронизируем выбор точек в ListBox с ViewModel
        PointsListBox.SelectedItems.Clear();
        foreach (var point in _viewModel.SelectedPoints)
        {
            PointsListBox.SelectedItems.Add(point);
        }
        
        if (_viewModel.SelectedPoint is not null)
        {
            PointsListBox.SelectedItem = _viewModel.SelectedPoint;
        }

        // Синхронизируем выбор функций в ListBox с ViewModel
        FunctionsListBox.SelectedItems.Clear();
        foreach (var function in _viewModel.SelectedFunctions)
        {
            FunctionsListBox.SelectedItems.Add(function);
        }
        
        if (_viewModel.SelectedFunction is not null)
        {
            FunctionsListBox.SelectedItem = _viewModel.SelectedFunction;
        }
        
        _isLoadingUI = false;
        _isUpdatingSelection = false;
    }

    private void AddPointToPlot(PlotPoint point)
    {
        var scatter = PlotControl.Plot.Add.Scatter(new double[] { point.X }, new double[] { point.Y });
        scatter.MarkerSize = point.MarkerSize;
        scatter.MarkerShape = point.MarkerShape;
        scatter.Color = point.Color;
        scatter.LineWidth = 0; // Убираем линии между точками

        _pointToScatterMap[point] = scatter;
    }

    private void RemovePointFromPlot(PlotPoint point)
    {
        if (_pointToScatterMap.TryGetValue(point, out var scatter))
        {
            PlotControl.Plot.Remove(scatter);
            _pointToScatterMap.Remove(point);
        }
    }

    private void UpdatePlotPointPosition(PlotPoint point)
    {
        if (_pointToScatterMap.TryGetValue(point, out var scatter))
        {
            // Удаляем старую точку и создаем новую с обновленными свойствами
            PlotControl.Plot.Remove(scatter);
            var newScatter = PlotControl.Plot.Add.Scatter(new double[] { point.X }, new double[] { point.Y });
            newScatter.MarkerSize = point.MarkerSize;
            newScatter.MarkerShape = point.MarkerShape;
            newScatter.Color = point.Color;
            newScatter.LineWidth = 0;
            _pointToScatterMap[point] = newScatter;
            PlotControl.Refresh();
        }
    }

    private void UpdatePlotFromViewModel()
    {
        foreach (var point in _viewModel.Points)
        {
            AddPointToPlot(point);
            point.PropertyChanged += Point_PropertyChanged;
        }
        
        foreach (var function in _viewModel.Functions)
        {
            AddFunctionToPlot(function);
            function.PropertyChanged += Function_PropertyChanged;
        }
        
        PlotControl.Refresh();
    }

    private void AddFunctionToPlot(PlotFunction function)
    {
        try
        {
            var scatter = CreateFunctionSignal(function);
            if (scatter != null)
            {
                _functionToSignalMap[function] = scatter;
                System.Diagnostics.Debug.WriteLine($"AddFunctionToPlot: добавлена функция {function.Name}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddFunctionToPlot: ошибка при добавлении функции {function.Name}: {ex.Message}");
        }
    }

    private void RemoveFunctionFromPlot(PlotFunction function)
    {
        // Удаляем обычные функции (Signal)
        if (_functionToSignalMap.TryGetValue(function, out var signal))
        {
            PlotControl.Plot.Remove(signal);
            _functionToSignalMap.Remove(function);
            System.Diagnostics.Debug.WriteLine($"RemoveFunctionFromPlot: удалена обычная функция {function.Name}");
        }
        
        // Удаляем неявные кривые (Scatter)
        if (_functionToImplicitMap.TryGetValue(function, out var curves))
        {
            foreach (var curve in curves)
            {
                PlotControl.Plot.Remove(curve);
            }
            _functionToImplicitMap.Remove(function);
            System.Diagnostics.Debug.WriteLine($"RemoveFunctionFromPlot: удалена неявная кривая {function.Name}");
        }
    }

    private void UpdatePlotFunction(PlotFunction function)
    {
        System.Diagnostics.Debug.WriteLine($"UpdatePlotFunction: обновление функции {function.Name}, IsVisible={function.IsVisible}");
        
        // Удаляем все старые представления функции
        RemoveFunctionFromPlot(function);
        
        // Если функция видимая, создаем новое представление
        if (function.IsVisible)
        {
            try
            {
                var newSignal = CreateFunctionSignal(function);
                if (newSignal != null)
                {
                    _functionToSignalMap[function] = newSignal;
                    System.Diagnostics.Debug.WriteLine($"UpdatePlotFunction: создан новый сигнал для {function.Name}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"UpdatePlotFunction: создана неявная кривая для {function.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdatePlotFunction: ошибка при создании функции {function.Name}: {ex.Message}");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"UpdatePlotFunction: функция {function.Name} невидимая");
        }
        
        PlotControl.Refresh();
    }

    private void UpdateFunctionsWithAutoScale()
    {
        if (_isUpdatingAutoScale) return; // Предотвращаем циклические обновления
        
        _isUpdatingAutoScale = true;
        try
        {
            // Получаем текущие границы графика
            var currentLimits = PlotControl.Plot.Axes.GetLimits();
            double currentMinX = currentLimits.Left;
            double currentMaxX = currentLimits.Right;
            
            // Обновляем все функции с включенным автомасштабированием
            foreach (var function in _viewModel.Functions.Where(f => f.AutoScale))
            {
                // Обновляем диапазон функции в соответствии с экраном с правильной точностью
                function.MinX = Math.Round(currentMinX, _viewModel.Precision);
                function.MaxX = Math.Round(currentMaxX, _viewModel.Precision);
                
                // UpdatePlotFunction будет вызван автоматически через PropertyChanged
            }
        }
        finally
        {
            _isUpdatingAutoScale = false;
        }
    }

    private ScottPlot.Plottables.Signal? CreateFunctionSignal(PlotFunction function)
    {
        if (!function.IsVisible) return null;

        try
        {
            // Регистрируем переменные и функции в парсере
            RegisterAllVariablesAndFunctions();

            // Проверяем, нужно ли использовать алгоритм неявных кривых
            if (function.IsImplicitCurve)
            {
                // Используем алгоритм неявных кривых только если установлена галочка
                CreateImplicitCurve(function);
                return null; // Неявные кривые не используют Signal
            }
            else if (function.ArgumentCount == 1)
            {
                // Обычная функция одной переменной
                return CreateStandardFunctionSignal(function);
            }
            else
            {
                // Функции от нескольких переменных без галочки "неявная кривая" - не отображаем
                System.Diagnostics.Debug.WriteLine($"CreateFunctionSignal: функция {function.Name} с {function.ArgumentCount} аргументами требует включения режима 'Неявная кривая'");
                return null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateFunctionSignal: ошибка создания сигнала для функции {function.Name}: {ex.Message}");
            return null;
        }
    }

    private ScottPlot.Plottables.Signal? CreateStandardFunctionSignal(PlotFunction function)
    {
        // Определяем диапазон X
        double minX = function.AutoScale ? _viewModel.MinX : function.MinX;
        double maxX = function.AutoScale ? _viewModel.MaxX : function.MaxX;
        
        var pointCount = function.PointCount;
        var step = (maxX - minX) / (pointCount - 1);
        
        var yValues = new double[pointCount];
        var argumentNames = GetArgumentNamesArray(function);
        var primaryArg = argumentNames[0]; // Главный аргумент (обычно x)

        // Вычисляем значения функции для каждой точки X
        for (int i = 0; i < pointCount; i++)
        {
            var x = minX + i * step;
            
            try
            {
                // Устанавливаем главный аргумент как константу
                _mathParser.SetConstant(primaryArg, x);
                
                var compiledExpression = _mathParser.Parse<Func<double>>(function.Expression);
                if (compiledExpression != null)
                {
                    yValues[i] = compiledExpression();
                    
                    // Проверяем на NaN или бесконечность
                    if (double.IsNaN(yValues[i]) || double.IsInfinity(yValues[i]))
                    {
                        yValues[i] = double.NaN;
                    }
                }
                else
                {
                    yValues[i] = double.NaN;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateStandardFunctionSignal: ошибка вычисления в точке {primaryArg}={x} для функции {function.Name}: {ex.Message}");
                yValues[i] = double.NaN;
            }
        }

        // Используем Signal Plot для лучшей производительности
        var signal = PlotControl.Plot.Add.Signal(yValues);
        signal.Color = function.Color;
        signal.LineWidth = function.LineWidth;
        
        // Настройка данных Signal для корректного отображения диапазона X
        signal.Data.XOffset = minX;  // Начальная точка X
        signal.Data.Period = step;   // Шаг между точками X

        System.Diagnostics.Debug.WriteLine($"CreateStandardFunctionSignal: успешно создан сигнал для функции {function.Name}({primaryArg})");
        return signal;
    }

    private void CreateImplicitCurve(PlotFunction function)
    {
        try
        {
            // Очищаем предыдущие кривые для этой функции
            if (_functionToImplicitMap.TryGetValue(function, out var oldCurves))
            {
                foreach (var curve in oldCurves)
                {
                    PlotControl.Plot.Remove(curve);
                }
            }

            // Определяем диапазоны
            double minX = function.AutoScale ? _viewModel.MinX : function.MinX;
            double maxX = function.AutoScale ? _viewModel.MaxX : function.MaxX;
            double minY = function.AutoScale ? _viewModel.MinY : function.MinY;
            double maxY = function.AutoScale ? _viewModel.MaxY : function.MaxY;

            // Создаем функцию для изолинии (ищем где f(x,y) = 0)
            IsolinePlotting.Func implicitFunc = (double[] point) =>
            {
                try
                {
                    var argumentNames = GetArgumentNamesArray(function);
                    
                    // Устанавливаем аргументы как константы
                    for (int i = 0; i < Math.Min(argumentNames.Length, point.Length); i++)
                    {
                        _mathParser.SetConstant(argumentNames[i], point[i]);
                    }

                    var compiledExpression = _mathParser.Parse<Func<double>>(function.Expression);
                    if (compiledExpression != null)
                    {
                        return compiledExpression();
                    }
                    return double.NaN;
                }
                catch
                {
                    return double.NaN;
                }
            };

            // Создаем ключ кэша на основе параметров функции
            var cacheKey = $"{function.Expression}_{minX}_{maxX}_{minY}_{maxY}_{function.PointCount}_{function.PerformanceMode}_{string.Join(",", GetArgumentNamesArray(function))}";
            
            List<List<IsolinePlotting.Point>> isolines;
            
            // Проверяем кэш
            if (_implicitCurveCache.TryGetValue(cacheKey, out var cachedIsolines))
            {
                isolines = cachedIsolines;
                System.Diagnostics.Debug.WriteLine($"CreateImplicitCurve: используется кэш для функции {function.Name}");
            }
            else
            {
                // Вычисляем изолинии используя ваш алгоритм с оптимизированными параметрами
                var pmin = new IsolinePlotting.Point(minX, minY);
                var pmax = new IsolinePlotting.Point(maxX, maxY);
                
                // Параметры в зависимости от режима производительности
                int adaptiveDepth, maxQuads;
                
                if (function.PerformanceMode)
                {
                    // Режим производительности: очень низкие значения для быстрой отрисовки
                    adaptiveDepth = Math.Max(1, Math.Min(3, function.PointCount / 500));
                    maxQuads = Math.Max(100, Math.Min(function.PointCount, 2000));
                    System.Diagnostics.Debug.WriteLine($"CreateImplicitCurve: режим производительности для {function.Name} (depth={adaptiveDepth}, quads={maxQuads})");
                }
                else
                {
                    // Обычный режим: адаптивные параметры для баланса качества и производительности
                    adaptiveDepth = Math.Max(3, Math.Min(6, function.PointCount / 200));
                    maxQuads = Math.Max(1000, Math.Min(function.PointCount * 5, 20000));
                    System.Diagnostics.Debug.WriteLine($"CreateImplicitCurve: обычный режим для {function.Name} (depth={adaptiveDepth}, quads={maxQuads})");
                }
                
                System.Diagnostics.Debug.WriteLine($"CreateImplicitCurve: начинаем вычисления для функции {function.Name}");
                
                isolines = IsolinePlotter.PlotIsoline(
                    implicitFunc, 
                    pmin, 
                    pmax,
                    minDepth: adaptiveDepth,
                    maxQuads: maxQuads
                );
                
                // Сохраняем в кэш (ограничиваем размер кэша)
                if (_implicitCurveCache.Count > 50)
                {
                    var firstKey = _implicitCurveCache.Keys.First();
                    _implicitCurveCache.Remove(firstKey);
                }
                _implicitCurveCache[cacheKey] = isolines;
                
                System.Diagnostics.Debug.WriteLine($"CreateImplicitCurve: вычислена и кэширована функция {function.Name}");
            }

            // Преобразуем изолинии в Scatter plots
            var scatterPlots = new List<ScottPlot.Plottables.Scatter>();
            
            foreach (var line in isolines)
            {
                if (line.Count >= 2)
                {
                    var xValues = line.Select(p => p[0]).ToArray();
                    var yValues = line.Select(p => p[1]).ToArray();
                    
                    var scatter = PlotControl.Plot.Add.Scatter(xValues, yValues);
                    scatter.Color = function.Color;
                    scatter.LineWidth = function.LineWidth;
                    scatter.MarkerSize = 0; // Только линии, без точек
                    
                    scatterPlots.Add(scatter);
                }
            }

            // Сохраняем ссылки на созданные кривые
            _functionToImplicitMap[function] = scatterPlots;
            
            System.Diagnostics.Debug.WriteLine($"CreateImplicitCurve: создано {scatterPlots.Count} кривых для функции {function.Name}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateImplicitCurve: ошибка создания неявной кривой для функции {function.Name}: {ex.Message}");
        }
    }

    private void RegisterAllVariablesAndFunctions()
    {
        // Очищаем все старые определения
        _mathParser.Clear();
        
        // Регистрируем точки как константы
        foreach (var point in _viewModel.Points)
        {
            var xVarName = $"{point.Name}.X";
            var yVarName = $"{point.Name}.Y";
            
            _mathParser.SetConstant(xVarName, point.X);
            _mathParser.SetConstant(yVarName, point.Y);
        }
        
        // Регистрируем функции как именованные функции для поддержки вложенных вызовов
        foreach (var function in _viewModel.Functions)
        {
            try
            {
                // Создаем определение функции с правильным количеством параметров
                var argumentNames = GetArgumentNamesArray(function);
                var argumentList = string.Join(",", argumentNames);
                var functionDefinition = $"{function.Name}({argumentList}) = {function.Expression}";
                
                _mathParser.AddDefinition(functionDefinition);
                System.Diagnostics.Debug.WriteLine($"RegisterAllVariablesAndFunctions: зарегистрирована функция {function.Name}({argumentList})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RegisterAllVariablesAndFunctions: ошибка регистрации функции {function.Name}: {ex.Message}");
            }
        }
    }

    private string[] GetArgumentNamesArray(PlotFunction function)
    {
        // Разбираем строку имен аргументов на массив
        if (string.IsNullOrWhiteSpace(function.ArgumentNames))
        {
            // Если имена не заданы, используем стандартные
            var defaultNames = new[] { "x", "y", "z", "u", "v" };
            return defaultNames.Take(function.ArgumentCount).ToArray();
        }
        
        var names = function.ArgumentNames.Split(',')
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
            
        // Если количество имен не совпадает с количеством аргументов, дополняем стандартными
        if (names.Length != function.ArgumentCount)
        {
            var defaultNames = new[] { "x", "y", "z", "u", "v" };
            var result = new string[function.ArgumentCount];
            
            for (int i = 0; i < function.ArgumentCount; i++)
            {
                if (i < names.Length)
                    result[i] = names[i];
                else
                    result[i] = defaultNames[Math.Min(i, defaultNames.Length - 1)];
            }
            
            return result;
        }
        
        return names;
    }

    private void HighlightSelectedElements()
    {
        // Запускаем мигание для всех выбранных элементов (точки и функции)
        if (_viewModel.SelectedPoints.Count > 0 || _viewModel.SelectedFunctions.Count > 0)
        {
            StartBlinking();
        }
        else
        {
            StopBlinking();
        }
    }

    // Обработка перетаскивания точек
    private Scatter? _draggedScatter;
    private PlotPoint? _draggedPoint;
    private List<PlotPoint> _draggedPoints = new();
    private Dictionary<PlotPoint, (double OriginalX, double OriginalY)> _dragStartPositions = new();

    private void PlotControl_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var mousePixel = GetScaledMousePosition(e);
            var mouseCoordinate = PlotControl.Plot.GetCoordinates((float)mousePixel.X, (float)mousePixel.Y);

            // Сначала ищем ближайшую точку
            var closestPoint = FindClosestPoint(mouseCoordinate.X, mouseCoordinate.Y);
            
            // Если точка не найдена, ищем ближайшую функцию
            var closestFunction = closestPoint == null ? FindClosestFunction(mouseCoordinate.X, mouseCoordinate.Y) : null;
            
            if (closestPoint is not null)
            {
                bool isCtrlPressed = Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) || Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);
                
                // Снимаем выделение с функций при выборе точки только если не зажат Ctrl
                if (!isCtrlPressed)
                {
                    _viewModel.ClearFunctionSelection();
                }
                
                if (isCtrlPressed)
                {
                    // Множественный выбор с Ctrl
                    _viewModel.ToggleSelection(closestPoint);
                    if (_viewModel.SelectedPoints.Contains(closestPoint))
                    {
                        _viewModel.SelectedPoint = closestPoint;
                    }
                }
                else
                {
                    // Обычный выбор: если точка уже выбрана в группе, не разрушаем группу
                    if (_viewModel.SelectedPoints.Contains(closestPoint))
                    {
                        // Точка уже выбрана в группе - просто делаем её основной для перетаскивания
                        _viewModel.SelectedPoint = closestPoint;
                    }
                    else
                    {
                        // Точка не выбрана - делаем одиночный выбор
                        _viewModel.SelectSingle(closestPoint);
                    }
                }

                // Подготовка к перетаскиванию - разрешаем тащить только если хотя бы одна координата не заблокирована
                _draggedPoints = _viewModel.SelectedPoints.ToList();
                _dragStartPositions.Clear();
                
                // Проверяем, есть ли точки которые можно перетаскивать (хотя бы по одной оси)
                var movablePoints = _draggedPoints.Where(p => !p.LockX || !p.LockY).ToList();
                
                if (movablePoints.Count > 0)
                {
                    foreach (var point in _draggedPoints)
                    {
                        _dragStartPositions[point] = (point.X, point.Y);
                    }
                    
                    _draggedPoint = closestPoint;
                    _draggedScatter = _pointToScatterMap[closestPoint];
                    PlotControl.UserInputProcessor.Disable();
                }
                else
                {
                    // Все выбранные точки полностью заблокированы - не начинаем перетаскивание
                    _draggedPoints.Clear();
                }
            }
            else if (closestFunction is not null)
            {
                bool isCtrlPressed = Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) || Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);
                
                if (isCtrlPressed)
                {
                    // Множественный выбор функций с Ctrl - не снимаем выделение с точек
                    _viewModel.ToggleFunctionSelection(closestFunction);
                    if (_viewModel.SelectedFunctions.Contains(closestFunction))
                    {
                        _viewModel.SelectedFunction = closestFunction;
                    }
                }
                else
                {
                    // Обычный выбор функции - снимаем выделение с точек
                    _viewModel.ClearSelection();
                    
                    if (_viewModel.SelectedFunctions.Contains(closestFunction))
                    {
                        _viewModel.SelectedFunction = closestFunction;
                    }
                    else
                    {
                        _viewModel.SelectSingleFunction(closestFunction);
                    }
                }
                
                // Синхронизируем выбор с ListBox
                SyncListBoxSelection();
            }
            else
            {
                // Клик в пустом месте - снимаем выделение если не нажат Ctrl
                if (!Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) && !Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl))
                {
                    _viewModel.ClearSelection();
                    _viewModel.ClearFunctionSelection();
                }
            }
        }
    }

    private void PlotControl_MouseMove(object sender, MouseEventArgs e)
    {
        // Обновление информации о курсоре с учетом масштаба экрана и динамической точности
        var mousePixel = GetScaledMousePosition(e);
        var mouseCoordinate = PlotControl.Plot.GetCoordinates((float)mousePixel.X, (float)mousePixel.Y);
        var formatString = $"F{_viewModel.Precision}";
        CursorInfoTextBlock.Text = $"Курсор: X={mouseCoordinate.X.ToString(formatString)}, Y={mouseCoordinate.Y.ToString(formatString)}";

        // Обновляем масштаб в реальном времени при панорамировании  
        // (убрано - теперь используем таймер для постоянного обновления)

        // Перетаскивание точек
        if (_draggedScatter is not null && _draggedPoint is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            var newCoordinate = PlotControl.Plot.GetCoordinates((float)mousePixel.X, (float)mousePixel.Y);
            
            if (_dragStartPositions.TryGetValue(_draggedPoint, out var startPos))
            {
                var deltaX = newCoordinate.X - startPos.OriginalX;
                var deltaY = newCoordinate.Y - startPos.OriginalY;

                _isUpdatingFromPlot = true;

                // Перемещаем все выбранные точки с учетом блокировок
                foreach (var point in _draggedPoints)
                {
                    if (_dragStartPositions.TryGetValue(point, out var originalPos))
                    {
                        // Применяем изменения только к незаблокированным координатам
                        double newX = point.X;
                        double newY = point.Y;
                        
                        if (!point.LockX)
                        {
                            newX = Math.Round(originalPos.OriginalX + deltaX, _viewModel.Precision);
                        }
                        
                        if (!point.LockY)
                        {
                            newY = Math.Round(originalPos.OriginalY + deltaY, _viewModel.Precision);
                        }
                        
                        point.X = newX;
                        point.Y = newY;

                        // Обновляем визуальное представление
                        if (_pointToScatterMap.TryGetValue(point, out var scatter))
                        {
                            PlotControl.Plot.Remove(scatter);
                            var newScatter = PlotControl.Plot.Add.Scatter(new double[] { point.X }, new double[] { point.Y });
                            newScatter.MarkerSize = point.MarkerSize;
                            newScatter.MarkerShape = point.MarkerShape;
                            newScatter.Color = point.Color;
                            newScatter.LineWidth = 0;
                            _pointToScatterMap[point] = newScatter;
                        }
                    }
                }

                PlotControl.Refresh();
                _isUpdatingFromPlot = false;
            }
        }
    }

    private void PlotControl_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggedScatter is not null)
        {
            _draggedScatter = null;
            _draggedPoint = null;
            _draggedPoints.Clear();
            _dragStartPositions.Clear();
            PlotControl.UserInputProcessor.Enable();
        }
        
        // Поля масштаба обновляются автоматически через таймер
    }

    private PlotPoint? FindClosestPoint(double x, double y)
    {
        PlotPoint? closestPoint = null;
        double minDistance = double.MaxValue;

        foreach (var point in _viewModel.Points)
        {
            // Вычисляем расстояние от клика до центра точки
            double distance = Math.Sqrt(Math.Pow(point.X - x, 2) + Math.Pow(point.Y - y, 2));
            
            // Вычисляем динамический порог на основе размера маркера и масштаба графика
            double threshold = CalculateClickThreshold(point);
            
            if (distance < threshold && distance < minDistance)
            {
                minDistance = distance;
                closestPoint = point;
            }
        }

        return closestPoint;
    }

    private double CalculateClickThreshold(PlotPoint point)
    {
        // Получаем текущие границы графика для расчета масштаба
        var limits = PlotControl.Plot.Axes.GetLimits();
        
        // Получаем размер области построения из PlotControl
        double plotWidth = PlotControl.ActualWidth;
        double plotHeight = PlotControl.ActualHeight;
        
        // Защита от деления на ноль
        if (plotWidth <= 0 || plotHeight <= 0)
        {
            return 0.5; // Возвращаем стандартное значение
        }
        
        // Вычисляем масштаб: сколько координат графика на пиксель
        double xScale = limits.XRange.Span / plotWidth;
        double yScale = limits.YRange.Span / plotHeight;
        
        // Используем среднее значение масштабов
        double averageScale = (xScale + yScale) / 2.0;
        
        // Базовый размер точки в пикселях (с учетом DisplayScale)
        double markerSizeInPixels = point.MarkerSize;
        if (PlotControl.DisplayScale != 1.0)
        {
            markerSizeInPixels *= PlotControl.DisplayScale;
        }
        
        // Добавляем дополнительный отступ для удобства клика (50% от размера маркера)
        double clickAreaInPixels = markerSizeInPixels * 1.5;
        
        // Преобразуем в координаты графика
        double threshold = clickAreaInPixels * averageScale;
        
        // Минимальный порог для очень маленьких точек
        return Math.Max(threshold, 0.05);
    }

    private PlotFunction? FindClosestFunction(double x, double y)
    {
        PlotFunction? closestFunction = null;
        double minDistance = double.MaxValue;
        
        // Динамический порог на основе текущего масштаба графика
        var limits = PlotControl.Plot.Axes.GetLimits();
        double yRange = limits.YRange.Span;
        double xRange = limits.XRange.Span;
        double clickThreshold = yRange * 0.05; // 5% от диапазона Y

        foreach (var function in _viewModel.Functions)
        {
            if (!function.IsVisible)
                continue;
            
            // Проверяем обычные функции (Signal)
            if (_functionToSignalMap.ContainsKey(function))
            {
                // Проверяем находится ли X в пределах функции
                if (x < function.MinX || x > function.MaxX)
                    continue;

                try
                {
                    // Вычисляем Y значение функции в точке X
                    RegisterAllVariablesAndFunctions();
                    _mathParser.SetConstant("x", x);
                    var compiledExpression = _mathParser.Parse<Func<double>>(function.Expression);
                    double functionY = compiledExpression();
                    
                    // Вычисляем расстояние от клика до точки на функции
                    double distance = Math.Abs(y - functionY);
                    
                    if (distance < clickThreshold && distance < minDistance)
                    {
                        minDistance = distance;
                        closestFunction = function;
                    }
                }
                catch (Exception)
                {
                    // Игнорируем ошибки вычисления
                    continue;
                }
            }
            
            // Проверяем неявные кривые (Scatter) - правильная проверка f(x,y) ≈ 0
            if (_functionToImplicitMap.TryGetValue(function, out var curves))
            {
                try
                {
                    // Вычисляем значение функции f(x,y) в точке клика
                    RegisterAllVariablesAndFunctions();
                    var argumentNames = GetArgumentNamesArray(function);
                    
                    // Устанавливаем аргументы функции
                    if (argumentNames.Length >= 2)
                    {
                        _mathParser.SetConstant(argumentNames[0], x); // обычно x
                        _mathParser.SetConstant(argumentNames[1], y); // обычно y
                        
                        // Дополнительные аргументы устанавливаем в 0 (если есть)
                        for (int i = 2; i < argumentNames.Length; i++)
                        {
                            _mathParser.SetConstant(argumentNames[i], 0);
                        }
                        
                        var compiledExpression = _mathParser.Parse<Func<double>>(function.Expression);
                        double functionValue = compiledExpression();
                        
                        // Проверяем, что |f(x,y)| ≤ ε (функция близка к нулю)
                        double epsilon = clickThreshold; // Используем тот же порог что и для обычных функций
                        double distance = Math.Abs(functionValue);
                        
                        if (distance <= epsilon && distance < minDistance)
                        {
                            minDistance = distance;
                            closestFunction = function;
                        }
                    }
                }
                catch (Exception)
                {
                    // Игнорируем ошибки вычисления
                    continue;
                }
            }
        }

        return closestFunction;
    }

    private void AddPointButton_Click(object sender, RoutedEventArgs e)
    {
        // Добавляем точку в центре текущего видимого диапазона
        var xCenter = (PlotControl.Plot.Axes.GetLimits().XRange.Min + PlotControl.Plot.Axes.GetLimits().XRange.Max) / 2;
        var yCenter = (PlotControl.Plot.Axes.GetLimits().YRange.Min + PlotControl.Plot.Axes.GetLimits().YRange.Max) / 2;

        _viewModel.AddPoint(Math.Round(xCenter, _viewModel.Precision), Math.Round(yCenter, _viewModel.Precision));
    }

    private void RemoveSelectedPointsButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RemoveSelectedPoints();
    }

    private void RecalculateExpressionsButton_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("RecalculateExpressionsButton_Click: Принудительный пересчет всех выражений и функций по нажатию кнопки");
        UpdateAllExpressions();
        UpdateAllFunctions();
    }

    private void PointsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingFromPlot || _isUpdatingSelection) return;

        _isUpdatingSelection = true;
        
        var listBox = sender as ListBox;
        if (listBox?.SelectedItems is not null)
        {
            _viewModel.ClearSelection();
            foreach (PlotPoint point in listBox.SelectedItems)
            {
                _viewModel.AddToSelection(point);
            }

            if (listBox.SelectedItem is PlotPoint selectedPoint)
            {
                _viewModel.SelectedPoint = selectedPoint;
            }
        }
        
        _isUpdatingSelection = false;
    }

    // Групповые операции через редактирование в списке
    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingSelection || _isUpdatingFromPlot || _isReorderingPoints) return;
        
        var textBox = sender as TextBox;
        if (textBox?.DataContext is PlotPoint editedPoint && _viewModel.SelectedPoints.Contains(editedPoint))
        {
            // Применяем изменение ко всем выбранным точкам кроме текущей, игнорируя заблокированные
            var newName = textBox.Text;
            var pointsToUpdate = _viewModel.SelectedPoints
                .Where(p => p != editedPoint && !p.LockName)
                .ToList();
            foreach (var point in pointsToUpdate)
            {
                point.Name = newName;
            }
        }
    }

    private void XTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingSelection || _isUpdatingFromPlot || _isUpdatingPrecision || _isReorderingPoints) return;
        
        var textBox = sender as Controls.PrecisionTextBox;
        if (textBox?.DataContext is PlotPoint editedPoint && _viewModel.SelectedPoints.Contains(editedPoint))
        {
            var newX = textBox.NumericValue;
            var pointsToUpdate = _viewModel.SelectedPoints
                .Where(p => p != editedPoint && !p.LockX)
                .ToList();
            foreach (var point in pointsToUpdate)
            {
                point.X = newX;
            }
        }
    }

    private void YTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingSelection || _isUpdatingFromPlot || _isUpdatingPrecision || _isReorderingPoints) return;
        
        var textBox = sender as Controls.PrecisionTextBox;
        if (textBox?.DataContext is PlotPoint editedPoint && _viewModel.SelectedPoints.Contains(editedPoint))
        {
            var newY = textBox.NumericValue;
            var pointsToUpdate = _viewModel.SelectedPoints
                .Where(p => p != editedPoint && !p.LockY)
                .ToList();
            foreach (var point in pointsToUpdate)
            {
                point.Y = newY;
            }
        }
    }

    private void SizeTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingSelection || _isUpdatingFromPlot || _isReorderingPoints) return;
        
        var textBox = sender as TextBox;
        if (textBox?.DataContext is PlotPoint editedPoint && _viewModel.SelectedPoints.Contains(editedPoint))
        {
            if (int.TryParse(textBox.Text, out int newSize) && newSize > 0)
            {
                var pointsToUpdate = _viewModel.SelectedPoints
                    .Where(p => p != editedPoint && !p.LockSize)
                    .ToList();
                foreach (var point in pointsToUpdate)
                {
                    point.MarkerSize = newSize;
                }
            }
        }
    }

    private void ShapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection || _isUpdatingFromPlot || _isReorderingPoints) return;
        
        var comboBox = sender as ComboBox;
        if (comboBox?.DataContext is PlotPoint editedPoint && 
            _viewModel.SelectedPoints.Contains(editedPoint) &&
            comboBox.SelectedItem is ComboBoxItem item && 
            item.Tag is string shapeTag &&
            !string.IsNullOrEmpty(shapeTag))
        {
            var shape = shapeTag switch
            {
                "FilledCircle" => MarkerShape.FilledCircle,
                "FilledSquare" => MarkerShape.FilledSquare,
                "FilledDiamond" => MarkerShape.FilledDiamond,
                "OpenCircle" => MarkerShape.OpenCircle,
                "OpenSquare" => MarkerShape.OpenSquare,
                _ => MarkerShape.FilledCircle
            };
            
            var pointsToUpdate = _viewModel.SelectedPoints
                .Where(p => p != editedPoint && !p.LockShape)
                .ToList();
            foreach (var point in pointsToUpdate)
            {
                point.MarkerShape = shape;
            }
        }
    }

    private void ColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
    {
        if (_isUpdatingSelection || _isUpdatingFromPlot || _isReorderingPoints) return;
        
        var colorPicker = sender as Xceed.Wpf.Toolkit.ColorPicker;
        if (colorPicker?.DataContext is PlotPoint editedPoint && _viewModel.SelectedPoints.Contains(editedPoint))
        {
            if (e.NewValue.HasValue)
            {
                var mediaColor = e.NewValue.Value;
                var scottPlotColor = new ScottPlot.Color(mediaColor.R, mediaColor.G, mediaColor.B, mediaColor.A);
                
                var pointsToUpdate = _viewModel.SelectedPoints
                    .Where(p => p != editedPoint && !p.LockColor)
                    .ToList();
                foreach (var point in pointsToUpdate)
                {
                    point.Color = scottPlotColor;
                }
            }
        }
    }

    private void XExpressionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("XExpressionTextBox_TextChanged: пересчитываем всё");
        // Просто обновляем всё
        UpdateAllExpressions();
        UpdateAllFunctions();
    }

    private void YExpressionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("YExpressionTextBox_TextChanged: пересчитываем всё");
        // Просто обновляем всё
        UpdateAllExpressions();
        UpdateAllFunctions();
    }

    private void EvaluateExpression(PlotPoint point, string coordinate, string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return;

        try
        {
            // Простой парсер для выражений вида "A.X", "B.Y"
            var result = ParseExpression(expression);
            
            if (coordinate == "X")
            {
                point.X = result;
            }
            else if (coordinate == "Y")
            {
                point.Y = result;
            }
        }
        catch (Exception ex)
        {
            // В случае ошибки можно показать уведомление или просто игнорировать
            System.Diagnostics.Debug.WriteLine($"Ошибка вычисления выражения '{expression}': {ex.Message}");
        }
    }

    private void EvaluateExpressionAndUpdate(PlotPoint point, string coordinate, string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return;

        try
        {
            System.Diagnostics.Debug.WriteLine($"EvaluateExpressionAndUpdate: парсим выражение '{expression}' для {point.Name}.{coordinate}");
            
            // ЗАНОВО парсим выражение при каждом вызове
            var result = ParseExpressionFresh(expression);
            
            System.Diagnostics.Debug.WriteLine($"EvaluateExpressionAndUpdate: результат = {result}");
            
            if (coordinate == "X")
            {
                var oldValue = point.X;
                point.X = result;
                System.Diagnostics.Debug.WriteLine($"EvaluateExpressionAndUpdate: обновили {point.Name}.X с {oldValue} на {result}");
            }
            else if (coordinate == "Y")
            {
                var oldValue = point.Y;
                point.Y = result;
                System.Diagnostics.Debug.WriteLine($"EvaluateExpressionAndUpdate: обновили {point.Name}.Y с {oldValue} на {result}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EvaluateExpressionAndUpdate: ОШИБКА при вычислении выражения '{expression}' для {point.Name}.{coordinate}: {ex.Message}");
        }
    }

    private double ParseExpression(string expression)
    {
        return ParseMathExpression(expression);
    }

    private double ParseExpressionFresh(string expression)
    {
        return ParseMathExpression(expression);
    }

    private double ParseMathExpression(string expression)
    {
        expression = expression.Trim();
        System.Diagnostics.Debug.WriteLine($"ParseMathExpression: парсим '{expression}'");
        
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Выражение не может быть пустым");
        }

        try
        {
            // Регистрируем переменные для всех точек в парсере
            RegisterPointVariables();
            
            // Парсим выражение как анонимную функцию (без параметров)
            var compiledExpression = _mathParser.Parse<Func<double>>(expression);
            
            // Вычисляем и возвращаем результат
            var result = compiledExpression();
            System.Diagnostics.Debug.WriteLine($"ParseMathExpression: результат = {result}");
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ParseMathExpression: ошибка при парсинге '{expression}': {ex.Message}");
            throw new ArgumentException($"Ошибка в выражении '{expression}': {ex.Message}", ex);
        }
    }

    private void RegisterPointVariables()
    {
        RegisterAllVariablesAndFunctions();
    }

    private void UpdateAllExpressions()
    {
        System.Diagnostics.Debug.WriteLine("UpdateAllExpressions");
        if (_isUpdatingFromExpressions) 
        {
            System.Diagnostics.Debug.WriteLine("UpdateAllExpressions: уже обновляем, пропускаем");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine("UpdateAllExpressions: НАЧИНАЕМ обновление всех выражений");
        _isUpdatingFromExpressions = true;
        
        try
        {
            // Находим все TextBox'ы с выражениями в UI и читаем их значения ЗАНОВО
            ReadAndEvaluateAllExpressionsFromUI();
        }
        finally
        {
            _isUpdatingFromExpressions = false;
            System.Diagnostics.Debug.WriteLine("UpdateAllExpressions: ЗАВЕРШИЛИ обновление всех выражений");
        }
    }

    private void ReadAndEvaluateAllExpressionsFromUI()
    {
        System.Diagnostics.Debug.WriteLine("ReadAndEvaluateAllExpressionsFromUI: ищем все TextBox'ы выражений в UI");
        
        // Проходим по всем элементам в PointsListBox
        for (int i = 0; i < PointsListBox.Items.Count; i++)
        {
            var listBoxItem = PointsListBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (listBoxItem == null) continue;
            
            var point = PointsListBox.Items[i] as PlotPoint;
            if (point == null) continue;
            
            System.Diagnostics.Debug.WriteLine($"ReadAndEvaluateAllExpressionsFromUI: проверяем UI элементы для точки {point.Name}");
            
            // Ищем TextBox'ы для выражений X и Y
            var xExpressionTextBox = FindChildByName<TextBox>(listBoxItem, "XExpressionTextBox");
            var yExpressionTextBox = FindChildByName<TextBox>(listBoxItem, "YExpressionTextBox");
            
            // Читаем X выражение из UI
            if (xExpressionTextBox != null)
            {
                var xExpression = xExpressionTextBox.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(xExpression))
                {
                    System.Diagnostics.Debug.WriteLine($"ReadAndEvaluateAllExpressionsFromUI: найдено X выражение '{xExpression}' для точки {point.Name}");
                    EvaluateExpressionAndUpdate(point, "X", xExpression);
                }
            }
            
            // Читаем Y выражение из UI
            if (yExpressionTextBox != null)
            {
                var yExpression = yExpressionTextBox.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(yExpression))
                {
                    System.Diagnostics.Debug.WriteLine($"ReadAndEvaluateAllExpressionsFromUI: найдено Y выражение '{yExpression}' для точки {point.Name}");
                    EvaluateExpressionAndUpdate(point, "Y", yExpression);
                }
            }
        }
    }

    private T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        if (parent == null) return null;
        
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            
            if (child is T element && element.Name == name)
            {
                return element;
            }
            
            var result = FindChildByName<T>(child, name);
            if (result != null)
                return result;
        }
        
        return null;
    }

    // Логика мигания
    private void SetupBlinkTimer()
    {
        _blinkTimer = new DispatcherTimer();
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(100);
        _blinkTimer.Tick += BlinkTimer_Tick;
    }

    private void SetupAxisUpdateTimer()
    {
        _axisUpdateTimer = new DispatcherTimer();
        _axisUpdateTimer.Interval = TimeSpan.FromMilliseconds(50); // Обновляем каждые 50мс
        _axisUpdateTimer.Tick += AxisUpdateTimer_Tick;
        _axisUpdateTimer.Start();
    }

    private void AxisUpdateTimer_Tick(object? sender, EventArgs e)
    {
        // Постоянно обновляем значения масштаба
        UpdateAxisLimitsFromPlot();
    }

    private void BlinkTimer_Tick(object? sender, EventArgs e)
    {
        _isBlinkVisible = !_isBlinkVisible;
        bool needsRefresh = false;
        
        // Мигают все выбранные точки - создаем копию коллекции
        var selectedPointsCopy = _viewModel.SelectedPoints.ToList();
        foreach (var point in selectedPointsCopy)
        {
            if (_pointToScatterMap.TryGetValue(point, out var scatter))
            {
                if (_isBlinkVisible)
                {
                    // Показываем точку с исходной прозрачностью пользователя
                    scatter.Color = point.Color;
                }
                else
                {
                    // Уменьшаем прозрачность для эффекта мигания
                    var originalAlpha = point.Color.A;
                    var dimmedAlpha = (byte)Math.Max(30, originalAlpha * 0.3);
                    scatter.Color = point.Color.WithAlpha(dimmedAlpha);
                }
                needsRefresh = true;
            }
        }
        
        // Мигают все выбранные функции
        var selectedFunctionsCopy = _viewModel.SelectedFunctions.ToList();
        foreach (var function in selectedFunctionsCopy)
        {
            // Мигание обычных функций (Signal)
            if (_functionToSignalMap.TryGetValue(function, out var signal))
            {
                if (_isBlinkVisible)
                {
                    // Показываем функцию с исходным цветом
                    signal.Color = function.Color;
                }
                else
                {
                    // Уменьшаем прозрачность для эффекта мигания
                    var originalAlpha = function.Color.A;
                    var dimmedAlpha = (byte)Math.Max(30, originalAlpha * 0.3);
                    signal.Color = function.Color.WithAlpha(dimmedAlpha);
                }
                needsRefresh = true;
            }
            
            // Мигание неявных кривых (Scatter)
            if (_functionToImplicitMap.TryGetValue(function, out var curves))
            {
                foreach (var curve in curves)
                {
                    if (_isBlinkVisible)
                    {
                        // Показываем кривую с исходным цветом
                        curve.Color = function.Color;
                    }
                    else
                    {
                        // Уменьшаем прозрачность для эффекта мигания
                        var originalAlpha = function.Color.A;
                        var dimmedAlpha = (byte)Math.Max(30, originalAlpha * 0.3);
                        curve.Color = function.Color.WithAlpha(dimmedAlpha);
                    }
                }
                needsRefresh = true;
            }
        }
        
        if (needsRefresh)
        {
            PlotControl.Refresh();
        }
    }

    private void StartBlinking()
    {
        StopBlinking();
        if (_viewModel.SelectedPoints.Count > 0 || _viewModel.SelectedFunctions.Count > 0)
        {
            _isBlinkVisible = true;
            _blinkTimer?.Start();
        }
    }

    private void StopBlinking()
    {
        _blinkTimer?.Stop();
        RestoreAllOpacity();
    }
    
    private void RestoreAllOpacity()
    {
        bool needsRefresh = false;
        
        // Возвращаем исходную прозрачность ВСЕМ точкам
        foreach (var point in _viewModel.Points)
        {
            if (_pointToScatterMap.TryGetValue(point, out var scatter))
            {
                scatter.Color = point.Color;
                needsRefresh = true;
            }
        }
        
        // Возвращаем исходную прозрачность ВСЕМ функциям
        foreach (var function in _viewModel.Functions)
        {
            // Обычные функции (Signal)
            if (_functionToSignalMap.TryGetValue(function, out var signal))
            {
                signal.Color = function.Color;
                needsRefresh = true;
            }
            
            // Неявные кривые (Scatter)
            if (_functionToImplicitMap.TryGetValue(function, out var curves))
            {
                foreach (var curve in curves)
                {
                    curve.Color = function.Color;
                }
                needsRefresh = true;
            }
        }
        
        if (needsRefresh)
        {
            PlotControl.Refresh();
        }
    }
    
    // Вспомогательный метод для корректной обработки координат мыши с учетом масштаба экрана
    private System.Windows.Point GetScaledMousePosition(MouseEventArgs e)
    {
        var position = e.GetPosition(PlotControl);
        if (PlotControl.DisplayScale != 1.0)
        {
            position.X *= PlotControl.DisplayScale;
            position.Y *= PlotControl.DisplayScale;
        }
        return position;
    }
    
    // Обработчики событий для управления масштабом
    private void UpdateAxisLimitsFromPlot()
    {
        if (_isUpdatingAxisLimits) return;
        
        _isUpdatingAxisLimits = true;
        
        // Получаем текущие границы графика
        var limits = PlotControl.Plot.Axes.GetLimits();
        
        // Проверяем и восстанавливаем заблокированные оси
        bool needsRestore = false;
        double newMinX = limits.XRange.Min, newMaxX = limits.XRange.Max;
        double newMinY = limits.YRange.Min, newMaxY = limits.YRange.Max;
        
        if (_viewModel.LockX)
        {
            if (Math.Abs(limits.XRange.Min - _lockedMinX) > 0.0001 || 
                Math.Abs(limits.XRange.Max - _lockedMaxX) > 0.0001)
            {
                newMinX = _lockedMinX;
                newMaxX = _lockedMaxX;
                needsRestore = true;
            }
        }
        
        if (_viewModel.LockY)
        {
            if (Math.Abs(limits.YRange.Min - _lockedMinY) > 0.0001 || 
                Math.Abs(limits.YRange.Max - _lockedMaxY) > 0.0001)
            {
                newMinY = _lockedMinY;
                newMaxY = _lockedMaxY;
                needsRestore = true;
            }
        }
        
        // Восстанавливаем заблокированные пределы если нужно
        if (needsRestore)
        {
            PlotControl.Plot.Axes.SetLimits(newMinX, newMaxX, newMinY, newMaxY);
            PlotControl.Refresh();
            limits = PlotControl.Plot.Axes.GetLimits(); // Обновляем limits после восстановления
        }
        
        // Обновляем ViewModel с динамической точностью
        _viewModel.MinX = Math.Round(limits.XRange.Min, _viewModel.Precision);
        _viewModel.MaxX = Math.Round(limits.XRange.Max, _viewModel.Precision);
        _viewModel.MinY = Math.Round(limits.YRange.Min, _viewModel.Precision);
        _viewModel.MaxY = Math.Round(limits.YRange.Max, _viewModel.Precision);
        
        _isUpdatingAxisLimits = false;
        
        // Обновляем функции с автомасштабированием при изменении границ графика
        UpdateFunctionsWithAutoScale();
    }

    private void AxisLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingAxisLimits || _isUpdatingPrecision) return;
        
        var textBox = sender as Controls.PrecisionTextBox;
        if (textBox?.Tag is string axisProperty)
        {
            _isUpdatingAxisLimits = true;
            var value = textBox.NumericValue;
            
            // Обновляем соответствующее свойство ViewModel
            switch (axisProperty)
            {
                case "MinX":
                    _viewModel.MinX = value;
                    break;
                case "MaxX":
                    _viewModel.MaxX = value;
                    break;
                case "MinY":
                    _viewModel.MinY = value;
                    break;
                case "MaxY":
                    _viewModel.MaxY = value;
                    break;
            }
            
            // Применяем новые границы к графику
            PlotControl.Plot.Axes.SetLimits(_viewModel.MinX, _viewModel.MaxX, _viewModel.MinY, _viewModel.MaxY);
            PlotControl.Refresh();
            
            _isUpdatingAxisLimits = false;
        }
    }

    private void PrecisionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox != null && int.TryParse(textBox.Text, out int precision))
        {
            _viewModel.Precision = precision;
        }
    }

    private double _lockedMinX;
    private double _lockedMaxX;
    private double _lockedMinY;
    private double _lockedMaxY;
    
    private void UpdateAxisLockRules()
    {
        var currentLimits = PlotControl.Plot.Axes.GetLimits();
        
        // Сохраняем текущие пределы осей для блокировки
        if (_viewModel.LockX)
        {
            _lockedMinX = currentLimits.Left;
            _lockedMaxX = currentLimits.Right;
        }
        
        if (_viewModel.LockY)
        {
            _lockedMinY = currentLimits.Bottom;
            _lockedMaxY = currentLimits.Top;
        }
        
        // Настраиваем обработку взаимодействия
        if (_viewModel.LockX && _viewModel.LockY)
        {
            // Если обе оси заблокированы, полностью отключаем взаимодействие
            PlotControl.UserInputProcessor.Disable();
        }
        else
        {
            // Включаем взаимодействие
            PlotControl.UserInputProcessor.Enable();
        }
        
        PlotControl.Refresh();
    }

    private void AxisLockCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Обновляем правила блокировки при изменении галочек
        UpdateAxisLockRules();
    }

    private void MovePointUpButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.DataContext is PlotPoint point)
        {
            _isReorderingPoints = true;
            _viewModel.MovePointUp(point);
            RefreshPlotAfterReorder();
            _isReorderingPoints = false;
        }
    }

    private void MovePointDownButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.DataContext is PlotPoint point)
        {
            _isReorderingPoints = true;
            _viewModel.MovePointDown(point);
            RefreshPlotAfterReorder();
            _isReorderingPoints = false;
        }
    }

    private void RefreshPlotAfterReorder()
    {
        // Сохраняем текущее выделение
        var selectedPoints = _viewModel.SelectedPoints.ToList();
        var selectedPoint = _viewModel.SelectedPoint;
        
        // Удаляем только точки, сохраняя оси и другие элементы
        var scattersToRemove = _pointToScatterMap.Values.ToList();
        foreach (var scatter in scattersToRemove)
        {
            PlotControl.Plot.Remove(scatter);
        }
        _pointToScatterMap.Clear();
        
        // Заново добавляем все точки в новом порядке с правильными свойствами
        foreach (var point in _viewModel.Points)
        {
            var scatter = PlotControl.Plot.Add.Scatter(new double[] { point.X }, new double[] { point.Y });
            scatter.MarkerSize = point.MarkerSize;
            scatter.MarkerShape = point.MarkerShape;
            scatter.Color = point.Color;
            scatter.LineWidth = 0;
            _pointToScatterMap[point] = scatter;
        }
        
        PlotControl.Refresh();
        
        // Восстанавливаем выделение после обновления
        RestoreAllOpacity();
        HighlightSelectedElements();
        SyncListBoxSelection();
        
        // Принудительно обновляем все привязки UI для корректного состояния кнопок
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Normal,
            new Action(() => {
                ForceUpdateButtonStates();
            }));
    }

    private void ForceUpdateButtonStates()
    {
        // Принудительно обновляем все элементы ListBox
        PointsListBox.Items.Refresh();
        
        // Принудительно обновляем состояние команд
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        
        // Обходим каждый элемент и принудительно обновляем его привязки
        foreach (var item in PointsListBox.Items)
        {
            var container = PointsListBox.ItemContainerGenerator.ContainerFromItem(item) as System.Windows.Controls.ListBoxItem;
            if (container != null)
            {
                // Принудительно обновляем привязки для всех кнопок в контейнере
                var buttons = FindVisualChildren<System.Windows.Controls.Button>(container);
                foreach (var button in buttons)
                {
                    var isEnabledBinding = System.Windows.Data.BindingOperations.GetBindingExpression(button, System.Windows.Controls.Button.IsEnabledProperty);
                    isEnabledBinding?.UpdateTarget();
                }
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject? depObj) where T : System.Windows.DependencyObject
    {
        if (depObj != null)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T)
                {
                    yield return (T)child;
                }

                if (child != null)
                {
                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }

    // Обработчики событий для функций
    private void AddFunctionButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddFunction();
    }

    private void RemoveSelectedFunctionsButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RemoveSelectedFunctions();
    }

    private void FunctionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection) return;

        _isUpdatingSelection = true;

        _viewModel.ClearFunctionSelection();

        foreach (PlotFunction function in FunctionsListBox.SelectedItems)
        {
            _viewModel.AddToFunctionSelection(function);
        }

        if (FunctionsListBox.SelectedItems.Count > 0)
        {
            _viewModel.SelectedFunction = FunctionsListBox.SelectedItems[0] as PlotFunction;
        }

        _isUpdatingSelection = false;
    }

    private void FunctionNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingUI || _isUpdatingSelection) return;
        
        if (sender is TextBox textBox && textBox.DataContext is PlotFunction function)
        {
            // Проверяем, действительно ли значение изменилось
            if (textBox.Text == function.Name) return;
            
            System.Diagnostics.Debug.WriteLine($"FunctionNameTextBox_TextChanged: имя функции {function.Name} изменилось");
            // Просто обновляем всё
            UpdateAllFunctions();
            UpdateAllExpressions();
        }
    }

    private void FunctionExpressionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingUI || _isUpdatingSelection) return;
        
        if (sender is TextBox textBox && textBox.DataContext is PlotFunction function)
        {
            // Проверяем, действительно ли значение изменилось
            if (textBox.Text == function.Expression) return;
            
            System.Diagnostics.Debug.WriteLine($"FunctionExpressionTextBox_TextChanged: выражение функции {function.Name} изменилось на {function.Expression}");
            // Просто обновляем всё
            UpdateAllFunctions();
            UpdateAllExpressions();
        }
    }

    private void FunctionMinXTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingUI || _isUpdatingSelection) return;
        
        if (sender is TextBox textBox && textBox.DataContext is PlotFunction function)
        {
            // Проверяем, действительно ли значение изменилось
            if (double.TryParse(textBox.Text, out double newValue) && Math.Abs(newValue - function.MinX) < 1e-10) return;
            
            UpdateAllFunctions();
        }
    }

    private void FunctionMaxXTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingUI || _isUpdatingSelection) return;
        
        if (sender is TextBox textBox && textBox.DataContext is PlotFunction function)
        {
            // Проверяем, действительно ли значение изменилось
            if (double.TryParse(textBox.Text, out double newValue) && Math.Abs(newValue - function.MaxX) < 1e-10) return;
            
            UpdateAllFunctions();
        }
    }

    private void FunctionColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
    {
        if (_isLoadingUI || _isUpdatingSelection) return;
        
        if (sender is Xceed.Wpf.Toolkit.ColorPicker colorPicker && 
            colorPicker.DataContext is PlotFunction editedFunction &&
            e.NewValue.HasValue)
        {
            var wpfColor = e.NewValue.Value;
            var scottPlotColor = new ScottPlot.Color(wpfColor.R, wpfColor.G, wpfColor.B);
            
            // Проверяем, действительно ли цвет изменился
            if (editedFunction.Color.R == scottPlotColor.R && 
                editedFunction.Color.G == scottPlotColor.G && 
                editedFunction.Color.B == scottPlotColor.B) return;
            
            editedFunction.Color = scottPlotColor;
            UpdateAllFunctions();
        }
    }

    private void FunctionLineWidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingUI || _isUpdatingSelection) return;
        
        if (sender is TextBox textBox && 
            textBox.DataContext is PlotFunction editedFunction &&
            int.TryParse(textBox.Text, out int width))
        {
            // Проверяем, действительно ли значение изменилось
            if (editedFunction.LineWidth == width) return;
            
            editedFunction.LineWidth = width;
            UpdateAllFunctions();
        }
    }

    private void FunctionPointCountTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingUI || _isUpdatingSelection) return;
        
        if (sender is TextBox textBox && 
            textBox.DataContext is PlotFunction editedFunction &&
            int.TryParse(textBox.Text, out int count))
        {
            // Проверяем, действительно ли значение изменилось
            if (editedFunction.PointCount == count) return;
            
            editedFunction.PointCount = count;
            UpdateAllFunctions();
        }
    }

    private void FunctionMinYTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingUI || _isUpdatingSelection) return;
        
        if (sender is TextBox textBox && textBox.DataContext is PlotFunction function)
        {
            // Проверяем, действительно ли значение изменилось
            if (double.TryParse(textBox.Text, out double newValue) && Math.Abs(newValue - function.MinY) < 1e-10) return;
            
            UpdateAllFunctions();
        }
    }

    private void FunctionMaxYTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingUI || _isUpdatingSelection) return;
        
        if (sender is TextBox textBox && textBox.DataContext is PlotFunction function)
        {
            // Проверяем, действительно ли значение изменилось
            if (double.TryParse(textBox.Text, out double newValue) && Math.Abs(newValue - function.MaxY) < 1e-10) return;
            
            UpdateAllFunctions();
        }
    }

    private void FunctionArgumentCountTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingUI || _isUpdatingSelection) return;
        
        if (sender is TextBox textBox && 
            textBox.DataContext is PlotFunction editedFunction &&
            int.TryParse(textBox.Text, out int count))
        {
            // Проверяем, действительно ли значение изменилось
            if (editedFunction.ArgumentCount == count) return;
            
            editedFunction.ArgumentCount = count;
            
            // Автоматически обновляем имена аргументов
            UpdateArgumentNames(editedFunction);
            
            UpdateAllFunctions();
        }
    }

    private void FunctionArgumentNamesTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingUI || _isUpdatingSelection) return;
        
        if (sender is TextBox textBox && textBox.DataContext is PlotFunction function)
        {
            // Проверяем, действительно ли значение изменилось
            if (function.ArgumentNames == textBox.Text) return;
            
            UpdateAllFunctions();
        }
    }

    private void UpdateArgumentNames(PlotFunction function)
    {
        // Генерируем стандартные имена аргументов: x, y, z, u, v
        var names = new[] { "x", "y", "z", "u", "v" };
        var argumentNames = string.Join(",", names.Take(function.ArgumentCount));
        
        if (function.ArgumentNames != argumentNames)
        {
            function.ArgumentNames = argumentNames;
        }
    }
    
    private void FunctionIsImplicitCurveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Очищаем кэш при изменении режима неявной кривой
        _implicitCurveCache.Clear();
        
        if (sender is System.Windows.Controls.CheckBox checkBox && 
            checkBox.DataContext is PlotFunction function)
        {
            System.Diagnostics.Debug.WriteLine($"IsImplicitCurve изменен для {function.Name}: {function.IsImplicitCurve}");
        }
    }
    
    private void FunctionPerformanceModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Очищаем кэш при изменении режима производительности
        _implicitCurveCache.Clear();
        
        if (sender is System.Windows.Controls.CheckBox checkBox && 
            checkBox.DataContext is PlotFunction function)
        {
            System.Diagnostics.Debug.WriteLine($"PerformanceMode изменен для {function.Name}: {function.PerformanceMode}");
        }
    }

    private void MoveFunctionUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is PlotFunction function)
        {
            _isReorderingFunctions = true;
            _viewModel.MoveFunctionUp(function);
            _isReorderingFunctions = false;
        }
    }

    private void MoveFunctionDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is PlotFunction function)
        {
            _isReorderingFunctions = true;
            _viewModel.MoveFunctionDown(function);
            _isReorderingFunctions = false;
        }
    }

    private void UpdateAllFunctions()
    {
        System.Diagnostics.Debug.WriteLine("UpdateAllFunctions: ОБНОВЛЯЕМ все функции");
        
        // Заново отрисовываем все функции
        foreach (var function in _viewModel.Functions)
        {
            UpdatePlotFunction(function);
        }
        PlotControl.Refresh();
    }

    private void ClearPlotDataSafely()
    {
        // Безопасная очистка данных без удаления осей
        
        // Удаляем все точки
        foreach (var scatter in _pointToScatterMap.Values)
        {
            PlotControl.Plot.Remove(scatter);
        }
        _pointToScatterMap.Clear();
        
        // Удаляем все функции (Signal)
        foreach (var signal in _functionToSignalMap.Values)
        {
            PlotControl.Plot.Remove(signal);
        }
        _functionToSignalMap.Clear();
        
        // Удаляем все неявные кривые (Scatter)
        foreach (var curves in _functionToImplicitMap.Values)
        {
            foreach (var curve in curves)
            {
                PlotControl.Plot.Remove(curve);
            }
        }
        _functionToImplicitMap.Clear();
        
        // НЕ вызываем PlotControl.Plot.Clear() - это сохранит оси
        PlotControl.Refresh();
    }

    // Обработчики событий для сохранения и загрузки проекта
    private async void SaveProjectButton_Click(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
            DefaultExt = "json",
            Title = "Сохранить проект"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            var success = await _viewModel.SaveToFileAsync(saveFileDialog.FileName);
            if (success)
            {
                MessageBox.Show("Проект успешно сохранен!", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Ошибка при сохранении проекта!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void LoadProjectButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
            Title = "Загрузить проект"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            _isLoadingProject = true; // Защита от циклических обновлений
            
            try
            {
                // Безопасно очищаем данные графика
                ClearPlotDataSafely();
                
                var success = await _viewModel.LoadFromFileAsync(openFileDialog.FileName);
                if (success)
                {
                    // График уже обновился автоматически через события CollectionChanged
                    // Остается только установить масштаб
                    PlotControl.Plot.Axes.SetLimits(_viewModel.MinX, _viewModel.MaxX, _viewModel.MinY, _viewModel.MaxY);
                    PlotControl.Refresh();
                    
                    MessageBox.Show("Проект успешно загружен!", "Загрузка", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Ошибка при загрузке проекта!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                _isLoadingProject = false; // Снимаем защиту
            }
        }
    }

    private void NewProjectButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Создать новый проект? Все несохраненные данные будут потеряны!", 
                                   "Новый проект", 
                                   MessageBoxButton.YesNo, 
                                   MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            _isLoadingProject = true; // Защита от циклических обновлений
            
            try
            {
                // Безопасно очищаем данные графика
                ClearPlotDataSafely();
                
                // Очищаем данные модели
                _viewModel.Points.Clear();
                _viewModel.Functions.Clear();
                _viewModel.SelectedPoints.Clear();
                _viewModel.SelectedFunctions.Clear();
                _viewModel.SelectedPoint = null;
                _viewModel.SelectedFunction = null;
                
                // Сбрасываем настройки к значениям по умолчанию
                _viewModel.Precision = 2;
                _viewModel.MinX = -10;
                _viewModel.MaxX = 10;
                _viewModel.MinY = -10;
                _viewModel.MaxY = 10;
                _viewModel.LockX = false;
                _viewModel.LockY = false;
                
                // Обновляем масштаб графика (оси остаются)
                PlotControl.Plot.Axes.SetLimits(_viewModel.MinX, _viewModel.MaxX, _viewModel.MinY, _viewModel.MaxY);
                PlotControl.Refresh();
                
                MessageBox.Show("Новый проект создан!", "Новый проект", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                _isLoadingProject = false; // Снимаем защиту
            }
        }
    }

}