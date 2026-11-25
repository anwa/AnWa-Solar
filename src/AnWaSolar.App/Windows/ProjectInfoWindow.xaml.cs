using System.Windows;

using AnWaSoft.Design.Wpf.Controls;
using AnWaSoft.Design.Wpf.Status;

namespace AnWaSolar.Windows;

/// <summary>Name, customer, address and notes of a project, for a new project or to change them.</summary>
public partial class ProjectInfoWindow : ChromeWindow
{
    public ProjectInfoWindow(string name, string? customer, string? address, string? notes, bool isNew)
    {
        InitializeComponent();
        Title = isNew ? "New project" : "Project";
        SaveButton.Content = isNew ? "Create" : "Save";
        NameInput.Text = name;
        CustomerInput.Text = customer ?? string.Empty;
        AddressInput.Text = address ?? string.Empty;
        NotesInput.Text = notes ?? string.Empty;
        Loaded += (_, _) =>
        {
            NameInput.Focus();
            NameInput.SelectAll();
        };
    }

    public string ProjectName { get; private set; } = string.Empty;

    public string? Customer { get; private set; }

    public string? Address { get; private set; }

    public string? Notes { get; private set; }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameInput.Text))
        {
            MessageDialog.Show(this, "Enter a project name.", Title, StatusKind.Error);
            return;
        }

        ProjectName = NameInput.Text.Trim();
        Customer = Optional(CustomerInput.Text);
        Address = Optional(AddressInput.Text);
        Notes = Optional(NotesInput.Text);
        DialogResult = true;
    }

    private static string? Optional(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
