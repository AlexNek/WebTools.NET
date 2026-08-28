namespace WebTools.NET.ContentAnalysis.Models;

/// <summary>
/// Classifies an extracted text block by its semantic HTML element.
/// </summary>
public enum HtmlTextBlockKind
{
    Other = 0,
    Heading = 1,
    Paragraph = 2,
    ListItem = 3,
    TableCell = 4,
    Label = 5,
    Link = 6,
    Container = 7
}
