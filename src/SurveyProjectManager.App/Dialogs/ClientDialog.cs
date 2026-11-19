using FlowDirection = Avalonia.Media.FlowDirection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Dialogs;

/// <summary>ساخت یا ویرایش مشخصات متقاضی.</summary>
public sealed class ClientDialog : Window
{
    private readonly TextBox _name = new();
    private readonly ComboBox _kind = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _phone = new() { FlowDirection = Avalonia.Media.FlowDirection.LeftToRight };
    private readonly TextBox _note = new();

    public bool Accepted { get; private set; }

    public ClientDialog(Client? client)
    {
        Title = client == null ? "متقاضی جدید" : "ویرایش متقاضی";
        FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        foreach (var k in Client.Kinds) _kind.Items.Add(k);
        _kind.SelectedIndex = 0;
        if (client != null)
        {
            _name.Text = client.Name;
            _kind.SelectedItem = client.Kind;
            _phone.Text = client.Phone;
            _note.Text = client.Note;
        }
        var root = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        root.Children.Add(UiBuild.Labeled("نام متقاضی", _name));
        root.Children.Add(UiBuild.Labeled("نوع (حقیقی، حقوقی یا نماینده‌ای که برای دیگران سفارش می‌دهد)", _kind));
        root.Children.Add(UiBuild.Labeled("تلفن", _phone));
        root.Children.Add(UiBuild.Labeled("توضیح", _note));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
        buttons.Children.Add(UiBuild.Btn("ثبت", Submit, primary: true));
        buttons.Children.Add(UiBuild.Btn("انصراف", Close));
        root.Children.Add(buttons);
        Content = root;
        Opened += (_, _) => _name.Focus();
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(_name.Text)) { _name.Focus(); return; }
        Accepted = true;
        Close();
    }

    public void ApplyTo(Client c)
    {
        c.Name = (_name.Text ?? "").Trim();
        c.Kind = _kind.SelectedItem as string ?? "حقیقی";
        c.Phone = (_phone.Text ?? "").Trim();
        c.Note = (_note.Text ?? "").Trim();
    }
}
