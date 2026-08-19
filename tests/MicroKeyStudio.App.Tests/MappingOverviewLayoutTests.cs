using System.Xml.Linq;

namespace MicroKeyStudio.App.Tests;

public sealed class MappingOverviewLayoutTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] LeftButtons = ["L2", "L", "Minus", "Up", "Left", "Right", "Down", "Star"];
    private static readonly string[] RightButtons = ["R2", "R", "Plus", "X", "A", "Y", "B", "Logo"];

    [Fact]
    public void Connector_overlay_is_above_the_device_image_and_has_one_endpoint_per_button()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml");
        XDocument document = XDocument.Load(path);
        XElement mappingCanvas = document.Descendants(Presentation + "Canvas")
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "DeviceMappingCanvas");
        IReadOnlyList<XElement> children = mappingCanvas.Elements().ToList();
        int imageIndex = children.ToList().FindIndex(element => element.Name == Presentation + "Image");
        int overlayIndex = children.ToList().FindIndex(element =>
            element.Name == Presentation + "Canvas"
            && (string?)element.Attribute(Xaml + "Name") == "ConnectorOverlay");

        Assert.True(imageIndex >= 0, "The device image must be present in the mapping canvas.");
        Assert.True(overlayIndex > imageIndex, "The connector overlay must render after and above the device image.");

        XElement overlay = children[overlayIndex];
        Assert.Equal("False", (string?)overlay.Attribute("IsHitTestVisible"));
        XElement[] connectors = overlay.Elements(Presentation + "Polyline").ToArray();
        XElement[] endpoints = overlay.Elements(Presentation + "Ellipse").ToArray();
        Assert.Equal(16, connectors.Length);
        Assert.Equal(16, endpoints.Length);

        string[] expectedButtons = [.. LeftButtons, .. RightButtons];
        Assert.Equal(expectedButtons.Order(), connectors.Select(element => (string?)element.Attribute("Tag")).Order());
        Assert.Equal(expectedButtons.Order(), endpoints.Select(element => (string?)element.Attribute("Tag")).Order());
    }

    [Fact]
    public void Connector_starts_align_with_the_centers_of_the_mapping_rows()
    {
        XDocument document = LoadDocument();
        XElement overlay = document.Descendants(Presentation + "Canvas")
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "ConnectorOverlay");
        Dictionary<string, string> pointsByButton = overlay.Elements(Presentation + "Polyline")
            .ToDictionary(
                element => (string)element.Attribute("Tag")!,
                element => (string)element.Attribute("Points")!);

        AssertRowStarts(pointsByButton, LeftButtons, 8);
        AssertRowStarts(pointsByButton, RightButtons, 422);
    }

    [Fact]
    public void Shoulder_and_plus_connectors_do_not_overlap_or_cross()
    {
        XDocument document = LoadDocument();
        XElement overlay = document.Descendants(Presentation + "Canvas")
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "ConnectorOverlay");
        string[] buttons = ["L2", "L", "R2", "R", "Plus"];
        Dictionary<string, Point[]> pointsByButton = overlay.Elements(Presentation + "Polyline")
            .Where(element => buttons.Contains((string?)element.Attribute("Tag")))
            .ToDictionary(
                element => (string)element.Attribute("Tag")!,
                element => ParsePoints((string)element.Attribute("Points")!));

        for (int first = 0; first < buttons.Length; first++)
        {
            for (int second = first + 1; second < buttons.Length; second++)
            {
                Assert.False(
                    PathsIntersect(pointsByButton[buttons[first]], pointsByButton[buttons[second]]),
                    $"{buttons[first]} and {buttons[second]} connectors must not overlap or cross.");
            }
        }
    }

    [Fact]
    public void Dpad_right_and_y_enter_horizontally_while_dpad_down_and_b_enter_vertically()
    {
        XDocument document = LoadDocument();
        XElement overlay = document.Descendants(Presentation + "Canvas")
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "ConnectorOverlay");
        string[] buttons = ["Right", "Down", "Y", "B"];
        Dictionary<string, Point[]> pointsByButton = overlay.Elements(Presentation + "Polyline")
            .Where(element => buttons.Contains((string?)element.Attribute("Tag")))
            .ToDictionary(
                element => (string)element.Attribute("Tag")!,
                element => ParsePoints((string)element.Attribute("Points")!));

        AssertHorizontalLastSegment(pointsByButton["Right"]);
        AssertVerticalLastSegment(pointsByButton["Down"]);
        AssertHorizontalLastSegment(pointsByButton["Y"]);
        AssertVerticalLastSegment(pointsByButton["B"]);

        Assert.False(PathsIntersect(pointsByButton["Right"], pointsByButton["Down"]));
        Assert.False(PathsIntersect(pointsByButton["Y"], pointsByButton["B"]));
    }

    [Fact]
    public void Disable_sleep_checkbox_uses_two_way_binding_and_device_read_availability()
    {
        XDocument document = LoadDocument();
        XElement checkBox = document.Descendants(Presentation + "CheckBox")
            .Single(element => ((string?)element.Attribute("Content"))?.Contains("Text.DisableSleep") == true);

        Assert.Contains("IsSleepDisabled", (string?)checkBox.Attribute("IsChecked"));
        Assert.Contains("Mode=TwoWay", (string?)checkBox.Attribute("IsChecked"));
        Assert.Contains("IsSleepSettingSupported", (string?)checkBox.Attribute("IsEnabled"));
    }

    private static XDocument LoadDocument()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml");
        return XDocument.Load(path);
    }

    private static void AssertRowStarts(IReadOnlyDictionary<string, string> pointsByButton, IReadOnlyList<string> buttons, int expectedX)
    {
        for (int index = 0; index < buttons.Count; index++)
        {
            string firstPoint = pointsByButton[buttons[index]].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            string[] coordinates = firstPoint.Split(',');
            Assert.Equal(expectedX, int.Parse(coordinates[0]));
            Assert.Equal(index * 50, int.Parse(coordinates[1]));
        }
    }

    private static Point[] ParsePoints(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(point => point.Split(','))
            .Select(coordinates => new Point(double.Parse(coordinates[0]), double.Parse(coordinates[1])))
            .ToArray();
    }

    private static bool PathsIntersect(IReadOnlyList<Point> first, IReadOnlyList<Point> second)
    {
        for (int firstIndex = 0; firstIndex < first.Count - 1; firstIndex++)
        {
            for (int secondIndex = 0; secondIndex < second.Count - 1; secondIndex++)
            {
                if (SegmentsIntersect(first[firstIndex], first[firstIndex + 1], second[secondIndex], second[secondIndex + 1]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AssertHorizontalLastSegment(IReadOnlyList<Point> points)
    {
        Assert.True(points.Count >= 2);
        Assert.Equal(points[^2].Y, points[^1].Y);
        Assert.NotEqual(points[^2].X, points[^1].X);
    }

    private static void AssertVerticalLastSegment(IReadOnlyList<Point> points)
    {
        Assert.True(points.Count >= 2);
        Assert.Equal(points[^2].X, points[^1].X);
        Assert.NotEqual(points[^2].Y, points[^1].Y);
    }

    private static bool SegmentsIntersect(Point firstStart, Point firstEnd, Point secondStart, Point secondEnd)
    {
        double firstSecondStart = Cross(firstStart, firstEnd, secondStart);
        double firstSecondEnd = Cross(firstStart, firstEnd, secondEnd);
        double secondFirstStart = Cross(secondStart, secondEnd, firstStart);
        double secondFirstEnd = Cross(secondStart, secondEnd, firstEnd);

        if (OppositeSigns(firstSecondStart, firstSecondEnd) && OppositeSigns(secondFirstStart, secondFirstEnd))
        {
            return true;
        }

        return IsZero(firstSecondStart) && IsOnSegment(firstStart, firstEnd, secondStart)
            || IsZero(firstSecondEnd) && IsOnSegment(firstStart, firstEnd, secondEnd)
            || IsZero(secondFirstStart) && IsOnSegment(secondStart, secondEnd, firstStart)
            || IsZero(secondFirstEnd) && IsOnSegment(secondStart, secondEnd, firstEnd);
    }

    private static double Cross(Point start, Point end, Point point)
        => (end.X - start.X) * (point.Y - start.Y) - (end.Y - start.Y) * (point.X - start.X);

    private static bool OppositeSigns(double first, double second)
        => first > 0 && second < 0 || first < 0 && second > 0;

    private static bool IsZero(double value) => Math.Abs(value) < 0.0001;

    private static bool IsOnSegment(Point start, Point end, Point point)
        => point.X >= Math.Min(start.X, end.X) && point.X <= Math.Max(start.X, end.X)
            && point.Y >= Math.Min(start.Y, end.Y) && point.Y <= Math.Max(start.Y, end.Y);

    private readonly record struct Point(double X, double Y);
}
