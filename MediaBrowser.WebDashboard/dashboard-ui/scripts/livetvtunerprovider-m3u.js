﻿(function ($, document, window) {

    function reload(page, providerId) {

        page.querySelector('.txtDevicePath').value = '';
        page.querySelector('.txtGuideGroup').value = 0;
        page.querySelector('.txtChannelMaps').value = '';


        if (providerId) {
            ApiClient.getNamedConfiguration("livetv").then(function (config) {

                var info = config.TunerHosts.filter(function (i) {
                    return i.Id == providerId;
                })[0];

                page.querySelector('.txtDevicePath').value = info.Url || '';
                page.querySelector('.txtGuideGroup').value = info.GuideGroup || 0;
                page.querySelector('.txtChannelMaps').value = info.ChannelMaps || '';

            });
        }
    }

    function submitForm(page) {

        Dashboard.showLoadingMsg();

        var info = {
            Type: 'm3u',
            Url: page.querySelector('.txtDevicePath').value,
            GuideGroup: page.querySelector('.txtGuideGroup').value,
            ChannelMaps: page.querySelector('.txtChannelMaps').value
        };

        var id = getParameterByName('id');

        if (id) {
            info.Id = id;
        }

        ApiClient.ajax({
            type: "POST",
            url: ApiClient.getUrl('LiveTv/TunerHosts'),
            data: JSON.stringify(info),
            contentType: "application/json"

        }).then(function () {

            Dashboard.processServerConfigurationUpdateResult();
            Dashboard.navigate('livetvstatus.html');

        }, function () {
            Dashboard.hideLoadingMsg();
            Dashboard.alert({
                message: Globalize.translate('ErrorSavingTvProvider')
            });
        });

    }

    $(document).on('pageinit', "#liveTvTunerProviderM3UPage", function () {

        var page = this;

        $('form', page).on('submit', function () {
            submitForm(page);
            return false;
        });

    }).on('pageshow', "#liveTvTunerProviderM3UPage", function () {

        var providerId = getParameterByName('id');
        var page = this;
        reload(page, providerId);
    });

})(jQuery, document, window);