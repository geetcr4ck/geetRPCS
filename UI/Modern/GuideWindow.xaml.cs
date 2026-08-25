/**
 * geetRPCS - Help & Guide window (ModernWpf / Fluent)
 * In-app guide distilled from the README (getting started, customization,
 * features, updates, troubleshooting, About). Content comes from the
 * guide_* localization keys; the About topic adds links to the full
 * online documentation.
 */
/*
 * Copyright (c) 2026 geetcr4ck
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using geetRPCS.Services;

namespace geetRPCS.UI.Modern
{
    public partial class GuideWindow : Window
    {
        private const string RepoUrl = "https://github.com/geetcr4ck/geetRPCS";

        private sealed class GuideTopic
        {
            // PROPERTIES, not fields: the nav ListBox resolves its items through
            // DisplayMemberPath, and WPF binding only works against properties —
            // with public fields every nav item silently rendered empty.
            public string Title { get; set; }
            public string[] Paragraphs { get; set; }
            public (string Label, string Url)[] Links { get; set; } // About topic only
        }

        private readonly List<GuideTopic> _topics = new List<GuideTopic>();

        // Test accessors (InternalsVisibleTo: Tests)
        internal int TopicCount => _topics.Count;
        internal int SelectedTopicIndex { get => NavList.SelectedIndex; set => NavList.SelectedIndex = value; }
        internal int RenderedParagraphCount
        {
            get
            {
                int count = 0;
                foreach (var child in ContentHost.Children)
                    if (child is TextBlock || child is Separator) count++;
                return count;
            }
        }

        public GuideWindow()
        {
            InitializeComponent();

            Title = LanguageManager.Current.WindowGuideTitle ?? "Help & Guide";
            CloseButton.Content = LanguageManager.Current.BtnClose ?? "Close";

            BuildTopics();
            NavList.ItemsSource = _topics;
            NavList.DisplayMemberPath = nameof(GuideTopic.Title);
            NavList.SelectedIndex = 0;

            try
            {
                string iconPath = Utils.AppPaths.IconPath;
                if (File.Exists(iconPath)) Icon = LoadIcon(iconPath);
            }
            catch { }

            Loaded += (s, e) =>
            {
                WindowActivation.ForceForeground(this);
            };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            };
        }

        private void BuildTopics()
        {
            var L = LanguageManager.Current;
            _topics.Add(new GuideTopic
            {
                Title = L.GuideNavGettingStarted ?? "Getting Started",
                Paragraphs = new[]
                {
                    L.GuideStarted1 ?? "",
                    L.GuideStarted2 ?? "",
                    L.GuideStarted3 ?? ""
                }
            });
            _topics.Add(new GuideTopic
            {
                Title = L.GuideNavCustomize ?? "Customize Presence",
                Paragraphs = new[]
                {
                    L.GuideCustomize1 ?? "",
                    L.GuideCustomize2 ?? "",
                    L.GuideCustomize3 ?? "",
                    L.GuideCustomize4 ?? "",
                    L.GuideCustomize5 ?? ""
                }
            });
            _topics.Add(new GuideTopic
            {
                Title = L.GuideNavFeatures ?? "Features",
                Paragraphs = new[]
                {
                    L.GuideFeatures1 ?? "",
                    L.GuideFeatures2 ?? "",
                    L.GuideFeatures3 ?? "",
                    L.GuideFeatures4 ?? ""
                }
            });
            _topics.Add(new GuideTopic
            {
                Title = L.GuideNavUpdates ?? "Updates & Stats",
                Paragraphs = new[]
                {
                    L.GuideUpdates1 ?? "",
                    L.GuideUpdates2 ?? ""
                }
            });
            _topics.Add(new GuideTopic
            {
                Title = L.GuideNavTroubleshooting ?? "Troubleshooting",
                Paragraphs = new[]
                {
                    L.GuideTrouble1 ?? "",
                    L.GuideTrouble2 ?? ""
                }
            });
            _topics.Add(new GuideTopic
            {
                Title = L.GuideNavAbout ?? "About",
                Paragraphs = new[]
                {
                    string.Format(L.GuideAbout1 ?? "geetRPCS v{0}", Utils.AppVersion.VersionText),
                    L.GuideAbout2 ?? ""
                },
                Links = new[]
                {
                    (L.GuideLinkReadme ?? "Open full README", RepoUrl + "#readme"),
                    (L.GuideLinkAppIdDoc ?? "Custom App ID Tutorial", L.UrlTutorial ?? RepoUrl + "/blob/main/docs/CUSTOM_APP_ID.md"),
                    (L.GuideLinkIssues ?? "Report an Issue", RepoUrl + "/issues"),
                    (L.GuideLinkDiscussions ?? "Discussions", RepoUrl + "/discussions"),
                    (L.GuideLinkReleases ?? "Releases", RepoUrl + "/releases")
                }
            });
        }

        private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(NavList.SelectedItem is GuideTopic topic)) return;
            RenderTopic(topic);
        }

        /// <summary>Renders one topic: a header plus each paragraph. Paragraph
        /// text keeps its "\n" line breaks (bullet-ish lists) via manual
        /// LineBreaks — TextBlock does not translate \n on its own.</summary>
        private void RenderTopic(GuideTopic topic)
        {
            ContentHost.Children.Clear();

            var header = new TextBlock
            {
                Text = topic.Title,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4),
                TextWrapping = TextWrapping.Wrap
            };
            ContentHost.Children.Add(header);

            var divider = new Separator { Margin = new Thickness(0, 4, 0, 12) };
            ContentHost.Children.Add(divider);

            foreach (var paragraph in topic.Paragraphs)
            {
                if (string.IsNullOrEmpty(paragraph)) continue;
                var tb = new TextBlock
                {
                    FontSize = 13,
                    LineHeight = 21,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 12),
                    Foreground = TryFindResource("TextFillColorPrimaryBrush") as Brush ?? Foreground
                };
                string[] lines = paragraph.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (i > 0) tb.Inlines.Add(new LineBreak());
                    tb.Inlines.Add(new Run(lines[i]));
                }
                ContentHost.Children.Add(tb);
            }

            if (topic.Links != null)
            {
                var links = new TextBlock { FontSize = 13, Margin = new Thickness(0, 4, 0, 0) };
                foreach (var (label, url) in topic.Links)
                {
                    if (links.Inlines.Count > 0) links.Inlines.Add(new Run("    "));
                    var hl = new Hyperlink
                    {
                        NavigateUri = new Uri(url),
                        Foreground = TryFindResource("AccentTextFillColorPrimaryBrush") as Brush ?? Foreground,
                        TextDecorations = null
                    };
                    hl.Inlines.Add(label);
                    hl.RequestNavigate += (s, e) => OpenUrl(e.Uri.ToString());
                    links.Inlines.Add(hl);
                }
                ContentHost.Children.Add(links);
            }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageDialog.ShowError(LanguageManager.Current.ErrorOpenLink + " " + ex.Message, "Error");
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        private static ImageSource LoadIcon(string path)
        {
            using var icon = new System.Drawing.Icon(path);
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
    }
}
