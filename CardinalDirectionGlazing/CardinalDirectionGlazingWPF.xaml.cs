using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CardinalDirectionGlazing
{
    public partial class CardinalDirectionGlazingWPF : Window
    {
        private const string HintSpaces = "Выберите связанный файл.";

        private const string HintRooms = "Текущий файл или выбранная связь.";

        private readonly List<RevitLinkInstance> _revitLinkInstances;
        private readonly IReadOnlyList<WindowAreaParameterOption> _windowAreaParameters;
        private bool _uiReady;

        public RevitLinkInstance? SelectedRevitLinkInstance;
        public string SpacesForProcessingButtonName = string.Empty;
        public string SpacesOrRoomsForProcessingButtonName = string.Empty;
        public bool UseWindowAreaParameter { get; private set; }
        public WindowAreaParameterOption? SelectedWindowAreaParameter { get; private set; }

        CardinalDirectionGlazingSettings? CardinalDirectionGlazingSettingsItem = null;

        public CardinalDirectionGlazingWPF(
            List<RevitLinkInstance> revitLinkInstanceList,
            IReadOnlyList<WindowAreaParameterOption> windowAreaParameters)
        {
            _revitLinkInstances = revitLinkInstanceList;
            _windowAreaParameters = windowAreaParameters ?? new List<WindowAreaParameterOption>();
            InitializeComponent();

            CardinalDirectionGlazingSettingsItem = CardinalDirectionGlazingSettings.GetSettings();
            ApplyLegacyRoomsLinkMigration(CardinalDirectionGlazingSettingsItem);

            listBox_RevitLinkInstance.ItemsSource = _revitLinkInstances;
            listBox_RevitLinkInstance.DisplayMemberPath = "Name";
            comboBox_WindowAreaParameter.ItemsSource = _windowAreaParameters;

            bool hasLinks = _revitLinkInstances.Count > 0;

            if (CardinalDirectionGlazingSettingsItem != null)
            {
                if (CardinalDirectionGlazingSettingsItem.SpacesForProcessingButtonName == "radioButton_Selected")
                    radioButton_Selected.IsChecked = true;
                else
                    radioButton_All.IsChecked = true;

                if (CardinalDirectionGlazingSettingsItem.SpacesOrRoomsForProcessingButtonName == "radioButton_Spaces")
                    radioButton_Spaces.IsChecked = true;
                else
                    radioButton_Rooms.IsChecked = true;

                if (CardinalDirectionGlazingSettingsItem.SpacesOrRoomsForProcessingButtonName == "radioButton_Rooms")
                {
                    if (CardinalDirectionGlazingSettingsItem.UseLinkedFileForRooms)
                    {
                        radioButton_LinkRoomsUse.IsChecked = true;
                        if (hasLinks)
                        {
                            var match = _revitLinkInstances.FirstOrDefault(li =>
                                li.Name == CardinalDirectionGlazingSettingsItem.SelectedRevitLinkInstanceName);
                            listBox_RevitLinkInstance.SelectedItem = match ?? _revitLinkInstances[0];
                        }
                    }
                    else
                    {
                        radioButton_LinkRoomsNone.IsChecked = true;
                        listBox_RevitLinkInstance.SelectedItem = null;
                    }
                }
                else
                {
                    if (hasLinks)
                    {
                        var match = _revitLinkInstances.FirstOrDefault(li =>
                            li.Name == CardinalDirectionGlazingSettingsItem.SelectedRevitLinkInstanceName);
                        listBox_RevitLinkInstance.SelectedItem = match ?? _revitLinkInstances[0];
                    }
                }
            }
            else
            {
                radioButton_Spaces.IsChecked = true;
                radioButton_All.IsChecked = true;
                radioButton_LinkRoomsNone.IsChecked = true;
                if (hasLinks)
                    listBox_RevitLinkInstance.SelectedItem = _revitLinkInstances[0];
            }

            RestoreWindowAreaParameterSelection();

            radioButton_Spaces.Checked += ModeRadio_Checked;
            radioButton_Rooms.Checked += ModeRadio_Checked;
            radioButton_LinkRoomsNone.Checked += LinkUsageRadio_Checked;
            radioButton_LinkRoomsUse.Checked += LinkUsageRadio_Checked;

            ApplyModeToUi();
            _uiReady = true;
        }

        private void RestoreWindowAreaParameterSelection()
        {
            if (CardinalDirectionGlazingSettingsItem?.UseWindowAreaParameter != true)
                return;

            checkBox_WindowAreaFromParameter.IsChecked = true;
            comboBox_WindowAreaParameter.SelectedItem = WindowAreaParameterSelection.Restore(
                _windowAreaParameters,
                CardinalDirectionGlazingSettingsItem.WindowAreaParameterName,
                CardinalDirectionGlazingSettingsItem.WindowAreaParameterScope,
                CardinalDirectionGlazingSettingsItem.WindowAreaParameterGuid);
        }

        /// <summary>Старые настройки: помещения + сохранённое имя связи без флага — считаем, что связь использовалась.</summary>
        private static void ApplyLegacyRoomsLinkMigration(CardinalDirectionGlazingSettings? settings)
        {
            if (settings == null)
                return;
            if (settings.SpacesOrRoomsForProcessingButtonName != "radioButton_Rooms")
                return;
            if (settings.UseLinkedFileForRooms)
                return;
            if (!string.IsNullOrWhiteSpace(settings.SelectedRevitLinkInstanceName))
                settings.UseLinkedFileForRooms = true;
        }

        private void ModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady)
                return;

            ApplyModeToUi();

            bool spaces = radioButton_Spaces.IsChecked == true;
            bool hasLinks = _revitLinkInstances.Count > 0;
            if (spaces && hasLinks && listBox_RevitLinkInstance.SelectedItem == null)
                listBox_RevitLinkInstance.SelectedItem = _revitLinkInstances[0];
        }

        private void LinkUsageRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady)
                return;

            ApplyModeToUi();

            bool useLink = radioButton_LinkRoomsUse.IsChecked == true;
            bool hasLinks = _revitLinkInstances.Count > 0;
            if (useLink && hasLinks && listBox_RevitLinkInstance.SelectedItem == null)
                listBox_RevitLinkInstance.SelectedItem = _revitLinkInstances[0];
        }

        private void ApplyModeToUi()
        {
            bool spaces = radioButton_Spaces.IsChecked == true;
            bool hasLinks = _revitLinkInstances.Count > 0;

            groupBox_SpacesForProcessing.Header = "Область расчёта";
            textBlock_ScopeHint.Text = spaces
                ? "Выбранные или все пространства."
                : "Выбранные или все помещения.";

            if (spaces)
            {
                textBlock_LinkHint.Text = HintSpaces;
                panel_LinkUsageRooms.Visibility = System.Windows.Visibility.Collapsed;
                listBox_RevitLinkInstance.IsEnabled = hasLinks;
            }
            else
            {
                textBlock_LinkHint.Text = HintRooms;
                panel_LinkUsageRooms.Visibility = System.Windows.Visibility.Visible;
                bool useLink = radioButton_LinkRoomsUse.IsChecked == true;
                listBox_RevitLinkInstance.IsEnabled = useLink && hasLinks;
                if (!useLink)
                    listBox_RevitLinkInstance.SelectedItem = null;
            }
        }

        private bool TryConfirm()
        {
            if (checkBox_WindowAreaFromParameter.IsChecked == true
                && comboBox_WindowAreaParameter.SelectedItem == null)
            {
                MessageBox.Show(
                    "Выберите параметр площади окон.",
                    "Остекление по сторонам",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            bool spaces = radioButton_Spaces.IsChecked == true;
            bool hasLinks = _revitLinkInstances.Count > 0;

            if (spaces)
            {
                if (!hasLinks)
                {
                    MessageBox.Show(
                        "В проекте нет связанных RVT-файлов. Для расчёта по пространствам связь обязательна.",
                        "Остекление по сторонам",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if (listBox_RevitLinkInstance.SelectedItem == null)
                {
                    MessageBox.Show(
                        "Выберите связанный файл.",
                        "Остекление по сторонам",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                return true;
            }

            if (radioButton_LinkRoomsUse.IsChecked == true)
            {
                if (!hasLinks)
                {
                    MessageBox.Show(
                        "В проекте нет связанных RVT-файлов.",
                        "Остекление по сторонам",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if (listBox_RevitLinkInstance.SelectedItem == null)
                {
                    MessageBox.Show(
                        "Выберите связанный файл или включите «Только текущий файл».",
                        "Остекление по сторонам",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private void btn_Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!TryConfirm())
                return;

            SaveSettings();
            DialogResult = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                if (!TryConfirm())
                {
                    e.Handled = true;
                    return;
                }

                SaveSettings();
                DialogResult = true;
                Close();
                e.Handled = true;
            }
        }

        private void btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveSettings()
        {
            bool spaces = radioButton_Spaces.IsChecked == true;
            bool roomsUseLink = !spaces && radioButton_LinkRoomsUse.IsChecked == true;
            UseWindowAreaParameter = checkBox_WindowAreaFromParameter.IsChecked == true;
            SelectedWindowAreaParameter = UseWindowAreaParameter
                ? comboBox_WindowAreaParameter.SelectedItem as WindowAreaParameterOption
                : null;

            SelectedRevitLinkInstance = spaces || roomsUseLink
                ? listBox_RevitLinkInstance.SelectedItem as RevitLinkInstance
                : null;

            CardinalDirectionGlazingSettingsItem = new CardinalDirectionGlazingSettings
            {
                SelectedRevitLinkInstanceName = SelectedRevitLinkInstance?.Name ?? string.Empty,
                UseLinkedFileForRooms = !spaces && roomsUseLink,
                UseWindowAreaParameter = UseWindowAreaParameter,
                WindowAreaParameterName = SelectedWindowAreaParameter?.Name ?? string.Empty,
                WindowAreaParameterScope = SelectedWindowAreaParameter?.Scope.ToString() ?? string.Empty,
                WindowAreaParameterGuid = SelectedWindowAreaParameter?.SharedGuid ?? string.Empty
            };

            SpacesForProcessingButtonName = radioButton_Selected.IsChecked == true
                ? radioButton_Selected.Name
                : radioButton_All.Name;
            CardinalDirectionGlazingSettingsItem.SpacesForProcessingButtonName = SpacesForProcessingButtonName;

            SpacesOrRoomsForProcessingButtonName = spaces
                ? radioButton_Spaces.Name
                : radioButton_Rooms.Name;
            CardinalDirectionGlazingSettingsItem.SpacesOrRoomsForProcessingButtonName = SpacesOrRoomsForProcessingButtonName;

            CardinalDirectionGlazingSettingsItem.SaveSettings();
        }
    }
}
