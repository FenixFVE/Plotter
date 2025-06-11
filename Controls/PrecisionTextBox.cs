using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WpfPlotApp.Controls;

public class PrecisionTextBox : TextBox
{
    public static readonly DependencyProperty PrecisionProperty =
        DependencyProperty.Register(nameof(Precision), typeof(int), typeof(PrecisionTextBox),
            new PropertyMetadata(2, OnPrecisionChanged));

    public static readonly DependencyProperty NumericValueProperty =
        DependencyProperty.Register(nameof(NumericValue), typeof(double), typeof(PrecisionTextBox),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnNumericValueChanged));

    private bool _isUpdating = false;

    public int Precision
    {
        get => (int)GetValue(PrecisionProperty);
        set => SetValue(PrecisionProperty, value);
    }

    public double NumericValue
    {
        get => (double)GetValue(NumericValueProperty);
        set => SetValue(NumericValueProperty, value);
    }

    public PrecisionTextBox()
    {
        TextChanged += OnTextChanged;
    }

    private static void OnPrecisionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PrecisionTextBox textBox)
        {
            textBox.UpdateDisplayText();
        }
    }

    private static void OnNumericValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PrecisionTextBox textBox)
        {
            textBox.UpdateDisplayText();
        }
    }

    private void UpdateDisplayText()
    {
        if (_isUpdating) return;
        
        _isUpdating = true;
        var formatString = $"F{Math.Max(0, Math.Min(10, Precision))}";
        Text = NumericValue.ToString(formatString, CultureInfo.CurrentCulture);
        _isUpdating = false;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        if (double.TryParse(Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double result))
        {
            _isUpdating = true;
            NumericValue = result;
            _isUpdating = false;
        }
    }
} 