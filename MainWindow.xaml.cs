// Add this handler to the MainWindow class
private void Container_RightTapped(object? sender, RightTappedRoutedEventArgs e)
{
    if (sender is FrameworkElement fe && fe.DataContext is AppContainer container && !container.IsAddButton)
    {
        // Use the template root element (fe) as the active UI for edit mode
        EnterDeleteMode(container, fe);
        e.Handled = true;
    }
}