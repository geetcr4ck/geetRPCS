/**
 * geetRPCS - Private Browsing Detector
 * Detects browser-private windows from process-specific title indicators.
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
using System.Linq;

namespace geetRPCS.Utils
{
    /// <summary>
    /// Best-effort detection for private browser windows. Browsers do not expose a
    /// shared Windows API for private mode, so detection is intentionally limited
    /// to known browser processes and private-mode markers in the window title or
    /// accessibility name.
    /// </summary>
    internal static class PrivateBrowsingDetector
    {
        // ponytail: indicators are compiled in; move them to a data file only when
        // translations need to be added without a rebuild.

        // Chromium's incognito annotation, localized. Used for title matching;
        // the accessibility-name path below is language-independent and catches
        // every locale these lists miss. Multi-word phrases or unambiguous words
        // only: a lone generic word ("privé", "anonymous") inside an ordinary
        // page title must never trigger hiding.
        private static readonly string[] ChromiumIndicators =
        {
            /* EN */ "Incognito",
            /* ID */ "Mode Samaran", "Tab Samaran",
            /* DE */ "Inkognito",
            /* ES */ "Incógnito",
            /* FR */ "Navigation privée",
            /* IT/NL/PL share "Incognito" with EN */
            /* JA */ "シークレット",
            /* KO */ "시크릿",
            /* RU/BG */ "Инкогнито",
            /* UK */ "Інкогніто",
            /* VI */ "ẩn danh",
            /* zh-CN */ "隐身",
            /* zh-TW */ "無痕"
        };

        // Firefox-family private-mode phrases (firefox and its fork zen). Firefox
        // appends the localized phrase to the window title itself and never uses
        // the "Incognito" family, so it needs its own set.
        private static readonly string[] FirefoxIndicators =
        {
            /* EN */ "Private Browsing", "Private Window", "Private Tab",
            /* ID */ "Penjelajahan Pribadi",
            /* MS */ "Pelayaran Peribadi",
            /* DE */ "Privater Modus", "Privates Fenster",
            /* NL */ "Privénavigatie", "Privévenster",
            /* FR */ "Navigation privée",
            /* ES */ "Navegación privada",
            /* PT */ "Navegação privada",
            /* IT */ "Navigazione anonima",
            /* RO */ "Navigare privată",
            /* CA */ "Navegació privada",
            /* PL */ "Tryb prywatny", "Okno prywatne",
            /* CS */ "Soukromé prohlížení", "Soukromé okno",
            /* SK */ "Súkromné prehliadanie",
            /* SL */ "Zasebno brskanje",
            /* HR */ "Privatno pregledavanje",
            /* HU */ "Privát böngészés",
            /* EL */ "Ιδιωτική περιήγηση",
            /* TR */ "Gizli Gezinti",
            /* FI */ "Yksityinen selaus",
            /* SV */ "Privat surfning",
            /* DA */ "Privat browsing",
            /* NB */ "Privat nettlesing",
            /* LT */ "Privatus naršymas",
            /* RU */ "Приватный режим", "Приватное окно",
            /* UK */ "Приватний режим", "Приватне вікно",
            /* BG */ "частен режим",
            /* AR */ "التصفح الخاص",
            /* HE */ "גלישה פרטית",
            /* FA */ "مرور خصوصی",
            /* HI */ "निजी ब्राउज़िंग",
            /* TH */ "การท่องเว็บแบบส่วนตัว",
            /* VI */ "Duyệt web riêng tư",
            /* JA */ "プライベート",
            /* KO */ "사생활 보호",
            /* zh-CN */ "隐私窗口", "隐私模式",
            /* zh-TW/HK */ "隱私視窗"
        };

        // Brave rebrands private windows ("New Private Window") on top of the
        // Chromium strings it inherits.
        private static readonly string[] BraveIndicators =
            { "Private Window", "Private with Tor", "Jendela Pribadi" };

        // Processes that report the private-mode annotation through their
        // accessible window name as "<title> (<localized word>)". The annotation
        // word changes per UI language, but the SHAPE does not — see
        // HasChromiumPrivateAnnotation.
        private static readonly HashSet<string> ChromiumProcesses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chrome", "msedge", "brave" };

        // The structural annotation rule only fires on genuine browser windows,
        // whose native title ends with the app name. A page that merely ends with
        // "(Private)" cannot fake it.
        private static readonly string[] ChromiumAppTitleSuffixes =
        {
            " - Google Chrome", " - Chromium", " - Microsoft Edge",
            " - Brave", " - Brave Beta", " - Brave Dev", " - Brave Canary"
        };

        // Known non-private accessible-name annotations. Anything else inside the
        // parentheses is treated as a private-mode marker (privacy-safe default).
        private static readonly HashSet<string> NonPrivateAnnotations =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                /* EN */ "Guest",
                /* ID */ "Tamu",
                /* DE/NL */ "Gast",
                /* FR */ "Invité",
                /* ES */ "Invitado",
                /* PT */ "Convidado",
                /* IT */ "Ospite",
                /* PL */ "Gość",
                /* CS */ "Host",
                /* EL */ "Επισκέπτης",
                /* HU */ "Vendég",
                /* TR */ "Konuk",
                /* RU */ "Гость",
                /* UK */ "Гість",
                /* AR */ "ضيف",
                /* HE */ "אורח",
                /* FA */ "مهمان",
                /* HI */ "अतिथि",
                /* JA */ "ゲスト",
                /* KO */ "게스트",
                /* zh-CN */ "访客",
                /* zh-TW/HK */ "來賓", "訪客",
                /* VI */ "Khách"
            };

        private static readonly IReadOnlyDictionary<string, string[]> Indicators =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["chrome"] = ChromiumIndicators,
                // InPrivate is Edge's brand name: identical in every UI language.
                ["msedge"] = ChromiumIndicators.Append("InPrivate").ToArray(),
                ["brave"] = ChromiumIndicators.Concat(BraveIndicators).ToArray(),
                ["firefox"] = FirefoxIndicators,
                ["zen"] = FirefoxIndicators
            };

        public static bool IsSupportedBrowser(string processName) =>
            !string.IsNullOrWhiteSpace(processName) && Indicators.ContainsKey(processName);

        public static bool IsPrivateWindow(string processName, string windowTitle, string accessibleWindowName = null)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            if (!Indicators.TryGetValue(processName, out var indicators))
                return false;

            if (ChromiumProcesses.Contains(processName)
                && HasChromiumPrivateAnnotation(windowTitle, accessibleWindowName))
                return true;

            foreach (string indicator in indicators)
            {
                if (ContainsIndicator(windowTitle, indicator)
                    || ContainsIndicator(accessibleWindowName, indicator))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Language-independent Chromium rule: in private mode Chromium appends a
        /// localized annotation to the accessible window name while the native
        /// title stays clean, so an accessible name of the exact shape
        /// "&lt;native title&gt; (&lt;word&gt;)" marks a private window regardless of
        /// language — unless the word is a known non-private annotation (Guest).
        /// </summary>
        private static bool HasChromiumPrivateAnnotation(string windowTitle, string accessibleWindowName)
        {
            if (string.IsNullOrWhiteSpace(accessibleWindowName) || string.IsNullOrWhiteSpace(windowTitle))
                return false;

            string title = windowTitle.TrimEnd();
            if (!ChromiumAppTitleSuffixes.Any(s => title.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
                return false;

            string accessible = accessibleWindowName.TrimEnd();
            if (!accessible.StartsWith(title, StringComparison.Ordinal))
                return false;

            string tail = accessible.Substring(title.Length);
            if (tail.Length < 4 || !tail.StartsWith(" (", StringComparison.Ordinal) || !tail.EndsWith(")", StringComparison.Ordinal))
                return false;

            string word = tail.Substring(2, tail.Length - 3);
            return word.Length > 0 && word.Length <= 40 && !NonPrivateAnnotations.Contains(word);
        }

        private static bool ContainsIndicator(string value, string indicator) =>
            !string.IsNullOrWhiteSpace(value)
            && value.IndexOf(indicator, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
