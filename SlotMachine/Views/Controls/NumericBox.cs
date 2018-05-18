﻿using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SlotMachine.Views.Controls
{
    [TemplatePart(Name = ElementTextBox, Type = typeof(TextBox))]
    public class NumericBox : Control
    {
        #region DEPENDENCY PROPERTIES

        public static readonly DependencyProperty TextAlignmentProperty =
            TextBox.TextAlignmentProperty.AddOwner(typeof(NumericBox));

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value),
            typeof(double?),
            typeof(NumericBox),
            new FrameworkPropertyMetadata(default(double?), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged, CoerceValue));

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
            nameof(Minimum),
            typeof(double),
            typeof(NumericBox),
            new FrameworkPropertyMetadata(double.MinValue, OnMinimumChanged));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum),
            typeof(double),
            typeof(NumericBox),
            new FrameworkPropertyMetadata(double.MaxValue, OnMaximumChanged, CoerceMaximum));

        public static readonly DependencyProperty IntervalProperty = DependencyProperty.Register(
            nameof(Interval),
            typeof(double),
            typeof(NumericBox),
            new FrameworkPropertyMetadata(DefaultInterval, IntervalChanged));

        public static readonly DependencyProperty HasDecimalsProperty = DependencyProperty.Register(
            nameof(HasDecimals),
            typeof(bool),
            typeof(NumericBox),
            new FrameworkPropertyMetadata(true, OnHasDecimalsChanged));

        #endregion DEPENDENCY PROPERTIES

        #region ROUTED EVENTS

        public static readonly RoutedEvent ValueIncrementedEvent = EventManager.RegisterRoutedEvent("ValueIncremented",
            RoutingStrategy.Bubble, typeof(NumericBoxChangedRoutedEventHandler), typeof(NumericBox));

        public static readonly RoutedEvent ValueDecrementedEvent = EventManager.RegisterRoutedEvent("ValueDecremented",
            RoutingStrategy.Bubble, typeof(NumericBoxChangedRoutedEventHandler), typeof(NumericBox));

        public static readonly RoutedEvent DelayChangedEvent = EventManager.RegisterRoutedEvent("DelayChanged",
            RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NumericBox));

        public static readonly RoutedEvent MaximumReachedEvent = EventManager.RegisterRoutedEvent("MaximumReached",
            RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NumericBox));

        public static readonly RoutedEvent MinimumReachedEvent = EventManager.RegisterRoutedEvent("MinimumReached",
            RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NumericBox));

        public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent("ValueChanged",
            RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<double?>), typeof(NumericBox));

        #endregion ROUTED EVENTS


        #region FIELDS

        private const double DefaultInterval = 1d;
        private const string ElementTextBox = "TextBoxPart";
        private const string ScientificNotationChar = "E";

        private readonly Tuple<string, string> _removeFromText = new Tuple<string, string>(string.Empty, string.Empty);

        private double _internalIntervalMultiplierForCalculation = DefaultInterval;
        private double _internalLargeChange = DefaultInterval * 100;
        private double _intervalValueSinceReset;
        private TextBox _valueTextBox;

        #endregion FIELDS


        #region CONSTRUCTORS

        static NumericBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NumericBox),
                new FrameworkPropertyMetadata(typeof(NumericBox)));

            VerticalContentAlignmentProperty.OverrideMetadata(typeof(NumericBox),
                new FrameworkPropertyMetadata(VerticalAlignment.Center));
            HorizontalContentAlignmentProperty.OverrideMetadata(typeof(NumericBox),
                new FrameworkPropertyMetadata(HorizontalAlignment.Right));
        }

        #endregion CONSTRUCTORS


        #region PROPERTIES

        public double Interval
        {
            get => (double) GetValue(IntervalProperty);
            set => SetValue(IntervalProperty, value);
        }

        public double Maximum
        {
            get => (double) GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public double Minimum
        {
            get => (double) GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public TextAlignment TextAlignment
        {
            get => (TextAlignment) GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        public double? Value
        {
            get => (double?) GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public bool HasDecimals
        {
            get => (bool) GetValue(HasDecimalsProperty);
            set => SetValue(HasDecimalsProperty, value);
        }

        #endregion PROPERTIES

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _valueTextBox = GetTemplateChild(ElementTextBox) as TextBox;

            if (_valueTextBox == null)
                throw new InvalidOperationException($@"Element {ElementTextBox} not found");

            _valueTextBox.LostFocus += OnTextBoxLostFocus;
            _valueTextBox.PreviewTextInput += OnPreviewTextInput;
            _valueTextBox.PreviewKeyDown += OnTextBoxKeyDown;
            _valueTextBox.TextChanged += OnTextChanged;

            DataObject.AddPastingHandler(_valueTextBox, OnValueTextBoxPaste);

            OnValueChanged(Value, Value);
        }

        public void SelectAll() => _valueTextBox?.SelectAll();

        protected virtual void OnMaximumChanged(double oldMaximum, double newMaximum)
        {
        }

        protected virtual void OnMinimumChanged(double oldMinimum, double newMinimum)
        {
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            switch (e.Key)
            {
                case Key.Up:
                    ChangeValueWithSpeedUp(true);
                    e.Handled = true;
                    break;
                case Key.Down:
                    ChangeValueWithSpeedUp(false);
                    e.Handled = true;
                    break;
            }

            if (e.Handled)
            {
                InternalSetText(Value);
            }
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);

            if (e.Key == Key.Down ||
                e.Key == Key.Up)
                ResetInternal();
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);

            if (!IsFocused && !_valueTextBox.IsFocused)
                return;

            var increment = e.Delta > 0;
            ChangeValueInternal(increment);
        }

        protected void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = true;
            if (string.IsNullOrWhiteSpace(e.Text) ||
                e.Text.Length != 1)
                return;

            var text = e.Text;
            if (char.IsDigit(text[0]))
                e.Handled = false;
            else
            {
                var equivalentCulture = CultureInfo.CurrentCulture;
                var numberFormatInfo = equivalentCulture.NumberFormat;
                var textBox = (TextBox) sender;
                var allTextSelected = textBox.SelectedText == textBox.Text;

                if (numberFormatInfo.NumberDecimalSeparator == text)
                {
                    if (textBox.Text.All(i =>
                            i.ToString(equivalentCulture) != numberFormatInfo.NumberDecimalSeparator) ||
                        allTextSelected && HasDecimals)
                        e.Handled = false;
                }
                else
                {
                    if (numberFormatInfo.NegativeSign == text ||
                        text == numberFormatInfo.PositiveSign)
                    {
                        if (textBox.SelectionStart == 0)
                        {
                            if (textBox.Text.Length > 1)
                            {
                                if (allTextSelected ||
                                    !textBox.Text.StartsWith(numberFormatInfo.NegativeSign,
                                        StringComparison.CurrentCultureIgnoreCase) &&
                                    !textBox.Text.StartsWith(numberFormatInfo.PositiveSign,
                                        StringComparison.CurrentCultureIgnoreCase))
                                    e.Handled = false;
                            }
                            else
                                e.Handled = false;
                        }
                        else if (textBox.SelectionStart > 0)
                        {
                            var elementBeforeCaret = textBox.Text
                                .ElementAt(textBox.SelectionStart - 1)
                                .ToString(equivalentCulture);

                            if (elementBeforeCaret.Equals(ScientificNotationChar,
                                StringComparison.CurrentCultureIgnoreCase))
                                e.Handled = false;
                        }
                    }
                    else if (text.Equals(ScientificNotationChar, StringComparison.CurrentCultureIgnoreCase) &&
                             textBox.SelectionStart > 0 &&
                             !textBox.Text.Any(i =>
                                 i.ToString(equivalentCulture).Equals(ScientificNotationChar,
                                     StringComparison.CurrentCultureIgnoreCase)))
                        e.Handled = false;
                }
            }
        }

        protected virtual void OnSpeedupChanged(bool oldSpeedup, bool newSpeedup)
        {
        }

        protected virtual void OnValueChanged(double? oldValue, double? newValue)
        {
            if (!newValue.HasValue)
            {
                if (_valueTextBox != null)
                    _valueTextBox.Text = null;

                if (oldValue != null)
                    RaiseEvent(new RoutedPropertyChangedEventArgs<double?>(oldValue, null, ValueChangedEvent));

                return;
            }

            if (newValue <= Minimum)
            {
                ResetInternal();

                if (IsLoaded)
                    RaiseEvent(new RoutedEventArgs(MinimumReachedEvent));
            }

            if (newValue >= Maximum)
            {
                ResetInternal();
                if (IsLoaded)
                    RaiseEvent(new RoutedEventArgs(MaximumReachedEvent));
            }

            if (_valueTextBox != null)
                InternalSetText(newValue);

            if (oldValue != null && !Equals(oldValue, newValue))
                RaiseEvent(new RoutedPropertyChangedEventArgs<double?>(oldValue, newValue, ValueChangedEvent));
        }


        private void InternalSetText(double? newValue)
        {
            if (!newValue.HasValue)
            {
                _valueTextBox.Text = null;
                return;
            }

            _valueTextBox.Text = newValue.Value.ToString(CultureInfo.CurrentCulture);
        }

        private void ChangeValueWithSpeedUp(bool toPositive)
        {
            double direction = toPositive ? 1 : -1;
            var d = Interval * _internalLargeChange;
            if ((_intervalValueSinceReset += Interval * _internalIntervalMultiplierForCalculation) > d)
            {
                _internalLargeChange *= 10;
                _internalIntervalMultiplierForCalculation *= 10;
            }

            ChangeValueInternal(direction * _internalIntervalMultiplierForCalculation);
        }

        private void ChangeValueInternal(bool addInterval)
        {
            ChangeValueInternal(addInterval ? Interval : -Interval);
        }

        private void ChangeValueInternal(double interval)
        {
            var routedEvent = interval > 0
                ? new NumericBoxChangedRoutedEventArgs(ValueIncrementedEvent, interval)
                : new NumericBoxChangedRoutedEventArgs(ValueDecrementedEvent, interval);

            RaiseEvent(routedEvent);

            if (!routedEvent.Handled)
            {
                ChangeValueBy(routedEvent.Interval);
                _valueTextBox.CaretIndex = _valueTextBox.Text.Length;
            }
        }

        private void ChangeValueBy(double difference)
        {
            var newValue = Value.GetValueOrDefault() + difference;
            Value = (double) CoerceValue(this, newValue);
        }

        private void ResetInternal()
        {
            _internalLargeChange = 100 * Interval;
            _internalIntervalMultiplierForCalculation = Interval;
            _intervalValueSinceReset = 0;
        }

        private bool ValidateText(string text, out double convertedValue)
        {
            text = RemoveStringFormatFromText(text);

            return double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out convertedValue);
        }

        private string RemoveStringFormatFromText(string text)
        {
            if (!string.IsNullOrEmpty(_removeFromText.Item1))
                text = text.Replace(_removeFromText.Item1, string.Empty);

            if (!string.IsNullOrEmpty(_removeFromText.Item2))
                text = text.Replace(_removeFromText.Item2, string.Empty);

            return text;
        }

        #region TEXT BOX EVENT HANDLERS

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (!HasDecimals || e.Key != Key.Decimal && e.Key != Key.OemPeriod)
                return;

            var textBox = sender as TextBox;

            if (textBox?.Text.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator) == false)
            {
                var caret = textBox.CaretIndex;
                textBox.Text = textBox.Text.Insert(caret,
                    CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator);
                textBox.CaretIndex = caret + 1;
            }

            e.Handled = true;
        }

        private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            var tb = (TextBox) sender;
          
            if (ValidateText(tb.Text, out var convertedValue))
            {
                if (Equals(Value, convertedValue))
                    OnValueChanged(Value, Value);

                if (convertedValue > Maximum)
                {
                    if (Equals(Value, Maximum))
                        OnValueChanged(Value, Value);
                    else
                        SetValue(ValueProperty, Maximum);
                }
                else if (convertedValue < Minimum)
                {
                    if (Equals(Value, Minimum))
                        OnValueChanged(Value, Value);
                    else
                        SetValue(ValueProperty, Minimum);
                }
                else
                    SetValue(ValueProperty, convertedValue);
            }
            else
                OnValueChanged(Value, Value);
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(((TextBox) sender).Text))
                Value = null;
            else if (ValidateText(((TextBox) sender).Text, out var convertedValue))
            {
                Value = (double?) CoerceValue(this, convertedValue);
                e.Handled = true;
            }
        }

        private void OnValueTextBoxPaste(object sender, DataObjectPastingEventArgs e)
        {
            var textBox = (TextBox) sender;
            var textPresent = textBox.Text;

            var isText = e.SourceDataObject.GetDataPresent(DataFormats.Text, true);
            if (!isText)
                return;

            var text = e.SourceDataObject.GetData(DataFormats.Text) as string;

            var newText = string.Concat(
                textPresent.Substring(0, textBox.SelectionStart),
                text,
                textPresent.Substring(textBox.SelectionStart + textBox.SelectionLength));

            if (!ValidateText(newText, out var _))
                e.CancelCommand();
        }

        #endregion TEXT BOX EVENT HANDLERS

        #region DEPENDENCY PROPERTY CALLBACK

        private static object CoerceMaximum(DependencyObject d, object value)
        {
            var minimum = ((NumericBox) d).Minimum;
            var val = (double) value;
            return val < minimum ? minimum : val;
        }

        private static object CoerceValue(DependencyObject d, object value)
        {
            if (value == null)
                return null;

            var numericBox = (NumericBox) d;
            var val = ((double?) value).Value;

            if (numericBox.HasDecimals == false)
                val = Math.Truncate(val);

            if (val < numericBox.Minimum)
                return numericBox.Minimum;

            if (val > numericBox.Maximum)
                return numericBox.Maximum;

            return val;
        }

        private static void IntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var numericBox = (NumericBox) d;

            numericBox.ResetInternal();
        }

        private static void OnMaximumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var numericBox = (NumericBox) d;

            numericBox.CoerceValue(ValueProperty);
            numericBox.OnMaximumChanged((double) e.OldValue, (double) e.NewValue);
        }

        private static void OnMinimumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var numericBox = (NumericBox) d;

            numericBox.CoerceValue(MaximumProperty);
            numericBox.CoerceValue(ValueProperty);
            numericBox.OnMinimumChanged((double) e.OldValue, (double) e.NewValue);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var numericBox = (NumericBox) d;

            numericBox.OnValueChanged((double?) e.OldValue, (double?) e.NewValue);
        }

        private static void OnHasDecimalsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var numericBox = (NumericBox) d;

            if ((bool) e.NewValue == false && numericBox.Value != null)
                numericBox.Value = Math.Truncate(numericBox.Value.GetValueOrDefault());
        }

        #endregion DEPENDENCY PROPERTY CALLBACK

        #region EVENTS

        public event RoutedPropertyChangedEventHandler<double?> ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        public event NumericBoxChangedRoutedEventHandler ValueIncremented
        {
            add => AddHandler(ValueIncrementedEvent, value);
            remove => RemoveHandler(ValueIncrementedEvent, value);
        }

        public event NumericBoxChangedRoutedEventHandler ValueDecremented
        {
            add => AddHandler(ValueDecrementedEvent, value);
            remove => RemoveHandler(ValueDecrementedEvent, value);
        }

        public event RoutedEventHandler DelayChanged
        {
            add => AddHandler(DelayChangedEvent, value);
            remove => RemoveHandler(DelayChangedEvent, value);
        }

        #endregion EVENTS
    }
}