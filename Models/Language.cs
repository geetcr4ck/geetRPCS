/**
 * geetRPCS - Localization
 * Language localization models for geetRPCS
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

using System.Text.Json.Serialization;
namespace geetRPCS.Models
{
    internal class Language
    {
        [JsonPropertyName("menu_pause")]
        public string MenuPause { get; set; }
        [JsonPropertyName("menu_resume")]
        public string MenuResume { get; set; }
        [JsonPropertyName("menu_private_mode")]
        public string MenuPrivateMode { get; set; }
        [JsonPropertyName("menu_reload_config")]
        public string MenuReloadConfig { get; set; }
        [JsonPropertyName("menu_startup")]
        public string MenuStartup { get; set; }
        [JsonPropertyName("menu_quick_actions")]
        public string MenuQuickActions { get; set; }
        [JsonPropertyName("menu_open_folder")]
        public string MenuOpenFolder { get; set; }
        [JsonPropertyName("menu_edit_config")]
        public string MenuEditConfig { get; set; }
        [JsonPropertyName("menu_edit_apps")]
        public string MenuEditApps { get; set; }
        [JsonPropertyName("menu_reload_all")]
        public string MenuReloadAll { get; set; }
        [JsonPropertyName("menu_statistics")]
        public string MenuStatistics { get; set; }
        [JsonPropertyName("menu_today")]
        public string MenuToday { get; set; }
        [JsonPropertyName("menu_this_week")]
        public string MenuThisWeek { get; set; }
        [JsonPropertyName("menu_this_month")]
        public string MenuThisMonth { get; set; }
        [JsonPropertyName("menu_all_time")]
        public string MenuAllTime { get; set; }
        [JsonPropertyName("menu_export_csv")]
        public string MenuExportCSV { get; set; }
        [JsonPropertyName("menu_export_json")]
        public string MenuExportJSON { get; set; }
        [JsonPropertyName("menu_reset_stats")]
        public string MenuResetStats { get; set; }
        [JsonPropertyName("menu_language")]
        public string MenuLanguage { get; set; }
        [JsonPropertyName("menu_check_updates")]
        public string MenuCheckUpdates { get; set; }
        [JsonPropertyName("menu_open_log")]
        public string MenuOpenLog { get; set; }
        [JsonPropertyName("menu_exit")]
        public string MenuExit { get; set; }
        [JsonPropertyName("menu_manage_apps")]
        public string MenuManageApps { get; set; }
        [JsonPropertyName("menu_preview_window")]
        public string MenuPreviewWindow { get; set; }
        [JsonPropertyName("filter_enabled")]
        public string FilterEnabled { get; set; }
        [JsonPropertyName("filter_disabled")]
        public string FilterDisabled { get; set; }
        [JsonPropertyName("manage_apps_title")]
        public string ManageAppsTitle { get; set; }
        [JsonPropertyName("manage_apps_search")]
        public string ManageAppsSearch { get; set; }
        [JsonPropertyName("manage_apps_found")]
        public string ManageAppsFound { get; set; }
        [JsonPropertyName("label_details")]
        public string LabelDetails { get; set; }
        [JsonPropertyName("label_state")]
        public string LabelState { get; set; }
        [JsonPropertyName("msg_presence_paused")]
        public string MsgPresencePaused { get; set; }
        [JsonPropertyName("msg_presence_resumed")]
        public string MsgPresenceResumed { get; set; }
        [JsonPropertyName("msg_private_mode_on")]
        public string MsgPrivateModeOn { get; set; }
        [JsonPropertyName("msg_private_mode_off")]
        public string MsgPrivateModeOff { get; set; }
        [JsonPropertyName("msg_config_reloaded")]
        public string MsgConfigReloaded { get; set; }
        [JsonPropertyName("msg_stats_reset")]
        public string MsgStatsReset { get; set; }
        [JsonPropertyName("msg_reload_tip")]
        public string MsgReloadTip { get; set; }
        [JsonPropertyName("msg_language_changed")]
        public string MsgLanguageChanged { get; set; }
        [JsonPropertyName("dialog_reload_title")]
        public string DialogReloadTitle { get; set; }
        [JsonPropertyName("dialog_reload_message")]
        public string DialogReloadMessage { get; set; }
        [JsonPropertyName("dialog_reset_stats_title")]
        public string DialogResetStatsTitle { get; set; }
        [JsonPropertyName("dialog_reset_stats_message")]
        public string DialogResetStatsMessage { get; set; }
        [JsonPropertyName("dialog_remove_desktop_shortcut")]
        public string DialogRemoveDesktopShortcut { get; set; }
        [JsonPropertyName("dialog_remove_start_menu_shortcut")]
        public string DialogRemoveStartMenuShortcut { get; set; }
        [JsonPropertyName("dialog_file_not_found")]
        public string DialogFileNotFound { get; set; }
        [JsonPropertyName("dialog_open_with_notepad")]
        public string DialogOpenWithNotepad { get; set; }
        [JsonPropertyName("dialog_log_not_created")]
        public string DialogLogNotCreated { get; set; }
        [JsonPropertyName("dialog_config_not_found")]
        public string DialogConfigNotFound { get; set; }
        [JsonPropertyName("msg_config_created")]
        public string MsgConfigCreated { get; set; }
        [JsonPropertyName("error_create_config")]
        public string ErrorCreateConfig { get; set; }
        [JsonPropertyName("error_open_link")]
        public string ErrorOpenLink { get; set; }
        [JsonPropertyName("error_startup_path_empty")]
        public string ErrorStartupPathEmpty { get; set; }
        [JsonPropertyName("error_startup_file_not_found")]
        public string ErrorStartupFileNotFound { get; set; }
        [JsonPropertyName("error_startup_path_not_absolute")]
        public string ErrorStartupPathNotAbsolute { get; set; }
        [JsonPropertyName("error_startup_temp_path")]
        public string ErrorStartupTempPath { get; set; }
        [JsonPropertyName("error_startup_failed")]
        public string ErrorStartupFailed { get; set; }
        [JsonPropertyName("error_startup_fatal")]
        public string ErrorStartupFatal { get; set; }
        [JsonPropertyName("error_unable_load_config")]
        public string ErrorUnableLoadConfig { get; set; }
        [JsonPropertyName("dialog_error_title")]
        public string DialogErrorTitle { get; set; }
        [JsonPropertyName("dialog_fatal_title")]
        public string DialogFatalTitle { get; set; }
        [JsonPropertyName("btn_save")]
        public string BtnSave { get; set; }
        [JsonPropertyName("btn_reset_default")]
        public string BtnResetDefault { get; set; }
        [JsonPropertyName("menu_change_app_id")]
        public string MenuChangeAppId { get; set; }
        [JsonPropertyName("dialog_change_app_id_title")]
        public string DialogChangeAppIdTitle { get; set; }
        [JsonPropertyName("dialog_change_app_id_message")]
        public string DialogChangeAppIdMessage { get; set; }
        [JsonPropertyName("msg_app_id_changed")]
        public string MsgAppIdChanged { get; set; }
        [JsonPropertyName("link_tutorial")]
        public string LinkTutorial { get; set; }
        [JsonPropertyName("url_tutorial")]
        public string UrlTutorial { get; set; }
        [JsonPropertyName("link_download_assets")]
        public string LinkDownloadAssets { get; set; }
        [JsonPropertyName("error_save_config")]
        public string ErrorSaveConfig { get; set; }
        [JsonPropertyName("error_invalid_app_id")]
        public string ErrorInvalidAppId { get; set; }
        [JsonPropertyName("error_prefix")]
        public string ErrorPrefix { get; set; }
        [JsonPropertyName("stats_no_data_today")]
        public string StatsNoDataToday { get; set; }
        [JsonPropertyName("stats_no_data_week")]
        public string StatsNoDataWeek { get; set; }
        [JsonPropertyName("stats_no_data_month")]
        public string StatsNoDataMonth { get; set; }
        [JsonPropertyName("stats_no_data")]
        public string StatsNoData { get; set; }
        [JsonPropertyName("stats_today_title")]
        public string StatsTodayTitle { get; set; }
        [JsonPropertyName("stats_week_title")]
        public string StatsWeekTitle { get; set; }
        [JsonPropertyName("stats_month_title")]
        public string StatsMonthTitle { get; set; }
        [JsonPropertyName("stats_all_time_title")]
        public string StatsAllTimeTitle { get; set; }
        [JsonPropertyName("stats_week_of")]
        public string StatsWeekOf { get; set; }
        [JsonPropertyName("stats_tracking_since")]
        public string StatsTrackingSince { get; set; }
        [JsonPropertyName("stats_total")]
        public string StatsTotal { get; set; }
        [JsonPropertyName("stats_total_tracked")]
        public string StatsTotalTracked { get; set; }
        [JsonPropertyName("stats_total_apps")]
        public string StatsTotalApps { get; set; }
        [JsonPropertyName("stats_export_success")]
        public string StatsExportSuccess { get; set; }
        [JsonPropertyName("stats_export_failed")]
        public string StatsExportFailed { get; set; }
        [JsonPropertyName("stats_open_folder")]
        public string StatsOpenFolder { get; set; }
        [JsonPropertyName("update_checking")]
        public string UpdateChecking { get; set; }
        [JsonPropertyName("update_available_title")]
        public string UpdateAvailableTitle { get; set; }
        [JsonPropertyName("update_available_message")]
        public string UpdateAvailableMessage { get; set; }
        [JsonPropertyName("update_latest_version")]
        public string UpdateLatestVersion { get; set; }
        [JsonPropertyName("update_current_version")]
        public string UpdateCurrentVersion { get; set; }
        [JsonPropertyName("update_changelog")]
        public string UpdateChangelog { get; set; }
        [JsonPropertyName("update_download_now")]
        public string UpdateDownloadNow { get; set; }
        [JsonPropertyName("update_up_to_date")]
        public string UpdateUpToDate { get; set; }
        [JsonPropertyName("update_check_failed")]
        public string UpdateCheckFailed { get; set; }
        [JsonPropertyName("update_error_title")]
        public string UpdateErrorTitle { get; set; }
        [JsonPropertyName("update_version_unknown")]
        public string UpdateVersionUnknown { get; set; }
        [JsonPropertyName("update_no_release_notes")]
        public string UpdateNoReleaseNotes { get; set; }
        [JsonPropertyName("update_subtitle")]
        public string UpdateSubtitle { get; set; }
        [JsonPropertyName("update_released")]
        public string UpdateReleased { get; set; }
        [JsonPropertyName("update_apps_available_title")]
        public string UpdateAppsAvailableTitle { get; set; }
        [JsonPropertyName("update_apps_available_message")]
        public string UpdateAppsAvailableMessage { get; set; }
        [JsonPropertyName("update_apps_latest_version")]
        public string UpdateAppsLatestVersion { get; set; }
        [JsonPropertyName("update_apps_available_body")]
        public string UpdateAppsAvailableBody { get; set; }
        [JsonPropertyName("btn_update_now")]
        public string BtnUpdateNow { get; set; }
        [JsonPropertyName("msg_apps_updated")]
        public string MsgAppsUpdated { get; set; }
        [JsonPropertyName("update_witty_available_title")]
        public string UpdateWittyAvailableTitle { get; set; }
        [JsonPropertyName("update_witty_available_message")]
        public string UpdateWittyAvailableMessage { get; set; }
        [JsonPropertyName("update_witty_latest_version")]
        public string UpdateWittyLatestVersion { get; set; }
        [JsonPropertyName("update_witty_available_body")]
        public string UpdateWittyAvailableBody { get; set; }
        [JsonPropertyName("msg_witty_updated")]
        public string MsgWittyUpdated { get; set; }
        [JsonPropertyName("update_how_to")]
        public string UpdateHowTo { get; set; }
        [JsonPropertyName("update_method_inapp")]
        public string UpdateMethodInApp { get; set; }
        [JsonPropertyName("update_method_ps")]
        public string UpdateMethodPs { get; set; }
        [JsonPropertyName("update_method_github")]
        public string UpdateMethodGithub { get; set; }
        [JsonPropertyName("update_downloading")]
        public string UpdateDownloading { get; set; }
        [JsonPropertyName("update_extracting")]
        public string UpdateExtracting { get; set; }
        [JsonPropertyName("update_verifying")]
        public string UpdateVerifying { get; set; }
        [JsonPropertyName("update_preparing")]
        public string UpdatePreparing { get; set; }
        [JsonPropertyName("update_ready_restart")]
        public string UpdateReadyRestart { get; set; }
        [JsonPropertyName("update_download_failed")]
        public string UpdateDownloadFailed { get; set; }
        [JsonPropertyName("btn_copy")]
        public string BtnCopy { get; set; }
        [JsonPropertyName("btn_copied")]
        public string BtnCopied { get; set; }
        [JsonPropertyName("btn_open_link")]
        public string BtnOpenLink { get; set; }
        [JsonPropertyName("btn_close")]
        public string BtnClose { get; set; }
        [JsonPropertyName("btn_cancel")]
        public string BtnCancel { get; set; }
        [JsonPropertyName("btn_yes")]
        public string BtnYes { get; set; }
        [JsonPropertyName("btn_no")]
        public string BtnNo { get; set; }
        [JsonPropertyName("btn_ok")]
        public string BtnOk { get; set; }
        [JsonPropertyName("error_missing_files")]
        public string ErrorMissingFiles { get; set; }
        [JsonPropertyName("error_files_location")]
        public string ErrorFilesLocation { get; set; }
        [JsonPropertyName("error_discord_connection")]
        public string ErrorDiscordConnection { get; set; }
        [JsonPropertyName("error_config_invalid")]
        public string ErrorConfigInvalid { get; set; }
        [JsonPropertyName("error_reload_failed")]
        public string ErrorReloadFailed { get; set; }
        [JsonPropertyName("error_startup_toggle")]
        public string ErrorStartupToggle { get; set; }
        [JsonPropertyName("error_manage_desktop_shortcut")]
        public string ErrorManageDesktopShortcut { get; set; }
        [JsonPropertyName("error_manage_start_menu_shortcut")]
        public string ErrorManageStartMenuShortcut { get; set; }
        [JsonPropertyName("error_open_folder")]
        public string ErrorOpenFolder { get; set; }
        [JsonPropertyName("error_open_file")]
        public string ErrorOpenFile { get; set; }
        [JsonPropertyName("error_export")]
        public string ErrorExport { get; set; }
        [JsonPropertyName("error_already_running")]
        public string ErrorAlreadyRunning { get; set; }
        [JsonPropertyName("media_playing")]
        public string MediaPlaying { get; set; }
        [JsonPropertyName("media_paused")]
        public string MediaPaused { get; set; }
        [JsonPropertyName("app_name")]
        public string AppName { get; set; }
        [JsonPropertyName("tray_paused")]
        public string TrayPaused { get; set; }
        [JsonPropertyName("tray_private")]
        public string TrayPrivate { get; set; }
        [JsonPropertyName("working")]
        public string Working { get; set; }
        [JsonPropertyName("idling")]
        public string Idling { get; set; }
        [JsonPropertyName("ready")]
        public string Ready { get; set; }
        [JsonPropertyName("energy_sleeping")]
        public string EnergySleeping { get; set; }
        [JsonPropertyName("energy_relaxing")]
        public string EnergyRelaxing { get; set; }
        [JsonPropertyName("energy_normal")]
        public string EnergyNormal { get; set; }
        [JsonPropertyName("energy_focused")]
        public string EnergyFocused { get; set; }
        [JsonPropertyName("energy_rush")]
        public string EnergyRush { get; set; }
        [JsonPropertyName("menu_mouse_energy")]
        public string MenuMouseEnergy { get; set; }
        [JsonPropertyName("msg_mouse_energy_on")]
        public string MsgMouseEnergyOn { get; set; }
        [JsonPropertyName("msg_mouse_energy_off")]
        public string MsgMouseEnergyOff { get; set; }
        [JsonPropertyName("menu_telemetry")]
        public string MenuTelemetry { get; set; }
        [JsonPropertyName("menu_auto_update")]
        public string MenuAutoUpdate { get; set; }
        [JsonPropertyName("menu_manage_shortcuts")]
        public string MenuManageShortcuts { get; set; }
        [JsonPropertyName("menu_shortcut_desktop")]
        public string MenuShortcutDesktop { get; set; }
        [JsonPropertyName("menu_shortcut_start_menu")]
        public string MenuShortcutStartMenu { get; set; }
        [JsonPropertyName("msg_telemetry_on")]
        public string MsgTelemetryOn { get; set; }
        [JsonPropertyName("msg_telemetry_off")]
        public string MsgTelemetryOff { get; set; }
        [JsonPropertyName("msg_auto_update_enabled")]
        public string MsgAutoUpdateEnabled { get; set; }
        [JsonPropertyName("msg_auto_update_disabled")]
        public string MsgAutoUpdateDisabled { get; set; }
        [JsonPropertyName("msg_shortcut_desktop_created")]
        public string MsgShortcutDesktopCreated { get; set; }
        [JsonPropertyName("msg_shortcut_desktop_removed")]
        public string MsgShortcutDesktopRemoved { get; set; }
        [JsonPropertyName("msg_shortcut_start_menu_created")]
        public string MsgShortcutStartMenuCreated { get; set; }
        [JsonPropertyName("msg_shortcut_start_menu_removed")]
        public string MsgShortcutStartMenuRemoved { get; set; }
        [JsonPropertyName("menu_tray_animation")]
        public string MenuTrayAnimation { get; set; }
        [JsonPropertyName("msg_tray_animation_on")]
        public string MsgTrayAnimationOn { get; set; }
        [JsonPropertyName("msg_tray_animation_off")]
        public string MsgTrayAnimationOff { get; set; }
        [JsonPropertyName("manage_apps_window_title")]
        public string WindowManageAppsTitle { get; set; }
        [JsonPropertyName("dialog_up_to_date_title")]
        public string DialogUpToDateTitle { get; set; }
        [JsonPropertyName("window_preview_title")]
        public string WindowPreviewTitle { get; set; }
        [JsonPropertyName("preview_playing_game")]
        public string PreviewPlayingGame { get; set; }
        [JsonPropertyName("preview_live")]
        public string PreviewLive { get; set; }
        [JsonPropertyName("preview_double_click_hide")]
        public string PreviewDoubleClickHide { get; set; }
        [JsonPropertyName("preview_refresh_assets")]
        public string PreviewRefreshAssets { get; set; }
        [JsonPropertyName("preview_clear_cache")]
        public string PreviewClearCache { get; set; }
        [JsonPropertyName("preview_always_on_top")]
        public string PreviewAlwaysOnTop { get; set; }
        [JsonPropertyName("preview_status_refreshing")]
        public string PreviewStatusRefreshing { get; set; }
        [JsonPropertyName("preview_status_cache_cleared")]
        public string PreviewStatusCacheCleared { get; set; }
        [JsonPropertyName("preview_status_no_app_id")]
        public string PreviewStatusNoAppId { get; set; }
        [JsonPropertyName("preview_status_assets_cached")]
        public string PreviewStatusAssetsCached { get; set; }
        [JsonPropertyName("preview_status_fetching")]
        public string PreviewStatusFetching { get; set; }
        [JsonPropertyName("preview_status_no_assets")]
        public string PreviewStatusNoAssets { get; set; }
        [JsonPropertyName("preview_status_assets_loaded")]
        public string PreviewStatusAssetsLoaded { get; set; }
        [JsonPropertyName("preview_status_api_error")]
        public string PreviewStatusApiError { get; set; }
        [JsonPropertyName("preview_status_error")]
        public string PreviewStatusError { get; set; }
        [JsonPropertyName("preview_header_asset_info")]
        public string PreviewHeaderAssetInfo { get; set; }
        [JsonPropertyName("preview_idling")]
        public string PreviewIdling { get; set; }
        [JsonPropertyName("preview_ready_to_work")]
        public string PreviewReadyToWork { get; set; }
        [JsonPropertyName("preview_paused")]
        public string PreviewPaused { get; set; }
        [JsonPropertyName("preview_presence_paused")]
        public string PreviewPresencePaused { get; set; }
        [JsonPropertyName("preview_not_showing")]
        public string PreviewNotShowing { get; set; }
        [JsonPropertyName("preview_initializing")]
        public string PreviewInitializing { get; set; }
        [JsonPropertyName("preview_button_placeholder")]
        public string PreviewButtonPlaceholder { get; set; }
        [JsonPropertyName("preview_large_image")]
        public string PreviewLargeImage { get; set; }
        [JsonPropertyName("preview_small_image")]
        public string PreviewSmallImage { get; set; }
        [JsonPropertyName("preview_large_empty")]
        public string PreviewLargeEmpty { get; set; }
        [JsonPropertyName("preview_small_empty")]
        public string PreviewSmallEmpty { get; set; }
        [JsonPropertyName("preview_elapsed")]
        public string PreviewElapsed { get; set; }
        [JsonPropertyName("update_dialog_current_version")]
        public string UpdateDialogCurrentVersion { get; set; }
        [JsonPropertyName("update_dialog_up_to_date_message")]
        public string UpdateDialogUpToDateMessage { get; set; }
        [JsonPropertyName("update_btn_awesome")]
        public string UpdateBtnAwesome { get; set; }
    }
}
