using Avalonia.Controls;
using System;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ForceSquashAcrossMerges : UserControl
    {
        public ForceSquashAcrossMerges()
        {
            InitializeComponent();
        }

        private async void OnShowFullDiff(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ForceSquashAcrossMerges vm)
                await vm.LoadFullDiff();
        }
    }
}
