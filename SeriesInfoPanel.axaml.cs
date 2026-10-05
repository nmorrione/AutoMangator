using Avalonia.Controls;

namespace AutoMangator;

public partial class SeriesInfoPanel : UserControl
{
    public SeriesInfoPanel() => InitializeComponent();

    public string Series
    {
        get => (txtSeries.Text ?? "").Trim();
        set => txtSeries.Text = value;
    }

    public void Fill(BookInfo b)
    {
        txtSeries.Text = b.Series;
        txtWriter.Text = b.Writer;
        txtArtist.Text = b.Artist;
        txtPublisher.Text = b.Publisher;
        txtYear.Text = b.Year;
        txtGenre.Text = b.Genre;
        txtLanguage.Text = b.Language;
        txtSummary.Text = b.Summary;
        chkRtl.IsChecked = b.RightToLeft;
    }

    public BookInfo Read() => new()
    {
        Series = Series,
        Writer = txtWriter.Text ?? "",
        Artist = txtArtist.Text ?? "",
        Publisher = txtPublisher.Text ?? "",
        Year = txtYear.Text ?? "",
        Genre = txtGenre.Text ?? "",
        Language = txtLanguage.Text ?? "",
        Summary = txtSummary.Text ?? "",
        RightToLeft = chkRtl.IsChecked == true,
    };
}
