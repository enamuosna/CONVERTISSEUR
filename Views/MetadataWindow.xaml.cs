using System.Windows;
using System.Windows.Input;
using MXFConverter.Models;

namespace MXFConverter.Views;

public partial class MetadataWindow : Window
{
    private readonly ConversionItem _item;

    public MetadataWindow(ConversionItem item)
    {
        InitializeComponent();
        _item = item;

        TxtTitle.Text = $"Métadonnées — {item.FileName}";

        var m = item.Metadata;
        TxtMetaTitle.Text     = m.Title;
        TxtMetaAuthor.Text    = m.Author;
        TxtMetaDesc.Text      = m.Description;
        TxtMetaCopyright.Text = m.Copyright;
        TxtMetaYear.Text      = m.Year;
        TxtMetaComment.Text   = m.Comment;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _item.Metadata.Title       = TxtMetaTitle.Text.Trim();
        _item.Metadata.Author      = TxtMetaAuthor.Text.Trim();
        _item.Metadata.Description = TxtMetaDesc.Text.Trim();
        _item.Metadata.Copyright   = TxtMetaCopyright.Text.Trim();
        _item.Metadata.Year        = TxtMetaYear.Text.Trim();
        _item.Metadata.Comment     = TxtMetaComment.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        TxtMetaTitle.Text     = "";
        TxtMetaAuthor.Text    = "";
        TxtMetaDesc.Text      = "";
        TxtMetaCopyright.Text = "";
        TxtMetaYear.Text      = "";
        TxtMetaComment.Text   = "";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
