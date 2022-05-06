﻿#region (c) 2019 Gilles Macabies All right reserved

// Author     : Gilles Macabies
// Solution   : FilterDataGrid
// Projet     : FilterDataGrid.Net5.0
// File       : FilterDataGrid.cs
// Created    : 06/03/2022
// 

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

// ReSharper disable InlineTemporaryVariable
// ReSharper disable UnusedMember.Local
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ArrangeAccessorOwnerBody
// ReSharper disable InvertIf
// ReSharper disable ExplicitCallerInfoArgument
// ReSharper disable CheckNamespace

// https://stackoverflow.com/questions/3685566/wpf-using-resizegrip-to-resize-controls
// https://www.c-sharpcorner.com/UploadFile/mahesh/binding-static-properties-in-wpf-4-5/
// https://www.csharp-examples.net/string-format-datetime/

namespace FilterDataGrid
{
    /// <summary>
    ///     Implementation of Datagrid
    /// </summary>
    public sealed class FilterDataGrid : DataGrid, INotifyPropertyChanged
    {
        #region Constructors

        /// <summary>
        ///     FilterDataGrid constructor
        /// </summary>
        public FilterDataGrid()
        {
            DefaultStyleKey = typeof(FilterDataGrid);

            Debug.WriteLineIf(DebugMode, "Constructor");

            // load resources
            var resourcesDico = new ResourceDictionary
            {
                Source = new Uri("/FilterDataGrid;component/Themes/FilterDataGrid.xaml",
                    UriKind.RelativeOrAbsolute)
            };

            Resources.MergedDictionaries.Add(resourcesDico);

            // initial popup size
            popUpSize = new Point
            {
                X = (double)TryFindResource("PopupWidth"),
                Y = (double)TryFindResource("PopupHeight")
            };

            CommandBindings.Add(new CommandBinding(ShowFilter, ShowFilterCommand, CanShowFilter));
            CommandBindings.Add(new CommandBinding(ApplyFilter, ApplyFilterCommand, CanApplyFilter)); // Ok
            CommandBindings.Add(new CommandBinding(CancelFilter, CancelFilterCommand));
            CommandBindings.Add(new CommandBinding(RemoveFilter, RemoveFilterCommand, CanRemoveFilter));
            CommandBindings.Add(new CommandBinding(IsChecked, CheckedAllCommand));
            CommandBindings.Add(new CommandBinding(ClearSearchBox, ClearSearchBoxClick));
        }

        #endregion Constructors

        #region Command

        public static readonly ICommand ApplyFilter = new RoutedCommand();

        public static readonly ICommand CancelFilter = new RoutedCommand();

        public static readonly ICommand ClearSearchBox = new RoutedCommand();

        public static readonly ICommand IsChecked = new RoutedCommand();

        public static readonly ICommand RemoveFilter = new RoutedCommand();

        public static readonly ICommand ShowFilter = new RoutedCommand();

        #endregion Command

        #region Public DependencyProperty

        /// <summary>
        ///     Excluded Fields on AutoColumn
        /// </summary>
        public static readonly DependencyProperty ExcludeFieldsProperty =
            DependencyProperty.Register("ExcludeFields",
                typeof(string),
                typeof(FilterDataGrid),
                new PropertyMetadata(""));

        /// <summary>
        ///     date format displayed
        /// </summary>
        public static readonly DependencyProperty DateFormatStringProperty =
            DependencyProperty.Register("DateFormatString",
                typeof(string),
                typeof(FilterDataGrid),
                new PropertyMetadata("d"));

        /// <summary>
        ///     Language displayed
        /// </summary>
        public static readonly DependencyProperty FilterLanguageProperty =
            DependencyProperty.Register("FilterLanguage",
                typeof(Local),
                typeof(FilterDataGrid),
                new PropertyMetadata(Local.English));

