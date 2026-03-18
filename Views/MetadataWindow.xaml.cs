using System.Windows;
using System.Windows.Input;
using MXFConverter.Models;

namespace MXFConverter.Views;

public partial class MetadataWindow : Window
{
    public VideoMetadata Result { get; private set; } = new();
    private readonly VideoMetadata _original;

    public MetadataWindow(string fileName, VideoMetadata current)
    {
        InitializeComponent();
        _original       = current;
        TxtTitle.Text   = fileName;

        TxtMetaTitle.Text     = current.Title;
        TxtMetaAuthor.Text    = current.Author;
        TxtMetaDesc.Text      = current.Description;
        TxtMetaCopyright.Text = current.Copyright;
        TxtMetaYear.Text      = current.Year;
        TxtMetaComment.Text   = current.Comment;
    }

    public MetadataWindow(ConversionItem fileName)
    {
        throw new NotImplementedException();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        Result = new VideoMetadata
        {
            Title       = TxtMetaTitle.Text.Trim(),
            Author      = TxtMetaAuthor.Text.Trim(),
            Description = TxtMetaDesc.Text.Trim(),
            Copyright   = TxtMetaCopyright.Text.Trim(),
            Year        = TxtMetaYear.Text.Trim(),
            Comment     = TxtMetaComment.Text.Trim(),
        };
        DialogResult = true;
        Close();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        TxtMetaTitle.Text = TxtMetaAuthor.Text = TxtMetaDesc.Text =
        TxtMetaCopyright.Text = TxtMetaYear.Text = TxtMetaComment.Text = "";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
