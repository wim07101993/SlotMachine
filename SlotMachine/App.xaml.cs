﻿using System.Windows;
using SlotMachine.Models;
using SlotMachine.Services;
using SlotMachine.Services.Implementations;
using SlotMachine.ViewModelInerfaces;
using SlotMachine.ViewModels;
using SlotMachine.Views;
using Unity;

namespace SlotMachine
{
    public partial class App
    {
        public IUnityContainer UnityContainer { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            Number.MaxValue = 3;

            Number.MinSpinTime = 100;
            Number.MaxSpinTime = 2000;

            base.OnStartup(e);

            RegisterTypes();

            MainWindow = UnityContainer.Resolve<MainWindow>();
            if (MainWindow != null)
                MainWindow.Show();
            else
                MessageBox.Show("No window to show.");
        }

        private void RegisterTypes()
        {
            UnityContainer = new UnityContainer();
            UnityContainer.RegisterType<IMainWindowViewModel, MainWindowViewModel>();
            UnityContainer.RegisterType<IColorProvider, ColorProvider>();
        }
    }
}