        /// <summary>
        ///     Show elapsed time in status bar
        /// </summary>
        public static readonly DependencyProperty ShowElapsedTimeProperty =
            DependencyProperty.Register("ShowElapsedTime",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        /// <summary>
        ///     Show statusbar
        /// </summary>
        public static readonly DependencyProperty ShowStatusBarProperty =
            DependencyProperty.Register("ShowStatusBar",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        /// <summary>
        ///     Show Rows Count
        /// </summary>
        public static readonly DependencyProperty ShowRowsCountProperty =
            DependencyProperty.Register("ShowRowsCount",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        #endregion Public DependencyProperty

        #region Public Event

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler Sorted;

        #endregion Public Event

        #region Private Fields

        private bool pending;
        private bool search;
        private Button button;
        private const bool DebugMode = false;
        private Cursor cursor;
        private int searchLength;
        private double minHeight;
        private double minWidth;
        private double sizableContentHeight;
        private double sizableContentWidth;
        private Grid sizableContentGrid;

        private List<object> sourceObjectList;
        private List<string> excludedFields;

        private Point popUpSize;
        private Popup popup;

        private string fieldName;
        private string lastFilter;
        private string searchText;
        private TextBox searchTextBox;
        private Thumb thumb;

        private TimeSpan elased;

        private TreeView treeview;
        private ListBox listBox;

        private Type collectionType;
        private Type fieldType;

        private bool startsWith;
        private object currentColumn;

        private readonly Dictionary<string, Predicate<object>> criteria = new Dictionary<string, Predicate<object>>();
        private readonly StringComparison ordinalIgnoreCase = StringComparison.OrdinalIgnoreCase;

        #endregion Private Fields

        #region Public Properties

        /// <summary>
        ///     Excluded Fileds
        /// </summary>
        public string ExcludeFields
        {
            get { return (string)GetValue(ExcludeFieldsProperty); }
            set { SetValue(ExcludeFieldsProperty, value); }
        }

        /// <summary>
        ///     String begins with the specified character. Used in popup searchBox
        /// </summary>
        public bool StartsWith
        {
            get => startsWith;
            set
            {
                startsWith = value;
                OnPropertyChanged();

                // refresh filter
                if (!string.IsNullOrEmpty(searchText)) ItemCollectionView.Refresh();
            }
        }

        /// <summary>
        ///     Date format displayed
        /// </summary>
        public string DateFormatString
        {
            get { return (string)GetValue(DateFormatStringProperty); }
            set { SetValue(DateFormatStringProperty, value); }
        }

        /// <summary>
        ///     Elapsed time
        /// </summary>
        public TimeSpan ElapsedTime
        {
            get => elased;
            set
            {
                elased = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        ///     Language
        /// </summary>
        public Local FilterLanguage
        {
            get { return (Local)GetValue(FilterLanguageProperty); }
            set { SetValue(FilterLanguageProperty, value); }
        }

        /// <summary>
        ///     Display items count
        /// </summary>
        public int ItemsSourceCount { get; set; }

        /// <summary>
        ///     Show elapsed time in status bar
        /// </summary>
        public bool ShowElapsedTime
        {
            get { return (bool)GetValue(ShowElapsedTimeProperty); }
            set { SetValue(ShowElapsedTimeProperty, value); }
        }

        /// <summary>
        ///     Show status bar
        /// </summary>
        public bool ShowStatusBar
        {
            get { return (bool)GetValue(ShowStatusBarProperty); }
            set { SetValue(ShowStatusBarProperty, value); }
        }

        /// <summary>
        ///     Show rows count
        /// </summary>
        public bool ShowRowsCount
        {
            get { return (bool)GetValue(ShowRowsCountProperty); }
            set { SetValue(ShowRowsCountProperty, value); }
        }

        /// <summary>
        ///     Instance of Loc
        /// </summary>
        public Loc Translate { get; private set; }

        /// <summary>
        ///     Row header size when ShowRowsCount is true
        /// </summary>
        public double RowHeaderSize { get; set; }

        #endregion Public Properties

        #region Private Properties

        private ICollectionView CollectionViewSource { get; set; }
        private FilterCommon CurrentFilter { get; set; }
        private List<FilterCommon> GlobalFilterList { get; } = new List<FilterCommon>();
        private ICollectionView ItemCollectionView { get; set; }

        private IEnumerable<FilterItem> PopupViewItems =>
            ItemCollectionView?.Cast<FilterItem>().Skip(1) ?? new List<FilterItem>();

        /// <summary>
        ///     DatagridFilterStyleKey ComponentResourceKey
        /// </summary>
        private static ComponentResourceKey DatagridFilterStyleKey =>
            new ComponentResourceKey(typeof(FilterDataGrid), "FilterDataGridStyle");

        /// <summary>
        ///     DataGridStyle, only internal usage
        /// </summary>
        private Style FilterDataGridStyle => (Style)TryFindResource(DatagridFilterStyleKey);

        #endregion Private Properties

        #region Protected Methods

        // CALL ORDER :
        // Constructor
        // OnInitialized
        // OnItemsSourceChanged

        /// <summary>
        ///     Initialize datagrid
        /// </summary>
        /// <param name="e"></param>
        protected override void OnInitialized(EventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "OnInitialized");

            base.OnInitialized(e);

            try
            {
                // FilterLanguage : default : 0 (english)
                Translate = new Loc { Language = FilterLanguage };

                // Show row count
                RowHeaderWidth = ShowRowsCount ? RowHeaderWidth > 0 ? RowHeaderWidth : double.NaN : 0;

                // fill excluded Fields list with values
                if (AutoGenerateColumns)
                    excludedFields = ExcludeFields.Split(',').Select(p => p.Trim()).ToList();

                // sorting event
                Sorted += OnSorted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.OnInitialized : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Auto generated column, set templateHeader
        /// </summary>
        /// <param name="e"></param>
        protected override void OnAutoGeneratingColumn(DataGridAutoGeneratingColumnEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "OnAutoGeneratingColumn");

            base.OnAutoGeneratingColumn(e);

            try
            {
                if (e.Column.GetType() != typeof(System.Windows.Controls.DataGridTextColumn)) return;

                var column = new DataGridTextColumn
                {
                    Binding = new Binding(e.PropertyName) { ConverterCulture = Translate.Culture /* StringFormat */ },
                    FieldName = e.PropertyName,
                    Header = e.Column.Header.ToString(),
                    IsColumnFiltered = false
                };

                // get type
                fieldType = Nullable.GetUnderlyingType(e.PropertyType) ?? e.PropertyType;

                // apply the format string provided
                if (fieldType == typeof(DateTime) && !string.IsNullOrEmpty(DateFormatString))
                    column.Binding.StringFormat = DateFormatString;

                // add DataGridHeaderTemplate template if not excluded
                if (excludedFields?.FindIndex(c =>
                        string.Equals(c, e.PropertyName, StringComparison.CurrentCultureIgnoreCase)) == -1)
                {
                    column.HeaderTemplate = (DataTemplate)TryFindResource("DataGridHeaderTemplate");
                    column.IsColumnFiltered = true;
                }

                e.Column = column;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.OnAutoGeneratingColumn : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     The source of the Datagrid items has been changed (refresh or on loading)
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            Debug.WriteLineIf(DebugMode, "OnItemsSourceChanged");

            base.OnItemsSourceChanged(oldValue, newValue);

            try
            {
                if (newValue == null) return;
                
                // reset current filter, !important
                CurrentFilter = null;

                // reset GlobalFilterList list
                GlobalFilterList.Clear();

                // reset criteria List
                criteria.Clear();

                CollectionViewSource = System.Windows.Data.CollectionViewSource.GetDefaultView(ItemsSource);

                // set Filter, contribution : STEFAN HEIMEL
                if (CollectionViewSource.CanFilter) CollectionViewSource.Filter = Filter;

                ItemsSourceCount = Items.Count;
                ElapsedTime = new TimeSpan(0, 0, 0);
                OnPropertyChanged("ItemsSourceCount");

                // Calculate row header width
                if (ShowRowsCount)
                {
                    TextBlock txt = new TextBlock { Text = ItemsSourceCount.ToString(), FontSize = FontSize, FontFamily = FontFamily, Margin = new Thickness(2.0) };
                    txt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    RowHeaderSize = Math.Ceiling(txt.DesiredSize.Width);
                    OnPropertyChanged("RowHeaderSize");
                }

                // get collection type
                if (ItemsSourceCount > 0)
                {
                    // contribution : APFLKUACHA 
                    collectionType = ItemsSource is ICollectionView collectionView
                        ? collectionView.SourceCollection?.GetType().GenericTypeArguments.FirstOrDefault()
                        : ItemsSource?.GetType().GenericTypeArguments.FirstOrDefault();
                }

                // scroll to top on reload collection
                if (oldValue != null)
                {
                    var scrollViewer = GetTemplateChild("DG_ScrollViewer") as ScrollViewer;
                    scrollViewer?.ScrollToTop();
                }

                // generating custom columns
                if (!AutoGenerateColumns && collectionType != null) GeneratingCustomsColumn();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.OnItemsSourceChanged : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Set the cursor to "Cursors.Wait" during a long sorting operation
        ///     https://stackoverflow.com/questions/8416961/how-can-i-be-notified-if-a-datagrid-column-is-sorted-and-not-sorting
        /// </summary>
        /// <param name="eventArgs"></param>
        protected override void OnSorting(DataGridSortingEventArgs eventArgs)
        {
            if (pending || (popup?.IsOpen ?? false)) return;

            Mouse.OverrideCursor = Cursors.Wait;
            base.OnSorting(eventArgs);
            Sorted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        ///     Adding Rows count
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoadingRow(DataGridRowEventArgs e) => e.Row.Header = (e.Row.GetIndex() + 1).ToString();

        #endregion Protected Methods

        #region Private Methods

        /// <summary>
        ///     Handle Mousedown, contribution : WORDIBOI
        /// </summary>
        private readonly MouseButtonEventHandler onMousedown = (o, eArgs) => { eArgs.Handled = true; };

        /// <summary>
        ///     Generate custom columns that can be filtered
        /// </summary>
        private void GeneratingCustomsColumn()
        {
            Debug.WriteLineIf(DebugMode, "GeneratingCustomColumn");

            try
            {
                // get the columns that can be filtered
                var columns = Columns
                    .Where(c => c is DataGridTextColumn dtx && dtx.IsColumnFiltered ||
                                c is DataGridTemplateColumn dtp && dtp.IsColumnFiltered)
                    .Select(c => c)
                    .ToList();

                // set header template
                foreach (var col in columns)
                {
                    var columnType = col.GetType();

                    if (col.HeaderTemplate != null)
                    {
                        // reset filter Button
                        var buttonFilter = VisualTreeHelpers.GetHeader(col, this)
                            ?.FindVisualChild<Button>("FilterButton");
                        if (buttonFilter != null) FilterState.SetIsFiltered(buttonFilter, false);
                    }
                    else
                    {
                        if (columnType == typeof(DataGridTextColumn))
                        {
                            var column = (DataGridTextColumn)col;

                            // template
                            column.HeaderTemplate = (DataTemplate)TryFindResource("DataGridHeaderTemplate");

                            fieldType = null;
                            var fieldProperty = collectionType.GetProperty(((Binding)column.Binding).Path.Path);

                            // get type or underlying type if nullable
                            if (fieldProperty != null)
                                fieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ??
                                            fieldProperty.PropertyType;

                            // apply DateFormatString when StringFormat for column is not provided or empty
                            if (fieldType == typeof(DateTime) && !string.IsNullOrEmpty(DateFormatString))
                                if (string.IsNullOrEmpty(column.Binding.StringFormat))
                                    column.Binding.StringFormat = DateFormatString;

                            // culture
                            if (((Binding)column.Binding).ConverterCulture == null)
                                ((Binding)column.Binding).ConverterCulture = Translate.Culture;

                            column.FieldName = ((Binding)column.Binding).Path.Path;
                        }
                        else if (columnType == typeof(DataGridTemplateColumn))
                        {
                            // DataGridTemplateColumn has no culture property
                            var column = (DataGridTemplateColumn)col;

                            // template
                            column.HeaderTemplate = (DataTemplate)TryFindResource("DataGridHeaderTemplate");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.GeneratingCustomColumn : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Reset the cursor at the end of the sort
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSorted(object sender, EventArgs e)
        {
            ResetCursor();
        }

        /// <summary>
        ///     Reactivate sorting
        /// </summary>
        private void ReactivateSorting()
        {
            switch (currentColumn)
            {
                case null:
                    return;

                case DataGridTextColumn column:
                    column.CanUserSort = true;
                    break;

                case DataGridTemplateColumn templateColumn:
                    templateColumn.CanUserSort = true;
                    break;
            }
        }

        /// <summary>
        ///     Reset cursor
        /// </summary>
        private async void ResetCursor()
        {
            // reset cursor
            await Dispatcher.BeginInvoke((Action)(() => { Mouse.OverrideCursor = null; }),
                DispatcherPriority.ContextIdle);
        }

        /// <summary>
        ///     Can Apply filter (popup Ok button)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanApplyFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            // CanExecute only when the popup is open
            if ((popup?.IsOpen ?? false) == false)
            {
                e.CanExecute = false;
            }
            else
            {
                if (search)
                    // in search, at least one article must be checked
                    e.CanExecute = CurrentFilter?.FieldType == typeof(DateTime)
                        ? CurrentFilter.AnyDateIsChecked()
                        : PopupViewItems.Any(f => f?.IsChecked == true);
                else
                    // on change state, at least one item must be checked and another must have changed status
                    e.CanExecute = CurrentFilter?.FieldType == typeof(DateTime)
                        ? CurrentFilter.AnyDateChanged()
                        : PopupViewItems.Any(f => f.Changed) && PopupViewItems.Any(f => f?.IsChecked == true);
            }
        }

        /// <summary>
        ///     Cancel button, close popup
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (popup == null) return;
            popup.IsOpen = false; // raise EventArgs PopupClosed
        }

        /// <summary>
        ///     Can remove filter when current column (CurrentFilter) filtered
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanRemoveFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CurrentFilter?.IsFiltered ?? false;
        }

        /// <summary>
        ///     Can show filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanShowFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CollectionViewSource?.CanFilter == true && (!popup?.IsOpen ?? true) && !pending;
        }

        /// <summary>
        ///     Check/uncheck all item when the action is (select all)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckedAllCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var item = (FilterItem)e.Parameter;

            // only when the item[0] (select all) is checked or unchecked
            if (item?.Id != 0 || ItemCollectionView == null) return;

            foreach (var obj in PopupViewItems.ToList()
                         .Where(f => f.IsChecked != item.IsChecked))
                obj.IsChecked = item.IsChecked;
        }

        /// <summary>
        ///     Clear Search Box text
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="routedEventArgs"></param>
        private void ClearSearchBoxClick(object sender, RoutedEventArgs routedEventArgs)
        {
            search = false;
            searchTextBox.Text = string.Empty; // raises TextChangedEventArgs
        }

        /// <summary>
        ///     Aggregate list of predicate as filter
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        private bool Filter(object o)
        {
            return criteria.Values
                .Aggregate(true, (prevValue, predicate) => prevValue && predicate(o));
        }

        /// <summary>
        ///     OnPropertyChange
        /// </summary>
        /// <param name="propertyName"></param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        ///     On Resize Thumb Drag Completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResizeThumbDragCompleted(object sender, DragCompletedEventArgs e)
        {
            Cursor = cursor;
        }

        /// <summary>
        ///     Get delta on drag thumb
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            // initialize the first Actual size Width/Height
            if (sizableContentHeight <= 0)
            {
                sizableContentHeight = sizableContentGrid.ActualHeight;
                sizableContentWidth = sizableContentGrid.ActualWidth;
            }

            var yAdjust = sizableContentGrid.Height + e.VerticalChange;
            var xAdjust = sizableContentGrid.Width + e.HorizontalChange;

            //make sure not to resize to negative width or heigth
            xAdjust = sizableContentGrid.ActualWidth + xAdjust > minWidth ? xAdjust : minWidth;
            yAdjust = sizableContentGrid.ActualHeight + yAdjust > minHeight ? yAdjust : minHeight;

            xAdjust = xAdjust < minWidth ? minWidth : xAdjust;
            yAdjust = yAdjust < minHeight ? minHeight : yAdjust;

            // set size of grid
            sizableContentGrid.Width = xAdjust;
            sizableContentGrid.Height = yAdjust;
        }

        /// <summary>
        ///     On Resize Thumb DragStarted
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResizeThumbDragStarted(object sender, DragStartedEventArgs e)
        {
            cursor = Cursor;
            Cursor = Cursors.SizeNWSE;
        }

        /// <summary>
        ///     Reset the size of popup to original size
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PopupClosed(object sender, EventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "PopupClosed");

            var pop = (Popup)sender;

            // free the resources if the popup is closed without filtering
            if (!pending)
            {
                // clear resources
                sourceObjectList = null;
                ItemCollectionView = null;
                CurrentFilter = null;
                ReactivateSorting();
            }

            // unsubscribe from event and re-enable datagrid
            pop.MouseDown -= onMousedown;
            thumb.DragCompleted -= OnResizeThumbDragCompleted;
            thumb.DragDelta -= OnResizeThumbDragDelta;
            thumb.DragStarted -= OnResizeThumbDragStarted;
            searchTextBox.TextChanged -= SearchTextBoxOnTextChanged;
            pop.Closed -= PopupClosed;

            sizableContentGrid.Width = sizableContentWidth;
            sizableContentGrid.Height = sizableContentHeight;
            Cursor = cursor;

            // re-enable datagrid
            IsEnabled = true;
        }

        /// <summary>
        ///     Remove current filter
        /// </summary>
        private void RemoveCurrentFilter()
        {
            Debug.WriteLineIf(DebugMode, "RemoveCurrentFilter");

            if (CurrentFilter == null) return;

            popup.IsOpen = false;

            // button icon reset
            FilterState.SetIsFiltered(button, false);

            var start = DateTime.Now;
            ElapsedTime = new TimeSpan(0, 0, 0);

            Mouse.OverrideCursor = Cursors.Wait;

            if (CurrentFilter.IsFiltered && criteria.Remove(CurrentFilter.FieldName))
                CollectionViewSource.Refresh();

            if (GlobalFilterList.Contains(CurrentFilter))
                _ = GlobalFilterList.Remove(CurrentFilter);

            // set the last filter applied
            lastFilter = GlobalFilterList.LastOrDefault()?.FieldName;

            ElapsedTime = DateTime.Now - start;

            CurrentFilter.IsFiltered = false;

            ResetCursor();
        }

        /// <summary>
        ///     remove current filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            RemoveCurrentFilter();
        }

        /// <summary>
        ///     Filter current list in popup
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private bool SearchFilter(object obj)
        {
            var item = (FilterItem)obj;
            if (string.IsNullOrEmpty(searchText) || item == null || item.Id == 0) return true;
            
            // Contains
            if (!StartsWith)
                return item.FieldType == typeof(DateTime)
                    ? ((DateTime?)item.Content)?.ToString(DateFormatString, Translate.Culture)
                    .IndexOf(searchText, ordinalIgnoreCase) >= 0
                    : item.Content?.ToString().IndexOf(searchText, ordinalIgnoreCase) >= 0;

            // StartsWith preserve RangeOverflow
            if (searchLength > item.ContentLength) return false;

            return item.FieldType == typeof(DateTime)
                ? ((DateTime?)item.Content)?.ToString(DateFormatString, Translate.Culture)
                .IndexOf(searchText, 0, searchLength, ordinalIgnoreCase) >= 0
                : item.Content?.ToString().IndexOf(searchText, 0, searchLength, ordinalIgnoreCase) >=
                  0;
        }

        /// <summary>
        ///     Search TextBox Text Changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchTextBoxOnTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;

            // fix TextChanged event fires twice I did not find another solution
            if (textBox == null || textBox.Text == searchText || ItemCollectionView == null) return;

            searchText = textBox.Text;

            searchLength = searchText.Length;

            search = !string.IsNullOrEmpty(searchText);

            // apply filter
            ItemCollectionView.Refresh();

            if (CurrentFilter.FieldType != typeof(DateTime) || treeview == null) return;

            // rebuild treeview rebuild treeview
            if (string.IsNullOrEmpty(searchText))
            {
                // fill the tree with the elements of the list of the original items
                treeview.ItemsSource = CurrentFilter.BuildTree(sourceObjectList, lastFilter);
            }
            else
            {
                // fill the tree only with the items found by the search
                var items = PopupViewItems.Where(i => i.IsChecked == true)
                    .Select(f => f.Content).ToList();

                // if at least one item is not null, fill in the tree structure otherwise the tree structure contains only the item (select all).
                treeview.ItemsSource = CurrentFilter.BuildTree(items.Any() ? items : null);
            }
        }

        /// <summary>
        ///     Open a pop-up window, Click on the header button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ShowFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "\r\nShowFilterCommand");

            // reset previous elapsed time
            var start = DateTime.Now;

            // clear search text (!important)
            searchText = string.Empty;
            search = false;

            try
            {
                // filter button
                button = (Button)e.OriginalSource;

                if (Items.Count == 0 || button == null) return;

                // contribution : OTTOSSON
                // for the moment this functionality is not tested, I do not know if it can cause unexpected effects
                _ = CommitEdit(DataGridEditingUnit.Row, true);

                // navigate up to the current header and get column type
                var header = VisualTreeHelpers.FindAncestor<DataGridColumnHeader>(button);
                var columnType = header.Column.GetType();

                // then down to the current popup
                popup = VisualTreeHelpers.FindChild<Popup>(header, "FilterPopup");

                if (popup == null) return;

                // popup handle event
                popup.Closed += PopupClosed;

                // disable popup background clickthrough, contribution : WORDIBOI
                popup.MouseDown += onMousedown;

                // disable datagrid while popup is open
                IsEnabled = false;

                // resizable grid
                sizableContentGrid = VisualTreeHelpers.FindChild<Grid>(popup.Child, "SizableContentGrid");

                // search textbox
                searchTextBox = VisualTreeHelpers.FindChild<TextBox>(popup.Child, "SearchBox");
                searchTextBox.Text = string.Empty;
                searchTextBox.TextChanged += SearchTextBoxOnTextChanged;
                searchTextBox.Focusable = true;

                // thumb resize grip
                thumb = VisualTreeHelpers.FindChild<Thumb>(sizableContentGrid, "PopupThumb");

                // minimum size of Grid
                sizableContentHeight = 0;
                sizableContentWidth = 0;

                sizableContentGrid.Height = popUpSize.Y;
                sizableContentGrid.MinHeight = popUpSize.Y;

                minHeight = sizableContentGrid.MinHeight;
                minWidth = sizableContentGrid.MinWidth;

                // thumb handle event
                thumb.DragCompleted += OnResizeThumbDragCompleted;
                thumb.DragDelta += OnResizeThumbDragDelta;
                thumb.DragStarted += OnResizeThumbDragStarted;

                // get field name from binding Path
                if (columnType == typeof(DataGridTextColumn))
                {
                    var column = (DataGridTextColumn)header.Column;
                    fieldName = column.FieldName;
                    column.CanUserSort = false;
                    currentColumn = column;
                }

                if (columnType == typeof(DataGridTemplateColumn))
                {
                    var column = (DataGridTemplateColumn)header.Column;
                    fieldName = column.FieldName;
                    column.CanUserSort = false;
                    currentColumn = column;
                }

                // invalid fieldName
                if (string.IsNullOrEmpty(fieldName)) return;

                // get type of field
                fieldType = null;
                var fieldProperty = collectionType.GetProperty(fieldName);

                // get type or underlying type if nullable
                if (fieldProperty != null)
                    fieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ?? fieldProperty.PropertyType;

                // If no filter, add filter to GlobalFilterList list
                CurrentFilter = GlobalFilterList.FirstOrDefault(f => f.FieldName == fieldName) ??
                                new FilterCommon
                                {
                                    FieldName = fieldName,
                                    FieldType = fieldType,
                                    Translate = Translate
                                };

                // list of all item values, filtered and unfiltered (previous filtered items)
                sourceObjectList = new List<object>();

                // set cursor
                Mouse.OverrideCursor = Cursors.Wait;

                var filterItemList = new List<FilterItem>();

                // get the list of values distinct from the list of raw values of the current column
                await Task.Run(() =>
                {
                    // empty item flag
                    var emptyItem = false;

                    // contribution : STEFAN HEIMEL
                    Dispatcher.Invoke(() =>
                    {
                        if (fieldType == typeof(DateTime))
                        {
                            // possible distinct values because time part is removed
                            sourceObjectList = Items.Cast<object>()
                                .Select(x => (object)((DateTime?)x.GetType().GetProperty(fieldName)?.GetValue(x, null))?.Date)
                                .Distinct()
                                .ToList();
                        }
                        else
                        {
                            sourceObjectList = Items.Cast<object>()
                                .Select(x => x.GetType().GetProperty(fieldName)?.GetValue(x, null))
                                .Distinct()
                                .ToList();
                        }
                    });

                    // adds the previous filtered items to the list of new items (CurrentFilter.PreviouslyFilteredItems) displays new (checked) and
                    if (lastFilter == CurrentFilter.FieldName)
                    {
                        sourceObjectList.AddRange(CurrentFilter?.PreviouslyFilteredItems);
                    }

                    // if they exist, remove from the list all null objects or empty strings
                    if (sourceObjectList.Any(l => l == null || l.Equals(string.Empty) || l.Equals(null)))
                    {
                        // element = null && "" are two different things but labeled as (Blank)
                        // in the list of items to be filtered
                        emptyItem = true;
                        sourceObjectList.RemoveAll(v => v == null || v.Equals(null) || v.Equals(string.Empty));
                    }

                    // sorting is a slow operation, using ParallelQuery
                    // TODO : AggregateException when user can add row
                    sourceObjectList = sourceObjectList.AsParallel().OrderBy(x => x).ToList();

                    // add the first element (select all) at the top of list
                    filterItemList = new List<FilterItem>(sourceObjectList.Count + 2)
                    {
                        new FilterItem { Label = Translate.All, IsChecked = true }
                    };

                    // add all items to the filterItemList filterItemList is used only for search and string list,
                    // the dates list is computed by FilterCommon.BuildTree
                    filterItemList.AddRange(sourceObjectList.Select((item, index) => new FilterItem
                    {
                        Id = index + 1,
                        Content = item,
                        FieldType = fieldType,
                        Label = item.ToString(),
                        ContentLength = item.ToString().Length,
                        Level = 1,
                        SetState = CurrentFilter.PreviouslyFilteredItems?.Contains(item) == false
                    }));

                    // add a empty item(if exist) at the bottom of the list
                    if (emptyItem)
                    {
                        sourceObjectList.Insert(sourceObjectList.Count, null);

                        filterItemList.Add(new FilterItem
                        {
                            Id = filterItemList.Count,
                            FieldType = fieldType,
                            Content = null,
                            Label = Translate.Empty,
                            SetState = CurrentFilter.PreviouslyFilteredItems?.Contains(null) == false
                        });
                    }
                }); // and task

                // the current listbox or treeview
                // the DataTemplateSelector doesn't work well with large
                // size collections, the loading time is extremely long, that's why the "item
                // source" property is populated by code behind.

                if (fieldType == typeof(DateTime))
                {
                    treeview = VisualTreeHelpers.FindChild<TreeView>(popup.Child, "PopupTreeview");

                    if (treeview != null)
                    {
                        // fill the treeview with CurrentFilter.BuildTree method and if it's the last filter, uncheck the items already filtered
                        treeview.ItemsSource =
                            CurrentFilter?.BuildTree(sourceObjectList, lastFilter);
                        treeview.Visibility = Visibility.Visible;
                    }

                    if (listBox != null)
                    {
                        // clear previous data
                        listBox.ItemsSource = null;
                        listBox.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    listBox = VisualTreeHelpers.FindChild<ListBox>(popup.Child, "PopupListBox");
                    if (listBox != null)
                    {
                        // set filterList as ItemsSource of ListBox
                        listBox.Visibility = Visibility.Visible;
                        listBox.ItemsSource = filterItemList;
                        listBox.UpdateLayout();

                        // scroll to top of view
                        var scrollViewer =
                            VisualTreeHelpers.GetDescendantByType(listBox, typeof(ScrollViewer)) as ScrollViewer;
                        scrollViewer?.ScrollToTop();
                    }

                    if (treeview != null)
                    {
                        // clear previous data
                        treeview.ItemsSource = null;
                        treeview.Visibility = Visibility.Collapsed;
                    }
                }

                // Set ICollectionView for filtering in the pop-up window
                ItemCollectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(filterItemList);

                // set filter in popup
                if (ItemCollectionView.CanFilter) ItemCollectionView.Filter = SearchFilter;

                // set the placement and offset of the PopUp in relation to the header and the main window of the application
                // i.e (placement : bottom left or bottom right)
                PopupPlacement(sizableContentGrid, header);

                popup.UpdateLayout();

                // open popup
                popup.IsOpen = true;

                // set focus on searchTextBox
                searchTextBox.Focus();
                Keyboard.Focus(searchTextBox);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.ShowFilterCommand error : {ex.Message}");
                throw;
            }
            finally
            {
                // show open popup elapsed time in UI
                ElapsedTime = DateTime.Now - start;

                // reset cursor
                ResetCursor();
            }
        }

        /// <summary>
        ///     Click OK Button when Popup is Open, apply filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ApplyFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "\r\nApplyFilterCommand");

            var start = DateTime.Now;
            pending = true;
            popup.IsOpen = false; // raise PopupClosed event

            // set cursor wait
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                // items already filtered
                var previousFiltered = new List<object>(CurrentFilter.PreviouslyFilteredItems);
                // list of content of items to filter
                var popupItems = PopupViewItems.ToList();

                await Task.Run(() =>
                {
                    // list of content of items not to be filtered
                    List<FilterItem> uncheckedItems;
                    List<FilterItem> checkedItems = null;

                    if (search)
                    {
                        // search items result displayed
                        var searchResult = popupItems;

                        Dispatcher.Invoke(() =>
                        {
                            // remove filter
                            ItemCollectionView.Filter = null;
                        });

                        // popup = all items except searchResult
                        uncheckedItems = PopupViewItems.Except(searchResult).ToList();
                        uncheckedItems.AddRange(searchResult.Where(c => c.IsChecked == false));

                        previousFiltered = previousFiltered.Except(searchResult
                            .Where(c => c.IsChecked == true)
                            .Select(c => c.Content)).ToList();

                        previousFiltered.AddRange(uncheckedItems.Select(c => c.Content));
                    }
                    else
                    {
                        var viewItems = CurrentFilter.FieldType == typeof(DateTime)
                            ? CurrentFilter.GetAllItemsTree().ToList()
                            : popupItems.Where(v => v.Changed).ToList();

                        checkedItems = viewItems.Where(i => i.IsChecked == true).ToList();
                        uncheckedItems = viewItems.Where(i => i.IsChecked == false).ToList();

                        // previous item except unchecked items checked again
                        previousFiltered = previousFiltered.Except(checkedItems.Select(c => c.Content)).ToList();
                        previousFiltered.AddRange(uncheckedItems.Select(c => c.Content));
                    }

                    // two values, null and string.empty for the list of strings
                    if (CurrentFilter.FieldType == typeof(string))
                    {
                        // add string.Empty
                        if (uncheckedItems.Any(v => v.Content == null))
                            previousFiltered.Add(string.Empty);

                        // remove string.Empty
                        if (checkedItems != null && checkedItems.Any(i => i.Content == null))
                            previousFiltered.RemoveAll(item => item?.ToString() == string.Empty);
                    }

                    // fill the PreviouslyFilteredItems HashSet with unchecked items
                    CurrentFilter.PreviouslyFilteredItems = new HashSet<object>(previousFiltered,
                        EqualityComparer<object>.Default);

                    // add a filter if it is not already added previously
                    if (!CurrentFilter.IsFiltered)
                        CurrentFilter.AddFilter(criteria);

                    // add current filter to GlobalFilterList
                    if (GlobalFilterList.All(f => f.FieldName != CurrentFilter.FieldName))
                        GlobalFilterList.Add(CurrentFilter);

                    // set the current field name as the last filter name
                    lastFilter = CurrentFilter.FieldName;
                });

                // apply filter
                CollectionViewSource.Refresh();

                // remove the current filter if there is no items to filter
                if (!CurrentFilter.PreviouslyFilteredItems.Any())
                    RemoveCurrentFilter();

                FilterState.SetIsFiltered(button, CurrentFilter?.IsFiltered ?? false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.ApplyFilterCommand error : {ex.Message}");
                throw;
            }
            finally
            {
                ReactivateSorting();
                ResetCursor();
                pending = false;
                CurrentFilter = null;
                ElapsedTime = elased.Add(DateTime.Now - start);

                Debug.WriteLineIf(DebugMode, $"Elapsed time : {ElapsedTime:mm\\:ss\\.ff}");
            }
        }

        /// <summary>
        ///     PopUp placement and offset
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="header"></param>
        private void PopupPlacement(FrameworkElement grid, FrameworkElement header)
        {
            try
            {
                popup.PlacementTarget = header;
                popup.HorizontalOffset = 0d;
                popup.VerticalOffset = -1d;
                popup.Placement = PlacementMode.Bottom;

                // get the host window of the datagrid, contribution : STEFAN HEIMEL
                var hostingWindow = Window.GetWindow(this);

                if (hostingWindow != null)
                {
                    // greater than or equal to 0.0
                    double MaxSize(double size) => (size >= 0.0d) ? size : 0.0d;

                    const double border = 1d;

                    // get the ContentPresenter from the hostingWindow
                    var contentPresenter = VisualTreeHelpers.FindChild<ContentPresenter>(hostingWindow);

                    var hostSize = new Point
                    {
                        X = contentPresenter.ActualWidth,
                        Y = contentPresenter.ActualHeight
                    };

                    // get the X, Y position of the header
                    var headerContentOrigin = header.TransformToVisual(contentPresenter).Transform(new Point(0, 0));
                    var headerDataGridOrigin = header.TransformToVisual(this).Transform(new Point(0, 0));

                    var headerSize = new Point { X = header.ActualWidth, Y = header.ActualHeight };
                    var offset = popUpSize.X - headerSize.X + border;

                    // the popup must stay in the DataGrid, move it to the left of the header, because it overflows on the right.
                    if (headerDataGridOrigin.X + headerSize.X > popUpSize.X) popup.HorizontalOffset -= offset;

                    // delta for max size popup
                    var delta = new Point
                    {
                        X = hostSize.X - (headerContentOrigin.X + headerSize.X),
                        Y = hostSize.Y - (headerContentOrigin.Y + headerSize.Y + popUpSize.Y)
                    };

                    // max size
                    grid.MaxWidth = MaxSize(popUpSize.X + delta.X - border);
                    grid.MaxHeight = MaxSize(popUpSize.Y + delta.Y - border);

                    // remove offset
                    if (popup.HorizontalOffset == 0)
                        grid.MaxWidth = MaxSize(grid.MaxWidth -= offset);

                    // the height of popup is too large, reduce it, because it overflows down.
                    if (delta.Y <= 0d)
                    {
                        grid.MaxHeight = MaxSize(popUpSize.Y - Math.Abs(delta.Y) - border);
                        grid.Height = grid.MaxHeight;
                        grid.MinHeight = grid.MaxHeight;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.PopupPlacement error : {ex.Message}");
                throw;
            }
        }

        #endregion Private Methods
    }
}