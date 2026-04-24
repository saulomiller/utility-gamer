using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace UtilityGamer.WinUI.Views.Controls;

public sealed partial class NumericSpinner : UserControl
{
    // Dependency properties.

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(NumericSpinner),
            new PropertyMetadata(0, OnValueChanged));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(NumericSpinner),
            new PropertyMetadata(0));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(NumericSpinner),
            new PropertyMetadata(9999));

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(int), typeof(NumericSpinner),
            new PropertyMetadata(1));

    // Public properties.

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, Minimum, Maximum));
    }

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public int Step
    {
        get => (int)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    // Events.

    public event EventHandler<int>? ValueChanged;

    // Constructor.

    public NumericSpinner()
    {
        InitializeComponent();
        UpdateText();
    }

    // Event handlers.

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NumericSpinner spinner) return;
        spinner.UpdateText();
        spinner.ValueChanged?.Invoke(spinner, spinner.Value);
    }

    private void OnUp(object sender, RoutedEventArgs e)
        => Value = Math.Clamp(Value + Step, Minimum, Maximum);

    private void OnDown(object sender, RoutedEventArgs e)
        => Value = Math.Clamp(Value - Step, Minimum, Maximum);

    private bool _updating;

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        if (int.TryParse(ValueBox.Text, out var parsed))
            SetValue(ValueProperty, Math.Clamp(parsed, Minimum, Maximum));
    }

    private void OnLostFocus(object sender, RoutedEventArgs e) => UpdateText();

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Up)
        {
            Value += Step;
            e.Handled = true;
        }

        if (e.Key == Windows.System.VirtualKey.Down)
        {
            Value -= Step;
            e.Handled = true;
        }

        if (e.Key == Windows.System.VirtualKey.Enter)
            UpdateText();
    }

    private void UpdateText()
    {
        _updating = true;
        ValueBox.Text = Value.ToString();
        _updating = false;
    }
}
