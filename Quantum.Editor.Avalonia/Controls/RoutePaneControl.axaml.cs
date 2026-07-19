using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Quantum.Editor.Avalonia.Models;

namespace Quantum.Editor.Avalonia.Controls;

public partial class RoutePaneControl : UserControl
{
    private IReadOnlyList<EditorGraphNode> graphNodes = Array.Empty<EditorGraphNode>();
    private readonly Dictionary<int, Button> graphNodeButtons = new();
    private EditorSelection? selection;
    private int highlightedSectionIndex = -1;

    public RoutePaneControl()
    {
        InitializeComponent();
    }

    public event EventHandler<GraphNodeSelectedEventArgs>? NodeSelected;

    public event EventHandler<SectionPointerChangedEventArgs>? SectionPointerChanged;

    public string DocumentTitle
    {
        get => DocumentTitleText.Text ?? string.Empty;
        set => DocumentTitleText.Text = value ?? string.Empty;
    }

    public string DocumentPath
    {
        get => DocumentPathText.Text ?? string.Empty;
        set => DocumentPathText.Text = value ?? string.Empty;
    }

    public IReadOnlyList<EditorGraphNode> GraphNodes
    {
        get => graphNodes;
        set
        {
            graphNodes = value ?? Array.Empty<EditorGraphNode>();
            RebuildRoute();
        }
    }

    public EditorSelection? Selection
    {
        get => selection;
        set
        {
            selection = value;
            UpdateNodeAppearance();
        }
    }

    public int HighlightedSectionIndex
    {
        get => highlightedSectionIndex;
        set
        {
            highlightedSectionIndex = value < -1 ? -1 : value;
            UpdateNodeAppearance();
        }
    }

    private void RebuildRoute()
    {
        GraphNodesPanel.Children.Clear();
        graphNodeButtons.Clear();

        for (int index = 0; index < graphNodes.Count; index++)
        {
            EditorGraphNode node = graphNodes[index];
            var header = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 8
            };
            header.Children.Add(new TextBlock
            {
                Text = (node.RouteIndex + 1).ToString("D2", CultureInfo.InvariantCulture),
                Foreground = Brush.Parse("#6FA9D3"),
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center
            });
            var title = new TextBlock
            {
                Text = node.NodeId,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(title, 1);
            header.Children.Add(title);
            var kind = new TextBlock
            {
                Text = node.SectionKind,
                Foreground = Brush.Parse("#8FA5B9"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(kind, 2);
            header.Children.Add(kind);

            var content = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    header,
                    new TextBlock
                    {
                        Text = node.Summary,
                        Foreground = Brush.Parse("#8FA5B9"),
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };
            var button = new Button
            {
                Tag = node,
                Content = content
            };
            button.Classes.Add("graphNode");
            button.Click += OnGraphNodeClick;
            button.PointerEntered += OnGraphNodePointerEntered;
            button.PointerExited += OnGraphNodePointerExited;
            graphNodeButtons[node.RouteIndex] = button;
            GraphNodesPanel.Children.Add(button);

            if (index + 1 < graphNodes.Count)
            {
                var connector = new Grid
                {
                    Height = 28,
                    IsHitTestVisible = false
                };
                connector.Children.Add(new Border
                {
                    Width = 2,
                    Height = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = Brush.Parse("#3E718F")
                });
                connector.Children.Add(new TextBlock
                {
                    Text = "\u25BC",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Foreground = Brush.Parse("#59B5E8"),
                    FontSize = 10
                });
                GraphNodesPanel.Children.Add(connector);
            }
        }

        UpdateNodeAppearance();
    }

    private void UpdateNodeAppearance()
    {
        foreach ((int sectionIndex, Button button) in graphNodeButtons)
        {
            bool selected = selection?.SectionIndex == sectionIndex;
            bool highlighted = highlightedSectionIndex == sectionIndex;
            button.Background = Brush.Parse(
                highlighted ? "#3A3421" : selected ? "#203B50" : "#18232E");
            button.BorderBrush = Brush.Parse(
                highlighted ? "#F4D35E" : selected ? "#59B5E8" : "#34495C");
        }
    }

    private void OnGraphNodeClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: EditorGraphNode node })
        {
            NodeSelected?.Invoke(this, new GraphNodeSelectedEventArgs(node));
        }
    }

    private void OnGraphNodePointerEntered(object? sender, PointerEventArgs eventArgs)
    {
        if (sender is Button { Tag: EditorGraphNode node })
        {
            SectionPointerChanged?.Invoke(this, new SectionPointerChangedEventArgs(node.RouteIndex));
        }
    }

    private void OnGraphNodePointerExited(object? sender, PointerEventArgs eventArgs)
    {
        SectionPointerChanged?.Invoke(this, new SectionPointerChangedEventArgs(null));
    }
}
