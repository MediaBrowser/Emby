﻿(function ($, document, window) {

    function loadPage(page, config) {

        if (config.MergeMetadataAndImagesByName) {
            $('.fldImagesByName', page).hide();
        } else {
            $('.fldImagesByName', page).show();
        }

        $('#txtSeasonZeroName', page).val(config.SeasonZeroDisplayName);

        $('#selectEnableRealtimeMonitor', page).val(config.EnableLibraryMonitor);

        $('#chkEnableAudioArchiveFiles', page).checked(config.EnableAudioArchiveFiles).checkboxradio("refresh");
        $('#chkEnableVideoArchiveFiles', page).checked(config.EnableVideoArchiveFiles).checkboxradio("refresh");

        Dashboard.hideLoadingMsg();
    }

    function onSubmit() {
        Dashboard.showLoadingMsg();

        var form = this;

        ApiClient.getServerConfiguration().done(function (config) {

            config.SeasonZeroDisplayName = $('#txtSeasonZeroName', form).val();

            config.EnableLibraryMonitor = $('#selectEnableRealtimeMonitor', form).val();

            config.EnableAudioArchiveFiles = $('#chkEnableAudioArchiveFiles', form).checked();
            config.EnableVideoArchiveFiles = $('#chkEnableVideoArchiveFiles', form).checked();

            ApiClient.updateServerConfiguration(config).done(Dashboard.processServerConfigurationUpdateResult);
        });

        // Disable default form submission
        return false;
    }

    $(document).on('pageshow', "#librarySettingsPage", function () {

        Dashboard.showLoadingMsg();

        var page = this;

        ApiClient.getServerConfiguration().done(function (config) {

            loadPage(page, config);

        });

    }).on('pageinit', "#librarySettingsPage", function () {

        var page = this;

        $('.librarySettingsForm').off('submit', onSubmit).on('submit', onSubmit);
    });

})(jQuery, document, window);
