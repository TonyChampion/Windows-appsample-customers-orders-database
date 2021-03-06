﻿//  ---------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// 
//  The MIT License (MIT)
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
//  ---------------------------------------------------------------------------------

using Contoso.App.Navigation;
using Contoso.App.UserControls;
using Contoso.App.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.System;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Contoso.App
{
    /// <summary>
    /// The "chrome" layer of the app that provides top-level navigation with
    /// proper keyboarding navigation.
    /// </summary>
    public sealed partial class AppShell : Page
    {
        private bool _isPaddingAdded = false;

        public static AppShell Current { get; private set; }

        /// <summary>
        /// Initializes a new instance of the AppShell, sets the static 'Current' reference,
        /// adds callbacks for Back requests and changes in the SplitView's DisplayMode, and
        /// provide the nav menu list with the data to display.
        /// </summary>
        public AppShell()
        {
            InitializeComponent();

            Loaded += (sender, args) =>
            {
                Current = this;

                CheckTogglePaneButtonSizeChanged();

                var titleBar = CoreApplication.GetCurrentView().TitleBar;
                titleBar.IsVisibleChanged += TitleBar_IsVisibleChanged;
            };


            RootSplitView.RegisterPropertyChangedCallback(SplitView.DisplayModeProperty, (s, a) =>
            {
                // Ensure that we update the reported size of the TogglePaneButton when the SplitView's
                // DisplayMode changes.
                CheckTogglePaneButtonSizeChanged();
            });

            SystemNavigationManager.GetForCurrentView().BackRequested += SystemNavigationManager_BackRequested;
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            SetFeedbackTimer();
        }

        public IReadOnlyCollection<NavMenuItem> PrimaryMenuItems = new ReadOnlyCollection<NavMenuItem>(new[]
        {
            new NavMenuItem
            {
                Symbol = Symbol.ContactInfo,
                Label = "Customer list",
                DestPage = typeof(CustomerListPage)
            },
            new NavMenuItem
            {
                Symbol = Symbol.Shop,
                Label = "Order list",
                DestPage = typeof(OrderListPage)
            },
            new NavMenuItem
            {
                Symbol = Symbol.Setting,
                Label = "Settings",
                DestPage = typeof(SettingsPage)
            }
        });

        public Frame AppFrame => frame;

        public Rect TogglePaneButtonRect { get; private set; }

        /// <summary>
        /// Invoked when window title bar visibility changes, such as after loading or in tablet mode
        /// Ensures correct padding at window top, between title bar and app content
        /// </summary>s
        private void TitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            if (!_isPaddingAdded && sender.IsVisible)
            {
                //add extra padding between window title bar and app content
                double extraPadding = (Double)App.Current.Resources["DesktopWindowTopPadding"];
                _isPaddingAdded = true;

                Thickness margin = NavMenuList.Margin;
                NavMenuList.Margin = new Thickness(margin.Left, margin.Top + extraPadding, margin.Right, margin.Bottom);
                margin = AppFrame.Margin;
                AppFrame.Margin = new Thickness(margin.Left, margin.Top + extraPadding, margin.Right, margin.Bottom);
                margin = TogglePaneButton.Margin;
                TogglePaneButton.Margin = new Thickness(margin.Left, margin.Top + extraPadding, margin.Right, margin.Bottom);
            }
        }

        /// <summary>
        /// Default keyboard focus movement for any unhandled keyboarding
        /// </summary>
        private void AppShell_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            FocusNavigationDirection direction = FocusNavigationDirection.None;
            switch (e.Key)
            {
                case VirtualKey.Left:
                case VirtualKey.GamepadDPadLeft:
                case VirtualKey.GamepadLeftThumbstickLeft:
                case VirtualKey.NavigationLeft:
                    direction = FocusNavigationDirection.Left;
                    break;
                case VirtualKey.Right:
                case VirtualKey.GamepadDPadRight:
                case VirtualKey.GamepadLeftThumbstickRight:
                case VirtualKey.NavigationRight:
                    direction = FocusNavigationDirection.Right;
                    break;

                case VirtualKey.Up:
                case VirtualKey.GamepadDPadUp:
                case VirtualKey.GamepadLeftThumbstickUp:
                case VirtualKey.NavigationUp:
                    direction = FocusNavigationDirection.Up;
                    break;

                case VirtualKey.Down:
                case VirtualKey.GamepadDPadDown:
                case VirtualKey.GamepadLeftThumbstickDown:
                case VirtualKey.NavigationDown:
                    direction = FocusNavigationDirection.Down;
                    break;
            }

            if (direction != FocusNavigationDirection.None &&
                FocusManager.FindNextFocusableElement(direction) is Control control)
            {
                control.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        }

        private void SystemNavigationManager_BackRequested(object sender, BackRequestedEventArgs e)
        {
            bool handled = e.Handled;
            BackRequested(ref handled);
            e.Handled = handled;
        }

        private void BackRequested(ref bool handled)
        {
            // Get a hold of the current frame so that we can inspect the app back stack.
            if (AppFrame == null)
            {
                return;
            }
            // Check to see if this is the top-most page on the app back stack.
            if (AppFrame.CanGoBack && !handled)
            {
                // If not, set the event to handled and go back to the previous page in the app.
                handled = true;
                AppFrame.GoBack();
            }
        }

        /// <summary>
        /// Navigate to the Page for the selected <paramref name="listViewItem"/>.
        /// </summary>
        private void NavMenuList_ItemInvoked(object sender, ListViewItem listViewItem)
        {
            foreach (var i in PrimaryMenuItems)
            {
                i.IsSelected = false;
            }

            var item = (NavMenuItem)((NavMenuListView)sender).ItemFromContainer(listViewItem);

            if (item != null)
            {
                item.IsSelected = true;
                if (item.DestPage != null && item.DestPage != AppFrame.CurrentSourcePageType)
                {
                    AppFrame.Navigate(item.DestPage, item.Arguments);
                }
            }
        }

        /// <summary>
        /// Ensures the nav menu reflects reality when navigation is triggered outside of
        /// the nav menu buttons.
        /// </summary>
        private void OnNavigatingToPage(object sender, NavigatingCancelEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back)
            {
                var item = (from p in PrimaryMenuItems where p.DestPage == e.SourcePageType select p).SingleOrDefault();
                if (item == null && AppFrame.BackStackDepth > 0)
                {
                    // In cases where a page drills into sub-pages then we'll highlight the most recent
                    // navigation menu item that appears in the BackStack
                    foreach (var entry in AppFrame.BackStack.Reverse())
                    {
                        item = (from p in PrimaryMenuItems where p.DestPage == entry.SourcePageType select p).SingleOrDefault();
                        if (item != null)
                        {

                        }
                    }
                }

                foreach (var i in PrimaryMenuItems)
                {
                    i.IsSelected = false;
                }
                if (item != null)
                {
                    item.IsSelected = true;
                }

                var container = (ListViewItem)NavMenuList.ContainerFromItem(item);

                // While updating the selection state of the item prevent it from taking keyboard focus.  If a
                // user is invoking the back button via the keyboard causing the selected nav menu item to change
                // then focus will remain on the back button.
                if (container != null)
                {
                    container.IsTabStop = false;
                }

                NavMenuList.SetSelectedItem(container);

                if (container != null)
                {
                    container.IsTabStop = true;
                }
            }

            App.Diagnostics.TrackNavigatingFromPage(AppFrame.CurrentSourcePageType);
            App.Diagnostics.TrackNavigatingToPage(AppFrame.CurrentSourcePageType, e.SourcePageType);
        }

        /// <summary>
        /// An event to notify listeners when the hamburger button may occlude other content in the app.
        /// The custom "PageHeader" user control is using this.
        /// </summary>
        public event TypedEventHandler<AppShell, Rect> TogglePaneButtonRectChanged;

        /// <summary>
        /// Public method to allow pages to open SplitView's pane.
        /// Used for custom app shortcuts like navigating left from page's left-most item
        /// </summary>
        public void OpenNavePane()
        {
            TogglePaneButton.IsChecked = true;
            NavPaneDivider.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides divider when nav pane is closed.
        /// </summary>
        private void RootSplitView_PaneClosed(SplitView sender, object args) =>
            NavPaneDivider.Visibility = Visibility.Collapsed;

        /// <summary>
        /// Callback when the SplitView's Pane is toggled closed.  When the Pane is not visible
        /// then the floating hamburger may be occluding other content in the app unless it is aware.
        /// </summary>
        private void TogglePaneButton_Unchecked(object sender, RoutedEventArgs e) =>
            CheckTogglePaneButtonSizeChanged();

        /// <summary>
        /// Callback when the SplitView's Pane is toggled opened.
        /// Restores divider's visibility and ensures that margins around the floating hamburger are correctly set.
        /// </summary>
        private void TogglePaneButton_Checked(object sender, RoutedEventArgs e)
        {
            NavPaneDivider.Visibility = Visibility.Visible;
            CheckTogglePaneButtonSizeChanged();
        }

        /// <summary>
        /// Check for the conditions where the navigation pane does not occupy the space under the floating
        /// hamburger button and trigger the event.
        /// </summary>
        private void CheckTogglePaneButtonSizeChanged()
        {
            if (RootSplitView.DisplayMode == SplitViewDisplayMode.Inline ||
                RootSplitView.DisplayMode == SplitViewDisplayMode.Overlay)
            {
                var transform = TogglePaneButton.TransformToVisual(this);
                var rect = transform.TransformBounds(new Rect(0, 0, TogglePaneButton.ActualWidth, TogglePaneButton.ActualHeight));
                TogglePaneButtonRect = rect;
            }
            else
            {
                TogglePaneButtonRect = new Rect();
            }

            var handler = TogglePaneButtonRectChanged;
            if (handler != null)
            {
                handler.DynamicInvoke(this, TogglePaneButtonRect);
            }
        }

        /// <summary>
        /// Enable accessibility on each nav menu item by setting the AutomationProperties.Name on each container
        /// using the associated Label of each item.
        /// </summary>s
        private void NavMenuItemContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue && args.Item != null && args.Item is NavMenuItem)
            {
                args.ItemContainer.SetValue(AutomationProperties.NameProperty, ((NavMenuItem)args.Item).Label);
            }
            else
            {
                args.ItemContainer.ClearValue(AutomationProperties.NameProperty);
            }
        }

        /// <summary>
        /// Invoked when the View Code button is clicked. Launches the repo on GitHub. 
        /// </summary>
        private async void ViewCodeNavPaneButton_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri(
                "https://github.com/Microsoft/Windows-appsample-customers-orders-database"));
        }


        /// <summary>
        /// Invoked when the Feedback button is clicked.
        /// </summary>
        private void FeedbackNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFeedbackFlyout(true);
        }

        /// <summary>
        /// Automatically shows the feedback flyout after 3 minutes.
        /// </summary>
        private void SetFeedbackTimer()
        {
            if (!App.Diagnostics.FeedbackProvided)
            {
                ThreadPoolTimer.CreateTimer(async (args) =>
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        if (!App.Diagnostics.FeedbackProvided)
                        {
                            ShowFeedbackFlyout(false);
                        }
                    });
                }, new TimeSpan(0, 1, 0));
            }
        }

        /// <summary>
        /// Displays the feedback flyout.
        /// </summary>
        private void ShowFeedbackFlyout(bool isUserInitiated)
        {
            var flyout = (FeedbackFlyout)FlyoutBase.GetAttachedFlyout(FeedbackNavButton);
            flyout.IsUserInitiated = isUserInitiated;

            if (RootSplitView.IsPaneOpen)
            {
                flyout.ShowAt(FeedbackNavButton);
            }
            else
            {
                flyout.ShowAt(FeedbackNavButton.FontIcon);
            }
        }
    }
}
